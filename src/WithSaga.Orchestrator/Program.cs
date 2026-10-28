using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using WithSaga.Orchestrator.Clients;
using WithSaga.Orchestrator.Data;
using WithSaga.Orchestrator.Services;
using WithSaga.Orchestrator.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Saga Orchestrator",
        Version = "v1",
        Description =
            """
            Phase 2 — place an order with a Saga orchestrator.

            POST /api/orders is the only call to make. The orchestrator runs:
            1. Create the order
            2. Reserve stock
            3. Charge payment
            4. Confirm the order

            If reserve fails, the orchestrator cancels the order. Payment is not called, and stock is not released.
            If payment fails, the orchestrator releases the stock and then cancels the order. The Failed payment row stays.

            To fail inventory, set Inventory:SimulateFailure to true in the Inventory service appsettings.
            To fail payment, set Payment:SimulateFailure to true in the Payment service appsettings.
            Read the rows in each database.
            """
    });

    options.SchemaFilter<DemoSchemaExamples>();

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddDbContext<OrchestratorDbContext>(options =>
    // GSS Encryption Mode=Disable is in the connection string. Without it, Npgsql on this Windows host waits on a GSS handshake the local Postgres server does not complete.
    options.UseNpgsql(builder.Configuration.GetConnectionString("OrchestratorDb")));

// The default HttpClient timeout is 100 seconds. A shorter one keeps a hung service from holding the request for minutes.
var callTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Services:TimeoutSeconds", 10));

builder.Services.AddHttpClient<OrderClient>(client =>
{
    client.BaseAddress = RequireBaseAddress(builder.Configuration["Services:OrderBaseUrl"], "Services:OrderBaseUrl");
    client.Timeout = callTimeout;
});

builder.Services.AddHttpClient<InventoryClient>(client =>
{
    client.BaseAddress = RequireBaseAddress(builder.Configuration["Services:InventoryBaseUrl"], "Services:InventoryBaseUrl");
    client.Timeout = callTimeout;
});

builder.Services.AddHttpClient<PaymentClient>(client =>
{
    client.BaseAddress = RequireBaseAddress(builder.Configuration["Services:PaymentBaseUrl"], "Services:PaymentBaseUrl");
    client.Timeout = callTimeout;
});

builder.Services.AddScoped<SagaOrchestrator>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Saga Orchestrator");
    options.DocumentTitle = "Saga Orchestrator";
    options.DisplayRequestDuration();
});

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();

app.Run();

static Uri RequireBaseAddress(string? value, string name)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"{name} is missing.");
    }

    return new Uri(value);
}
