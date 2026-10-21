using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Administracion;
using SaveStock.Core.ControlAcceso.Contrasenas;
using SaveStock.Core.ControlAcceso.InicioSesion;
using SaveStock.Core.ControlAcceso.Registro;
using SaveStock.Core.ControlAcceso.Sesiones;
using SaveStock.Core.Correo;
using SaveStock.Core.Datos;
using SaveStock.Core.GestionPermisos;

namespace SaveStock.Core;

/// <summary>
/// Registra en la inyección de dependencias todo lo que ofrece el Core (RD-01).
/// La web y el enviador llaman a AddSaveStockCore y no conocen los detalles internos.
/// </summary>
public static class ServiciosCore
{
    public static IServiceCollection AddSaveStockCore(this IServiceCollection services, ConfiguracionSaveStock configuracion)
    {
        // Piezas compartidas.
        services.AddSingleton(configuracion);
        services.AddSingleton<IReloj, RelojSistema>();

        // La cadena de conexión se lee de la configuración registrada (no de la variable local)
        // para que las pruebas puedan reemplazarla por una base temporal.
        services.AddDbContext<CoreDbContext>((proveedor, opciones) =>
            opciones.UseSqlite(proveedor.GetRequiredService<ConfiguracionSaveStock>().CadenaConexion));

        services.AddScoped<GestorSesiones>();
        services.AddScoped<ICorreoCola, CorreoCola>();

        // Un método por feature: cada una registra solo sus propios servicios.
        services.AddRegistro();
        services.AddInicioSesion();
        services.AddAdministracion();
        services.AddContrasenas();
        services.AddCorreo();
        services.AddGestionPermisos();

        return services;
    }
}
