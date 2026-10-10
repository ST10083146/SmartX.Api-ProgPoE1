using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Pretends to be the ESP32 devices. The brief says simulated data "has to hit the API like
/// it was a device", so this does NOT write into the app's memory directly. Every reading is
/// sent with a real HTTP POST to /api/telemetry, exactly like a device would.
/// Actuators (pumps / relays) also ask the API what state they should be in, which is how a
/// real relay would receive an override command.
///
/// <para>References:</para>
///   Microsoft (n.d.h) Background tasks with hosted services in ASP.NET Core.
///     Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services (Accessed: 10 October 2026).
/// </summary>
public class DeviceSimulator : BackgroundService
{
    private readonly DeviceRegistry _devices;
    private readonly SimulatorControls _controls;
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<DeviceSimulator> _logger;
    private readonly Random _random = new Random();

    public DeviceSimulator(
        DeviceRegistry devices,
        SimulatorControls controls,
        IHttpClientFactory clientFactory,
        IConfiguration config,
        ILogger<DeviceSimulator> logger)
    {
        _devices = devices;
        _controls = controls;
        _clientFactory = clientFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue<bool>("Simulator:Enabled", true))
        {
            return;
        }

        var baseUrl = _config["Simulator:BaseUrl"] ?? "http://localhost:5080";
        var interval = TimeSpan.FromSeconds(_config.GetValue<int>("Simulator:IntervalSeconds", 2));

        // Give the web server a few seconds to start listening before the first request.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var client = _clientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var device in _devices.GetAll())
            {
                // A silenced device is "unplugged", so it sends nothing at all.
                if (_controls.IsSilenced(device.Mac))
                {
                    continue;
                }

                try
                {
                    var value = await ChooseValueAsync(client, device, stoppingToken);
                    await client.PostAsJsonAsync("/api/telemetry", new TelemetryRequest(device.Mac, value), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Simulator could not reach the API for {Mac}: {Message}", device.Mac, ex.Message);
                }
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task<float> ChooseValueAsync(HttpClient client, DeviceInfo device, CancellationToken token)
    {
        // A dry-soil fault recovers once a pump is running (the soil gets watered).
        if (device.Category == "Environmental" && _devices.GetActuatorsThatAreOn().Count > 0)
        {
            _controls.CancelSpike(device.Mac);
        }
        // A creeping fault: every report is a little worse than the last one. This is what lets
        // the early-warning predictor spot the problem before the alert limit is reached.
        // It stops when a pump is running (moisture only).
        if (_controls.IsDrifting(device.Mac))
        {
            if (device.Category == "Environmental" && _devices.GetActuatorsThatAreOn().Count > 0)
            {
                _controls.StopDrift(device.Mac);
            }
            else
            {
                var last = device.LastValue ?? 50f;
                if (device.Category == "Environmental") return Math.Max(5f, last - 2.5f);
                return Math.Min(3800f, last + 150f);
            }
        }

        var spike = _controls.TakeSpike(device.Mac);

        if (device.Category == "Actuator")
        {
            // Ask the API which state the operator wants, then report that state back.
            var url = "/api/devices/" + Uri.EscapeDataString(device.Mac) + "/desired-state";
            var desired = await client.GetFromJsonAsync<DesiredState>(url, token);
            return (desired != null && desired.On) ? 1f : 0f;
        }

        if (device.Category == "Environmental")
        {
            if (spike) return _random.Next(3, 13);                       // moisture crashes (critical)
            if (_random.NextDouble() < 0.03) return _random.Next(22, 28); // now and then a warning
            return 35f + (float)(_random.NextDouble() * 30.0);            // normal 35% - 65%
        }

        // Power consumption
        if (spike) return 3200f + _random.Next(0, 600);                  // big power spike (critical)
        if (_random.NextDouble() < 0.03) return 2300f + _random.Next(0, 400);
        return 300f + (float)(_random.NextDouble() * 1200.0);            // normal 300W - 1500W
    }

    private record DesiredState(bool On);
}
