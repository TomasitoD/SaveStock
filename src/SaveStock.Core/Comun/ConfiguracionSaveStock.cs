namespace SaveStock.Core.Comun;

/// <summary>
/// Configuración de SaveStock leída de variables de entorno (RD-10). Ninguna credencial
/// vive en el código: el archivo .env (que no se sube) solo carga esas variables.
/// </summary>
public class ConfiguracionSaveStock
{
    public const string RutaBaseDatosPorDefecto = "datos/savestock.db";
    public const string RutaBaseDatosInventarioPorDefecto = "datos/inventario.db";
    public const string RutaDocumentosPorDefecto = "datos/documentos";
    public const string UrlBasePorDefecto = "http://localhost:5080";
    public const int SmtpPuertoPorDefecto = 587;

    /// <summary>Ruta absoluta del archivo SQLite del Core (web y enviador usan el mismo).</summary>
    public string RutaBaseDatos { get; init; } = Path.GetFullPath(RutaBaseDatosPorDefecto);

    /// <summary>Ruta absoluta del archivo SQLite del inventario.</summary>
    public string RutaBaseDatosInventario { get; init; } = Path.GetFullPath(RutaBaseDatosInventarioPorDefecto);

    /// <summary>Ruta absoluta de la carpeta donde se guardan los archivos de los documentos (RF-DOC-05).</summary>
    public string RutaDocumentos { get; init; } = Path.GetFullPath(RutaDocumentosPorDefecto);

    /// <summary>URL pública de la web, para armar los enlaces de los correos. Sin barra final.</summary>
    public string UrlBase { get; init; } = UrlBasePorDefecto;

    // Administrador inicial: si falta alguno de los tres, no se crea.
    public string? AdminCorreo { get; init; }
    public string? AdminContrasena { get; init; }
    public string? AdminNombre { get; init; }

    // Servidor de correo: solo lo usa SaveStock.Enviador.
    public string? SmtpHost { get; init; }
    public int SmtpPuerto { get; init; } = SmtpPuertoPorDefecto;
    public string? SmtpUsuario { get; init; }
    public string? SmtpContrasena { get; init; }
    public string? SmtpRemitente { get; init; }

    /// <summary>Clave de ExchangeRate-API para consultar la tasa USD/DOP. Si falta, esa consulta se rechaza.</summary>
    public string? ExchangeApiKey { get; init; }

    public string CadenaConexion => $"Data Source={RutaBaseDatos}";

    public string CadenaConexionInventario => $"Data Source={RutaBaseDatosInventario}";

    /// <summary>
    /// Lee la configuración de las variables de entorno del proceso. Una variable vacía
    /// cuenta como ausente y se usa el valor por defecto.
    /// </summary>
    public static ConfiguracionSaveStock DesdeVariablesDeEntorno()
    {
        return new ConfiguracionSaveStock
        {
            // Las rutas relativas se resuelven desde el directorio actual (la raíz del repo al usar dotnet run).
            RutaBaseDatos = Path.GetFullPath(Leer("SAVESTOCK_DB_RUTA") ?? RutaBaseDatosPorDefecto),
            RutaBaseDatosInventario = Path.GetFullPath(Leer("SAVESTOCK_INVENTARIO_DB_RUTA") ?? RutaBaseDatosInventarioPorDefecto),
            RutaDocumentos = Path.GetFullPath(Leer("SAVESTOCK_DOCUMENTOS_RUTA") ?? RutaDocumentosPorDefecto),
            UrlBase = (Leer("SAVESTOCK_URL_BASE") ?? UrlBasePorDefecto).TrimEnd('/'),
            AdminCorreo = Leer("SAVESTOCK_ADMIN_CORREO"),
            AdminContrasena = Leer("SAVESTOCK_ADMIN_CONTRASENA"),
            AdminNombre = Leer("SAVESTOCK_ADMIN_NOMBRE"),
            SmtpHost = Leer("SMTP_HOST"),
            SmtpPuerto = int.TryParse(Leer("SMTP_PUERTO"), out var puerto) ? puerto : SmtpPuertoPorDefecto,
            SmtpUsuario = Leer("SMTP_USUARIO"),
            SmtpContrasena = Leer("SMTP_CONTRASENA"),
            SmtpRemitente = Leer("SMTP_REMITENTE"),
            ExchangeApiKey = Leer("EXCHANGE_API_KEY"),
        };
    }

    /// <summary>
    /// Crea las carpetas de los archivos SQLite si todavía no existen (por ejemplo, datos/).
    /// </summary>
    public void AsegurarCarpetas()
    {
        foreach (var ruta in new[] { RutaBaseDatos, RutaBaseDatosInventario })
        {
            var carpeta = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrEmpty(carpeta))
            {
                Directory.CreateDirectory(carpeta);
            }
        }
    }

    private static string? Leer(string nombre)
    {
        var valor = Environment.GetEnvironmentVariable(nombre);
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }
}
