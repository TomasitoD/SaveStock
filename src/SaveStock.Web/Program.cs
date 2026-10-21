using System.Text.Json.Serialization;
using DotNetEnv;
using SaveStock.Core;
using SaveStock.Core.Comun;
using SaveStock.Core.Datos;
using SaveStock.Inventario;
using SaveStock.Web.Endpoints;
using SaveStock.Web.Errores;
using Scalar.AspNetCore;

// Si hay un .env en la carpeta actual, carga sus variables. Las variables reales del entorno tienen prioridad (RD-10).
if (File.Exists(".env"))
{
    Env.NoClobber().Load(".env");
}

var configuracion = ConfiguracionSaveStock.DesdeVariablesDeEntorno();

var builder = WebApplication.CreateBuilder(args);

// No hay appsettings.json: la configuración viene de variables de entorno. Solo bajamos el ruido del log.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

builder.Services.AddSaveStockCore(configuracion);
builder.Services.AddSaveStockInventario(configuracion);

builder.Services.AddOpenApi();
builder.Services.AddRazorPages();

// Los enums viajan como texto en el JSON (por ejemplo "Administrador").
builder.Services.ConfigureHttpJsonOptions(opciones =>
    opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Un JSON mal formado o con tipos incorrectos lanza BadHttpRequestException, que el manejador
// global convierte en 400 { "mensaje": "La solicitud no es válida." } (RD-07).
builder.Services.Configure<RouteHandlerOptions>(opciones => opciones.ThrowOnBadRequest = true);

var app = builder.Build();

// Primero en el pipeline: ninguna excepción llega al usuario con detalles internos (RD-08).
app.UseExceptionHandler(manejador => manejador.Run(ManejoErrores.ResponderErrorAsync));

await InicializadorBaseDatos.InicializarAsync(app.Services);

app.MapOpenApi();
app.MapScalarApiReference(); // Documentación interactiva en /scalar
app.UseStaticFiles(); // Estilos de las páginas (wwwroot/css/sitio.css)
app.MapRazorPages();

// Un archivo de endpoints por feature.
app.MapSaludEndpoints();
app.MapRegistroEndpoints();
app.MapSesionEndpoints();
app.MapAdministracionEndpoints();
app.MapContrasenasEndpoints();
app.MapGestionPermisosEndpoints();

app.Run();

/// <summary>Visible para las pruebas de integración (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program
{
}
