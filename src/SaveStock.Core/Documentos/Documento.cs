using SaveStock.Core.ControlAcceso.Entidades;

namespace SaveStock.Core.Documentos;

/// <summary>
/// Registro de un documento subido (RF-DOC-01). El contenido no vive en la base: se guarda como
/// archivo en la carpeta de documentos (SAVESTOCK_DOCUMENTOS_RUTA) con el nombre <see cref="NombreArchivo"/>.
/// </summary>
public class Documento
{
    public const int LargoMaximoNombre = 255;
    public const int LargoMaximoTipo = 100;

    public int Id { get; set; }

    /// <summary>Nombre original del archivo, como lo subió el usuario.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Tipo de contenido (por ejemplo application/pdf).</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Tamaño en bytes.</summary>
    public long TamanoBytes { get; set; }

    /// <summary>Fecha de subida en UTC (RD-11).</summary>
    public DateTime FechaSubida { get; set; }

    /// <summary>Quién lo subió: es el dueño del documento.</summary>
    public int SubidoPorUsuarioId { get; set; }

    public Usuario? SubidoPor { get; set; }

    /// <summary>
    /// Nombre con el que se guarda el archivo en disco. Es un GUID y no el nombre original, para
    /// que dos documentos con el mismo nombre no se pisen y nadie pueda escribir fuera de la carpeta.
    /// </summary>
    public string NombreArchivo { get; set; } = string.Empty;

    /// <summary>
    /// Eliminación lógica (RF-DOC-04): el registro y el archivo se conservan, pero deja de listarse
    /// y de poder descargarse.
    /// </summary>
    public bool Eliminado { get; set; }

    public DateTime? FechaEliminacion { get; set; }
}
