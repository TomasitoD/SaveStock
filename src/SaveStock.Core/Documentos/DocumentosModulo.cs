using Microsoft.Extensions.DependencyInjection;

namespace SaveStock.Core.Documentos;

/// <summary>
/// Servicios del manejador de documentos (#52: RF-DOC-01 a RF-DOC-04).
/// </summary>
public static class DocumentosModulo
{
    public static IServiceCollection AddDocumentos(this IServiceCollection services)
    {
        services.AddScoped<ServicioDocumentos>();
        return services;
    }
}
