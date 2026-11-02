using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Inventario.OrdenesDeCompra;

/// <summary>
/// EL punto único de las transiciones de una orden de compra (RD-04). Aquí está la única
/// tabla de cambios permitidos y el único código que cambia el estado de una orden.
/// Ningún otro lugar del sistema valida estados: todos llaman a <see cref="Aplicar"/>.
/// La misma tabla está documentada en docs/maquina-de-estados.md.
/// </summary>
public static class MaquinaEstadosOrdenCompra
{
    private static readonly Rol[] EstandarOAdministrador = [Rol.Estandar, Rol.Administrador];
    private static readonly Rol[] SoloAdministrador = [Rol.Administrador];

    /// <summary>
    /// Las únicas transiciones permitidas. Cualquier combinación que no esté aquí se rechaza
    /// y el estado de la orden no cambia.
    /// </summary>
    public static readonly IReadOnlyList<Transicion> Transiciones =
    [
        new(EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Enviada, EstandarOAdministrador,
            "La orden tiene un proveedor indicado."),

        new(EstadoOrdenCompra.Enviada, EstadoOrdenCompra.Recibida, EstandarOAdministrador,
            "Llegó la mercancía. Un Estándar necesita además el permiso RecibirOrdenesDeCompra (lo revisa ServicioOrdenesCompra)."),

        new(EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Cancelada, EstandarOAdministrador,
            "Ninguna: el borrador todavía no se mandó al proveedor."),

        new(EstadoOrdenCompra.Enviada, EstadoOrdenCompra.Cancelada, SoloAdministrador,
            "El proveedor no entrega la mercancía."),
    ];

    /// <summary>Estados de los que no sale ninguna transición (RF-NEG-05).</summary>
    public static readonly IReadOnlySet<EstadoOrdenCompra> EstadosTerminales = new HashSet<EstadoOrdenCompra>
    {
        EstadoOrdenCompra.Recibida,
        EstadoOrdenCompra.Cancelada,
    };

    /// <summary>
    /// Transiciones prohibidas a propósito, con su motivo (RF-NEG-04). Quedan rechazadas por no
    /// estar en <see cref="Transiciones"/>; se declaran aparte para explicar por qué.
    /// </summary>
    public static readonly IReadOnlyList<TransicionProhibida> TransicionesProhibidasExplicitas =
    [
        new(EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Borrador,
            "la mercancía ya entró al stock y la orden no se puede volver a editar."),
    ];

    /// <summary>
    /// Indica si la tabla permite pasar de <paramref name="desde"/> a <paramref name="hacia"/>
    /// para ese rol. No revisa la condición sobre los datos de la orden: eso lo hace <see cref="Aplicar"/>.
    /// </summary>
    public static bool PuedeTransicionar(EstadoOrdenCompra desde, EstadoOrdenCompra hacia, Rol rol)
    {
        var transicion = Buscar(desde, hacia);
        return transicion is not null && transicion.QuienEjecuta.Contains(rol);
    }

    /// <summary>
    /// Cambia el estado de la orden solo si la transición está permitida para ese rol y se cumple
    /// su condición. Si no, devuelve el error y la orden queda exactamente como estaba.
    /// No guarda en la base: eso lo hace quien la llama.
    /// </summary>
    public static Resultado Aplicar(OrdenDeCompra orden, EstadoOrdenCompra hacia, Rol rol, IReloj reloj)
    {
        var desde = orden.Estado;

        // La tabla decide: lo que no está en Transiciones no se puede hacer.
        var transicion = Buscar(desde, hacia);
        if (transicion is null)
        {
            return Resultado.Error(TipoError.Validacion, MotivoDeRechazo(desde, hacia));
        }

        if (!transicion.QuienEjecuta.Contains(rol))
        {
            return Resultado.Error(TipoError.Prohibido, Autorizador.MensajeSinPermiso);
        }

        if (transicion.CumpleCondicion is not null && !transicion.CumpleCondicion(orden))
        {
            return Resultado.Error(TipoError.Validacion, transicion.MensajeSiNoCumple);
        }

        orden.Estado = hacia;
        orden.FechaActualizacion = reloj.AhoraUtc;
        return Resultado.Ok($"La orden pasó de {desde} a {hacia}.");
    }

    /// <summary>El mensaje para una transición que no está en la tabla.</summary>
    private static string MotivoDeRechazo(EstadoOrdenCompra desde, EstadoOrdenCompra hacia)
    {
        var prohibida = TransicionesProhibidasExplicitas.FirstOrDefault(p => p.Desde == desde && p.Hacia == hacia);
        if (prohibida is not null)
        {
            return $"No se puede pasar una orden de {desde} a {hacia}: {prohibida.Motivo}";
        }

        return EstadosTerminales.Contains(desde)
            ? $"La orden está {desde} y ya no puede cambiar de estado."
            : $"No se puede pasar una orden de {desde} a {hacia}.";
    }

    private static Transicion? Buscar(EstadoOrdenCompra desde, EstadoOrdenCompra hacia)
    {
        return Transiciones.FirstOrDefault(t => t.Desde == desde && t.Hacia == hacia);
    }
}
