using SaveStock.Core.Comun;
using SaveStock.Inventario.TasaDeCambio;

namespace SaveStock.Tests.Apoyo;

/// <summary>
/// Doble de <see cref="ITasaDeCambio"/>: devuelve lo que la prueba le indique, sin llamar a la red.
/// Por defecto responde 60 pesos por dólar.
/// </summary>
public class TasaDeCambioFalsa : ITasaDeCambio
{
    public Resultado<TasaUsdDop> Respuesta { get; set; } =
        Resultado<TasaUsdDop>.Ok(new TasaUsdDop(60m, "Tue, 10 Nov 2026 00:00:01 +0000"));

    public Task<Resultado<TasaUsdDop>> ObtenerUsdDopAsync(CancellationToken cancelacion = default) =>
        Task.FromResult(Respuesta);
}
