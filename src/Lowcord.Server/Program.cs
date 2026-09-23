using Lowcord.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

// 1. Determinar de forma robusta la ubicación de wwwroot (incluso si se ejecuta desde otra carpeta)
var baseDir = AppContext.BaseDirectory;
var wwwrootDir = Path.Combine(baseDir, "wwwroot");

if (!Directory.Exists(wwwrootDir))
{
    var cwdWwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    if (Directory.Exists(cwdWwwroot))
    {
        wwwrootDir = cwdWwwroot;
    }
    else
    {
        var srcWwwroot = Path.Combine(Directory.GetCurrentDirectory(), "src", "Lowcord.Server", "wwwroot");
        if (Directory.Exists(srcWwwroot))
        {
            wwwrootDir = srcWwwroot;
        }
    }
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = baseDir,
    WebRootPath = Directory.Exists(wwwrootDir) ? wwwrootDir : null
});

// 2. Forzar explícitamente a escuchar en el puerto 8080 en todas las interfaces de red
builder.WebHost.UseUrls("http://0.0.0.0:8080");

// 3. Configurar SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1 MB para SDPs complejos
});

// 4. Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

// 5. Servir archivos estáticos del frontend desde la carpeta resuelta
app.UseDefaultFiles();
app.UseStaticFiles();

// 6. Mapear SignalR Hub y endpoints
app.MapHub<SignalingHub>("/signalingHub");

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    service = "Lowcord Signaling Server",
    version = "1.0.0"
}));

app.MapPost("/api/shutdown", async (IHubContext<SignalingHub> hubContext, IHostApplicationLifetime lifetime) =>
{
    Console.WriteLine(">> Recibida solicitud de apagado. Notificando a todos los clientes...");
    await hubContext.Clients.All.SendAsync("ServerShuttingDown", "El anfitrión ha cerrado la sala. La llamada ha finalizado.");
    
    _ = Task.Run(async () =>
    {
        await Task.Delay(350);
        lifetime.StopApplication();
    });

    return Results.Ok(new { message = "Apagando servidor..." });
});

app.MapGet("/api/shutdown", async (IHubContext<SignalingHub> hubContext, IHostApplicationLifetime lifetime) =>
{
    Console.WriteLine(">> Recibida solicitud de apagado (GET). Notificando a todos los clientes...");
    await hubContext.Clients.All.SendAsync("ServerShuttingDown", "El anfitrión ha cerrado la sala. La llamada ha finalizado.");
    
    _ = Task.Run(async () =>
    {
        await Task.Delay(350);
        lifetime.StopApplication();
    });

    return Results.Ok(new { message = "Apagando servidor..." });
});

Console.WriteLine("====================================================");
Console.WriteLine(" [LOWCORD] Servidor de Señalización WebRTC Iniciado");
Console.WriteLine(" Escuchando en: http://0.0.0.0:8080");
Console.WriteLine($" Carpeta Web: {wwwrootDir}");
Console.WriteLine("====================================================");

app.Run();
