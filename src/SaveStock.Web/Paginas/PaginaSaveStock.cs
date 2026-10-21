using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.ControlAcceso.Sesiones;
using SaveStock.Core.GestionPermisos;
using SaveStock.Web.Errores;
using SaveStock.Web.Filtros;

namespace SaveStock.Web.Paginas;

/// <summary>Mensaje que devolvió un servicio del Core, listo para mostrarlo en la página.</summary>
public record MensajePagina(bool Exito, string Texto)
{
    public static MensajePagina De(Resultado resultado) => new(resultado.Exito, resultado.Mensaje);
}

/// <summary>
/// Base de todas las páginas. Hace lo mismo que <see cref="FiltroOperacion"/> hace con la API:
/// lee el token de la cookie, lo valida con <see cref="GestorSesiones"/> y le pregunta a
/// <see cref="Autorizador"/> (que lee <see cref="Permisos"/>) si el usuario puede ejecutar la
/// operación. Así las páginas y la API comparten el mismo punto de permisos (RF-CA-05, RD-06).
/// </summary>
public abstract class PaginaSaveStock : PageModel
{
    /// <summary>El usuario con sesión válida, o null si no hay sesión.</summary>
    public Usuario? UsuarioActual { get; private set; }

    /// <summary>El token de la cookie, solo si la sesión es válida (por ejemplo, para cerrarla).</summary>
    protected string? TokenActual { get; private set; }

    /// <summary>Lo que respondió el servicio en el último envío del formulario.</summary>
    public MensajePagina? Mensaje { get; protected set; }

    /// <summary>Carga el usuario de la cookie de sesión, si la sesión existe, no venció ni se revocó y el usuario está activo.</summary>
    protected async Task CargarSesionAsync()
    {
        var token = Request.Cookies[FiltroOperacion.NombreCookieSesion];
        var gestor = HttpContext.RequestServices.GetRequiredService<GestorSesiones>();
        var sesion = await gestor.ValidarAsync(token);

        UsuarioActual = sesion?.Usuario;
        TokenActual = sesion is null ? null : token;
    }

    /// <summary>
    /// Verifica, del lado del servidor, que quien hace la petición puede ejecutar la operación.
    /// Devuelve null si puede; si no, la respuesta que hay que devolver: sin sesión válida
    /// redirige a /iniciar-sesion y sin permiso muestra la página de rechazo con 403.
    /// Cada handler lo llama con SU operación, también los POST armados a mano.
    /// </summary>
    protected async Task<IActionResult?> AutorizarAsync(Operacion operacion)
    {
        await CargarSesionAsync();

        // Igual que FiltroOperacion: los permisos otorgados por una solicitud aprobada también cuentan (RF-GP-05).
        IReadOnlyCollection<PermisoSolicitable> permisosOtorgados = [];
        if (UsuarioActual is not null)
        {
            var gestionPermisos = HttpContext.RequestServices.GetRequiredService<ServicioGestionPermisos>();
            permisosOtorgados = await gestionPermisos.ListarPermisosOtorgadosAsync(UsuarioActual.Id);
        }

        var autorizacion = Autorizador.Autorizar(operacion, UsuarioActual, permisosOtorgados);
        if (autorizacion.Exito)
        {
            return null;
        }

        if (autorizacion.TipoError == TipoError.NoAutenticado)
        {
            return Redirect("/iniciar-sesion");
        }

        var rechazo = Partial("_Rechazo", autorizacion.Mensaje);
        rechazo.StatusCode = ResultadoHttp.CodigoHttp(autorizacion.TipoError);
        return rechazo;
    }

    /// <summary>Guarda el token en la cookie HttpOnly que leen las páginas y el filtro de la API.</summary>
    protected void GuardarCookieSesion(SesionCreada sesion)
    {
        Response.Cookies.Append(FiltroOperacion.NombreCookieSesion, sesion.Token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/",
            Expires = new DateTimeOffset(DateTime.SpecifyKind(sesion.FechaVencimiento, DateTimeKind.Utc)),
        });
    }

    protected void BorrarCookieSesion()
    {
        Response.Cookies.Delete(FiltroOperacion.NombreCookieSesion, new CookieOptions { Path = "/" });
    }

    /// <summary>Nombre del rol para mostrar en pantalla.</summary>
    public static string TextoRol(Rol rol) => rol == Rol.Administrador ? "Administrador" : "Estándar";
}
