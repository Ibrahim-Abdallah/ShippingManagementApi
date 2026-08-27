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

## Phase 01 boundary

Phase 01 supplies only the solution foundation: project boundaries, dependency injection entry points, configurable EF Core SQL Server registration, an empty DbContext, safe Problem Details handling, correlation-aware logging, health checks, OpenAPI with Scalar, and executable test projects. Shipping entities, authentication, merchants, carriers, quotes, shipments, background processing, and all other business workflows are intentionally deferred to their assigned phases.
