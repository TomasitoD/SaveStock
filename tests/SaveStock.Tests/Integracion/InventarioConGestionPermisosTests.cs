using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.GestionPermisos;
using SaveStock.Inventario.Datos;
using SaveStock.Inventario.OrdenesDeCompra;
using SaveStock.Tests.Apoyo;

namespace SaveStock.Tests.Integracion;

/// <summary>
/// Inventario y Gestión de permisos reales, sin dobles y sin levantar la aplicación: cada servicio
/// usa su propia base SQLite en memoria. Prueban que recibir una orden depende del permiso aprobado.
/// </summary>
public sealed class InventarioConGestionPermisosTests : IDisposable
{
    private readonly BaseDatosEnMemoria _bd = new();
    private readonly SqliteConnection _conexionInventario = new("Data Source=:memory:");
    private readonly RelojFalso _reloj = new();

    public InventarioConGestionPermisosTests()
    {
        _conexionInventario.Open();
        using var db = CrearInventario();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _conexionInventario.Dispose();
        _bd.Dispose();
    }

    private InventarioDbContext CrearInventario() => new(
        new DbContextOptionsBuilder<InventarioDbContext>().UseSqlite(_conexionInventario).Options);

    private ServicioGestionPermisos GestionPermisos() => new(_bd.CrearContexto(), _reloj);

    private ServicioOrdenesCompra Ordenes() => new(CrearInventario(), GestionPermisos(), _reloj);

    private async Task<int> CrearOrdenEnviadaAsync(Usuario usuario)
    {
        var creada = await Ordenes().CrearAsync(usuario, "Distribuidora Central");
        await Ordenes().EnviarAsync(usuario, creada.Valor!.Id);
        return creada.Valor.Id;
    }

    private async Task<EstadoOrdenCompra> EstadoDeAsync(int idOrden)
    {
        using var db = CrearInventario();
        return (await db.OrdenesDeCompra.SingleAsync(o => o.Id == idOrden)).Estado;
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task RecibirOrden_SeRechazaAntesDeAprobarYSePermiteDespues()
    {
        // Preparar
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var idOrden = await CrearOrdenEnviadaAsync(estandar);
        var solicitud = await GestionPermisos().CrearSolicitudAsync(estandar, nameof(PermisoSolicitable.RecibirOrdenesDeCompra));

        // Actuar y verificar: con la solicitud sin aprobar, se rechaza y el estado no cambia.
        var antes = await Ordenes().RecibirAsync(estandar, idOrden);
        Assert.Equal(TipoError.Prohibido, antes.TipoError);
        Assert.Equal(EstadoOrdenCompra.Enviada, await EstadoDeAsync(idOrden));

        // Actuar y verificar: cuando un Administrador aprueba, la misma operación se permite.
        await GestionPermisos().AprobarAsync(admin, solicitud.Valor!.Id);
        var despues = await Ordenes().RecibirAsync(estandar, idOrden);
        Assert.True(despues.Exito);
        Assert.Equal(EstadoOrdenCompra.Recibida, await EstadoDeAsync(idOrden));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task RecibirOrden_ConLaSolicitudRechazada_SigueProhibido()
    {
        // Preparar
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var idOrden = await CrearOrdenEnviadaAsync(estandar);
        var solicitud = await GestionPermisos().CrearSolicitudAsync(estandar, nameof(PermisoSolicitable.RecibirOrdenesDeCompra));
        await GestionPermisos().RechazarAsync(admin, solicitud.Valor!.Id, "No trabaja en el almacén.");

        // Actuar
        var resultado = await Ordenes().RecibirAsync(estandar, idOrden);

        // Verificar
        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        Assert.Equal(EstadoOrdenCompra.Enviada, await EstadoDeAsync(idOrden));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task RecibirOrden_ConElPermisoDeOtroUsuario_SigueProhibido()
    {
        // Preparar
        var conPermiso = await _bd.CrearUsuarioAsync("con@negocio.com");
        var sinPermiso = await _bd.CrearUsuarioAsync("sin@negocio.com");
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var idOrden = await CrearOrdenEnviadaAsync(sinPermiso);
        var solicitud = await GestionPermisos().CrearSolicitudAsync(conPermiso, nameof(PermisoSolicitable.RecibirOrdenesDeCompra));
        await GestionPermisos().AprobarAsync(admin, solicitud.Valor!.Id);

        // Actuar
        var resultado = await Ordenes().RecibirAsync(sinPermiso, idOrden);

        // Verificar
        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        Assert.Equal(EstadoOrdenCompra.Enviada, await EstadoDeAsync(idOrden));
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task CrearOrden_NaceEnBorradorYSeGuardaConSuCreador()
    {
        // Preparar
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");

        // Actuar
        var resultado = await Ordenes().CrearAsync(estandar, "Distribuidora Central");

        // Verificar
        Assert.Equal(EstadoOrdenCompra.Borrador, await EstadoDeAsync(resultado.Valor!.Id));
        Assert.Equal(estandar.Id, resultado.Valor.CreadaPorUsuarioId);
    }

    [Theory]
    [Trait("Requisito", "RF-NEG-02")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CrearOrden_SinProveedor_SeRechaza(string? proveedor)
    {
        // Preparar
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");

        // Actuar
        var resultado = await Ordenes().CrearAsync(estandar, proveedor);

        // Verificar
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task ListarOrdenes_DeUnEstandar_SoloMuestraLasSuyas()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var luis = await _bd.CrearUsuarioAsync("luis@negocio.com");
        var deAna = await Ordenes().CrearAsync(ana, "Proveedor de Ana");
        await Ordenes().CrearAsync(luis, "Proveedor de Luis");

        // Actuar
        var ordenes = await Ordenes().ListarAsync(ana);

        // Verificar
        Assert.Equal(deAna.Valor!.Id, Assert.Single(ordenes).Id);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-02")]
    public async Task ConsultarOrden_DeOtroEstandar_DevuelveNoEncontrado()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var luis = await _bd.CrearUsuarioAsync("luis@negocio.com");
        var deLuis = await Ordenes().CrearAsync(luis, "Proveedor de Luis");

        // Actuar
        var resultado = await Ordenes().ConsultarAsync(ana, deLuis.Valor!.Id);

        // Verificar
        Assert.Equal(TipoError.NoEncontrado, resultado.TipoError);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public async Task CancelarOrdenEnviada_PorUnEstandar_SeRechazaYSigueEnviada()
    {
        // Preparar
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        var idOrden = await CrearOrdenEnviadaAsync(estandar);

        // Actuar
        var resultado = await Ordenes().CancelarAsync(estandar, idOrden);

        // Verificar
        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        Assert.Equal(EstadoOrdenCompra.Enviada, await EstadoDeAsync(idOrden));
    }
}
