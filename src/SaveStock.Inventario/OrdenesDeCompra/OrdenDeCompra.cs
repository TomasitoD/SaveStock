namespace SaveStock.Inventario.OrdenesDeCompra;

/// <summary>
/// Orden de compra a un proveedor: es lo que repone el stock del negocio.
/// Su estado solo cambia a través de <see cref="MaquinaEstadosOrdenCompra.Aplicar"/> (RD-04).
/// </summary>
public class OrdenDeCompra
{
    public const int LargoMaximoProveedor = 200;

    public int Id { get; set; }

    /// <summary>Nombre del proveedor al que se le hace el pedido. Obligatorio, hasta 200 caracteres.</summary>
    public string Proveedor { get; set; } = string.Empty;

    /// <summary>Monto total de la orden en pesos dominicanos (DOP). Opcional al crear (0) y nunca negativo.</summary>
    public decimal MontoDop { get; set; }

    /// <summary>
    /// Estado actual. Toda orden nace en Borrador. El setter es interno para que fuera del
    /// módulo nadie lo cambie sin pasar por la máquina de estados.
    /// </summary>
    public EstadoOrdenCompra Estado { get; internal set; } = EstadoOrdenCompra.Borrador;

    /// <summary>
    /// Id del usuario del Core que creó la orden. No es una clave foránea: los usuarios viven
    /// en otra base (savestock.db) y el inventario no depende de sus tablas.
    /// </summary>
    public int CreadaPorUsuarioId { get; set; }

    /// <summary>Fecha de creación en UTC (RD-11).</summary>
    public DateTime FechaCreacion { get; set; }

    /// <summary>Fecha del último cambio en UTC (RD-11). Se actualiza en cada cambio de estado.</summary>
    public DateTime FechaActualizacion { get; set; }
}
