using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaveStock.Core.Comun;
using SaveStock.Inventario.Datos;
using SaveStock.Inventario.OrdenesDeCompra;

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

        // La máquina de estados (OrdenesDeCompra/MaquinaEstadosOrdenCompra) es una clase estática,
        // como Permisos en el Core: no hace falta registrarla.
        return services;
    }
}
