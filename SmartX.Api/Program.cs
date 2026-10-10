using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Endpoints;
using SmartX.Api.Hubs;
using SmartX.Api.Models;
using SmartX.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Part 1 services
builder.Services.AddSingleton<SensorRegistry>();
builder.Services.AddSingleton<TelemetryHistoryStore>();
builder.Services.AddSingleton<AnomalyDetector>();

// Part 2 services (each one wraps one of the data structures)
builder.Services.AddSingleton<DeviceRegistry>();       // Dictionary keyed by MAC
builder.Services.AddSingleton<TelemetryQueues>();      // Queue<T> + PriorityQueue
builder.Services.AddSingleton<TelemetryLog>();         // SortedDictionary history
builder.Services.AddSingleton<FaultTracker>();         // HashSet of active faults
builder.Services.AddSingleton<CommandService>();       // Stack<T> for undo
builder.Services.AddSingleton<ActionPatternEngine>();  // predictive suggestions
builder.Services.AddSingleton<SimulatorControls>();

builder.Services.AddHttpClient();
builder.Services.AddHostedService<TelemetryProcessor>();
builder.Services.AddHostedService<DeviceSimulator>();

// SignalR needs its own JSON setting, otherwise enums are sent as numbers.
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

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

app.MapPost("/api/sensors", async (SensorRegistrationRequest request, SensorRegistry registry,
    DeviceRegistry devices, IHubContext<TelemetryHub> hub) =>
{
    var mac = DeviceRegistry.NormaliseMac(request.MacAddress);

    // Part 2: the MAC is now the dictionary key, so it has to be unique.
    if (devices.Contains(mac))
    {
        return Results.BadRequest(new { errors = new[] { "A device with MAC " + mac + " is already registered." } });
    }

    var (ok, sensor, errors) = registry.Register(request with { MacAddress = mac });
    if (!ok)
    {
        return Results.BadRequest(new { errors });
    }

    var info = devices.Add(sensor!);
    await hub.Clients.All.SendAsync("deviceUpdate", info);

    return Results.Created("/api/sensors/" + sensor!.Id, sensor);
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

// Part 2 endpoints
app.MapTelemetryEndpoints();
app.MapCommandEndpoints();
app.MapSimulatorEndpoints();

app.MapGet("/api/health", () => Results.Ok(new { status = "running" }));

// Startup landing pillar status — the React client's landing page reads this
// so "disabled" pillars are driven by the backend, not hard-coded twice.
app.MapGet("/api/pillars", () => Results.Ok(new[]
{
    new { key = "ingestion", label = "Sensor Data Ingestion and Telemetry", enabled = true },
    new { key = "commands", label = "Real-Time Command Stream and History", enabled = true },
    new { key = "topology", label = "Network Topology and Mesh Routing", enabled = false }
}));

app.Run();

