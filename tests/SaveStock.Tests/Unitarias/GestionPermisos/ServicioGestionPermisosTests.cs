using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Autorizacion;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.Datos;
using SaveStock.Core.GestionPermisos;
using SaveStock.Tests.Apoyo;

namespace SaveStock.Tests.Unitarias.GestionPermisos;

/// <summary>
/// Pruebas del servicio de gestión de permisos contra una base SQLite en memoria, sin levantar la
/// web (RD-12). xUnit crea una instancia de esta clase por prueba: cada una tiene su propia base y
/// su propio reloj, así que pasan en cualquier orden y por separado.
/// </summary>
public sealed class ServicioGestionPermisosTests : IDisposable
{
    private const string Permiso = "VerListadoUsuarios";

    private readonly BaseDatosEnMemoria _bd = new();
    private readonly RelojFalso _reloj = new();
    private readonly List<CoreDbContext> _contextos = [];

    public void Dispose()
    {
        foreach (var contexto in _contextos)
        {
            contexto.Dispose();
        }

        _bd.Dispose();
    }

    /// <summary>Un servicio con un contexto nuevo, como si fuera otra petición.</summary>
    private ServicioGestionPermisos NuevoServicio()
    {
        var contexto = _bd.CrearContexto();
        _contextos.Add(contexto);
        return new ServicioGestionPermisos(contexto, _reloj);
    }

    /// <summary>Lee la solicitud tal como quedó guardada, desde un contexto nuevo.</summary>
    private async Task<SolicitudPermiso> LeerSolicitudAsync(int id)
    {
        using var db = _bd.CrearContexto();
        return await db.SolicitudesPermiso.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    private async Task<int> CrearSolicitudAsync(Usuario solicitante)
    {
        var creada = await NuevoServicio().CrearSolicitudAsync(solicitante, Permiso);
        return creada.Valor!.Id;
    }

    // RF-GP-01: crear.

    [Fact]
    [Trait("Requisito", "RF-GP-01")]
    public async Task CrearSolicitud_PermisoValido_NaceEnPendiente()
    {
        // Preparar
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");

        // Actuar
        var resultado = await NuevoServicio().CrearSolicitudAsync(estandar, Permiso);

        // Verificar
        Assert.True(resultado.Exito);
        var guardada = await LeerSolicitudAsync(resultado.Valor!.Id);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
    }

    [Theory]
    [Trait("Requisito", "RD-07")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SerAdministrador")]
    [InlineData("0")]
    public async Task CrearSolicitud_PermisoQueNoExiste_SeRechazaYNoGuardaNada(string? permiso)
    {
        // Preparar
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");

        // Actuar
        var resultado = await NuevoServicio().CrearSolicitudAsync(estandar, permiso);

        // Verificar
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal("El permiso solicitado no existe. Permisos válidos: VerListadoUsuarios.", resultado.Mensaje);
        using var db = _bd.CrearContexto();
        Assert.Equal(0, await db.SolicitudesPermiso.CountAsync());
    }

    [Fact]
    [Trait("Requisito", "RF-GP-01")]
    public async Task CrearSolicitud_YaHayUnaPendienteDelMismoPermiso_SeRechazaConConflicto()
    {
        // Preparar
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        await CrearSolicitudAsync(estandar);

        // Actuar
        var resultado = await NuevoServicio().CrearSolicitudAsync(estandar, Permiso);

        // Verificar
        Assert.Equal(TipoError.Conflicto, resultado.TipoError);
        Assert.Equal(ServicioGestionPermisos.MensajeYaHayPendiente, resultado.Mensaje);
    }

    // RF-GP-02: consultar.

    [Fact]
    [Trait("Requisito", "RF-GP-02")]
    public async Task ListarMias_ConSolicitudesDeVariosUsuarios_SoloDevuelveLasDelPropioUsuario()
    {
        // Preparar
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var beto = await _bd.CrearUsuarioAsync("beto@negocio.com");
        var idDeAna = await CrearSolicitudAsync(ana);
        await CrearSolicitudAsync(beto);

        // Actuar
        var mias = await NuevoServicio().ListarMiasAsync(ana);

        // Verificar
        var unica = Assert.Single(mias);
        Assert.Equal(idDeAna, unica.Id);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-02")]
    public async Task ListarPendientes_ConUnaYaResuelta_SoloDevuelveLasQueSiguenPendientes()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var ana = await _bd.CrearUsuarioAsync("ana@negocio.com");
        var beto = await _bd.CrearUsuarioAsync("beto@negocio.com");
        var idDeAna = await CrearSolicitudAsync(ana);
        var idDeBeto = await CrearSolicitudAsync(beto);
        await NuevoServicio().AprobarAsync(admin, idDeAna);

        // Actuar
        var pendientes = await NuevoServicio().ListarPendientesAsync();

        // Verificar
        var unica = Assert.Single(pendientes);
        Assert.Equal(idDeBeto, unica.Id);
    }

    // RF-GP-03 y RF-GP-05: aprobar registra quién y cuándo, y aplica el permiso.

    [Fact]
    [Trait("Requisito", "RF-GP-03")]
    public async Task Aprobar_SolicitudPendiente_QuedaAplicadaYRegistraQuienAproboYCuando()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        var id = await CrearSolicitudAsync(estandar);
        _reloj.Avanzar(TimeSpan.FromHours(3)); // La aprobación ocurre después de la creación.

        // Actuar
        var resultado = await NuevoServicio().AprobarAsync(admin, id);

        // Verificar
        Assert.True(resultado.Exito);
        var guardada = await LeerSolicitudAsync(id);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, guardada.Estado);
        Assert.Equal(admin.Id, guardada.AprobadorId);
        Assert.Equal(_reloj.AhoraUtc, guardada.FechaResolucion);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-05")]
    public async Task Aprobar_SolicitudPendiente_OtorgaElPermisoAlSolicitanteSinOtroPaso()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        var id = await CrearSolicitudAsync(estandar);

        // Actuar
        await NuevoServicio().AprobarAsync(admin, id);

        // Verificar
        using var db = _bd.CrearContexto();
        var otorgado = await db.PermisosOtorgados.SingleAsync();
        Assert.Equal(estandar.Id, otorgado.UsuarioId);
        Assert.Equal(PermisoSolicitable.VerListadoUsuarios, otorgado.Permiso);
        Assert.Equal(id, otorgado.SolicitudId);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-03")]
    public async Task Aprobar_PorUnUsuarioEstandar_SeRechazaYSiguePendiente()
    {
        // Preparar: el servicio se llama directo, sin el filtro de la Web.
        var solicitante = await _bd.CrearUsuarioAsync("solicitante@negocio.com");
        var otroEstandar = await _bd.CrearUsuarioAsync("otro@negocio.com");
        var id = await CrearSolicitudAsync(solicitante);

        // Actuar
        var resultado = await NuevoServicio().AprobarAsync(otroEstandar, id);

        // Verificar
        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        Assert.Equal("Solo un Administrador puede resolver solicitudes de permiso.", resultado.Mensaje);
        var guardada = await LeerSolicitudAsync(id);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.AprobadorId);
        Assert.Null(guardada.FechaResolucion);
        using var db = _bd.CrearContexto();
        Assert.Equal(0, await db.PermisosOtorgados.CountAsync());
    }

