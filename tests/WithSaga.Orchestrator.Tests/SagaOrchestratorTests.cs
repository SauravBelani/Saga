using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WithSaga.Orchestrator.Clients;
using WithSaga.Orchestrator.Contracts;
using WithSaga.Orchestrator.Data;
using WithSaga.Orchestrator.Models;
using WithSaga.Orchestrator.Services;

namespace WithSaga.Orchestrator.Tests;

/// <summary>
/// Runs the orchestrator against fake Order, Inventory and Payment services.
/// No real service or database is started.
/// </summary>
public class SagaOrchestratorTests
{
    private static readonly CreateOrderRequest Request = new() { ProductId = "A", Quantity = 1, Amount = 49.99m };

    [Fact]
    public async Task Every_step_succeeds_so_the_saga_completes()
    {
        var services = new FakeServices();
        using var db = NewDb();

        var result = await NewOrchestrator(db, services).PlaceAsync(Request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("Completed", result.Response.Status);
        Assert.Equal(["create", "reserve", "charge", "confirm"], services.Calls);
        Assert.Equal(SagaStatus.Completed, db.Sagas.Single().Status);
    }

    [Fact]
    public async Task Inventory_fails_so_only_the_order_is_cancelled()
    {
        var services = new FakeServices { Reserve = Respond(HttpStatusCode.InternalServerError) };
        using var db = NewDb();

        var result = await NewOrchestrator(db, services).PlaceAsync(Request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("Compensated", result.Response.Status);
        Assert.Equal(["create", "reserve", "cancel"], services.Calls);
    }

    [Fact]
    public async Task Payment_fails_so_stock_is_released_before_the_order_is_cancelled()
    {
        var services = new FakeServices { Charge = Respond(HttpStatusCode.InternalServerError) };
        using var db = NewDb();

        var result = await NewOrchestrator(db, services).PlaceAsync(Request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal("Compensated", result.Response.Status);
        Assert.Equal(["create", "reserve", "charge", "release", "cancel"], services.Calls);
    }

    [Fact]
    public async Task Payment_times_out_so_nothing_is_undone()
    {
        var services = new FakeServices { Charge = _ => throw new TaskCanceledException("Simulated timeout") };
        using var db = NewDb();

        var result = await NewOrchestrator(db, services).PlaceAsync(Request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal("Running", result.Response.Status);
        Assert.DoesNotContain("release", services.Calls);
        Assert.DoesNotContain("cancel", services.Calls);
    }

    [Fact]
    public async Task Release_fails_so_the_saga_stays_running()
    {
        var services = new FakeServices
        {
            Charge = Respond(HttpStatusCode.InternalServerError),
            Release = Respond(HttpStatusCode.InternalServerError)
        };
        using var db = NewDb();

        var result = await NewOrchestrator(db, services).PlaceAsync(Request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal("Running", result.Response.Status);
        Assert.Equal(SagaStatus.Running, db.Sagas.Single().Status);
    }

    [Fact]
    public async Task Order_service_is_down_so_no_saga_starts()
    {
        var services = new FakeServices
        {
            Create = _ => throw new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused")
        };
        using var db = NewDb();

        var result = await NewOrchestrator(db, services).PlaceAsync(Request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal(SagaOrchestrator.NotStarted, result.Response.Status);
        Assert.Empty(db.Sagas);
    }

    [Fact]
    public async Task Saga_row_cannot_be_saved_so_the_new_order_is_cancelled()
    {
        var services = new FakeServices();
        using var db = new FailingDb(Options());

        var result = await NewOrchestrator(db, services).PlaceAsync(Request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("Compensated", result.Response.Status);
        Assert.Equal(["create", "cancel"], services.Calls);
    }

    private static SagaOrchestrator NewOrchestrator(OrchestratorDbContext db, FakeServices services) => new(
        db,
        new OrderClient(services.Client("http://order/")),
        new InventoryClient(services.Client("http://inventory/")),
        new PaymentClient(services.Client("http://payment/")));

    private static DbContextOptions<OrchestratorDbContext> Options() =>
        new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static OrchestratorDbContext NewDb() => new(Options());

    private static Func<HttpRequestMessage, HttpResponseMessage> Respond(HttpStatusCode status, string json = """{"message":"fake"}""") =>
        _ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>One fake for all three services. Each step returns success unless a test replaces it.</summary>
    private sealed class FakeServices : HttpMessageHandler
    {
        public List<string> Calls { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> Create { get; init; } =
            Respond(HttpStatusCode.OK, """{"message":"created","orderId":1,"status":"Created"}""");
        public Func<HttpRequestMessage, HttpResponseMessage> Confirm { get; init; } = Respond(HttpStatusCode.OK);
        public Func<HttpRequestMessage, HttpResponseMessage> Cancel { get; init; } = Respond(HttpStatusCode.OK);
        public Func<HttpRequestMessage, HttpResponseMessage> Reserve { get; init; } = Respond(HttpStatusCode.OK);
        public Func<HttpRequestMessage, HttpResponseMessage> Release { get; init; } = Respond(HttpStatusCode.OK);
        public Func<HttpRequestMessage, HttpResponseMessage> Charge { get; init; } = Respond(HttpStatusCode.OK);

        public HttpClient Client(string baseAddress) => new(this, disposeHandler: false) { BaseAddress = new Uri(baseAddress) };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var (name, step) = path switch
            {
                "/api/orders" => ("create", Create),
                _ when path.EndsWith("/confirm") => ("confirm", Confirm),
                _ when path.EndsWith("/cancel") => ("cancel", Cancel),
                "/api/inventory/reserve" => ("reserve", Reserve),
                "/api/inventory/release" => ("release", Release),
                "/api/payments" => ("charge", Charge),
                _ => throw new InvalidOperationException($"Unexpected call to {path}")
            };

            Calls.Add(name);
            return Task.FromResult(step(request));
        }
    }

    /// <summary>An orchestrator database whose writes always fail, like a database that has gone away.</summary>
    private sealed class FailingDb(DbContextOptions<OrchestratorDbContext> options) : OrchestratorDbContext(options)
    {
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Simulated database failure.");
    }
}
