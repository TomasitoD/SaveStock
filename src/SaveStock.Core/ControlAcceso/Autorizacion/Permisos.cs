using SaveStock.Core.GestionPermisos;

namespace SaveStock.Core.ControlAcceso.Autorizacion;

/// <summary>
/// EL único lugar donde se lee qué rol exige cada operación (RF-CA-05).
/// La Web lo aplica del lado del servidor en cada endpoint (RD-06).
/// </summary>
public static class Permisos
{
    public static readonly IReadOnlyDictionary<Operacion, NivelAcceso> PorOperacion = new Dictionary<Operacion, NivelAcceso>
    {
        [Operacion.Salud] = NivelAcceso.Publico,

        [Operacion.Registrar] = NivelAcceso.Publico,
        [Operacion.ActivarCuenta] = NivelAcceso.Publico,
        [Operacion.ReenviarActivacion] = NivelAcceso.Publico,
        [Operacion.IniciarSesion] = NivelAcceso.Publico,
        [Operacion.IniciarRecuperacion] = NivelAcceso.Publico,
        [Operacion.RestablecerContrasena] = NivelAcceso.Publico,

        [Operacion.ConsultarUsuarioActual] = NivelAcceso.Autenticado,
        [Operacion.CerrarSesion] = NivelAcceso.Autenticado,
        [Operacion.CambiarContrasena] = NivelAcceso.Autenticado,
        [Operacion.CrearSolicitudPermiso] = NivelAcceso.Autenticado,
        [Operacion.ConsultarMisSolicitudesPermiso] = NivelAcceso.Autenticado,

        [Operacion.ListarUsuarios] = NivelAcceso.Administrador,
        [Operacion.CambiarRol] = NivelAcceso.Administrador,
        [Operacion.DesactivarUsuario] = NivelAcceso.Administrador,
        [Operacion.ReactivarUsuario] = NivelAcceso.Administrador,
        [Operacion.ForzarRestablecimiento] = NivelAcceso.Administrador,
        [Operacion.ConsultarSolicitudesPendientes] = NivelAcceso.Administrador,
        [Operacion.AprobarSolicitudPermiso] = NivelAcceso.Administrador,
        [Operacion.RechazarSolicitudPermiso] = NivelAcceso.Administrador,
    };

    /// <summary>
    /// Qué operación de Administrador habilita cada permiso que se puede solicitar (RF-GP-05).
    /// Un usuario que tiene el permiso otorgado puede ejecutar esa operación aunque su rol no alcance.
    /// Es la única operación que se puede conceder así; las demás siguen dependiendo solo del rol.
    /// </summary>
    public static readonly IReadOnlyDictionary<PermisoSolicitable, Operacion> OperacionesPorPermiso = new Dictionary<PermisoSolicitable, Operacion>
    {
        [PermisoSolicitable.VerListadoUsuarios] = Operacion.ListarUsuarios,
    };

    /// <summary>true si alguno de los permisos otorgados habilita la operación.</summary>
    public static bool AlgunPermisoHabilita(IEnumerable<PermisoSolicitable> permisosOtorgados, Operacion operacion) =>
        permisosOtorgados.Any(permiso => OperacionesPorPermiso.TryGetValue(permiso, out var habilitada) && habilitada == operacion);

    /// <summary>
    /// Nivel que exige la operación. Si alguien agrega una operación y olvida declararla aquí,
    /// falla de forma explícita (y lo detecta la prueba de permisos).
    /// </summary>
    public static NivelAcceso NivelDe(Operacion operacion)
    {
        if (!PorOperacion.TryGetValue(operacion, out var nivel))
        {
            throw new InvalidOperationException($"La operación {operacion} no tiene un nivel de acceso declarado en Permisos.");
        }

        return nivel;
    }
}
