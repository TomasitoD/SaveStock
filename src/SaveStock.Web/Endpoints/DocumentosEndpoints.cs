using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.Documentos;
using SaveStock.Web.Errores;
using SaveStock.Web.Filtros;

namespace SaveStock.Web.Endpoints;

public static class DocumentosEndpoints
{
    /// <summary>
    /// Manejador de documentos (#52, RF-DOC-01 a RF-DOC-04). Todas exigen sesión; la regla de dueño o
    /// Administrador la aplica ServicioDocumentos, así que llamar la API directamente también se rechaza (RD-06).
    /// </summary>
    public static IEndpointRouteBuilder MapDocumentosEndpoints(this IEndpointRouteBuilder app)
    {
        // El archivo llega como multipart/form-data en el campo "archivo". Si falta, el servicio lo
        // rechaza con 400 (RD-07). La protección antiforgery es para formularios del navegador; esta
        // API se autentica con el token de sesión, igual que las demás.
        app.MapPost("/api/documentos", async (IFormFile? archivo, HttpContext http, ServicioDocumentos servicio) =>
            {
                await using var contenido = archivo?.OpenReadStream();
                var resultado = await servicio.SubirAsync(http.ObtenerUsuarioActual(), archivo?.FileName, archivo?.ContentType, archivo?.Length ?? 0, contenido);
                return resultado.Exito ? Results.Json(resultado.Valor, statusCode: StatusCodes.Status201Created) : ResultadoHttp.Error(resultado);
            })
            .DisableAntiforgery()
            .RequiereOperacion(Operacion.SubirDocumento);

        app.MapGet("/api/documentos", async (HttpContext http, ServicioDocumentos servicio) =>
                Results.Ok(await servicio.ListarAsync(http.ObtenerUsuarioActual())))
            .RequiereOperacion(Operacion.ListarDocumentos);

        app.MapGet("/api/documentos/{id:int}", async (int id, HttpContext http, ServicioDocumentos servicio) =>
            {
                var resultado = await servicio.ConsultarAsync(http.ObtenerUsuarioActual(), id);
                return resultado.Exito ? Results.Ok(resultado.Valor) : ResultadoHttp.Error(resultado);
            })
            .RequiereOperacion(Operacion.ConsultarDocumento);

        app.MapGet("/api/documentos/{id:int}/descargar", async (int id, HttpContext http, ServicioDocumentos servicio) =>
            {
                var resultado = await servicio.DescargarAsync(http.ObtenerUsuarioActual(), id);
                if (!resultado.Exito)
                {
                    return ResultadoHttp.Error(resultado);
                }

                var descarga = resultado.Valor!;
                return Results.File(descarga.RutaArchivo, descarga.Documento.Tipo, descarga.Documento.Nombre);
            })
            .RequiereOperacion(Operacion.DescargarDocumento);

        app.MapDelete("/api/documentos/{id:int}", async (int id, HttpContext http, ServicioDocumentos servicio) =>
            {
                var resultado = await servicio.EliminarAsync(http.ObtenerUsuarioActual(), id);
                return resultado.Exito ? ResultadoHttp.Mensaje(resultado.Mensaje) : ResultadoHttp.Error(resultado);
            })
            .RequiereOperacion(Operacion.EliminarDocumento);

        return app;
    }
}
