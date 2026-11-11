using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Seguridad;
using SaveStock.Core.ControlAcceso.Sesiones;
using SaveStock.Core.Correo;
using SaveStock.Core.Datos;

namespace SaveStock.Tests.Integracion;

/// <summary>
/// Levanta la web en memoria con una base SQLite temporal propia (una por fábrica), así las
/// pruebas no tocan datos/ ni se pisan entre sí. Uso: <c>IClassFixture&lt;SaveStockFactory&gt;</c>.
/// </summary>
public class SaveStockFactory : WebApplicationFactory<Program>
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "savestock-pruebas", Guid.NewGuid().ToString("N"));

    public SaveStockFactory()
    {
        Configuracion = new ConfiguracionSaveStock
        {
            RutaBaseDatos = Path.Combine(_carpeta, "savestock.db"),
            RutaBaseDatosInventario = Path.Combine(_carpeta, "inventario.db"),
            RutaDocumentos = Path.Combine(_carpeta, "documentos"),
            UrlBase = "http://localhost",
        };
    }

    /// <summary>La configuración que usa la web de prueba (sin administrador inicial ni SMTP).</summary>
    public ConfiguracionSaveStock Configuracion { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Reemplaza la configuración leída del entorno por la de la prueba. El CoreDbContext
        // toma la cadena de conexión de este objeto, así que apunta a la base temporal.
        builder.ConfigureTestServices(servicios =>
        {
            servicios.RemoveAll<ConfiguracionSaveStock>();
            servicios.AddSingleton(Configuracion);
        });
    }

    /// <summary>Guarda un usuario directamente en la base (sin pasar por el registro) y lo devuelve.</summary>
    /// <param name="activado">true: cuenta activada y activa. false: recién registrada (inactiva, sin activar).</param>
    public async Task<Usuario> CrearUsuarioAsync(string nombre, string correo, string contrasena, Rol rol, bool activado = true)
    {
        using var alcance = Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CoreDbContext>();
        var ahora = alcance.ServiceProvider.GetRequiredService<IReloj>().AhoraUtc;

        var usuario = new Usuario
        {
            Nombre = nombre,
            Correo = ValidadorEntrada.NormalizarCorreo(correo),
            HashContrasena = HasherContrasenas.Hashear(contrasena),
            Rol = rol,
            Activo = activado,
            FechaActivacion = activado ? ahora : null,
            FechaCreacion = ahora,
        };

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    /// <summary>Abre una sesión para el usuario con GestorSesiones y devuelve un cliente con el header Bearer.</summary>
    public async Task<HttpClient> ClienteConSesionAsync(Usuario usuario)
    {
        using var alcance = Services.CreateScope();
        var gestor = alcance.ServiceProvider.GetRequiredService<GestorSesiones>();
        var sesion = await gestor.CrearAsync(usuario);

        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sesion.Token);
        return cliente;
    }

    /// <summary>Todos los correos de la cola, del más viejo al más nuevo.</summary>
    public async Task<List<CorreoEnCola>> LeerCorreosEnColaAsync()
    {
        using var alcance = Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CoreDbContext>();
        return await db.CorreosEnCola.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
    }

    /// <summary>Ejecuta una consulta sobre la base de la prueba, por ejemplo para revisar el estado de un usuario.</summary>
    public async Task<T> ConsultarBaseAsync<T>(Func<CoreDbContext, Task<T>> consulta)
    {
        using var alcance = Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CoreDbContext>();
        return await consulta(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Suelta las conexiones abiertas para poder borrar el archivo temporal.
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_carpeta, recursive: true);
        }
        catch (IOException)
        {
            // Si el sistema todavía tiene el archivo abierto, queda en la carpeta temporal.
        }
    }
}
