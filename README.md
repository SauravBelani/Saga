# Saga Demo

A .NET 10 demo that compares placing an order **without** a Saga versus **with** an orchestration Saga. Each service owns its own PostgreSQL database and commits locally. Failures in later steps show why compensating actions matter.

## Solutions

| Solution | Purpose |
|----------|---------|
| `WithoutSaga.sln` | Phase 1 — Order service drives Inventory and Payment directly. No compensation. |
| `WithSaga.sln` | Phase 2 — Orchestrator runs create → reserve → charge → confirm, with compensating cancel/release. |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL listening on `localhost:5432`
- Update `Password=YOUR_PASSWORD` in each service’s `appsettings.json` to match your Postgres user

On Windows, connection strings include `GSS Encryption Mode=Disable` so Npgsql does not hang on a GSS handshake the local server does not complete.

## Databases

Each service creates and seeds its database on startup (`EnsureCreated` / initializer).

### Without Saga (`SagaDemo_*`)

| Service | Database | Port |
|---------|----------|------|
| Order | `SagaDemo_Order` | `http://localhost:5101` |
| Inventory | `SagaDemo_Inventory` | `http://localhost:5102` |
| Payment | `SagaDemo_Payment` | `http://localhost:5103` |

### With Saga (`WithSaga_*`)

| Service | Database | Port |
|---------|----------|------|
| Orchestrator | `WithSaga_Orchestrator` | `http://localhost:5200` |
| Order | `WithSaga_Order` | `http://localhost:5201` |
| Inventory | `WithSaga_Inventory` | `http://localhost:5202` |
| Payment | `WithSaga_Payment` | `http://localhost:5203` |

Demo catalog seeds product **`A`** (“Product A”) with **10** units available.

## Run — Phase 1 (Without Saga)

Start Inventory and Payment first, then Order:

```bash
dotnet run --project src/WithoutSaga.InventoryService
dotnet run --project src/WithoutSaga.PaymentService
dotnet run --project src/WithoutSaga.OrderService
```

Open Swagger at `http://localhost:5101/swagger`.

**Happy path:** `POST /api/orders`

```json
{ "productId": "A", "quantity": 1, "amount": 49.99 }
```

Flow (each step is a separate DB commit):

1. Commit order (`Created`) in Order DB  
2. Commit stock reservation in Inventory DB  
3. Commit payment in Payment DB  
4. Confirm order in Order DB  

**Failure demo:** set `Payment:SimulateFailure` to `true` in `WithoutSaga.PaymentService/appsettings.json`, restart Payment, place another order. The order row and reservation stay committed; nothing cancels or releases them.

## Run — Phase 2 (With Saga)

Start Order, Inventory, and Payment, then the Orchestrator:

```bash
dotnet run --project src/WithSaga.OrderService
dotnet run --project src/WithSaga.InventoryService
dotnet run --project src/WithSaga.PaymentService
dotnet run --project src/WithSaga.Orchestrator
```

Open Swagger at `http://localhost:5200/swagger`. Call only the orchestrator:

**Happy path:** `POST /api/orders`

```json
{ "productId": "A", "quantity": 1, "amount": 49.99 }
```

Orchestrator steps:

1. Create order  
2. Reserve stock  
3. Charge payment  
4. Confirm order  

Compensation (new calls, not DB rollbacks):

| Failure | Compensation |
|---------|----------------|
| Inventory reserve fails | Cancel order. Payment is not called. |
| Payment fails | Release stock, then cancel order. Failed payment row remains. |

**Failure toggles** (restart the affected service after changing):

- `Inventory:SimulateFailure` → `true` in `WithSaga.InventoryService/appsettings.json`
- `Payment:SimulateFailure` → `true` in `WithSaga.PaymentService/appsettings.json`

Saga state is stored in `WithSaga_Orchestrator` (`Running` / `Completed` / `Compensated`).

## Project layout

```
src/
  WithoutSaga.OrderService/      # Places order; calls Inventory + Payment
  WithoutSaga.InventoryService/  # Reserve only (no release API in this phase)
  WithoutSaga.PaymentService/    # Charge; optional simulated failure

  WithSaga.Orchestrator/         # Saga coordinator + saga instance DB
  WithSaga.OrderService/         # Create / confirm / cancel
  WithSaga.InventoryService/     # Reserve + release (compensation)
  WithSaga.PaymentService/       # Charge; optional simulated failure
```

## Stack

- ASP.NET Core Web APIs + Swagger (Swashbuckle)
- Entity Framework Core + Npgsql (PostgreSQL)
- HttpClient for cross-service calls

Inspect rows in each Postgres database after success and failure runs to see the difference between uncompensated commits and saga compensation.
