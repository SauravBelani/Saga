using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using WithSaga.OrderService.Data;
using WithSaga.OrderService.Services;

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
        Title = "Order Service (with Saga)",
        Version = "v1",
        Description = "Owns the Order database only. Create, confirm, and cancel are local commits. The orchestrator decides which one to call."
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddDbContext<OrderDbContext>(options =>
    // GSS Encryption Mode=Disable is in the connection string. Without it, Npgsql on this Windows host waits on a GSS handshake the local Postgres server does not complete.
    options.UseNpgsql(builder.Configuration.GetConnectionString("OrderDb")));

builder.Services.AddScoped<OrderCommands>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Service");
    options.DocumentTitle = "Order Service — With Saga";
    options.DisplayRequestDuration();
});

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();

app.Run();
