using SaveStock.Core.Comun;

namespace SaveStock.Inventario.TasaDeCambio;

/// <summary>Tasa del día: cuántos pesos dominicanos vale un dólar, y cuándo la actualizó la fuente.</summary>
public record TasaUsdDop(decimal PesosPorDolar, string FechaActualizacion);

/// <summary>
/// Fuente de la tasa de cambio USD → DOP. La implementación real llama a ExchangeRate-API;
/// las pruebas usan un doble para no depender de la red.
/// </summary>
public interface ITasaDeCambio
{
    /// <summary>
    /// Devuelve la tasa del día o un rechazo <see cref="TipoError.ServicioExterno"/> con un
    /// mensaje comprensible (RD-07, RD-08). Nunca lanza excepciones por fallos de la API.
    /// </summary>
    Task<Resultado<TasaUsdDop>> ObtenerUsdDopAsync(CancellationToken cancelacion = default);
}
