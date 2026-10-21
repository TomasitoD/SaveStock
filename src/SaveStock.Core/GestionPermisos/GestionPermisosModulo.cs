using Microsoft.Extensions.DependencyInjection;

namespace SaveStock.Core.GestionPermisos;

/// <summary>
/// Servicios de gestión de permisos (#40: RF-GP-01 a RF-GP-07).
/// </summary>
public static class GestionPermisosModulo
{
    public static IServiceCollection AddGestionPermisos(this IServiceCollection services)
    {
        services.AddScoped<ServicioGestionPermisos>();
        return services;
    }
}
