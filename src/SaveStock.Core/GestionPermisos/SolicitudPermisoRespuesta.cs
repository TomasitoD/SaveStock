namespace SaveStock.Core.GestionPermisos;

/// <summary>Un usuario tal como aparece dentro de una solicitud: solo id y nombre.</summary>
public record UsuarioReferencia(int Id, string Nombre);

/// <summary>
/// Lo que la API muestra de una solicitud de permiso. Es un DTO aparte de la entidad a propósito:
/// del solicitante y del aprobador solo viajan el id y el nombre, nunca el hash ni el correo.
/// </summary>
/// <param name="Aprobador">El Administrador que la resolvió (aprobó o rechazó), o null si sigue Pendiente.</param>
public record SolicitudPermisoRespuesta(
    int Id,
    UsuarioReferencia Solicitante,
    PermisoSolicitable Permiso,
    EstadoSolicitudPermiso Estado,
    UsuarioReferencia? Aprobador,
    DateTime FechaCreacion,
    DateTime? FechaResolucion,
    string? MotivoRechazo,
    DateTime? FechaAplicacion)
{
    /// <summary>Arma la respuesta. La solicitud tiene que venir con Solicitante (y Aprobador, si lo hay) cargados.</summary>
    public static SolicitudPermisoRespuesta De(SolicitudPermiso solicitud) => new(
        solicitud.Id,
        new UsuarioReferencia(solicitud.SolicitanteId, solicitud.Solicitante?.Nombre ?? string.Empty),
        solicitud.Permiso,
        solicitud.Estado,
        solicitud.AprobadorId is null ? null : new UsuarioReferencia(solicitud.AprobadorId.Value, solicitud.Aprobador?.Nombre ?? string.Empty),
        solicitud.FechaCreacion,
        solicitud.FechaResolucion,
        solicitud.MotivoRechazo,
        solicitud.FechaAplicacion);
}
