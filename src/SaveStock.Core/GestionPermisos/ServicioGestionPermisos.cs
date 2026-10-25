using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.Datos;

namespace SaveStock.Core.GestionPermisos;

/// <summary>
/// Solicitud, aprobación y rechazo de permisos (RF-GP-01 a RF-GP-07). Quién puede llamar cada
/// método lo declara Permisos y lo aplica la Web (RD-06); además, aprobar y rechazar verifican
/// aquí mismo que quien resuelve sea Administrador. Los cambios de estado los decide solo
/// <see cref="MaquinaEstadosSolicitudPermiso"/> (RD-04).
/// </summary>
public class ServicioGestionPermisos
{
    public const string MensajeSolicitudNoExiste = "No existe una solicitud de permiso con ese id.";
    public const string MensajePropiaSolicitud = "No puedes resolver tu propia solicitud.";
    public const string MensajeYaTienePermiso = "Ya tienes ese permiso.";
    public const string MensajeYaHayPendiente = "Ya tienes una solicitud pendiente para ese permiso.";
    public const string MensajeSoloAdministrador = "Solo un Administrador puede resolver solicitudes de permiso.";

    /// <summary>"El permiso solicitado no existe. Permisos válidos: VerListadoUsuarios."</summary>
    public static readonly string MensajePermisoNoExiste =
        $"El permiso solicitado no existe. Permisos válidos: {string.Join(", ", Enum.GetNames<PermisoSolicitable>())}.";

    private readonly CoreDbContext _db;
    private readonly IReloj _reloj;

    public ServicioGestionPermisos(CoreDbContext db, IReloj reloj)
    {
        _db = db;
        _reloj = reloj;
    }

    /// <summary>
    /// Crea una solicitud en Pendiente (RF-GP-01). Rechaza un permiso que no está en el catálogo (RD-07),
    /// uno que el usuario ya tiene y uno que ya pidió y sigue pendiente.
    /// </summary>
    /// <param name="permisoTexto">El nombre del permiso tal como llega en el JSON, por ejemplo "VerListadoUsuarios".</param>
    public async Task<Resultado<SolicitudPermisoRespuesta>> CrearSolicitudAsync(Usuario solicitante, string? permisoTexto)
    {
        var permiso = LeerPermiso(permisoTexto);
        if (permiso is null)
        {
            return Resultado<SolicitudPermisoRespuesta>.Error(TipoError.Validacion, MensajePermisoNoExiste);
        }

        if (await _db.PermisosOtorgados.AnyAsync(p => p.UsuarioId == solicitante.Id && p.Permiso == permiso.Value))
        {
            return Resultado<SolicitudPermisoRespuesta>.Error(TipoError.Conflicto, MensajeYaTienePermiso);
        }

        var yaPendiente = await _db.SolicitudesPermiso.AnyAsync(s =>
            s.SolicitanteId == solicitante.Id && s.Permiso == permiso.Value && s.Estado == EstadoSolicitudPermiso.Pendiente);
        if (yaPendiente)
        {
            return Resultado<SolicitudPermisoRespuesta>.Error(TipoError.Conflicto, MensajeYaHayPendiente);
        }

        // El estado no se asigna aquí: toda solicitud nace en Pendiente (valor inicial de la entidad).
        var solicitud = new SolicitudPermiso
        {
            SolicitanteId = solicitante.Id,
            Permiso = permiso.Value,
            FechaCreacion = _reloj.AhoraUtc,
        };
        _db.SolicitudesPermiso.Add(solicitud);
        await _db.SaveChangesAsync();

        return Resultado<SolicitudPermisoRespuesta>.Ok(await LeerRespuestaAsync(solicitud.Id), "La solicitud se creó y está Pendiente.");
    }

    /// <summary>Solo las solicitudes del propio usuario (RF-GP-02): nunca las de otros.</summary>
    public async Task<IReadOnlyList<SolicitudPermisoRespuesta>> ListarMiasAsync(Usuario usuario)
    {
        var solicitudes = await ConsultaConUsuarios()
            .Where(s => s.SolicitanteId == usuario.Id)
            .OrderBy(s => s.Id)
            .ToListAsync();
        return solicitudes.Select(SolicitudPermisoRespuesta.De).ToList();
    }

    /// <summary>Las solicitudes que esperan resolución, de todos los usuarios (RF-GP-02). Solo Administrador.</summary>
    public async Task<IReadOnlyList<SolicitudPermisoRespuesta>> ListarPendientesAsync()
    {
        var solicitudes = await ConsultaConUsuarios()
            .Where(s => s.Estado == EstadoSolicitudPermiso.Pendiente)
            .OrderBy(s => s.Id)
            .ToListAsync();
        return solicitudes.Select(SolicitudPermisoRespuesta.De).ToList();
    }

