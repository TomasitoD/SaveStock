using SaveStock.Core.Comun;

namespace SaveStock.Core.GestionPermisos;

/// <summary>
/// EL punto único de las transiciones de una solicitud de permiso (RD-04, RF-GP-06). Aquí está
/// la única tabla de cambios permitidos y el único código que cambia el estado de una solicitud.
/// Es independiente de la máquina de la orden de compra: no comparte tabla ni estados con ella.
/// La misma tabla está documentada en docs/gestion-de-permisos.md.
/// </summary>
public static class MaquinaEstadosSolicitudPermiso
{
    public const string MensajeMotivoObligatorio = "El rechazo necesita un motivo escrito.";
    public const string MensajeMotivoMuyLargo = "El motivo del rechazo no puede tener más de 500 caracteres.";

    /// <summary>
    /// Las únicas transiciones permitidas. Cualquier combinación que no esté aquí se rechaza
    /// y la solicitud queda exactamente como estaba.
    /// </summary>
    public static readonly IReadOnlyList<TransicionSolicitudPermiso> Transiciones =
    [
        new(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aprobada, EjecutorTransicion.Administrador,
            "La solicitud está en Pendiente."),

        new(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aplicada, EjecutorTransicion.Sistema,
            "Automática, inmediatamente después de aprobar."),

        new(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Rechazada, EjecutorTransicion.Administrador,
            "Exige un motivo escrito."),
        new(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Rechazada, EjecutorTransicion.Administrador,
            "Exige un motivo escrito."),
    ];

    /// <summary>Indica si la tabla permite pasar de <paramref name="desde"/> a <paramref name="hacia"/>.</summary>
    public static bool EstaPermitida(EstadoSolicitudPermiso desde, EstadoSolicitudPermiso hacia) =>
        Buscar(desde, hacia) is not null;

    /// <summary>
    /// Cambia el estado de la solicitud solo si la transición está en la tabla, la ejecuta quien
    /// corresponde y se cumple su condición. Deja registrado el dato de cada transición:
    /// <list type="bullet">
    /// <item>Aprobada: quién aprobó y cuándo (RF-GP-03).</item>
    /// <item>Rechazada: quién rechazó, cuándo y el motivo (RF-GP-04).</item>
    /// <item>Aplicada: cuándo se otorgó el permiso (RF-GP-05).</item>
    /// </list>
    /// Si algo no se cumple devuelve el error y la solicitud no cambia en nada. No guarda en la base:
    /// eso lo hace quien la llama.
    /// </summary>
    /// <param name="administradorId">El Administrador que resuelve. Obligatorio si la transición es de Administrador.</param>
    /// <param name="motivo">El motivo del rechazo. Solo se usa al pasar a Rechazada.</param>
    public static Resultado Aplicar(
        SolicitudPermiso solicitud,
        EstadoSolicitudPermiso hacia,
        EjecutorTransicion quien,
        IReloj reloj,
        int? administradorId = null,
        string? motivo = null)
    {
        var desde = solicitud.Estado;

        var transicion = Buscar(desde, hacia);
        if (transicion is null)
        {
            return Resultado.Error(TipoError.Conflicto, $"La solicitud está en estado {desde} y no se puede {Verbo(hacia)}.");
        }

        if (transicion.QuienEjecuta != quien)
        {
            return Resultado.Error(TipoError.Prohibido, $"El paso de {desde} a {hacia} solo lo ejecuta: {transicion.QuienEjecuta}.");
        }

        if (quien == EjecutorTransicion.Administrador && administradorId is null)
        {
            throw new ArgumentException("Una transición de Administrador necesita saber qué Administrador la ejecuta.", nameof(administradorId));
        }

        var motivoLimpio = motivo?.Trim();
        if (hacia == EstadoSolicitudPermiso.Rechazada)
        {
            if (string.IsNullOrEmpty(motivoLimpio))
            {
                return Resultado.Error(TipoError.Validacion, MensajeMotivoObligatorio);
            }

            if (motivoLimpio.Length > SolicitudPermiso.LargoMaximoMotivo)
            {
                return Resultado.Error(TipoError.Validacion, MensajeMotivoMuyLargo);
            }
        }

        // Todo se cumple: recién ahora se toca la solicitud.
        var ahora = reloj.AhoraUtc;
        solicitud.Estado = hacia;
        switch (hacia)
        {
            case EstadoSolicitudPermiso.Aprobada:
                solicitud.AprobadorId = administradorId;
                solicitud.FechaResolucion = ahora;
                break;
            case EstadoSolicitudPermiso.Rechazada:
                solicitud.AprobadorId = administradorId;
                solicitud.FechaResolucion = ahora;
                solicitud.MotivoRechazo = motivoLimpio;
                break;
            case EstadoSolicitudPermiso.Aplicada:
                solicitud.FechaAplicacion = ahora;
                break;
        }

        return Resultado.Ok($"La solicitud pasó de {desde} a {hacia}.");
    }

    private static TransicionSolicitudPermiso? Buscar(EstadoSolicitudPermiso desde, EstadoSolicitudPermiso hacia) =>
        Transiciones.FirstOrDefault(t => t.Desde == desde && t.Hacia == hacia);

    /// <summary>Cómo se nombra la acción en el mensaje de rechazo, por ejemplo "no se puede aprobar".</summary>
    private static string Verbo(EstadoSolicitudPermiso hacia) => hacia switch
    {
        EstadoSolicitudPermiso.Aprobada => "aprobar",
        EstadoSolicitudPermiso.Rechazada => "rechazar",
        EstadoSolicitudPermiso.Aplicada => "aplicar",
        _ => $"pasar a {hacia}",
    };
}
