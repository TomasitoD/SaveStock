using System.Text;
using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.Documentos;
using SaveStock.Tests.Apoyo;

namespace SaveStock.Tests.Unitarias.Documentos;

/// <summary>
/// Manejador de documentos sin levantar la web: base SQLite en memoria y una carpeta temporal
/// propia para los archivos, que se borra al terminar cada prueba.
/// </summary>
public sealed class ServicioDocumentosTests : IDisposable
{
    private readonly BaseDatosEnMemoria _bd = new();
    private readonly RelojFalso _reloj = new();
    private readonly ConfiguracionSaveStock _configuracion = new()
    {
        RutaDocumentos = Path.Combine(Path.GetTempPath(), "savestock-documentos", Guid.NewGuid().ToString("N")),
    };

    public void Dispose()
    {
        _bd.Dispose();
        if (Directory.Exists(_configuracion.RutaDocumentos))
        {
            Directory.Delete(_configuracion.RutaDocumentos, recursive: true);
        }
    }

    private ServicioDocumentos Servicio() => new(_bd.CrearContexto(), _configuracion, _reloj);

    private static MemoryStream Contenido(string texto) => new(Encoding.UTF8.GetBytes(texto));

    private async Task<DocumentoRespuesta> SubirAsync(Usuario usuario, string nombre = "factura.txt", string texto = "Factura 001")
    {
        using var contenido = Contenido(texto);
        var resultado = await Servicio().SubirAsync(usuario, nombre, "text/plain", contenido.Length, contenido);
        return resultado.Valor!;
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-01")]
    public async Task Subir_ArchivoValido_RegistraNombreTipoTamanoFechaYQuienLoSubio()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        using var contenido = Contenido("Factura 001");

        // Actuar
        var resultado = await Servicio().SubirAsync(ana, "factura.txt", "text/plain", contenido.Length, contenido);

        // Verificar
        Assert.True(resultado.Exito);
        using var db = _bd.CrearContexto();
        var guardado = await db.Documentos.SingleAsync(d => d.Id == resultado.Valor!.Id);
        Assert.Equal("factura.txt", guardado.Nombre);
        Assert.Equal("text/plain", guardado.Tipo);
        Assert.Equal(11, guardado.TamanoBytes);
        Assert.Equal(_reloj.AhoraUtc, guardado.FechaSubida);
        Assert.Equal(ana.Id, guardado.SubidoPorUsuarioId);
        Assert.Equal("Factura 001", await File.ReadAllTextAsync(Path.Combine(_configuracion.RutaDocumentos, guardado.NombreArchivo)));
    }

    [Fact]
    [Trait("Requisito", "RD-07")]
    public async Task Subir_ArchivoVacio_SeRechazaSinGuardarNada()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        using var vacio = new MemoryStream();

        // Actuar
        var resultado = await Servicio().SubirAsync(ana, "vacio.txt", "text/plain", 0, vacio);

        // Verificar
        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        using var db = _bd.CrearContexto();
        Assert.Empty(db.Documentos);
    }

    [Fact]
    [Trait("Requisito", "RD-07")]
    public async Task Subir_SinArchivo_SeRechazaConMensajeComprensible()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");

        // Actuar
        var resultado = await Servicio().SubirAsync(ana, null, null, 0, null);

        // Verificar
        Assert.False(resultado.Exito);
        Assert.Equal(ServicioDocumentos.MensajeArchivoVacio, resultado.Mensaje);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-02")]
    public async Task Listar_UnEstandar_SoloVeLosSuyosSinLosEliminados()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var luis = await _bd.CrearUsuarioAsync("luis@negocio.com");
        var visible = await SubirAsync(ana, "visible.txt");
        var eliminado = await SubirAsync(ana, "eliminado.txt");
        await SubirAsync(luis, "de-luis.txt");
        await Servicio().EliminarAsync(ana, eliminado.Id);

        // Actuar
        var listado = await Servicio().ListarAsync(ana);

        // Verificar
        Assert.Equal([visible.Id], listado.Select(d => d.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-02")]
    public async Task Listar_UnAdministrador_VeLosDeTodosSinLosEliminados()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var luis = await _bd.CrearUsuarioAsync("luis@negocio.com");
        var deAna = await SubirAsync(ana, "de-ana.txt");
        var deLuis = await SubirAsync(luis, "de-luis.txt");
        var eliminado = await SubirAsync(luis, "eliminado.txt");
        await Servicio().EliminarAsync(luis, eliminado.Id);

        // Actuar
        var listado = await Servicio().ListarAsync(admin);

        // Verificar
        Assert.Equal([deAna.Id, deLuis.Id], listado.Select(d => d.Id));
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-03")]
    public async Task Descargar_ElDueno_ObtieneElArchivo()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var documento = await SubirAsync(ana, texto: "contenido de ana");

        // Actuar
        var resultado = await Servicio().DescargarAsync(ana, documento.Id);

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal("contenido de ana", await File.ReadAllTextAsync(resultado.Valor!.RutaArchivo));
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-03")]
    public async Task Descargar_UnAdministrador_ObtieneElArchivoDeOtro()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var documento = await SubirAsync(ana);

        // Actuar
        var resultado = await Servicio().DescargarAsync(admin, documento.Id);

        // Verificar
        Assert.True(resultado.Exito);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-03")]
    public async Task Descargar_OtroEstandar_SeRechazaComoProhibido()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var luis = await _bd.CrearUsuarioAsync("luis@negocio.com");
        var documento = await SubirAsync(ana);

        // Actuar
        var resultado = await Servicio().DescargarAsync(luis, documento.Id);

        // Verificar
        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-04")]
    public async Task Eliminar_ElDueno_MarcaElRegistroComoEliminadoSinBorrarloNiBorrarElArchivo()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var documento = await SubirAsync(ana);

        // Actuar
        var resultado = await Servicio().EliminarAsync(ana, documento.Id);

        // Verificar
        Assert.True(resultado.Exito);
        using var db = _bd.CrearContexto();
        var guardado = await db.Documentos.SingleAsync(d => d.Id == documento.Id);
        Assert.True(guardado.Eliminado);
        Assert.Equal(_reloj.AhoraUtc, guardado.FechaEliminacion);
        Assert.True(File.Exists(Path.Combine(_configuracion.RutaDocumentos, guardado.NombreArchivo)));
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-04")]
    public async Task Eliminar_OtroEstandar_SeRechazaYElDocumentoSigueVisible()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var luis = await _bd.CrearUsuarioAsync("luis@negocio.com");
        var documento = await SubirAsync(ana);

        // Actuar
        var resultado = await Servicio().EliminarAsync(luis, documento.Id);

        // Verificar
        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        using var db = _bd.CrearContexto();
        Assert.False((await db.Documentos.SingleAsync(d => d.Id == documento.Id)).Eliminado);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-04")]
    public async Task Descargar_UnDocumentoEliminado_SeRechazaComoNoEncontrado()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var documento = await SubirAsync(ana);
        await Servicio().EliminarAsync(ana, documento.Id);

        // Actuar
        var resultado = await Servicio().DescargarAsync(ana, documento.Id);

        // Verificar
        Assert.Equal(TipoError.NoEncontrado, resultado.TipoError);
    }
}
