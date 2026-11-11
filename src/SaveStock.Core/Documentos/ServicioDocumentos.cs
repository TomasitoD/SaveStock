using Microsoft.EntityFrameworkCore;
using SaveStock.Core.Comun;
using SaveStock.Core.ControlAcceso.Entidades;
using SaveStock.Core.Datos;

namespace SaveStock.Core.Documentos;

/// <summary>Lo que se muestra de un documento (RF-DOC-01): nombre, tipo, tamaño, fecha y quién lo subió.</summary>
public record DocumentoRespuesta(
    int Id,
    string Nombre,
    string Tipo,
    long TamanoBytes,
    DateTime FechaSubida,
    int SubidoPorUsuarioId,
    string SubidoPorCorreo);

/// <summary>Un documento listo para descargar: su registro y la ruta del archivo en disco.</summary>
public record DocumentoParaDescargar(DocumentoRespuesta Documento, string RutaArchivo);

/// <summary>
/// Manejador de documentos, pieza 3 del Core (RF-DOC-01 a RF-DOC-04). Un Estándar trabaja solo con
/// sus documentos; un Administrador, con todos. Las reglas se verifican aquí, en el servidor (RD-06).
/// </summary>
public class ServicioDocumentos
{
    /// <summary>Límite de tamaño de un documento: 10 MB.</summary>
    public const long TamanoMaximoBytes = 10 * 1024 * 1024;

    public const string MensajeArchivoVacio = "Adjunta un archivo con contenido en el campo «archivo».";
    public const string MensajeNoExiste = "No existe un documento con ese id.";
    public const string MensajeSoloDueno = "Solo quien subió el documento o un Administrador puede hacer esto.";
    public const string MensajeArchivoNoDisponible = "El archivo del documento no está disponible.";

    private readonly CoreDbContext _db;
    private readonly ConfiguracionSaveStock _configuracion;
    private readonly IReloj _reloj;

    public ServicioDocumentos(CoreDbContext db, ConfiguracionSaveStock configuracion, IReloj reloj)
    {
        _db = db;
        _configuracion = configuracion;
        _reloj = reloj;
    }

    /// <summary>
    /// Guarda el archivo en la carpeta de documentos y registra sus datos (RF-DOC-01).
    /// Un archivo ausente o vacío se rechaza sin excepción (RD-07).
    /// </summary>
    public async Task<Resultado<DocumentoRespuesta>> SubirAsync(Usuario usuario, string? nombre, string? tipo, long tamanoBytes, Stream? contenido)
    {
        if (contenido is null || tamanoBytes <= 0)
        {
            return Resultado<DocumentoRespuesta>.Error(TipoError.Validacion, MensajeArchivoVacio);
        }

        if (tamanoBytes > TamanoMaximoBytes)
        {
            return Resultado<DocumentoRespuesta>.Error(TipoError.Validacion, "El archivo no puede superar los 10 MB.");
        }

        // Solo el nombre, sin carpetas: "../../x.txt" queda como "x.txt".
        var nombreLimpio = Path.GetFileName(nombre?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(nombreLimpio))
        {
            return Resultado<DocumentoRespuesta>.Error(TipoError.Validacion, "El archivo debe tener un nombre.");
        }

        if (nombreLimpio.Length > Documento.LargoMaximoNombre)
        {
            return Resultado<DocumentoRespuesta>.Error(TipoError.Validacion, $"El nombre del archivo no puede superar los {Documento.LargoMaximoNombre} caracteres.");
        }

        var tipoLimpio = string.IsNullOrWhiteSpace(tipo) ? "application/octet-stream" : tipo.Trim();
        if (tipoLimpio.Length > Documento.LargoMaximoTipo)
        {
            tipoLimpio = "application/octet-stream";
        }

        // Primero el archivo y después el registro: si falla la escritura, no queda un registro sin archivo.
        Directory.CreateDirectory(_configuracion.RutaDocumentos);
        var nombreArchivo = Guid.NewGuid().ToString("N");
        await using (var destino = File.Create(RutaDe(nombreArchivo)))
        {
            await contenido.CopyToAsync(destino);
        }

        var documento = new Documento
        {
            Nombre = nombreLimpio,
            Tipo = tipoLimpio,
            TamanoBytes = tamanoBytes,
            FechaSubida = _reloj.AhoraUtc,
            SubidoPorUsuarioId = usuario.Id,
            NombreArchivo = nombreArchivo,
        };

        _db.Documentos.Add(documento);
        await _db.SaveChangesAsync();
        return Resultado<DocumentoRespuesta>.Ok(ARespuesta(documento, usuario.Correo));
    }

