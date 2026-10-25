using SaveStock.Core.Comun;
using SaveStock.Core.GestionPermisos;
using SaveStock.Tests.Apoyo;

namespace SaveStock.Tests.Unitarias.GestionPermisos;

/// <summary>
/// Pruebas de la máquina de estados de la solicitud de permiso, sin base de datos ni web (RD-12).
/// Cada prueba crea su propia solicitud y su propio reloj: no comparten nada.
/// </summary>
public class MaquinaEstadosSolicitudPermisoTests
{
    private const int IdAdministrador = 7;
    private const int IdOtroAdministrador = 8;

    private static readonly DateTime FechaCreacion = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private static SolicitudPermiso NuevaSolicitudPendiente() => new()
    {
        Id = 1,
        SolicitanteId = 3,
        Permiso = PermisoSolicitable.VerListadoUsuarios,
        FechaCreacion = FechaCreacion,
    };

    /// <summary>
    /// Lleva una solicitud nueva hasta Aprobada, sin aplicarla todavía. El estado solo se cambia a
    /// través de la máquina (RD-04), así que se llega a él por la única transición permitida.
    /// </summary>
    private static SolicitudPermiso SolicitudAprobada(IReloj reloj)
    {
        var solicitud = NuevaSolicitudPendiente();
        MaquinaEstadosSolicitudPermiso.Aplicar(solicitud, EstadoSolicitudPermiso.Aprobada, EjecutorTransicion.Administrador, reloj, IdAdministrador);
        return solicitud;
    }

    /// <summary>Lleva una solicitud nueva hasta Aplicada por el camino permitido (aprobar y aplicar).</summary>
    private static SolicitudPermiso SolicitudAplicada(IReloj reloj)
    {
        var solicitud = NuevaSolicitudPendiente();
        MaquinaEstadosSolicitudPermiso.Aplicar(solicitud, EstadoSolicitudPermiso.Aprobada, EjecutorTransicion.Administrador, reloj, IdAdministrador);
        MaquinaEstadosSolicitudPermiso.Aplicar(solicitud, EstadoSolicitudPermiso.Aplicada, EjecutorTransicion.Sistema, reloj);
        return solicitud;
    }

    /// <summary>Lleva una solicitud nueva hasta Rechazada por el camino permitido.</summary>
    private static SolicitudPermiso SolicitudRechazada(IReloj reloj)
    {
        var solicitud = NuevaSolicitudPendiente();
        MaquinaEstadosSolicitudPermiso.Aplicar(solicitud, EstadoSolicitudPermiso.Rechazada, EjecutorTransicion.Administrador, reloj, IdAdministrador, "No lo necesita.");
        return solicitud;
    }

    // Transiciones válidas: estado resultante y dato registrado.

