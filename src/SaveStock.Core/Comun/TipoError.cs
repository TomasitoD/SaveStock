namespace SaveStock.Core.Comun;

/// <summary>
/// Tipo de error de negocio. La Web lo traduce a un código HTTP (RD-07, RD-08).
/// </summary>
public enum TipoError
{
    /// <summary>Sin error (el resultado fue exitoso).</summary>
    Ninguno,

    /// <summary>Datos de entrada inválidos → 400.</summary>
    Validacion,

    /// <summary>Falta una sesión válida → 401.</summary>
    NoAutenticado,

    /// <summary>El usuario no tiene permiso para la operación → 403.</summary>
    Prohibido,

    /// <summary>El recurso no existe → 404.</summary>
    NoEncontrado,

    /// <summary>Choca con datos existentes, por ejemplo un correo repetido → 409.</summary>
    Conflicto,

    /// <summary>La cuenta está bloqueada temporalmente por intentos fallidos → 423 (RF-CA-19).</summary>
    Bloqueado,

    /// <summary>Un servicio externo (por ejemplo, la API de tasa de cambio) no está disponible o rechazó la consulta → 503.</summary>
    ServicioExterno,
}
