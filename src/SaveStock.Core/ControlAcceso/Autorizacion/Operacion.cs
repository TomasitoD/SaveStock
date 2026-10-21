namespace SaveStock.Core.ControlAcceso.Autorizacion;

/// <summary>
/// Operaciones del sistema. Qué nivel de acceso exige cada una se declara en <see cref="Permisos"/>.
/// </summary>
public enum Operacion
{
    // Sistema
    Salud,

    // Registro y activación (#15)
    Registrar,
    ActivarCuenta,
    ReenviarActivacion,

    // Sesión (#16)
    IniciarSesion,
    ConsultarUsuarioActual,
    CerrarSesion,

    // Administración de usuarios (#17)
    ListarUsuarios,
    CambiarRol,
    DesactivarUsuario,
    ReactivarUsuario,

    // Contraseñas (#18)
    IniciarRecuperacion,
    RestablecerContrasena,
    CambiarContrasena,
    ForzarRestablecimiento,

    // Gestión de permisos (#40)
    CrearSolicitudPermiso,
    ConsultarMisSolicitudesPermiso,
    ConsultarSolicitudesPendientes,
    AprobarSolicitudPermiso,
    RechazarSolicitudPermiso,
}
