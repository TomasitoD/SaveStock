using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.GestionPermisos;
using SaveStock.Web.Errores;
using SaveStock.Web.Filtros;

namespace SaveStock.Web.Endpoints;

/// <summary>Cuerpo de POST /api/permisos/solicitudes. El permiso llega como texto y lo valida el servicio.</summary>
public record CuerpoSolicitudPermiso(string? Permiso);

/// <summary>Cuerpo de POST /api/permisos/solicitudes/{id}/rechazar.</summary>
public record CuerpoRechazoSolicitud(string? Motivo);

public static class GestionPermisosEndpoints
{
    /// <summary>
    /// Gestión de permisos (#40, RF-GP-01 a RF-GP-07). Cada endpoint declara .RequiereOperacion(...):
    /// crear y ver las propias es para cualquier usuario con sesión; ver las pendientes, aprobar y
    /// rechazar es solo de Administrador (RD-06). La lógica vive en ServicioGestionPermisos (RD-02).
    /// </summary>
    public static IEndpointRouteBuilder MapGestionPermisosEndpoints(this IEndpointRouteBuilder app)
    {
        // El cuerpo es opcional para que una petición sin cuerpo llegue al filtro (401) y, si pasa,
        // el servicio la rechace con 400 porque el permiso no existe.
        app.MapPost("/api/permisos/solicitudes", async (CuerpoSolicitudPermiso? cuerpo, HttpContext http, ServicioGestionPermisos servicio) =>
                Responder(await servicio.CrearSolicitudAsync(http.ObtenerUsuarioActual(), cuerpo?.Permiso), StatusCodes.Status201Created))
            .RequiereOperacion(Operacion.CrearSolicitudPermiso);

        app.MapGet("/api/permisos/solicitudes/mias", async (HttpContext http, ServicioGestionPermisos servicio) =>
                Results.Ok(await servicio.ListarMiasAsync(http.ObtenerUsuarioActual())))
            .RequiereOperacion(Operacion.ConsultarMisSolicitudesPermiso);

        app.MapGet("/api/permisos/solicitudes/pendientes", async (ServicioGestionPermisos servicio) =>
                Results.Ok(await servicio.ListarPendientesAsync()))
            .RequiereOperacion(Operacion.ConsultarSolicitudesPendientes);

        app.MapPost("/api/permisos/solicitudes/{id:int}/aprobar", async (int id, HttpContext http, ServicioGestionPermisos servicio) =>
                Responder(await servicio.AprobarAsync(http.ObtenerUsuarioActual(), id)))
            .RequiereOperacion(Operacion.AprobarSolicitudPermiso);

        app.MapPost("/api/permisos/solicitudes/{id:int}/rechazar", async (int id, CuerpoRechazoSolicitud? cuerpo, HttpContext http, ServicioGestionPermisos servicio) =>
                Responder(await servicio.RechazarAsync(http.ObtenerUsuarioActual(), id, cuerpo?.Motivo)))
            .RequiereOperacion(Operacion.RechazarSolicitudPermiso);

        return app;
    }

    /// <summary>La solicitud (sin datos internos) si salió bien; si no, { "mensaje": "..." } con su código.</summary>
    private static IResult Responder(Resultado<SolicitudPermisoRespuesta> resultado, int codigoExito = StatusCodes.Status200OK) =>
        resultado.Exito ? Results.Json(resultado.Valor, statusCode: codigoExito) : ResultadoHttp.Error(resultado);
}
