namespace SaveStock.Core.GestionPermisos;

/// <summary>
/// Catálogo de los permisos que un usuario puede pedir (RF-GP-01). Qué operación habilita cada uno
/// se declara en <see cref="ControlAcceso.Autorizacion.Permisos.OperacionesPorPermiso"/>.
/// Se guarda como texto en la base.
/// </summary>
public enum PermisoSolicitable
{
    /// <summary>Ver el listado de usuarios (GET /api/admin/usuarios), que por rol es solo de Administrador.</summary>
    VerListadoUsuarios,
}
