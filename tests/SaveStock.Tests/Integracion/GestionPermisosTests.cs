using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Tests.Integracion;

/// <summary>
/// Gestión de permisos a través de la API (RF-GP-01 a RF-GP-07, RD-06, RD-07). La base se comparte
/// dentro de la clase, así que cada prueba crea sus propios usuarios con correos únicos.
/// </summary>
public class GestionPermisosTests : IClassFixture<SaveStockFactory>
{
    private readonly SaveStockFactory _fabrica;

    public GestionPermisosTests(SaveStockFactory fabrica)
    {
        _fabrica = fabrica;
    }

    private Task<Usuario> CrearAsync(string prefijo, Rol rol) =>
        _fabrica.CrearUsuarioAsync("Usuario " + prefijo, $"{prefijo}.{Guid.NewGuid():N}@negocio.com", "clave1234", rol);

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<JsonElement>())!;

    private static async Task<int> CrearSolicitudAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsync("/api/permisos/solicitudes", Json("{\"permiso\":\"VerListadoUsuarios\"}"));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await LeerJsonAsync(respuesta)).GetProperty("id").GetInt32();
    }

    [Fact]
    [Trait("Requisito", "RF-GP-05")]
    public async Task ListarUsuarios_EstandarConSolicitudAprobada_PasaDe403A200SinOtroPaso()
    {
        // Preparar
        var estandar = await CrearAsync("solicitante", Rol.Estandar);
        var admin = await CrearAsync("admin", Rol.Administrador);
        using var clienteEstandar = await _fabrica.ClienteConSesionAsync(estandar);
        using var clienteAdmin = await _fabrica.ClienteConSesionAsync(admin);
        var antes = await clienteEstandar.GetAsync("/api/admin/usuarios");
        var id = await CrearSolicitudAsync(clienteEstandar);

        // Actuar
        var aprobacion = await clienteAdmin.PostAsync($"/api/permisos/solicitudes/{id}/aprobar", null);
        var despues = await clienteEstandar.GetAsync("/api/admin/usuarios");

        // Verificar
        Assert.Equal(HttpStatusCode.Forbidden, antes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aprobacion.StatusCode);
        Assert.Equal("Aplicada", (await LeerJsonAsync(aprobacion)).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.OK, despues.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-02")]
    public async Task Mias_DeUnEstandar_NoMuestraLasSolicitudesDeOtrosUsuarios()
    {
        // Preparar
        var ana = await CrearAsync("ana", Rol.Estandar);
        var beto = await CrearAsync("beto", Rol.Estandar);
        using var clienteAna = await _fabrica.ClienteConSesionAsync(ana);
        using var clienteBeto = await _fabrica.ClienteConSesionAsync(beto);
        await CrearSolicitudAsync(clienteBeto);

        // Actuar
        var respuesta = await clienteAna.GetAsync("/api/permisos/solicitudes/mias");

        // Verificar
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty((await LeerJsonAsync(respuesta)).EnumerateArray());
    }

    [Fact]
    [Trait("Requisito", "RF-GP-02")]
    public async Task Pendientes_ConUnEstandar_Devuelve403()
    {
        // Preparar
        var estandar = await CrearAsync("curioso", Rol.Estandar);
        using var cliente = await _fabrica.ClienteConSesionAsync(estandar);

        // Actuar
        var respuesta = await cliente.GetAsync("/api/permisos/solicitudes/pendientes");

        // Verificar
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    [Trait("Requisito", "RD-07")]
    public async Task CrearSolicitud_ConJsonMalFormado_Devuelve400ConMensajeControlado()
    {
        // Preparar
        var estandar = await CrearAsync("mal-formado", Rol.Estandar);
        using var cliente = await _fabrica.ClienteConSesionAsync(estandar);

        // Actuar
        var respuesta = await cliente.PostAsync("/api/permisos/solicitudes", Json("{\"permiso\":"));

        // Verificar
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("La solicitud no es válida.", (await LeerJsonAsync(respuesta)).GetProperty("mensaje").GetString());
    }

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public async Task Aprobar_DosVecesPorLaApi_LaSegundaDevuelve409()
    {
        // Preparar
        var estandar = await CrearAsync("doble", Rol.Estandar);
        var admin = await CrearAsync("admin-doble", Rol.Administrador);
        using var clienteEstandar = await _fabrica.ClienteConSesionAsync(estandar);
        using var clienteAdmin = await _fabrica.ClienteConSesionAsync(admin);
        var id = await CrearSolicitudAsync(clienteEstandar);
        await clienteAdmin.PostAsync($"/api/permisos/solicitudes/{id}/aprobar", null);

        // Actuar
        var segunda = await clienteAdmin.PostAsync($"/api/permisos/solicitudes/{id}/aprobar", null);

        // Verificar
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal("La solicitud está en estado Aplicada y no se puede aprobar.", (await LeerJsonAsync(segunda)).GetProperty("mensaje").GetString());
    }
}
