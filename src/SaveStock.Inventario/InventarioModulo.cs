using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaveStock.Core.Comun;
using SaveStock.Inventario.Datos;
using SaveStock.Inventario.OrdenesDeCompra;
using SaveStock.Inventario.TasaDeCambio;

namespace SaveStock.Inventario;

/// <summary>
/// Servicios del módulo de inventario (#19). Usa su propio archivo SQLite
/// (ConfiguracionSaveStock.RutaBaseDatosInventario). El Core no conoce este proyecto (RD-03).
/// </summary>
public static class InventarioModulo
{
    public static IServiceCollection AddSaveStockInventario(this IServiceCollection services, ConfiguracionSaveStock configuracion)
    {
        // Como en el Core, la cadena de conexión se lee de la configuración registrada (no del
        // parámetro) para que las pruebas puedan apuntar a una base temporal.
        services.AddDbContext<InventarioDbContext>((proveedor, opciones) =>
            opciones.UseSqlite(proveedor.GetRequiredService<ConfiguracionSaveStock>().CadenaConexionInventario));

        // Crea datos/inventario.db y la tabla OrdenesDeCompra al iniciar.
        services.AddHostedService<InicializadorInventario>();

        services.AddScoped<ServicioOrdenesCompra>();

        // Cliente de ExchangeRate-API. El timeout evita que una API lenta deje colgada la petición.
        // RemoveAllLoggers: el registro automático de HttpClient escribe la URL de cada petición,
        // y en esta API la clave va dentro de la URL (RD-10).
        services.AddHttpClient<ITasaDeCambio, ClienteExchangeRateApi>(cliente => cliente.Timeout = TimeSpan.FromSeconds(10))
            .RemoveAllLoggers();

        // La máquina de estados (OrdenesDeCompra/MaquinaEstadosOrdenCompra) es una clase estática,
        // como Permisos en el Core: no hace falta registrarla.
        return services;
    }
}
