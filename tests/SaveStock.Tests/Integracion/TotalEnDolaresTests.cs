using System.Net;
using System.Net.Http.Json;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Inventario.TasaDeCambio;

namespace SaveStock.Tests.Integracion;

/// <summary>
/// GET /api/ordenes/{id}/total-usd en la web de prueba. La fábrica no configura EXCHANGE_API_KEY,
/// así que el cliente rechaza la consulta sin salir a la red.
/// </summary>
public class TotalEnDolaresTests : IClassFixture<SaveStockFactory>
{
    private readonly SaveStockFactory _fabrica;

    public TotalEnDolaresTests(SaveStockFactory fabrica)
    {
        _fabrica = fabrica;
    }

    private record OrdenCreada(int Id);

    [Fact]
    [Trait("Requisito", "RD-08")]
    public async Task TotalEnDolares_SinClaveConfigurada_Responde503YLasOrdenesSiguenFuncionando()
    {
        // Preparar
        var usuario = await _fabrica.CrearUsuarioAsync("Ana", $"ana-{Guid.NewGuid():N}@negocio.com", "Clave-Segura-123", Rol.Estandar);
        var cliente = await _fabrica.ClienteConSesionAsync(usuario);
        var creada = await cliente.PostAsJsonAsync("/api/ordenes", new { proveedor = "Distribuidora Central", montoDop = 5000m });
        var orden = await creada.Content.ReadFromJsonAsync<OrdenCreada>();

        // Actuar
        var respuesta = await cliente.GetAsync($"/api/ordenes/{orden!.Id}/total-usd");

        // Verificar
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal(ClienteExchangeRateApi.MensajeSinClave, cuerpo!["mensaje"]);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/ordenes/{orden.Id}")).StatusCode);
    }
}
