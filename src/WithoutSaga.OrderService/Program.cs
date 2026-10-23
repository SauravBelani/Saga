using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using WithoutSaga.OrderService.Clients;
using WithoutSaga.OrderService.Data;
using WithoutSaga.OrderService.Services;
using WithoutSaga.OrderService.Swagger;

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
        Title = "Order Service (without Saga)",
        Version = "v1",
        Description =
            """
            Phase 1 — place an order without a Saga.

            POST /api/orders is the only business call. It runs:
            1. Commit the order in the Order database
            2. Commit a stock reservation in the Inventory database
            3. Commit a payment in the Payment database
            4. Confirm the order back in the Order database

            These are three separate Postgres databases. A failure in a later step leaves the earlier commits in place.

            To fail payment, set Payment:SimulateFailure to true in the Payment service appsettings and place another order. Read the rows in each database.
            """
    });

    options.SchemaFilter<DemoSchemaExamples>();

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddDbContext<OrderDbContext>(options =>
    // GSS Encryption Mode=Disable is in the connection string. Without it, Npgsql on this Windows host waits on a GSS handshake the local Postgres server does not complete.
    options.UseNpgsql(builder.Configuration.GetConnectionString("OrderDb")));

builder.Services.AddHttpClient<InventoryClient>(client =>
{
    client.BaseAddress = RequireBaseAddress(builder.Configuration["Services:InventoryBaseUrl"], "Services:InventoryBaseUrl");
});

builder.Services.AddHttpClient<PaymentClient>(client =>
{
    client.BaseAddress = RequireBaseAddress(builder.Configuration["Services:PaymentBaseUrl"], "Services:PaymentBaseUrl");
});

builder.Services.AddScoped<BusinessStateReader>();
builder.Services.AddScoped<OrderPlacementService>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Service");
    options.DocumentTitle = "Order Service — Without Saga";
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