    /// <summary>
    /// Sin eliminados; un Estándar ve solo los suyos y un Administrador, los de todos (RF-DOC-02).
    /// </summary>
    public async Task<IReadOnlyList<DocumentoRespuesta>> ListarAsync(Usuario usuario)
    {
        var documentos = await _db.Documentos
            .AsNoTracking()
            .Include(d => d.SubidoPor)
            .Where(d => !d.Eliminado)
            .Where(d => usuario.Rol == Rol.Administrador || d.SubidoPorUsuarioId == usuario.Id)
            .OrderBy(d => d.Id)
            .ToListAsync();

        return documentos.Select(d => ARespuesta(d, d.SubidoPor!.Correo)).ToList();
    }

    /// <summary>Los datos de un documento (RF-DOC-01). Solo el dueño o un Administrador.</summary>
    public async Task<Resultado<DocumentoRespuesta>> ConsultarAsync(Usuario usuario, int idDocumento)
    {
        var buscado = await BuscarDelUsuarioAsync(usuario, idDocumento);
        return buscado.Exito
            ? Resultado<DocumentoRespuesta>.Ok(ARespuesta(buscado.Valor!, buscado.Valor!.SubidoPor!.Correo))
            : Resultado<DocumentoRespuesta>.Error(buscado.TipoError, buscado.Mensaje);
    }

    /// <summary>Solo el dueño o un Administrador descargan; cualquier otro recibe un rechazo (RF-DOC-03).</summary>
    public async Task<Resultado<DocumentoParaDescargar>> DescargarAsync(Usuario usuario, int idDocumento)
    {
        var buscado = await BuscarDelUsuarioAsync(usuario, idDocumento);
        if (!buscado.Exito)
        {
            return Resultado<DocumentoParaDescargar>.Error(buscado.TipoError, buscado.Mensaje);
        }

        var documento = buscado.Valor!;
        var ruta = RutaDe(documento.NombreArchivo);
        if (!File.Exists(ruta))
        {
            return Resultado<DocumentoParaDescargar>.Error(TipoError.NoEncontrado, MensajeArchivoNoDisponible);
        }

        return Resultado<DocumentoParaDescargar>.Ok(new DocumentoParaDescargar(ARespuesta(documento, documento.SubidoPor!.Correo), ruta));
    }

    /// <summary>
    /// Eliminación lógica (RF-DOC-04): marca el registro como eliminado y no borra nada, ni la fila
    /// ni el archivo. Solo el dueño o un Administrador.
    /// </summary>
    public async Task<Resultado> EliminarAsync(Usuario usuario, int idDocumento)
    {
        var buscado = await BuscarDelUsuarioAsync(usuario, idDocumento);
        if (!buscado.Exito)
        {
            return buscado;
        }

        buscado.Valor!.Eliminado = true;
        buscado.Valor.FechaEliminacion = _reloj.AhoraUtc;
        await _db.SaveChangesAsync();
        return Resultado.Ok("El documento fue eliminado.");
    }

    /// <summary>
    /// El documento si existe, no está eliminado y el usuario es su dueño o un Administrador.
    /// Si existe pero es de otro, el rechazo es Prohibido (RD-06).
    /// </summary>
    private async Task<Resultado<Documento>> BuscarDelUsuarioAsync(Usuario usuario, int idDocumento)
    {
        var documento = await _db.Documentos
            .Include(d => d.SubidoPor)
            .SingleOrDefaultAsync(d => d.Id == idDocumento && !d.Eliminado);

        if (documento is null)
        {
            return Resultado<Documento>.Error(TipoError.NoEncontrado, MensajeNoExiste);
        }

        if (usuario.Rol != Rol.Administrador && documento.SubidoPorUsuarioId != usuario.Id)
        {
            return Resultado<Documento>.Error(TipoError.Prohibido, MensajeSoloDueno);
        }

        return Resultado<Documento>.Ok(documento);
    }

    private string RutaDe(string nombreArchivo) => Path.Combine(_configuracion.RutaDocumentos, nombreArchivo);

    private static DocumentoRespuesta ARespuesta(Documento documento, string correoDueno) => new(
        documento.Id,
        documento.Nombre,
        documento.Tipo,
        documento.TamanoBytes,
        documento.FechaSubida,
        documento.SubidoPorUsuarioId,
        correoDueno);
}
