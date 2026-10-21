namespace SaveStock.Core.GestionPermisos;

/// <summary>Quién ejecuta una transición de la solicitud de permiso.</summary>
public enum EjecutorTransicion
{
    /// <summary>Un Administrador con sesión, que queda registrado como quien resolvió la solicitud.</summary>
    Administrador,

    /// <summary>El propio sistema, sin intervención de nadie (por ejemplo, aplicar el permiso al aprobar).</summary>
    Sistema,
}

/// <summary>
/// Una fila de la tabla de transiciones permitidas de <see cref="MaquinaEstadosSolicitudPermiso"/>.
/// </summary>
/// <param name="Desde">Estado en el que tiene que estar la solicitud.</param>
/// <param name="Hacia">Estado al que pasa.</param>
/// <param name="QuienEjecuta">Quién puede hacer este cambio.</param>
/// <param name="Condicion">Qué tiene que cumplirse (se muestra en la documentación).</param>
public record TransicionSolicitudPermiso(
    EstadoSolicitudPermiso Desde,
    EstadoSolicitudPermiso Hacia,
    EjecutorTransicion QuienEjecuta,
    string Condicion);