    // RF-GP-04: rechazar.

    [Fact]
    [Trait("Requisito", "RF-GP-04")]
    public async Task Rechazar_ConMotivo_QuedaRechazadaYGuardaElMotivo()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        var id = await CrearSolicitudAsync(estandar);

        // Actuar
        var resultado = await NuevoServicio().RechazarAsync(admin, id, "Su puesto no necesita ver a los usuarios.");

        // Verificar
        Assert.True(resultado.Exito);
        var guardada = await LeerSolicitudAsync(id);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, guardada.Estado);
        Assert.Equal("Su puesto no necesita ver a los usuarios.", guardada.MotivoRechazo);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-04")]
    public async Task Rechazar_SinMotivo_NoSeCompletaYLaSolicitudSiguePendiente()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        var id = await CrearSolicitudAsync(estandar);

        // Actuar
        var resultado = await NuevoServicio().RechazarAsync(admin, id, "   ");

        // Verificar
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal("El rechazo necesita un motivo escrito.", resultado.Mensaje);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, (await LeerSolicitudAsync(id)).Estado);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-04")]
    public async Task Rechazar_PorUnUsuarioEstandar_SeRechazaYSiguePendiente()
    {
        // Preparar: el servicio se llama directo, sin el filtro de la Web.
        var solicitante = await _bd.CrearUsuarioAsync("solicitante@negocio.com");
        var otroEstandar = await _bd.CrearUsuarioAsync("otro@negocio.com");
        var id = await CrearSolicitudAsync(solicitante);

        // Actuar
        var resultado = await NuevoServicio().RechazarAsync(otroEstandar, id, "No lo necesita.");

        // Verificar
        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        Assert.Equal("Solo un Administrador puede resolver solicitudes de permiso.", resultado.Mensaje);
        var guardada = await LeerSolicitudAsync(id);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.AprobadorId);
        Assert.Null(guardada.FechaResolucion);
        Assert.Null(guardada.MotivoRechazo);
    }

    // RF-GP-06: transiciones prohibidas, el estado no cambia.

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public async Task Aprobar_DosVecesLaMismaSolicitud_LaSegundaSeRechazaYNadaCambia()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var otroAdmin = await _bd.CrearUsuarioAsync("admin2@negocio.com", rol: Rol.Administrador);
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        var id = await CrearSolicitudAsync(estandar);
        await NuevoServicio().AprobarAsync(admin, id);
        var antes = await LeerSolicitudAsync(id);
        _reloj.Avanzar(TimeSpan.FromHours(1));

        // Actuar
        var resultado = await NuevoServicio().AprobarAsync(otroAdmin, id);

        // Verificar
        Assert.Equal(TipoError.Conflicto, resultado.TipoError);
        Assert.Equal("La solicitud está en estado Aplicada y no se puede aprobar.", resultado.Mensaje);
        var despues = await LeerSolicitudAsync(id);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, despues.Estado);
        Assert.Equal(antes.AprobadorId, despues.AprobadorId);
        Assert.Equal(antes.FechaResolucion, despues.FechaResolucion);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public async Task Aprobar_SolicitudRechazada_SeRechazaYSigueRechazada()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        var id = await CrearSolicitudAsync(estandar);
        await NuevoServicio().RechazarAsync(admin, id, "No corresponde.");

        // Actuar
        var resultado = await NuevoServicio().AprobarAsync(admin, id);

        // Verificar
        Assert.Equal(TipoError.Conflicto, resultado.TipoError);
        Assert.Equal("La solicitud está en estado Rechazada y no se puede aprobar.", resultado.Mensaje);
        var guardada = await LeerSolicitudAsync(id);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, guardada.Estado);
        Assert.Null(guardada.FechaAplicacion);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public async Task Rechazar_SolicitudYaAplicada_SeRechazaYSigueAplicada()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        var id = await CrearSolicitudAsync(estandar);
        await NuevoServicio().AprobarAsync(admin, id);

        // Actuar
        var resultado = await NuevoServicio().RechazarAsync(admin, id, "Me arrepentí.");

        // Verificar
        Assert.Equal(TipoError.Conflicto, resultado.TipoError);
        var guardada = await LeerSolicitudAsync(id);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, guardada.Estado);
        Assert.Null(guardada.MotivoRechazo);
    }

    // RF-GP-07: nadie resuelve su propia solicitud.

    [Fact]
    [Trait("Requisito", "RF-GP-07")]
    public async Task Aprobar_SolicitudCreadaPorElMismoAdministrador_SeRechazaYSiguePendiente()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var id = await CrearSolicitudAsync(admin);

        // Actuar
        var resultado = await NuevoServicio().AprobarAsync(admin, id);

        // Verificar
        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        Assert.Equal("No puedes resolver tu propia solicitud.", resultado.Mensaje);
        var guardada = await LeerSolicitudAsync(id);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.AprobadorId);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-07")]
    public async Task Rechazar_SolicitudCreadaPorElMismoAdministrador_SeRechazaYSiguePendiente()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var id = await CrearSolicitudAsync(admin);

        // Actuar
        var resultado = await NuevoServicio().RechazarAsync(admin, id, "Me la rechazo a mí mismo.");

        // Verificar
        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        Assert.Equal(ServicioGestionPermisos.MensajePropiaSolicitud, resultado.Mensaje);
        var guardada = await LeerSolicitudAsync(id);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, guardada.Estado);
        Assert.Null(guardada.MotivoRechazo);
    }

    [Fact]
    public async Task Aprobar_SolicitudQueNoExiste_DevuelveNoEncontrado()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);

        // Actuar
        var resultado = await NuevoServicio().AprobarAsync(admin, 999);

        // Verificar
        Assert.Equal(TipoError.NoEncontrado, resultado.TipoError);
    }

    // RF-GP-05: el Autorizador usa el permiso otorgado.

    [Fact]
    [Trait("Requisito", "RF-GP-05")]
    public async Task Autorizar_ListarUsuarios_EstandarConSolicitudSinAprobar_EsProhibido()
    {
        // Preparar
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        await CrearSolicitudAsync(estandar);
        var permisos = await NuevoServicio().ListarPermisosOtorgadosAsync(estandar.Id);

        // Actuar
        var autorizacion = Autorizador.Autorizar(Operacion.ListarUsuarios, estandar, permisos);

        // Verificar
        Assert.Equal(TipoError.Prohibido, autorizacion.TipoError);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-05")]
    public async Task Autorizar_ListarUsuarios_EstandarConSolicitudAprobada_EstaPermitido()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        var id = await CrearSolicitudAsync(estandar);
        await NuevoServicio().AprobarAsync(admin, id);
        var permisos = await NuevoServicio().ListarPermisosOtorgadosAsync(estandar.Id);

        // Actuar
        var autorizacion = Autorizador.Autorizar(Operacion.ListarUsuarios, estandar, permisos);

        // Verificar
        Assert.True(autorizacion.Exito);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-05")]
    public async Task Autorizar_ListarUsuarios_OtroEstandarSinSolicitudAprobada_SigueProhibido()
    {
        // Preparar
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var conPermiso = await _bd.CrearUsuarioAsync("con-permiso@negocio.com");
        var sinPermiso = await _bd.CrearUsuarioAsync("sin-permiso@negocio.com");
        await NuevoServicio().AprobarAsync(admin, await CrearSolicitudAsync(conPermiso));
        var permisos = await NuevoServicio().ListarPermisosOtorgadosAsync(sinPermiso.Id);

        // Actuar
        var autorizacion = Autorizador.Autorizar(Operacion.ListarUsuarios, sinPermiso, permisos);

        // Verificar
        Assert.Equal(TipoError.Prohibido, autorizacion.TipoError);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-05")]
    public async Task Autorizar_CambiarRol_EstandarConPermisoDeVerListado_SigueProhibido()
    {
        // Preparar: el permiso habilita solo ListarUsuarios, no el resto de la administración.
        var admin = await _bd.CrearUsuarioAsync("admin@negocio.com", rol: Rol.Administrador);
        var estandar = await _bd.CrearUsuarioAsync("estandar@negocio.com");
        await NuevoServicio().AprobarAsync(admin, await CrearSolicitudAsync(estandar));
        var permisos = await NuevoServicio().ListarPermisosOtorgadosAsync(estandar.Id);

        // Actuar
        var autorizacion = Autorizador.Autorizar(Operacion.CambiarRol, estandar, permisos);

        // Verificar
        Assert.Equal(TipoError.Prohibido, autorizacion.TipoError);
    }

    // RD-09: las solicitudes sobreviven a reiniciar (un contexto nuevo sobre el mismo archivo).

    [Fact]
    [Trait("Requisito", "RD-09")]
    public async Task SolicitudGuardada_SeLeeDesdeUnContextoNuevoSobreElMismoArchivo()
    {
        // Preparar: una base en un archivo temporal propio de esta prueba.
        var ruta = Path.Combine(Path.GetTempPath(), $"savestock-gp-{Guid.NewGuid():N}.db");
        var opciones = new DbContextOptionsBuilder<CoreDbContext>().UseSqlite($"Data Source={ruta}").Options;
        try
        {
            int id;
            using (var primero = new CoreDbContext(opciones))
            {
                await primero.Database.EnsureCreatedAsync();
                var estandar = new Usuario { Nombre = "Ana", Correo = "ana@negocio.com", HashContrasena = "x", Activo = true };
                primero.Usuarios.Add(estandar);
                await primero.SaveChangesAsync();

                // Actuar
                var creada = await new ServicioGestionPermisos(primero, _reloj).CrearSolicitudAsync(estandar, Permiso);
                id = creada.Valor!.Id;
            }

            // Verificar: otro contexto, como la aplicación después de reiniciar.
            using var segundo = new CoreDbContext(opciones);
            var leida = await segundo.SolicitudesPermiso.AsNoTracking().SingleAsync(s => s.Id == id);
            Assert.Equal(EstadoSolicitudPermiso.Pendiente, leida.Estado);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(ruta);
        }
    }
}
