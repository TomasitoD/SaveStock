using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Inventario.OrdenesDeCompra;
using SaveStock.Web.Errores;
using SaveStock.Web.Filtros;

namespace SaveStock.Web.Endpoints;

/// <summary>Cuerpo de POST /api/ordenes.</summary>
public record CuerpoOrdenDeCompra(string? Proveedor, decimal? MontoDop);

public static class OrdenesDeCompraEndpoints
{
    /// <summary>
    /// Inventario (#50, RF-NEG-02). Cada endpoint declara .RequiereOperacion(...) y exige sesión
    /// (RF-NEG-06). La lógica vive en ServicioOrdenesCompra (RD-02).
    /// </summary>
    public static IEndpointRouteBuilder MapOrdenesDeCompraEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ordenes", async (CuerpoOrdenDeCompra? cuerpo, HttpContext http, ServicioOrdenesCompra servicio) =>
                Responder(await servicio.CrearAsync(http.ObtenerUsuarioActual(), cuerpo?.Proveedor, cuerpo?.MontoDop), StatusCodes.Status201Created))
            .RequiereOperacion(Operacion.CrearOrdenDeCompra);

        app.MapGet("/api/ordenes", async (HttpContext http, ServicioOrdenesCompra servicio) =>
                Results.Ok(await servicio.ListarAsync(http.ObtenerUsuarioActual())))
            .RequiereOperacion(Operacion.ListarOrdenesDeCompra);

        app.MapGet("/api/ordenes/{id:int}", async (int id, HttpContext http, ServicioOrdenesCompra servicio) =>
                Responder(await servicio.ConsultarAsync(http.ObtenerUsuarioActual(), id)))
            .RequiereOperacion(Operacion.ConsultarOrdenDeCompra);

        // Usa la API externa de tasa de cambio: si falla responde 503 con un mensaje claro,
        // y las demás rutas de órdenes siguen funcionando igual.
        app.MapGet("/api/ordenes/{id:int}/total-usd", async (int id, HttpContext http, ServicioOrdenesCompra servicio) =>
            {
                var resultado = await servicio.TotalEnDolaresAsync(http.ObtenerUsuarioActual(), id);
                return resultado.Exito ? Results.Ok(resultado.Valor) : ResultadoHttp.Error(resultado);
            })
            .RequiereOperacion(Operacion.ConsultarTotalEnDolares);

        app.MapPost("/api/ordenes/{id:int}/enviar", async (int id, HttpContext http, ServicioOrdenesCompra servicio) =>
                Responder(await servicio.EnviarAsync(http.ObtenerUsuarioActual(), id)))
            .RequiereOperacion(Operacion.EnviarOrdenDeCompra);

        app.MapPost("/api/ordenes/{id:int}/recibir", async (int id, HttpContext http, ServicioOrdenesCompra servicio) =>
                Responder(await servicio.RecibirAsync(http.ObtenerUsuarioActual(), id)))
            .RequiereOperacion(Operacion.RecibirOrdenDeCompra);

        app.MapPost("/api/ordenes/{id:int}/cancelar", async (int id, HttpContext http, ServicioOrdenesCompra servicio) =>
                Responder(await servicio.CancelarAsync(http.ObtenerUsuarioActual(), id)))
            .RequiereOperacion(Operacion.CancelarOrdenDeCompra);

        return app;
    }

    private static IResult Responder(Resultado<OrdenDeCompraRespuesta> resultado, int codigoExito = StatusCodes.Status200OK) =>
        resultado.Exito ? Results.Json(resultado.Valor, statusCode: codigoExito) : ResultadoHttp.Error(resultado);
}
