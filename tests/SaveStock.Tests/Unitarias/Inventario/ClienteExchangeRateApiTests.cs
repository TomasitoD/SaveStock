using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SaveStock.Core.Comun;
using SaveStock.Inventario.TasaDeCambio;

namespace SaveStock.Tests.Unitarias.Inventario;

/// <summary>
/// El cliente de ExchangeRate-API con un HttpMessageHandler falso: nunca se llama a la API real.
/// </summary>
public class ClienteExchangeRateApiTests
{
    private const string ClaveDePrueba = "clave-de-prueba";

    /// <summary>Handler falso: devuelve la respuesta indicada (o lanza la excepción) y cuenta las llamadas.</summary>
    private sealed class HandlerFalso : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _responder;

        public HandlerFalso(Func<HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public int Llamadas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Llamadas++;
            return Task.FromResult(_responder());
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode codigo, string json) =>
        new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static ClienteExchangeRateApi CrearCliente(HandlerFalso handler, string? clave = ClaveDePrueba) => new(
        new HttpClient(handler),
        new ConfiguracionSaveStock { ExchangeApiKey = clave },
        NullLogger<ClienteExchangeRateApi>.Instance);

    [Fact]
    [Trait("Requisito", "RD-10")]
    public async Task ObtenerTasa_ConRespuestaExitosa_DevuelveLaTasaYSuFecha()
    {
        // Preparar
        var handler = new HandlerFalso(() => Json(HttpStatusCode.OK,
            """{"result":"success","base_code":"USD","target_code":"DOP","conversion_rate":59.1234,"time_last_update_utc":"Tue, 10 Nov 2026 00:00:01 +0000"}"""));

        // Actuar
        var resultado = await CrearCliente(handler).ObtenerUsdDopAsync();

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal(59.1234m, resultado.Valor!.PesosPorDolar);
        Assert.Equal("Tue, 10 Nov 2026 00:00:01 +0000", resultado.Valor.FechaActualizacion);
    }

    [Fact]
    [Trait("Requisito", "RD-10")]
    public async Task ObtenerTasa_SinClave_SeRechazaSinLlamarALaApi()
    {
        // Preparar
        var handler = new HandlerFalso(() => Json(HttpStatusCode.OK, """{"result":"success","conversion_rate":59}"""));

        // Actuar
        var resultado = await CrearCliente(handler, clave: null).ObtenerUsdDopAsync();

        // Verificar
        Assert.Equal(TipoError.ServicioExterno, resultado.TipoError);
        Assert.Equal(ClienteExchangeRateApi.MensajeSinClave, resultado.Mensaje);
        Assert.Equal(0, handler.Llamadas);
    }

    [Theory]
    [Trait("Requisito", "RD-08")]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ObtenerTasa_ConClaveInvalida_DevuelveElMensajeDeClaveRechazadaSinMostrarLaClave(HttpStatusCode codigo)
    {
        // Preparar
        var handler = new HandlerFalso(() => Json(codigo, """{"result":"error","error-type":"invalid-key"}"""));

        // Actuar
        var resultado = await CrearCliente(handler).ObtenerUsdDopAsync();

        // Verificar
        Assert.Equal(TipoError.ServicioExterno, resultado.TipoError);
        Assert.Equal(ClienteExchangeRateApi.MensajeClaveRechazada, resultado.Mensaje);
        Assert.DoesNotContain(ClaveDePrueba, resultado.Mensaje);
    }

    [Fact]
    [Trait("Requisito", "RD-07")]
    public async Task ObtenerTasa_SiLaRedFalla_DevuelveQueElServicioNoResponde()
    {
        // Preparar
        var handler = new HandlerFalso(() => throw new HttpRequestException("Sin conexión"));

        // Actuar
        var resultado = await CrearCliente(handler).ObtenerUsdDopAsync();

        // Verificar
        Assert.Equal(TipoError.ServicioExterno, resultado.TipoError);
        Assert.Equal(ClienteExchangeRateApi.MensajeNoResponde, resultado.Mensaje);
    }

    [Fact]
    [Trait("Requisito", "RD-07")]
    public async Task ObtenerTasa_ConRespuestaQueNoEsJson_DevuelveQueElServicioNoResponde()
    {
        // Preparar
        var handler = new HandlerFalso(() => Json(HttpStatusCode.BadGateway, "<html>Bad Gateway</html>"));

        // Actuar
        var resultado = await CrearCliente(handler).ObtenerUsdDopAsync();

        // Verificar
        Assert.Equal(TipoError.ServicioExterno, resultado.TipoError);
        Assert.Equal(ClienteExchangeRateApi.MensajeNoResponde, resultado.Mensaje);
    }
}
