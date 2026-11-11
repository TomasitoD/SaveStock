using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.GestionPermisos;
using SaveStock.Inventario.Datos;

namespace SaveStock.Inventario.OrdenesDeCompra;

/// <summary>Lo que se muestra de una orden de compra.</summary>
public record OrdenDeCompraRespuesta(
    int Id,
    string Proveedor,
    EstadoOrdenCompra Estado,
    int CreadaPorUsuarioId,
    DateTime FechaCreacion,
    DateTime FechaActualizacion,
    decimal MontoDop);

/// <summary>
/// Operaciones de las órdenes de compra (RF-NEG-02). Los cambios de estado los decide solo
/// <see cref="MaquinaEstadosOrdenCompra"/> (RD-04). Un Estándar trabaja con sus propias órdenes;
/// un Administrador, con todas.
/// </summary>
public class ServicioOrdenesCompra
{
    public const string MensajeOrdenNoExiste = "No existe una orden de compra con ese id.";
    public const string MensajeMontoNegativo = "El monto no puede ser negativo.";
    public const string MensajeNecesitaPermiso = "Necesitas el permiso RecibirOrdenesDeCompra aprobado para recibir órdenes.";

    private readonly InventarioDbContext _db;
    private readonly ServicioGestionPermisos _gestionPermisos;
    private readonly IReloj _reloj;

    public ServicioOrdenesCompra(InventarioDbContext db, ServicioGestionPermisos gestionPermisos, IReloj reloj)
    {
        _db = db;
        _gestionPermisos = gestionPermisos;
        _reloj = reloj;
    }

    /// <summary>
    /// Crea una orden en Borrador. El proveedor es obligatorio; el monto en pesos es opcional
    /// (si no se envía queda en 0) y no puede ser negativo (RD-07).
    /// </summary>
    public async Task<Resultado<OrdenDeCompraRespuesta>> CrearAsync(Usuario usuario, string? proveedor, decimal? montoDop = null)
    {
        var validacion = ValidadorEntrada.ValidarTextoObligatorio(proveedor, "proveedor", OrdenDeCompra.LargoMaximoProveedor);
        if (!validacion.Exito)
        {
            return Resultado<OrdenDeCompraRespuesta>.Error(validacion.TipoError, validacion.Mensaje);
        }

        var monto = montoDop ?? 0m;
        if (monto < 0)
        {
            return Resultado<OrdenDeCompraRespuesta>.Error(TipoError.Validacion, MensajeMontoNegativo);
        }

        var ahora = _reloj.AhoraUtc;
        var orden = new OrdenDeCompra
        {
            Proveedor = proveedor!.Trim(),
            MontoDop = monto,
            CreadaPorUsuarioId = usuario.Id,
            FechaCreacion = ahora,
            FechaActualizacion = ahora,
        };

        _db.OrdenesDeCompra.Add(orden);
        await _db.SaveChangesAsync();
        return Resultado<OrdenDeCompraRespuesta>.Ok(ARespuesta(orden));
    }

    /// <summary>Un Administrador ve todas las órdenes; un Estándar, solo las suyas.</summary>
    public async Task<IReadOnlyList<OrdenDeCompraRespuesta>> ListarAsync(Usuario usuario)
    {
        var ordenes = await _db.OrdenesDeCompra
            .AsNoTracking()
            .Where(o => usuario.Rol == Rol.Administrador || o.CreadaPorUsuarioId == usuario.Id)
            .OrderBy(o => o.Id)
            .ToListAsync();

        return ordenes.Select(ARespuesta).ToList();
    }

    public async Task<Resultado<OrdenDeCompraRespuesta>> ConsultarAsync(Usuario usuario, int idOrden)
    {
        var orden = await BuscarVisibleAsync(usuario, idOrden);
        return orden is null
            ? Resultado<OrdenDeCompraRespuesta>.Error(TipoError.NoEncontrado, MensajeOrdenNoExiste)
            : Resultado<OrdenDeCompraRespuesta>.Ok(ARespuesta(orden));
    }

    public async Task<Resultado<OrdenDeCompraRespuesta>> EnviarAsync(Usuario usuario, int idOrden)
    {
        var orden = await BuscarVisibleAsync(usuario, idOrden);
        return await CambiarEstadoAsync(usuario, orden, EstadoOrdenCompra.Enviada);
    }

    public async Task<Resultado<OrdenDeCompraRespuesta>> CancelarAsync(Usuario usuario, int idOrden)
    {
        var orden = await BuscarVisibleAsync(usuario, idOrden);
        return await CambiarEstadoAsync(usuario, orden, EstadoOrdenCompra.Cancelada);
    }

    /// <summary>
    /// Registra que llegó la mercancía. Un Estándar solo puede hacerlo si Gestión de permisos le
    /// concedió RecibirOrdenesDeCompra, aunque la orden no sea suya.
    /// </summary>
    public async Task<Resultado<OrdenDeCompraRespuesta>> RecibirAsync(Usuario usuario, int idOrden)
    {
        var orden = await _db.OrdenesDeCompra.FindAsync(idOrden);

        if (usuario.Rol != Rol.Administrador)
        {
            var permisos = await _gestionPermisos.ListarPermisosOtorgadosAsync(usuario.Id);
            if (!permisos.Contains(PermisoSolicitable.RecibirOrdenesDeCompra))
            {
                return Resultado<OrdenDeCompraRespuesta>.Error(TipoError.Prohibido, MensajeNecesitaPermiso);
            }
        }

        return await CambiarEstadoAsync(usuario, orden, EstadoOrdenCompra.Recibida);
    }

    private async Task<Resultado<OrdenDeCompraRespuesta>> CambiarEstadoAsync(Usuario usuario, OrdenDeCompra? orden, EstadoOrdenCompra hacia)
    {
        if (orden is null)
        {
            return Resultado<OrdenDeCompraRespuesta>.Error(TipoError.NoEncontrado, MensajeOrdenNoExiste);
        }

        var cambio = MaquinaEstadosOrdenCompra.Aplicar(orden, hacia, usuario.Rol, _reloj);
        if (!cambio.Exito)
        {
            return Resultado<OrdenDeCompraRespuesta>.Error(cambio.TipoError, cambio.Mensaje);
        }

        await _db.SaveChangesAsync();
        return Resultado<OrdenDeCompraRespuesta>.Ok(ARespuesta(orden));
    }

    /// <summary>La orden si el usuario puede trabajar con ella; si no, null, igual que si no existiera.</summary>
    private async Task<OrdenDeCompra?> BuscarVisibleAsync(Usuario usuario, int idOrden)
    {
        var orden = await _db.OrdenesDeCompra.FindAsync(idOrden);
        var esVisible = orden is not null && (usuario.Rol == Rol.Administrador || orden.CreadaPorUsuarioId == usuario.Id);
        return esVisible ? orden : null;
    }

    private static OrdenDeCompraRespuesta ARespuesta(OrdenDeCompra orden) => new(
        orden.Id, orden.Proveedor, orden.Estado, orden.CreadaPorUsuarioId, orden.FechaCreacion, orden.FechaActualizacion, orden.MontoDop);
}
