using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Inventario.OrdenesDeCompra;
using SaveStock.Tests.Apoyo;

namespace SaveStock.Tests.Unitarias.Inventario;

/// <summary>
/// Una prueba por cada fila de las tablas de docs/maquina-de-estados.md. No levantan la web (RD-12)
/// y cada una crea su propia orden.
/// </summary>
public class MaquinaEstadosOrdenCompraTests
{
    private static OrdenDeCompra OrdenEn(EstadoOrdenCompra estado, RelojFalso reloj)
    {
        var orden = new OrdenDeCompra { Proveedor = "Distribuidora Central" };

        // El estado solo cambia por la máquina, así que se llega a él por transiciones permitidas.
        if (estado is EstadoOrdenCompra.Enviada or EstadoOrdenCompra.Recibida)
        {
            MaquinaEstadosOrdenCompra.Aplicar(orden, EstadoOrdenCompra.Enviada, Rol.Administrador, reloj);
        }

        if (estado is EstadoOrdenCompra.Recibida or EstadoOrdenCompra.Cancelada)
        {
            MaquinaEstadosOrdenCompra.Aplicar(orden, estado, Rol.Administrador, reloj);
        }

        return orden;
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-03")]
    public void OrdenNueva_NaceEnBorrador()
    {
        // Preparar y actuar
        var orden = new OrdenDeCompra();

        // Verificar
        Assert.Equal(EstadoOrdenCompra.Borrador, orden.Estado);
    }

    [Fact]
    [Trait("Requisito", "RD-04")]
    public void Enviar_OrdenEnBorradorConProveedor_QuedaEnviada()
    {
        // Preparar
        var reloj = new RelojFalso();
        var orden = OrdenEn(EstadoOrdenCompra.Borrador, reloj);

        // Actuar
        var resultado = MaquinaEstadosOrdenCompra.Aplicar(orden, EstadoOrdenCompra.Enviada, Rol.Estandar, reloj);

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoOrdenCompra.Enviada, orden.Estado);
    }

    [Theory]
    [Trait("Requisito", "RD-04")]
    [InlineData("")]
    [InlineData("   ")]
    public void Enviar_OrdenSinProveedor_SeRechazaYSigueEnBorrador(string proveedor)
    {
        // Preparar
        var reloj = new RelojFalso();
        var orden = new OrdenDeCompra { Proveedor = proveedor };

        // Actuar
        var resultado = MaquinaEstadosOrdenCompra.Aplicar(orden, EstadoOrdenCompra.Enviada, Rol.Administrador, reloj);

        // Verificar
        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal(EstadoOrdenCompra.Borrador, orden.Estado);
    }

