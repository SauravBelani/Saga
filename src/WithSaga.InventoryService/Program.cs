using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using WithSaga.InventoryService.Data;
using WithSaga.InventoryService.Services;

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
        Title = "Inventory Service (with Saga)",
        Version = "v1",
        Description = "Owns the Inventory database only. Reserve commits stock here. Release is the compensating action. Set Inventory:SimulateFailure to true to fail reserve before any stock change."
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddDbContext<InventoryDbContext>(options =>
    // GSS Encryption Mode=Disable is in the connection string. Without it, Npgsql on this Windows host waits on a GSS handshake the local Postgres server does not complete.
    options.UseNpgsql(builder.Configuration.GetConnectionString("InventoryDb")));

builder.Services.AddScoped<StockService>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory Service");
    options.DocumentTitle = "Inventory Service — With Saga";
    options.DisplayRequestDuration();
});

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();

app.Run();