    /// <summary>
    /// Aprueba la solicitud (RF-GP-03) y aplica el permiso en el mismo paso (RF-GP-05):
    /// Pendiente → Aprobada → Aplicada y se crea el <see cref="PermisoOtorgado"/>. Todo se guarda con
    /// un único SaveChanges, que EF Core ejecuta en una transacción: o queda todo, o nada.
    /// </summary>
    public async Task<Resultado<SolicitudPermisoRespuesta>> AprobarAsync(Usuario administrador, int idSolicitud)
    {
        var solicitud = await _db.SolicitudesPermiso.FindAsync(idSolicitud);
        if (solicitud is null)
        {
            return Resultado<SolicitudPermisoRespuesta>.Error(TipoError.NoEncontrado, MensajeSolicitudNoExiste);
        }

        // RF-GP-03 y RD-06: solo un Administrador aprueba, aunque alguien llame al servicio sin pasar por la Web.
        if (!PuedeResolver(administrador))
        {
            return Resultado<SolicitudPermisoRespuesta>.Error(TipoError.Prohibido, MensajeSoloAdministrador);
        }

        // RF-GP-07: se revisa antes de tocar el estado, así la solicitud queda igual.
        if (solicitud.SolicitanteId == administrador.Id)
        {
            return Resultado<SolicitudPermisoRespuesta>.Error(TipoError.Prohibido, MensajePropiaSolicitud);
        }

        var aprobacion = MaquinaEstadosSolicitudPermiso.Aplicar(
            solicitud, EstadoSolicitudPermiso.Aprobada, EjecutorTransicion.Administrador, _reloj, administradorId: administrador.Id);
        if (!aprobacion.Exito)
        {
            return Resultado<SolicitudPermisoRespuesta>.Error(aprobacion.TipoError, aprobacion.Mensaje);
        }

        // Aprobada → Aplicada la ejecuta el sistema, sin que nadie más intervenga.
        var aplicacion = MaquinaEstadosSolicitudPermiso.Aplicar(
            solicitud, EstadoSolicitudPermiso.Aplicada, EjecutorTransicion.Sistema, _reloj);
        if (!aplicacion.Exito)
        {
            throw new InvalidOperationException($"No se pudo aplicar una solicitud recién aprobada: {aplicacion.Mensaje}");
        }

        _db.PermisosOtorgados.Add(new PermisoOtorgado
        {
            UsuarioId = solicitud.SolicitanteId,
            Permiso = solicitud.Permiso,
            SolicitudId = solicitud.Id,
            FechaOtorgamiento = solicitud.FechaAplicacion!.Value,
        });
        await _db.SaveChangesAsync();

        return Resultado<SolicitudPermisoRespuesta>.Ok(await LeerRespuestaAsync(solicitud.Id), "La solicitud se aprobó y el permiso quedó aplicado.");
    }

    /// <summary>
    /// Rechaza la solicitud con un motivo escrito (RF-GP-04). Sin motivo no se completa y sigue Pendiente.
    /// </summary>
    public async Task<Resultado<SolicitudPermisoRespuesta>> RechazarAsync(Usuario administrador, int idSolicitud, string? motivo)
    {
        var solicitud = await _db.SolicitudesPermiso.FindAsync(idSolicitud);
        if (solicitud is null)
        {
            return Resultado<SolicitudPermisoRespuesta>.Error(TipoError.NoEncontrado, MensajeSolicitudNoExiste);
        }

        // RF-GP-03, RF-GP-04 y RD-06: solo un Administrador rechaza, aunque alguien llame al servicio sin pasar por la Web.
        if (!PuedeResolver(administrador))
        {
            return Resultado<SolicitudPermisoRespuesta>.Error(TipoError.Prohibido, MensajeSoloAdministrador);
        }

        // RF-GP-07: nadie rechaza su propia solicitud.
        if (solicitud.SolicitanteId == administrador.Id)
        {
            return Resultado<SolicitudPermisoRespuesta>.Error(TipoError.Prohibido, MensajePropiaSolicitud);
        }

        var rechazo = MaquinaEstadosSolicitudPermiso.Aplicar(
            solicitud, EstadoSolicitudPermiso.Rechazada, EjecutorTransicion.Administrador, _reloj,
            administradorId: administrador.Id, motivo: motivo);
        if (!rechazo.Exito)
        {
            return Resultado<SolicitudPermisoRespuesta>.Error(rechazo.TipoError, rechazo.Mensaje);
        }

        await _db.SaveChangesAsync();
        return Resultado<SolicitudPermisoRespuesta>.Ok(await LeerRespuestaAsync(solicitud.Id), "La solicitud se rechazó.");
    }

    /// <summary>Los permisos que el usuario recibió por solicitudes aprobadas. Lo usa la autorización (RF-GP-05).</summary>
    public async Task<IReadOnlyCollection<PermisoSolicitable>> ListarPermisosOtorgadosAsync(int idUsuario)
    {
        return await _db.PermisosOtorgados
            .AsNoTracking()
            .Where(p => p.UsuarioId == idUsuario)
            .Select(p => p.Permiso)
            .ToListAsync();
    }

    /// <summary>
    /// Solo un Administrador resuelve (aprueba o rechaza) solicitudes (RF-GP-03, RD-06). La Web ya lo
    /// filtra, pero el servicio no confía en eso: la autorización se revisa en cada operación.
    /// </summary>
    private static bool PuedeResolver(Usuario usuario) => usuario.Rol == Rol.Administrador;

    private IQueryable<SolicitudPermiso> ConsultaConUsuarios() =>
        _db.SolicitudesPermiso.AsNoTracking().Include(s => s.Solicitante).Include(s => s.Aprobador);

    private async Task<SolicitudPermisoRespuesta> LeerRespuestaAsync(int idSolicitud) =>
        SolicitudPermisoRespuesta.De(await ConsultaConUsuarios().SingleAsync(s => s.Id == idSolicitud));

    /// <summary>
    /// Convierte el texto en un <see cref="PermisoSolicitable"/>. Solo acepta los nombres del catálogo
    /// (sin importar mayúsculas); un número como "0" o cualquier otro texto da null.
    /// </summary>
    private static PermisoSolicitable? LeerPermiso(string? permisoTexto)
    {
        var texto = permisoTexto?.Trim();
        foreach (var permiso in Enum.GetValues<PermisoSolicitable>())
        {
            if (string.Equals(permiso.ToString(), texto, StringComparison.OrdinalIgnoreCase))
            {
                return permiso;
            }
        }

        return null;
    }
}
