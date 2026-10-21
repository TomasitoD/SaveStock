namespace SaveStock.Core.GestionPermisos;

/// <summary>
/// EL único lugar donde se declaran los estados de una solicitud de permiso. Qué cambios entre
/// ellos están permitidos lo decide solo <see cref="MaquinaEstadosSolicitudPermiso"/> (RD-04).
/// Se guarda como texto en la base.
/// </summary>
public enum EstadoSolicitudPermiso
{
    /// <summary>Recién creada, espera que un Administrador la resuelva. Es el estado inicial (RF-GP-01).</summary>
    Pendiente,

    /// <summary>Un Administrador la aprobó (RF-GP-03). El sistema la pasa enseguida a Aplicada.</summary>
    Aprobada,

    /// <summary>El permiso ya quedó otorgado al solicitante (RF-GP-05). Estado terminal.</summary>
    Aplicada,

    /// <summary>Un Administrador la rechazó con un motivo (RF-GP-04). Estado terminal.</summary>
    Rechazada,
}
