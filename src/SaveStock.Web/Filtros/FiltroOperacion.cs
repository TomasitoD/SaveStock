using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Sesiones;
using SaveStock.Core.GestionPermisos;
using SaveStock.Web.Errores;

namespace SaveStock.Web.Filtros;

/// <summary>
/// Metadato que indica qué operación ejecuta un endpoint. Una prueba revisa que todo endpoint
/// de /api lo tenga (RF-CA-05).
/// </summary>
public record OperacionRequerida(Operacion Operacion);

/// <summary>
/// Filtro que verifica, del lado del servidor y antes de ejecutar el endpoint, que quien llama
/// puede ejecutar la operación (RD-06, RF-CA-06). No se apoya en lo que muestre la interfaz.
/// </summary>
public class FiltroOperacion : IEndpointFilter
{
    /// <summary>Cookie con el token de sesión que usan las páginas.</summary>
    public const string NombreCookieSesion = "savestock_sesion";

    private readonly Operacion _operacion;

    public FiltroOperacion(Operacion operacion)
    {
        _operacion = operacion;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        var http = contexto.HttpContext;
        Usuario? usuario = null;
        IReadOnlyCollection<PermisoSolicitable> permisosOtorgados = [];

        // Las operaciones públicas no necesitan sesión; las demás la validan con GestorSesiones.
        if (Permisos.NivelDe(_operacion) != NivelAcceso.Publico)
        {
            var token = LeerToken(http);
            var gestor = http.RequestServices.GetRequiredService<GestorSesiones>();
            var sesion = await gestor.ValidarAsync(token);
            if (sesion is not null)
            {
                usuario = sesion.Usuario;
                UsuarioActual.Guardar(http, sesion, token!);

                // Los permisos que recibió por una solicitud aprobada también cuentan (RF-GP-05).
                var gestionPermisos = http.RequestServices.GetRequiredService<ServicioGestionPermisos>();
                permisosOtorgados = await gestionPermisos.ListarPermisosOtorgadosAsync(usuario.Id);
            }
        }

        // Permisos/Autorizador deciden: 401 sin sesión válida, 403 si ni el rol ni un permiso otorgado alcanzan.
        var autorizacion = Autorizador.Autorizar(_operacion, usuario, permisosOtorgados);
        if (!autorizacion.Exito)
        {
            return ResultadoHttp.Error(autorizacion);
        }

        return await siguiente(contexto);
    }

    /// <summary>El token viaja en <c>Authorization: Bearer &lt;token&gt;</c> o en la cookie de sesión.</summary>
    private static string? LeerToken(HttpContext http)
    {
        var autorizacion = http.Request.Headers.Authorization.ToString();
        const string prefijo = "Bearer ";
        if (autorizacion.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
        {
            return autorizacion[prefijo.Length..].Trim();
        }

        return http.Request.Cookies[NombreCookieSesion];
    }
}

public static class RequiereOperacionExtensiones
{
    /// <summary>
    /// Declara la operación del endpoint y le aplica el filtro de permisos. Todo endpoint lo usa:
    /// <c>app.MapGet("/api/sesion/yo", ...).RequiereOperacion(Operacion.ConsultarUsuarioActual);</c>
    /// </summary>
    public static RouteHandlerBuilder RequiereOperacion(this RouteHandlerBuilder endpoint, Operacion operacion)
    {
        return endpoint
            .WithMetadata(new OperacionRequerida(operacion))
            .AddEndpointFilter(new FiltroOperacion(operacion));
    }
}
