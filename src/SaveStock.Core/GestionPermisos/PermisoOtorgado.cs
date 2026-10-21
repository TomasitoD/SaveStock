using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Core.GestionPermisos;

/// <summary>
/// Un permiso que un usuario tiene porque se le aprobó una solicitud (RF-GP-05). El
/// <see cref="ControlAcceso.Autorizacion.Autorizador"/> lo tiene en cuenta además del rol.
/// Un usuario tiene cada permiso una sola vez (índice único UsuarioId + Permiso).
/// </summary>
public class PermisoOtorgado
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    public PermisoSolicitable Permiso { get; set; }

    /// <summary>La solicitud aprobada que otorgó este permiso.</summary>
    public int SolicitudId { get; set; }

    public SolicitudPermiso? Solicitud { get; set; }

    /// <summary>Fecha en UTC (RD-11). Es la misma que la FechaAplicacion de la solicitud.</summary>
    public DateTime FechaOtorgamiento { get; set; }
}
