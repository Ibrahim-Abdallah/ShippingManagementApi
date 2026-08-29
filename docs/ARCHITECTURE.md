# ShippingManagementApi Architecture

## Layers and dependency direction

The solution uses a pragmatic layered architecture. Dependencies point inward:

```text
Api -> Application -> Domain
Api -> Infrastructure -> Application -> Domain
```

- **Domain** will contain shipping entities, value objects, invariants, and state-transition rules. It has no dependencies on other solution projects.
- **Application** will contain use cases, contracts, validation, and orchestration. It depends only on Domain.
- **Infrastructure** owns EF Core, SQL Server persistence, and future external carrier integrations. It depends on Application and Domain.
- **Api** is the composition root and HTTP boundary. It configures middleware and maps endpoints while depending on Application and Infrastructure.

API endpoints and future controllers remain thin: they translate HTTP input, invoke application use cases, and translate results. Business rules belong in Domain; use-case orchestration belongs in Application. EF Core configurations and database concerns belong in Infrastructure. Concrete carrier adapters will also live in Infrastructure behind contracts defined by Application when those capabilities are introduced.

## Testing

`ShippingManagementApi.UnitTests` covers domain, application, and architecture rules without external infrastructure. `ShippingManagementApi.IntegrationTests` starts the real API through `WebApplicationFactory` and exercises HTTP behavior. Database-specific tests can add isolated test infrastructure in later phases without making basic API-health tests require SQL Server.

## Phase 02 identity and merchant boundary

The Domain owns the `Merchant` entity and its normalization/invariants. Application owns authentication, current-user, and merchant use-case contracts plus centralized role/policy names. Infrastructure owns ASP.NET Core Identity entities, EF persistence, JWT issuance, refresh-token hashing/rotation, and merchant orchestration. API owns the `HttpContext`-backed current-user implementation and thin HTTP mappings.

Merchant reads add the authenticated `MerchantId` predicate at the EF query boundary. Merchant identity is derived only from signed claims; request data cannot override it. Operators do not receive merchant-administration access. Admin provisioning creates a Merchant and initial Identity user in one transaction.

See `docs/PHASE_02_SETUP.md` for configuration, migration, and manual verification.

## Phase 03 carrier catalog and provider boundary

The Domain owns `Carrier`, `CarrierService`, and the strongly typed `ServiceLevel` model. Carrier and service codes are normalized stable keys, while explicit methods control descriptive updates and activation. A carrier's activation does not rewrite its services; availability requires both the carrier and service to be active.

Application owns the administration/catalog contracts and the minimal provider abstractions. `ICarrierProvider` exposes only a stable carrier code and capability metadata needed in Phase 03. `ICarrierProviderResolver` resolves normalized codes without exposing concrete adapters. Quote, shipment, pickup, tracking, and webhook provider contracts are intentionally deferred to their phases.

Infrastructure owns EF persistence, carrier use-case implementations, provider registration/resolution, and `DemoCarrier`. The resolver builds a provider dictionary from dependency-injection registrations and rejects duplicate normalized codes during construction. Unknown codes throw a predictable `KeyNotFoundException`; `TryResolve` returns `false`. API code depends only on Application contracts and never names `DemoCarrier`.

Persisted carrier configuration is distinct from executable provider adapters. The `DEMO` configuration and its active `STANDARD` (2–5 days) and `EXPRESS` (1–2 days) services are deterministic migration seed data. The matching Infrastructure provider advertises Phase 03 capabilities but contains no quote, rate, shipment, tracking, label, pickup, or webhook behavior.

Administrator endpoints expose all configuration, including inactive records. Authenticated catalog endpoints expose only active carriers and active services. Carrier deletion is restrictive: a carrier with services returns a conflict and should be deactivated instead. The database also uses restrictive carrier-service foreign-key deletion, unique carrier code and per-carrier service code indexes, active-state indexes, and delivery-range check constraints.

## Phase 01 foundation

Phase 01 supplies only the solution foundation: project boundaries, dependency injection entry points, configurable EF Core SQL Server registration, an empty DbContext, safe Problem Details handling, correlation-aware logging, health checks, OpenAPI with Scalar, and executable test projects. Shipping entities, authentication, merchants, carriers, quotes, shipments, background processing, and all other business workflows are intentionally deferred to their assigned phases.
