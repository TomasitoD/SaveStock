using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SaveStock.Core.Comun;

namespace SaveStock.Inventario.TasaDeCambio;

/// <summary>
/// Consulta la tasa USD/DOP en ExchangeRate-API v6 (GET /v6/{clave}/pair/USD/DOP).
/// La clave se lee de EXCHANGE_API_KEY (RD-10). Todo fallo se convierte en un rechazo
/// controlado; la clave nunca aparece en mensajes ni en el registro.
/// </summary>
public class ClienteExchangeRateApi : ITasaDeCambio
{
    public const string MensajeSinClave = "La consulta de la tasa de cambio no está configurada: falta la variable EXCHANGE_API_KEY.";
    public const string MensajeClaveRechazada = "El servicio de tasa de cambio rechazó la clave configurada en EXCHANGE_API_KEY.";
    public const string MensajeNoResponde = "El servicio de tasa de cambio no responde. Intenta más tarde.";

    private const string UrlBase = "https://v6.exchangerate-api.com/v6/";

    private readonly HttpClient _http;
    private readonly ConfiguracionSaveStock _configuracion;
    private readonly ILogger<ClienteExchangeRateApi> _logger;

    public ClienteExchangeRateApi(HttpClient http, ConfiguracionSaveStock configuracion, ILogger<ClienteExchangeRateApi> logger)
    {
        _http = http;
        _configuracion = configuracion;
        _logger = logger;
    }

    public async Task<Resultado<TasaUsdDop>> ObtenerUsdDopAsync(CancellationToken cancelacion = default)
    {
        var clave = _configuracion.ExchangeApiKey;
        if (string.IsNullOrWhiteSpace(clave))
        {
            return Resultado<TasaUsdDop>.Error(TipoError.ServicioExterno, MensajeSinClave);
        }

        // La URL lleva la clave: no se guarda en ningún mensaje ni en el registro.
        var url = $"{UrlBase}{Uri.EscapeDataString(clave)}/pair/USD/DOP";

        RespuestaApi? respuesta;
        try
        {
            // No se mira el código HTTP: con clave inválida la API puede responder 200 o 4xx,
            // y en ambos casos el cuerpo trae "result": "error" con el motivo.
            using var http = await _http.GetAsync(url, cancelacion);
            var cuerpo = await http.Content.ReadAsStringAsync(cancelacion);
            respuesta = JsonSerializer.Deserialize<RespuestaApi>(cuerpo);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Solo el tipo de la excepción: su mensaje podría incluir la URL con la clave.
            _logger.LogWarning("No se pudo consultar la tasa de cambio ({TipoExcepcion}).", ex.GetType().Name);
            return Resultado<TasaUsdDop>.Error(TipoError.ServicioExterno, MensajeNoResponde);
        }

        if (respuesta?.Resultado == "success" && respuesta.PesosPorDolar > 0)
        {
            return Resultado<TasaUsdDop>.Ok(new TasaUsdDop(respuesta.PesosPorDolar.Value, respuesta.FechaActualizacion ?? string.Empty));
        }

        if (respuesta?.TipoDeError is "invalid-key" or "inactive-account")
        {
            _logger.LogWarning("El servicio de tasa de cambio rechazó la clave ({TipoDeError}).", respuesta.TipoDeError);
            return Resultado<TasaUsdDop>.Error(TipoError.ServicioExterno, MensajeClaveRechazada);
        }

        _logger.LogWarning("Respuesta inesperada del servicio de tasa de cambio ({TipoDeError}).", respuesta?.TipoDeError ?? "sin detalle");
        return Resultado<TasaUsdDop>.Error(TipoError.ServicioExterno, MensajeNoResponde);
    }

    /// <summary>Los campos de la respuesta de ExchangeRate-API que se usan. Los demás se ignoran.</summary>
    private class RespuestaApi
    {
        [JsonPropertyName("result")]
        public string? Resultado { get; set; }

        [JsonPropertyName("error-type")]
        public string? TipoDeError { get; set; }

        [JsonPropertyName("conversion_rate")]
        public decimal? PesosPorDolar { get; set; }

        [JsonPropertyName("time_last_update_utc")]
        public string? FechaActualizacion { get; set; }
    }
}
