using SmartX.Api.Hubs;
using SmartX.Api.Models;
using SmartX.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddSingleton<SensorRegistry>();
builder.Services.AddSingleton<TelemetryHistoryStore>();
builder.Services.AddSingleton<AnomalyDetector>();
builder.Services.AddSignalR();
builder.Services.AddHostedService<TelemetrySimulator>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("SmartXClient", policy =>
    {
        // Vite dev server default port. Adjust here if you run the client elsewhere.
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("SmartXClient");
app.UseStaticFiles(); // serves wwwroot/uploads so attached files/photos are retrievable

// ---- Sensor Data Ingestion and Telemetry (Part 1 scope) --------------------

app.MapGet("/api/sensors", (SensorRegistry registry) => Results.Ok(registry.All));

app.MapPost("/api/sensors", (SensorRegistrationRequest request, SensorRegistry registry) =>
{
    var (ok, sensor, errors) = registry.Register(request);
    return ok
        ? Results.Created($"/api/sensors/{sensor!.Id}", sensor)
        : Results.BadRequest(new { errors });
});

app.MapGet("/api/sensors/{id:guid}/history", (Guid id, TelemetryHistoryStore history) =>
    Results.Ok(history.GetHistory(id)));

app.MapGet("/api/deployment-tree", (SensorRegistry registry) => Results.Ok(registry.Tree));

app.MapPost("/api/sensors/{id:guid}/upload", async (Guid id, HttpRequest request, SensorRegistry registry) =>
{
    var sensor = registry.Get(id);
    if (sensor is null) return Results.NotFound();

    if (!request.HasFormContentType) return Results.BadRequest("Expected multipart/form-data.");

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0) return Results.BadRequest("No file received.");

    var uploadsDir = Path.Combine(builder.Environment.WebRootPath ?? "wwwroot", "uploads");
    Directory.CreateDirectory(uploadsDir);

    var safeName = $"{sensor.NodeId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{Path.GetFileName(file.FileName)}";
    var fullPath = Path.Combine(uploadsDir, safeName);

    await using (var stream = File.Create(fullPath))
    {
        await file.CopyToAsync(stream);
    }

    sensor.AttachedFiles.Add($"/uploads/{safeName}");
    return Results.Ok(new { path = $"/uploads/{safeName}" });
});

app.MapHub<TelemetryHub>("/hubs/telemetry");

// Startup landing pillar status — the React client's landing page reads this
// so "disabled" pillars are driven by the backend, not hard-coded twice.
app.MapGet("/api/pillars", () => Results.Ok(new[]
{
    new { key = "ingestion", label = "Sensor Data Ingestion and Telemetry", enabled = true },
    new { key = "commands", label = "Real-Time Command Stream and History", enabled = false },
    new { key = "topology", label = "Network Topology and Mesh Routing", enabled = false }
}));

app.Run();

