using System.Net;
using System.Net.Http.Json;
using System.Text;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.Documentos;

namespace SaveStock.Tests.Integracion;

/// <summary>
/// Los endpoints /api/documentos de punta a punta: multipart, sesión y rechazos HTTP (RD-06, RD-07).
/// </summary>
public class DocumentosTests : IClassFixture<SaveStockFactory>
{
    private readonly SaveStockFactory _fabrica;

    public DocumentosTests(SaveStockFactory fabrica)
    {
        _fabrica = fabrica;
    }

    private static MultipartFormDataContent Formulario(string nombre, string texto)
    {
        var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(texto));
        archivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        return new MultipartFormDataContent { { archivo, "archivo", nombre } };
    }

    private async Task<HttpClient> ClienteNuevoAsync(Rol rol = Rol.Estandar)
    {
        var correo = $"{Guid.NewGuid():N}@negocio.com";
        var usuario = await _fabrica.CrearUsuarioAsync("Prueba", correo, "clave1234", rol);
        return await _fabrica.ClienteConSesionAsync(usuario);
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-01")]
    public async Task Subir_SinSesion_Responde401()
    {
        // Preparar
        var cliente = _fabrica.CreateClient();

        // Actuar
        var respuesta = await cliente.PostAsync("/api/documentos", Formulario("a.txt", "hola"));

        // Verificar
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RD-07")]
    public async Task Subir_SinArchivo_Responde400ConMensaje()
    {
        // Preparar
        var cliente = await ClienteNuevoAsync();

        // Actuar
        var respuesta = await cliente.PostAsync("/api/documentos", new MultipartFormDataContent { { new StringContent("x"), "otro" } });

        // Verificar
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("archivo", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Requisito", "RF-DOC-03")]
    public async Task SubirYDescargar_ElDueno_RecibeElMismoContenido()
    {
        // Preparar
        var cliente = await ClienteNuevoAsync();
        var subida = await cliente.PostAsync("/api/documentos", Formulario("nota.txt", "contenido original"));
        var documento = await subida.Content.ReadFromJsonAsync<DocumentoRespuesta>();

        // Actuar
        var respuesta = await cliente.GetAsync($"/api/documentos/{documento!.Id}/descargar");

        // Verificar
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("contenido original", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Requisito", "RD-06")]
    public async Task DescargarYEliminar_OtroEstandar_Responden403()
    {
        // Preparar
        var dueno = await ClienteNuevoAsync();
        var otro = await ClienteNuevoAsync();
        var subida = await dueno.PostAsync("/api/documentos", Formulario("privado.txt", "secreto"));
        var documento = await subida.Content.ReadFromJsonAsync<DocumentoRespuesta>();

        // Actuar
        var descarga = await otro.GetAsync($"/api/documentos/{documento!.Id}/descargar");
        var eliminacion = await otro.DeleteAsync($"/api/documentos/{documento.Id}");

        // Verificar
        Assert.Equal(HttpStatusCode.Forbidden, descarga.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, eliminacion.StatusCode);
    }
}
