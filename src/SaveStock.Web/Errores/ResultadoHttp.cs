using SaveStock.Core.Comun;

namespace SaveStock.Web.Errores;

/// <summary>
/// Traduce un <see cref="Resultado"/> fallido del Core a una respuesta HTTP <c>{ "mensaje": "..." }</c>.
/// Así los endpoints no repiten la tabla de códigos.
/// </summary>
public static class ResultadoHttp
{
    public static int CodigoHttp(TipoError tipoError) => tipoError switch
    {
        TipoError.Validacion => StatusCodes.Status400BadRequest,
        TipoError.NoAutenticado => StatusCodes.Status401Unauthorized,
        TipoError.Prohibido => StatusCodes.Status403Forbidden,
        TipoError.NoEncontrado => StatusCodes.Status404NotFound,
        TipoError.Conflicto => StatusCodes.Status409Conflict,
        TipoError.Bloqueado => StatusCodes.Status423Locked,
        TipoError.ServicioExterno => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Respuesta de error con el código que corresponde al tipo de error del resultado.</summary>
    public static IResult Error(Resultado resultado) =>
        Results.Json(new RespuestaMensaje(resultado.Mensaje), statusCode: CodigoHttp(resultado.TipoError));

    /// <summary>Respuesta exitosa con un mensaje, por ejemplo <c>{ "mensaje": "Te enviamos un correo..." }</c>.</summary>
    public static IResult Mensaje(string mensaje, int codigoHttp = StatusCodes.Status200OK) =>
        Results.Json(new RespuestaMensaje(mensaje), statusCode: codigoHttp);
}