    [Fact]
    [Trait("Requisito", "RF-GP-03")]
    public void Aprobar_SolicitudPendiente_QuedaAprobadaYRegistraQuienAproboYCuando()
    {
        // Preparar
        var reloj = new RelojFalso { AhoraUtc = new DateTime(2026, 10, 2, 15, 30, 0, DateTimeKind.Utc) };
        var solicitud = NuevaSolicitudPendiente();

        // Actuar
        var resultado = MaquinaEstadosSolicitudPermiso.Aplicar(
            solicitud, EstadoSolicitudPermiso.Aprobada, EjecutorTransicion.Administrador, reloj, IdAdministrador);

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoSolicitudPermiso.Aprobada, solicitud.Estado);
        Assert.Equal(IdAdministrador, solicitud.AprobadorId);
        Assert.Equal(reloj.AhoraUtc, solicitud.FechaResolucion);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-04")]
    public void Rechazar_SolicitudPendienteConMotivo_QuedaRechazadaYGuardaElMotivo()
    {
        // Preparar
        var reloj = new RelojFalso { AhoraUtc = new DateTime(2026, 10, 2, 16, 0, 0, DateTimeKind.Utc) };
        var solicitud = NuevaSolicitudPendiente();

        // Actuar
        var resultado = MaquinaEstadosSolicitudPermiso.Aplicar(
            solicitud, EstadoSolicitudPermiso.Rechazada, EjecutorTransicion.Administrador, reloj, IdAdministrador, "  Su puesto no lo requiere.  ");

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Equal("Su puesto no lo requiere.", solicitud.MotivoRechazo);
        Assert.Equal(IdAdministrador, solicitud.AprobadorId);
        Assert.Equal(reloj.AhoraUtc, solicitud.FechaResolucion);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-05")]
    public void Aplicar_SolicitudAprobadaPorElSistema_QuedaAplicadaYRegistraLaFechaDeAplicacion()
    {
        // Preparar
        var reloj = new RelojFalso();
        var solicitud = NuevaSolicitudPendiente();
        MaquinaEstadosSolicitudPermiso.Aplicar(solicitud, EstadoSolicitudPermiso.Aprobada, EjecutorTransicion.Administrador, reloj, IdAdministrador);
        reloj.Avanzar(TimeSpan.FromSeconds(1));

        // Actuar
        var resultado = MaquinaEstadosSolicitudPermiso.Aplicar(solicitud, EstadoSolicitudPermiso.Aplicada, EjecutorTransicion.Sistema, reloj);

        // Verificar
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Equal(reloj.AhoraUtc, solicitud.FechaAplicacion);
    }

    // Rechazo sin motivo: no se completa.

    [Theory]
    [Trait("Requisito", "RF-GP-04")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rechazar_SinMotivoEscrito_SeRechazaYLaSolicitudSigueEnPendiente(string? motivo)
    {
        // Preparar
        var solicitud = NuevaSolicitudPendiente();

        // Actuar
        var resultado = MaquinaEstadosSolicitudPermiso.Aplicar(
            solicitud, EstadoSolicitudPermiso.Rechazada, EjecutorTransicion.Administrador, new RelojFalso(), IdAdministrador, motivo);

        // Verificar
        Assert.Equal(TipoError.Validacion, resultado.TipoError);
        Assert.Equal(MaquinaEstadosSolicitudPermiso.MensajeMotivoObligatorio, resultado.Mensaje);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
        Assert.Null(solicitud.AprobadorId);
    }

    // Transiciones prohibidas (RF-GP-06): se rechazan y el estado no cambia.

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public void Aplicar_DeRechazadaAAplicada_SeRechazaYElEstadoSigueRechazada()
    {
        // Preparar
        var reloj = new RelojFalso();
        var solicitud = SolicitudRechazada(reloj);
        reloj.Avanzar(TimeSpan.FromHours(1));

        // Actuar
        var resultado = MaquinaEstadosSolicitudPermiso.Aplicar(solicitud, EstadoSolicitudPermiso.Aplicada, EjecutorTransicion.Sistema, reloj);

        // Verificar
        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.TipoError);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Null(solicitud.FechaAplicacion);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public void Aprobar_SolicitudYaAplicada_SeRechazaYNoCambiaNiElEstadoNiQuienAprobo()
    {
        // Preparar: la primera aprobación ya ocurrió (y el sistema la aplicó).
        var reloj = new RelojFalso();
        var solicitud = SolicitudAplicada(reloj);
        var fechaPrimeraAprobacion = solicitud.FechaResolucion;
        reloj.Avanzar(TimeSpan.FromHours(1));

        // Actuar: un segundo Administrador intenta aprobarla otra vez.
        var resultado = MaquinaEstadosSolicitudPermiso.Aplicar(
            solicitud, EstadoSolicitudPermiso.Aprobada, EjecutorTransicion.Administrador, reloj, IdOtroAdministrador);

        // Verificar
        Assert.Equal(TipoError.Conflicto, resultado.TipoError);
        Assert.Equal("La solicitud está en estado Aplicada y no se puede aprobar.", resultado.Mensaje);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Equal(IdAdministrador, solicitud.AprobadorId);
        Assert.Equal(fechaPrimeraAprobacion, solicitud.FechaResolucion);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public void Rechazar_SolicitudAprobada_SeRechazaYSigueAprobada()
    {
        // Preparar
        var reloj = new RelojFalso();
        var solicitud = SolicitudAprobada(reloj);
        var fechaAprobacion = solicitud.FechaResolucion;
        reloj.Avanzar(TimeSpan.FromHours(1));

        // Actuar: otro Administrador intenta rechazar una solicitud que ya se aprobó.
        var resultado = MaquinaEstadosSolicitudPermiso.Aplicar(
            solicitud, EstadoSolicitudPermiso.Rechazada, EjecutorTransicion.Administrador, reloj, IdOtroAdministrador, "Me arrepentí.");

        // Verificar
        Assert.Equal(TipoError.Conflicto, resultado.TipoError);
        Assert.Equal("La solicitud está en estado Aprobada y no se puede rechazar.", resultado.Mensaje);
        Assert.Equal(EstadoSolicitudPermiso.Aprobada, solicitud.Estado);
        Assert.Null(solicitud.MotivoRechazo);
        Assert.Equal(IdAdministrador, solicitud.AprobadorId);
        Assert.Equal(fechaAprobacion, solicitud.FechaResolucion);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public void Aplicar_DePendienteAAplicadaSinAprobar_SeRechazaYSigueEnPendiente()
    {
        // Preparar
        var solicitud = NuevaSolicitudPendiente();

        // Actuar
        var resultado = MaquinaEstadosSolicitudPermiso.Aplicar(solicitud, EstadoSolicitudPermiso.Aplicada, EjecutorTransicion.Sistema, new RelojFalso());

        // Verificar
        Assert.Equal(TipoError.Conflicto, resultado.TipoError);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
        Assert.Null(solicitud.FechaAplicacion);
    }

    [Fact]
    [Trait("Requisito", "RF-GP-06")]
    public void Aprobar_PorElSistemaEnLugarDeUnAdministrador_SeRechazaYSigueEnPendiente()
    {
        // Preparar
        var solicitud = NuevaSolicitudPendiente();

        // Actuar
        var resultado = MaquinaEstadosSolicitudPermiso.Aplicar(solicitud, EstadoSolicitudPermiso.Aprobada, EjecutorTransicion.Sistema, new RelojFalso());

        // Verificar
        Assert.Equal(TipoError.Prohibido, resultado.TipoError);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
    }

    [Fact]
    [Trait("Requisito", "RD-04")]
    public void Transiciones_SoloPermitenLasTresDeLaTabla()
    {
        // Preparar
        var estados = Enum.GetValues<EstadoSolicitudPermiso>();
        var esperadas = new HashSet<(EstadoSolicitudPermiso, EstadoSolicitudPermiso)>
        {
            (EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aprobada),
            (EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aplicada),
            (EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Rechazada),
        };

        // Actuar
        var permitidas = estados
            .SelectMany(desde => estados.Select(hacia => (desde, hacia)))
            .Where(par => MaquinaEstadosSolicitudPermiso.EstaPermitida(par.desde, par.hacia))
            .ToHashSet();

        // Verificar
        Assert.Equal(esperadas, permitidas);
    }
}
