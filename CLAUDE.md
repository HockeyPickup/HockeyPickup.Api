# HockeyPickup.Api — Repo Conventions

## Stack
.NET 10 Web API, EF Core (SQL Server), ASP.NET Identity (custom `CustomUserStore`), GraphQL (HotChocolate annotations on response models), Swagger/NSwag, Azure Service Bus via the `IServiceBus` abstraction (`ResilientServiceBus`).

## Patterns to follow (read examples before writing)
- **Service layer**: methods return `ServiceResult<T>` / `ServiceResult<T>.CreateSuccess/CreateFailure`. Catch exceptions at service boundaries, log via `ILogger`, return failure with `ex.GetRelevantMessage()` (see `Extensions.cs`). Mirror `BuySellService.cs`.
- **Eligibility checks** return rich status objects (see `BuySellStatusResponse`) with `IsAllowed` + human-readable `Reason`, not exceptions.
- **Concurrency**: mutations that can race use the transaction + in-transaction re-check + bounded retry pattern in `ProcessBuyRequestAsync`. Reuse it; don't invent a new pattern.
- **Repositories**: interface in `IXxxRepository`, implementation mirrors `BuySellRepository`. Transactions begin at the repository (`BeginTransactionAsync`).
- **Response models**: every property gets `[Description]`, `[JsonPropertyName]`, Newtonsoft `[JsonProperty]`, `[GraphQLName]`, `[GraphQLDescription]`, and `[Required]`/`required` where appropriate. Computed window properties live on `SessionDetailedResponse` — follow the `BuyWindow*` expression-body style.
- **Controllers**: thin; auth attributes; wrap results in the existing `ApiResponse<T>` envelope; Swagger annotations consistent with `BuySellController`.
- **Migrations**: EF Core migrations named `Add<Thing>` (see `20250122015550_AddTransactionTables.cs`). Check `SchemaUpdates` for the parallel raw-SQL convention and keep it in sync if used.
- **Service Bus publish**: build `ServiceBusCommsMessage` with `Metadata["Type"]` and flat string `MessageData`; send via injected `IServiceBus`.
- **Time**: `TimeZoneUtils.GetCurrentPacificTime()` everywhere; windows are computed properties, never stored.

## Tests
xUnit + Moq + FluentAssertions in the test project. Arrange/Act/Assert with comments, one behavior per test, mock `UserManager`, repositories, and `IServiceBus` exactly as `BuySellServiceTest.cs` does. New logic requires tests for happy path, denial reasons, boundary times, and race/idempotency behavior. All existing tests must pass unmodified. Code coverage must be 100% on all code, new and existing - 100% branch AND 100% line coverage.
