using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using WithSaga.PaymentService.Data;
using WithSaga.PaymentService.Services;

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
        Title = "Payment Service (with Saga)",
        Version = "v1",
        Description = "Owns the Payment database only. When simulated failure is on, a Failed payment row is committed and the call returns HTTP 500. The orchestrator then releases stock and cancels the order."
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddDbContext<PaymentDbContext>(options =>
    // GSS Encryption Mode=Disable is in the connection string. Without it, Npgsql on this Windows host waits on a GSS handshake the local Postgres server does not complete.
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDb")));

builder.Services.AddScoped<PaymentProcessor>();

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Service");
    options.DocumentTitle = "Payment Service — With Saga";
    options.DisplayRequestDuration();
});

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();

app.Run();