    [Theory]
    [Trait("Requisito", "RD-04")]
    [InlineData(Rol.Estandar)]
    [InlineData(Rol.Administrador)]
    public void Recibir_OrdenEnviada_QuedaRecibida(Rol rol)
    {
        // Preparar
        var reloj = new RelojFalso();
        var orden = OrdenEn(EstadoOrdenCompra.Enviada, reloj);

        // Actuar
        var resultado = MaquinaEstadosOrdenCompra.Aplicar(orden, EstadoOrdenCompra.Recibida, rol, reloj);

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoOrdenCompra.Recibida, orden.Estado);
    }

    [Fact]
    [Trait("Requisito", "RD-04")]
    public void Cancelar_OrdenEnBorrador_QuedaCancelada()
    {
        // Preparar
        var reloj = new RelojFalso();
        var orden = OrdenEn(EstadoOrdenCompra.Borrador, reloj);

        // Actuar
        var resultado = MaquinaEstadosOrdenCompra.Aplicar(orden, EstadoOrdenCompra.Cancelada, Rol.Estandar, reloj);

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoOrdenCompra.Cancelada, orden.Estado);
    }

    [Fact]
    [Trait("Requisito", "RD-04")]
    public void Cancelar_OrdenEnviadaPorUnAdministrador_QuedaCancelada()
    {
        // Preparar
        var reloj = new RelojFalso();
        var orden = OrdenEn(EstadoOrdenCompra.Enviada, reloj);

        // Actuar
        var resultado = MaquinaEstadosOrdenCompra.Aplicar(orden, EstadoOrdenCompra.Cancelada, Rol.Administrador, reloj);

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoOrdenCompra.Cancelada, orden.Estado);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-06")]
    public void Cancelar_OrdenEnviadaPorUnEstandar_SeRechazaYSigueEnviada()
    {
        // Preparar
        var reloj = new RelojFalso();
        var orden = OrdenEn(EstadoOrdenCompra.Enviada, reloj);

        // Actuar
        var resultado = MaquinaEstadosOrdenCompra.Aplicar(orden, EstadoOrdenCompra.Cancelada, Rol.Estandar, reloj);

        // Verificar
        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        Assert.Equal(EstadoOrdenCompra.Enviada, orden.Estado);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-04")]
    public void Aplicar_DeRecibidaABorrador_SeRechazaYSigueRecibida()
    {
        // Preparar
        var reloj = new RelojFalso();
        var orden = OrdenEn(EstadoOrdenCompra.Recibida, reloj);

        // Actuar
        var resultado = MaquinaEstadosOrdenCompra.Aplicar(orden, EstadoOrdenCompra.Borrador, Rol.Administrador, reloj);

        // Verificar
        Assert.False(resultado.Exito);
        Assert.Contains("Recibida a Borrador", resultado.Mensaje);
        Assert.Equal(EstadoOrdenCompra.Recibida, orden.Estado);
    }

    [Theory]
    [Trait("Requisito", "RF-NEG-04")]
    [InlineData(EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Borrador)]
    [InlineData(EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Recibida)]
    [InlineData(EstadoOrdenCompra.Enviada, EstadoOrdenCompra.Borrador)]
    [InlineData(EstadoOrdenCompra.Enviada, EstadoOrdenCompra.Enviada)]
    public void Aplicar_CombinacionFueraDeLaTabla_SeRechazaYElEstadoNoCambia(EstadoOrdenCompra desde, EstadoOrdenCompra hacia)
    {
        // Preparar
        var reloj = new RelojFalso();
        var orden = OrdenEn(desde, reloj);

        // Actuar
        var resultado = MaquinaEstadosOrdenCompra.Aplicar(orden, hacia, Rol.Administrador, reloj);

        // Verificar
        Assert.False(resultado.Exito);
        Assert.Equal(desde, orden.Estado);
    }

    [Theory]
    [Trait("Requisito", "RF-NEG-05")]
    [InlineData(EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Borrador)]
    [InlineData(EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Enviada)]
    [InlineData(EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Cancelada)]
    [InlineData(EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Recibida)]
    [InlineData(EstadoOrdenCompra.Cancelada, EstadoOrdenCompra.Borrador)]
    [InlineData(EstadoOrdenCompra.Cancelada, EstadoOrdenCompra.Enviada)]
    [InlineData(EstadoOrdenCompra.Cancelada, EstadoOrdenCompra.Recibida)]
    [InlineData(EstadoOrdenCompra.Cancelada, EstadoOrdenCompra.Cancelada)]
    public void Aplicar_DesdeUnEstadoTerminal_SeRechazaYElEstadoNoCambia(EstadoOrdenCompra terminal, EstadoOrdenCompra hacia)
    {
        // Preparar
        var reloj = new RelojFalso();
        var orden = OrdenEn(terminal, reloj);

        // Actuar
        var resultado = MaquinaEstadosOrdenCompra.Aplicar(orden, hacia, Rol.Administrador, reloj);

        // Verificar
        Assert.False(resultado.Exito);
        Assert.Equal(terminal, orden.Estado);
    }

    [Fact]
    [Trait("Requisito", "RF-NEG-05")]
    public void Transiciones_NingunaParteDeUnEstadoTerminal()
    {
        // Preparar
        var terminales = MaquinaEstadosOrdenCompra.EstadosTerminales;

        // Actuar
        var desdeTerminales = MaquinaEstadosOrdenCompra.Transiciones.Where(t => terminales.Contains(t.Desde));

        // Verificar
        Assert.Empty(desdeTerminales);
    }
}
