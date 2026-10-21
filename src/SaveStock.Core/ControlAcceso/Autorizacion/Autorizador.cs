using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.GestionPermisos;

namespace SaveStock.Core.ControlAcceso.Autorizacion;

/// <summary>
/// Decide si un usuario puede ejecutar una operación, según lo declarado en <see cref="Permisos"/>
/// (RF-CA-05, RF-CA-06), y en los permisos que se le otorgaron al aprobar una solicitud (RF-GP-05).
/// No sabe nada de HTTP: la Web solo traduce el resultado.
/// </summary>
public static class Autorizador
{
    public const string MensajeSinSesion = "Necesitas iniciar sesión.";
    public const string MensajeSinPermiso = "No tienes permiso para realizar esta operación.";

    /// <param name="operacion">La operación que se quiere ejecutar.</param>
    /// <param name="usuario">El usuario con sesión válida, o null si no hay sesión.</param>
    public static Resultado Autorizar(Operacion operacion, Usuario? usuario) =>
        Autorizar(operacion, usuario, []);

    /// <param name="operacion">La operación que se quiere ejecutar.</param>
    /// <param name="usuario">El usuario con sesión válida, o null si no hay sesión.</param>
    /// <param name="permisosOtorgados">
    /// Los permisos que ese usuario recibió por una solicitud aprobada. Un permiso que habilita la
    /// operación (según <see cref="Permisos.OperacionesPorPermiso"/>) alcanza aunque el rol no sea Administrador.
    /// </param>
    public static Resultado Autorizar(Operacion operacion, Usuario? usuario, IReadOnlyCollection<PermisoSolicitable> permisosOtorgados)
    {
        var nivel = Permisos.NivelDe(operacion);

        if (nivel == NivelAcceso.Publico)
        {
            return Resultado.Ok();
        }

        if (usuario is null)
        {
            return Resultado.Error(TipoError.NoAutenticado, MensajeSinSesion);
        }

        if (nivel == NivelAcceso.Administrador
            && usuario.Rol != Rol.Administrador
            && !Permisos.AlgunPermisoHabilita(permisosOtorgados, operacion))
        {
            return Resultado.Error(TipoError.Prohibido, MensajeSinPermiso);
        }

        return Resultado.Ok();
    }
}
