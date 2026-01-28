using DRaaS.Core.Services.Reconciliation;
using DRaaS.Reconciliation;
using DRaaS.Reconciliation.Strategies;

var builder = Host.CreateApplicationBuilder(args);

// Configure reconciliation options
builder.Services.Configure<ReconciliationOptions>(
    builder.Configuration.GetSection("Reconciliation"));

// Configure HTTP client for ControlPlane API
var controlPlaneUrl = builder.Configuration["ControlPlaneUrl"] ?? "http://localhost:5000";
builder.Services.AddHttpClient<IReconciliationApiClient, ReconciliationApiClient>(client =>
{
    client.BaseAddress = new Uri(controlPlaneUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

// Reconciliation services (no direct Core dependencies)
builder.Services.AddSingleton<IConfigurationStateStore, ConfigurationStateStore>();
builder.Services.AddSingleton<IReconciliationStrategy, RestartReconciliationStrategy>();
builder.Services.AddSingleton<IReconciliationService, ReconciliationService>();

// Background service
builder.Services.AddHostedService<ReconciliationBackgroundService>();

var host = builder.Build();
await host.RunAsync();

