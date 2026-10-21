using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Core.GestionPermisos;

/// <summary>
/// Pedido de un usuario para obtener un permiso que su rol no le da (RF-GP-01).
/// Su estado y los datos de la resolución solo cambian a través de
/// <see cref="MaquinaEstadosSolicitudPermiso.Aplicar"/> (RD-04): por eso sus setters son internos.
/// </summary>
public class SolicitudPermiso
{
    public const int LargoMaximoMotivo = 500;

    public int Id { get; set; }

    /// <summary>Quién pidió el permiso.</summary>
    public int SolicitanteId { get; set; }

    public Usuario? Solicitante { get; set; }

    public PermisoSolicitable Permiso { get; set; }

    /// <summary>Toda solicitud nace en Pendiente (RF-GP-01).</summary>
    public EstadoSolicitudPermiso Estado { get; internal set; } = EstadoSolicitudPermiso.Pendiente;

    /// <summary>
    /// El Administrador que la resolvió, tanto si la aprobó como si la rechazó (RF-GP-03).
    /// null mientras está Pendiente.
    /// </summary>
    public int? AprobadorId { get; internal set; }

    public Usuario? Aprobador { get; set; }

    /// <summary>Fecha de creación en UTC (RD-11).</summary>
    public DateTime FechaCreacion { get; set; }

    /// <summary>Cuándo se aprobó o se rechazó, en UTC (RF-GP-03).</summary>
    public DateTime? FechaResolucion { get; internal set; }

    /// <summary>Por qué se rechazó (RF-GP-04). Solo tiene valor en una solicitud Rechazada.</summary>
    public string? MotivoRechazo { get; internal set; }

    /// <summary>Cuándo el sistema otorgó el permiso, en UTC (RF-GP-05).</summary>
    public DateTime? FechaAplicacion { get; internal set; }
}
