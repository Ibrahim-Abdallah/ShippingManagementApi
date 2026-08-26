# ShippingManagementApi — Project Plan

## 1. Project Overview

**ShippingManagementApi** is a production-style ASP.NET Core REST API that models the core business workflows of a modern shipping platform.

The project is designed as a portfolio-grade backend system that demonstrates practical knowledge of shipping operations beyond basic CRUD. It focuses on shipment lifecycle management, multi-carrier abstraction, rate quoting, pickup scheduling, tracking, delivery attempts, cash on delivery, return-to-sender workflows, carrier webhooks, outbound merchant webhooks, idempotency, background processing, reporting, and operational reliability.

The system is intentionally scoped as a **shipping management and carrier orchestration backend**, not as a full logistics ERP, fleet management system, warehouse management system, route optimizer, or driver mobile platform.

---

## 2. Primary Goals

The project must demonstrate the ability to design and implement:

- Real-world shipping business workflows.
- Explicit shipment state transitions and domain rules.
- Multi-carrier integrations through a clean abstraction layer.
- Shipping quotes and rate-shopping behavior.
- Shipment confirmation, tracking numbers, and shipping labels.
- Pickup scheduling and pickup lifecycle management.
- Tracking event ingestion and carrier-status normalization.
- Delivery-attempt and failed-delivery workflows.
- Return-to-sender flows.
- Cash-on-delivery tracking.
- Carrier webhook processing.
- Outbound merchant webhook delivery.
- Idempotent APIs and webhook processing.
- Background processing and retry behavior.
- Role-based authorization and merchant data isolation.
- Operational search, filtering, pagination, sorting, and reporting.
- Auditability and production-style error handling.
- Automated tests for critical business workflows.

---

## 3. Non-Goals

The following are intentionally outside the initial project scope:

- Driver mobile applications.
- Live driver GPS tracking.
- Route optimization.
- Fleet and vehicle management.
- Warehouse management.
- Inventory management.
- Order management.
- Customs clearance workflows.
- Full international customs documentation.
- Tax calculation.
- Full carrier invoice reconciliation.
- Full accounting or settlement systems.
- Real payment gateway integration.
- Production DHL/FedEx/UPS/Aramex credentials.
- AI-based route or delivery prediction.
- Map-based dispatching.
- Full SaaS multi-tenancy infrastructure.
- Customer-facing frontend applications.

These may be referenced as possible future extensions, but must not expand the implementation scope.

---

## 4. Target Users and Actors

### 4.1 Merchant

Represents an external business using the shipping API.

Typical examples:

- E-commerce store.
- Marketplace.
- ERP.
- Warehouse platform.
- Seller application.

A Merchant may:

- Request shipping quotes.
- Create shipments.
- Confirm shipments.
- Schedule pickups.
- View tracking.
- View shipment history.
- Request cancellation when allowed.
- View its own reports.
- Configure outbound webhook endpoints.

A Merchant must only access its own data.

### 4.2 Operator

Represents an internal operational user.

An Operator may:

- Review shipments.
- Inspect tracking activity.
- Record operational delivery information when applicable.
- Review failed deliveries.
- Initiate allowed operational actions.
- Review pickup activity.

### 4.3 Administrator

An Administrator may:

- Manage carriers.
- Manage carrier services.
- Manage merchant accounts.
- Review webhook failures.
- Review system operations.
- Manage operational configuration.
- Access cross-merchant reporting.

### 4.4 Carrier

Represents a shipping provider.

The initial project must include a deterministic internal/demo carrier implementation rather than requiring real third-party credentials.

The architecture must allow future carrier adapters such as:

- DHL
- FedEx
- UPS
- Aramex
- Local carriers

---

## 5. Core Business Modules

The project is divided into the following business modules:

1. Identity and Access
2. Merchants
3. Addresses
4. Carriers
5. Carrier Services
6. Shipping Quotes
7. Shipments
8. Packages
9. Shipment Confirmation
10. Shipping Labels
11. Pickup Management
12. Tracking
13. Delivery Attempts
14. Return To Sender
15. Cash On Delivery
16. Carrier Webhooks
17. Outbound Merchant Webhooks
18. Background Processing
19. Audit Trail
20. Reporting and Operational Queries

---

## 6. High-Level Architecture

The solution should follow a pragmatic layered architecture suitable for a portfolio-grade ASP.NET Core backend.

Suggested structure:

```text
src/
  ShippingManagementApi.Api
  ShippingManagementApi.Application
  ShippingManagementApi.Domain
  ShippingManagementApi.Infrastructure

tests/
  ShippingManagementApi.UnitTests
  ShippingManagementApi.IntegrationTests
```

### 6.1 Domain

Responsibilities:

- Domain entities.
- Enums and value objects.
- State-transition rules.
- Domain invariants.
- Domain exceptions when appropriate.
- Domain events when they provide clear value.

The Domain layer must not depend on Infrastructure.

### 6.2 Application

Responsibilities:

- Use cases.
- Application services.
- DTOs.
- Interfaces.
- Validation.
- Mapping.
- Authorization-aware business orchestration.
- Query contracts.
- Carrier abstraction contracts.
- Webhook abstraction contracts.

### 6.3 Infrastructure

Responsibilities:

- EF Core.
- SQL Server.
- Repository implementations.
- Carrier provider adapters.
- Background jobs/workers.
- Webhook delivery infrastructure.
- Persistence.
- Retry mechanisms.
- External HTTP integrations.
- Observability infrastructure.

### 6.4 API

Responsibilities:

- REST endpoints.
- Authentication.
- Authorization.
- Request/response models where appropriate.
- Problem Details.
- API versioning if adopted.
- OpenAPI/Swagger.
- Correlation metadata.
- Thin controllers.

Controllers must not contain business logic.

---

## 7. Technology Stack

Primary stack:

- C#
- Latest stable supported .NET version selected when implementation starts.
- ASP.NET Core Web API.
- Entity Framework Core.
- SQL Server.
- FluentValidation.
- JWT Authentication.
- Refresh Tokens.
- OpenAPI / Swagger.
- Structured logging.
- Background hosted services or a suitable job-processing abstraction.
- xUnit for automated tests.

Optional supporting libraries are allowed only when they reduce complexity and are justified.

Avoid unnecessary framework or architectural complexity.

---

## 8. Core Domain Model

The exact database design may evolve during implementation, but the following model represents the required business capabilities.

### 8.1 Merchant

Key properties:

- Id
- Name
- Code
- IsActive
- CreatedAtUtc
- UpdatedAtUtc

Relationships:

- Users
- Shipments
- Quotes
- WebhookEndpoints

### 8.2 Address

Key properties:

- Id
- ContactName
- CompanyName
- Phone
- Email
- AddressLine1
- AddressLine2
- City
- StateOrProvince
- PostalCode
- CountryCode
- Latitude
- Longitude
- CreatedAtUtc

Addresses used by a shipment should be snapshotted or otherwise protected from unexpected historical mutation.

### 8.3 Carrier

Key properties:

- Id
- Code
- Name
- IsActive
- SupportsPickup
- SupportsTracking
- SupportsCancellation
- SupportsCod
- CreatedAtUtc
- UpdatedAtUtc

### 8.4 CarrierService

Key properties:

- Id
- CarrierId
- Code
- Name
- ServiceLevel
- IsActive
- EstimatedMinDays
- EstimatedMaxDays

Possible service levels:

- Economy
- Standard
- Express
- NextDay
- SameDay

Not every carrier must support every level.

### 8.5 ShippingQuote

Key properties:

- Id
- MerchantId
- Origin snapshot
- Destination snapshot
- Currency
- Status
- ExpiresAtUtc
- CreatedAtUtc

A quote contains one or more QuoteOptions.

### 8.6 QuoteOption

Key properties:

- Id
- ShippingQuoteId
- CarrierId
- CarrierServiceId
- Amount
- Currency
- EstimatedMinDays
- EstimatedMaxDays
- ProviderReference

### 8.7 Shipment

Key properties:

- Id
- MerchantId
- ExternalReference
- TrackingNumber
- CarrierId
- CarrierServiceId
- Status
- SenderAddress snapshot
- RecipientAddress snapshot
- IsCod
- CodAmount
- Currency
- DeclaredValue
- LabelUrl / LabelReference
- LabelFormat
- ConfirmedAtUtc
- PickedUpAtUtc
- DeliveredAtUtc
- CancelledAtUtc
- CreatedAtUtc
- UpdatedAtUtc
- Concurrency token where useful

### 8.8 Package

Key properties:

- Id
- ShipmentId
- PackageNumber
- Weight
- WeightUnit
- Length
- Width
- Height
- DimensionUnit
- Description
- DeclaredValue

A shipment must support one or more packages.

### 8.9 Pickup

Key properties:

- Id
- MerchantId
- ShipmentId or shipment grouping reference
- PickupDate
- WindowStart
- WindowEnd
- ContactName
- Phone
- Instructions
- Status
- CarrierReference
- CreatedAtUtc
- UpdatedAtUtc

### 8.10 TrackingEvent

Key properties:

- Id
- ShipmentId
- CarrierEventId
- CarrierStatus
- NormalizedStatus
- Description
- Location
- EventOccurredAtUtc
- ReceivedAtUtc
- Source
- RawPayloadReference when needed

### 8.11 DeliveryAttempt

Key properties:

- Id
- ShipmentId
- AttemptNumber
- AttemptedAtUtc
- FailureReason
- Notes
- NextAttemptAtUtc

### 8.12 ReturnToSender

Key properties:

- Id
- ShipmentId
- Status
- Reason
- InitiatedAtUtc
- ReturnedAtUtc
- CarrierReference

### 8.13 CodTransaction / CodRecord

The project does not implement a full accounting system.

Required fields may include:

- Id
- ShipmentId
- Amount
- Currency
- Status
- CollectedAtUtc
- UpdatedAtUtc

### 8.14 CarrierWebhookEvent

Key properties:

- Id
- CarrierId
- ExternalEventId
- EventType
- PayloadHash
- ReceivedAtUtc
- ProcessingStatus
- ProcessedAtUtc
- FailureReason
- RetryCount

### 8.15 MerchantWebhookEndpoint

Key properties:

- Id
- MerchantId
- Url
- Secret
- IsActive
- EventSubscriptions
- CreatedAtUtc

Webhook secrets must never be returned after initial creation if persisted as sensitive values.

### 8.16 OutboundWebhookDelivery

Key properties:

- Id
- MerchantWebhookEndpointId
- EventId
- EventType
- Payload
- AttemptCount
- Status
- NextAttemptAtUtc
- LastAttemptAtUtc
- LastResponseStatusCode
- FailureReason

### 8.17 AuditEntry

Key properties:

- Id
- MerchantId where applicable
- UserId
- EntityType
- EntityId
- Action
- Metadata
- CreatedAtUtc

Audit history should focus on meaningful operational actions rather than logging every database update.

---

## 9. Shipment Lifecycle

The primary lifecycle is:

```text
Draft
  ↓
Quoted
  ↓
Confirmed
  ↓
PickupScheduled
  ↓
PickedUp
  ↓
InTransit
  ↓
OutForDelivery
  ↓
Delivered
```

Supporting terminal or exceptional flows include:

```text
Draft / Quoted / Confirmed
        ↓
     Cancelled
```

```text
OutForDelivery
       ↓
DeliveryFailed
       ↓
OutForDelivery
```

```text
DeliveryFailed
       ↓
ReturnInitiated
       ↓
ReturningToSender
       ↓
Returned
```

The exact enum names may be refined, but state transitions must be explicit and validated.

---

## 10. Shipment State Rules

Required business rules include:

- A shipment cannot be confirmed without at least one package.
- Package weight must be greater than zero.
- Package dimensions, when supplied, must be valid positive values.
- A selected carrier service must belong to the selected carrier.
- A selected carrier service must be active.
- A merchant may access only its own shipment.
- `ExternalReference` should be unique per merchant when used as an idempotent business reference.
- A shipment cannot use an expired quote option.
- A shipment cannot be confirmed multiple times.
- Confirmation must produce a provider shipment reference or equivalent internal result.
- A confirmed shipment must receive a tracking number.
- A confirmed shipment must not silently change to another carrier/service.
- Recipient and package details must not be arbitrarily modified after pickup.
- Cancellation is only allowed before a defined lifecycle boundary.
- A Delivered shipment cannot transition backward to InTransit.
- Tracking events arriving out of order must not incorrectly regress shipment state.
- Duplicate carrier events must not create duplicate tracking history.
- Delivery attempts can only be created in appropriate delivery states.
- Delivery attempt numbers must be sequential.
- Return-to-sender can only be initiated from allowed failed-delivery states.
- COD amount must be greater than zero when COD is enabled.
- A non-COD shipment must not contain a COD collection amount.
- COD cannot be marked collected before a valid delivery/collection event.
- Terminal states must reject invalid future transitions.

State transitions should be implemented through explicit domain/application operations rather than direct status assignment from controllers.

---

## 11. Shipping Quote Workflow

### 11.1 Request

A Merchant submits:

- Origin
- Destination
- One or more packages
- Weight and dimensions
- Declared value
- Optional requested service constraints
- Optional COD requirement

### 11.2 Processing

The application:

1. Validates the request.
2. Determines active carrier services.
3. Calls eligible carrier providers.
4. Normalizes provider results.
5. Persists the quote and quote options.
6. Defines an expiration time.

### 11.3 Response

Each option may include:

- Carrier
- Service
- Price
- Currency
- Estimated delivery range
- Quote expiration
- Quote option identifier

### 11.4 Rules

- Expired quotes cannot be used for shipment confirmation.
- Disabled carriers/services cannot produce new quote options.
- Quote prices are immutable after persistence.
- Provider-specific metadata must not leak unnecessarily into public contracts.

---

## 12. Carrier Integration Abstraction

Carrier behavior must be implemented behind a provider abstraction.

Conceptual contract:

```csharp
public interface IShippingCarrier
{
    Task<IReadOnlyCollection<CarrierRateResult>> GetRatesAsync(...);
    Task<CarrierShipmentResult> CreateShipmentAsync(...);
    Task<CarrierCancellationResult> CancelShipmentAsync(...);
    Task<CarrierPickupResult> SchedulePickupAsync(...);
    Task<CarrierTrackingResult> GetTrackingAsync(...);
}
```

The exact interface may be split if needed to avoid unsupported operations forcing meaningless implementations.

Possible capability interfaces:

- `ICarrierRateProvider`
- `ICarrierShipmentProvider`
- `ICarrierPickupProvider`
- `ICarrierTrackingProvider`

A provider resolver/factory may map Carrier configuration to the correct implementation.

The Domain/Application layers must not depend directly on concrete carrier SDKs.

---

## 13. Demo Carrier

The initial project must include a deterministic **DemoCarrier**.

It should support enough behavior to exercise the full business workflow without external credentials.

DemoCarrier capabilities should include:

- Deterministic quote generation.
- Standard and Express services.
- Shipment creation.
- Tracking number generation.
- Label metadata generation.
- Cancellation before pickup.
- Pickup scheduling.
- Simulated tracking events.
- Webhook-compatible event payloads.

The DemoCarrier should be predictable enough for automated integration tests.

---

## 14. Shipment Creation and Confirmation

Recommended separation:

### Create Shipment

Creates an internal Draft shipment with:

- Merchant reference
- Addresses
- Packages
- COD details
- Chosen quote option or shipping selection

### Confirm Shipment

Confirmation is an explicit business action.

The system should:

1. Validate shipment readiness.
2. Validate quote/service.
3. Call the carrier provider.
4. Persist carrier shipment reference.
5. Persist tracking number.
6. Persist label metadata.
7. Move shipment to Confirmed.
8. Create audit entry.
9. Publish relevant internal event.

Failure must not leave a partially confirmed shipment without a recoverable state.

---

## 15. Shipping Labels

The application should model label metadata without requiring complex document rendering.

Supported values may include:

- PDF
- PNG
- ZPL

A provider may return:

- URL
- Stored object reference
- Base64 only if explicitly justified

The preferred contract is a reference/URL rather than large binary payloads in normal shipment responses.

---

## 16. Pickup Workflow

Pickup statuses may include:

```text
Requested
Scheduled
Completed
Cancelled
Failed
```

Required rules:

- Pickup can only be scheduled for eligible confirmed shipments.
- A shipment should not be picked up before confirmation.
- Pickup cancellation must follow provider capability rules.
- Pickup completion may move shipment to PickedUp.
- Pickup provider references must be persisted.
- Pickup schedule dates must not be in the past at creation time.

---

## 17. Tracking Model

Tracking must preserve both:

- Carrier-native status.
- Normalized internal shipment status.

Example:

```text
Carrier A: ARRIVED_HUB
Carrier B: FACILITY_RECEIVED
Normalized: InTransit
```

Tracking events should include:

- External event ID when supplied.
- Carrier status.
- Normalized status.
- Description.
- Event timestamp.
- Receive timestamp.
- Optional location.

The system must preserve history even when multiple events map to the same normalized shipment status.

---

## 18. Tracking State Normalization

A carrier adapter must translate provider-specific statuses into system statuses.

Examples:

```text
PICKED_UP             -> PickedUp
ARRIVED_HUB           -> InTransit
DEPARTED_HUB          -> InTransit
OUT_FOR_DELIVERY      -> OutForDelivery
DELIVERY_FAILED       -> DeliveryFailed
DELIVERED             -> Delivered
RETURN_STARTED        -> ReturningToSender
RETURNED_TO_SENDER    -> Returned
```

Normalization logic belongs in carrier integration/application infrastructure, not controllers.

Unknown provider statuses must be preserved safely and must not cause invalid lifecycle transitions.

---

## 19. Delivery Attempts

A failed delivery should be modeled explicitly.

Failure reasons may include:

- RecipientUnavailable
- InvalidAddress
- RecipientRefused
- UnableToContact
- RestrictedArea
- DamagedShipment
- Other

Rules:

- An attempt may only be recorded in allowed delivery states.
- Attempt numbers must be sequential.
- A maximum configurable attempt count may be used.
- A failed attempt may schedule a next attempt.
- Repeated failure may allow Return To Sender.
- Attempts must remain part of shipment history.

A sensible initial maximum is 3 attempts, but it should be configuration-driven or represented through a policy rather than hard-coded across the codebase.

---

## 20. Return To Sender Workflow

Return To Sender is a logistics failure/recovery workflow, not an e-commerce customer return workflow.

Possible statuses:

```text
Initiated
Returning
Returned
Cancelled
```

Rules:

- Return may only start from explicitly allowed shipment states.
- A Delivered shipment cannot enter Return To Sender.
- Return tracking continues to append shipment tracking events.
- Returned is a terminal shipment state.
- Return reason must be persisted.

---

## 21. Cash On Delivery

COD support is intentionally operational, not accounting-heavy.

Possible COD statuses:

```text
Pending
Collected
Failed
Cancelled
```

Required rules:

- COD amount > 0 when enabled.
- Currency is required for COD.
- Collection is associated with the shipment.
- Duplicate collection updates must be idempotent.
- COD reporting should expose collected vs pending amounts.
- Full merchant settlement and reconciliation are outside scope.

---

## 22. Carrier Webhooks

Carrier webhook endpoint pattern may follow:

```text
POST /api/webhooks/carriers/{carrierCode}
```

Processing flow:

```text
Receive Request
    ↓
Resolve Carrier
    ↓
Validate Signature / Authenticity
    ↓
Persist Raw Event Metadata
    ↓
Check Duplicate
    ↓
Acknowledge Request
    ↓
Background Processing
    ↓
Normalize Event
    ↓
Update Shipment
    ↓
Append Tracking Event
    ↓
Create Audit/Operational Metadata
    ↓
Publish Internal Event
```

### Requirements

- Webhook processing must be idempotent.
- Signature validation must be implemented for DemoCarrier using a realistic shared-secret/HMAC approach or equivalent.
- Duplicate events must not duplicate side effects.
- Unknown shipments must be handled safely.
- Invalid signatures return an appropriate error.
- Processing failures must be observable and retryable.
- Raw sensitive payloads must not be logged indiscriminately.

---

## 23. Idempotency

Idempotency is a first-class project concern.

### 23.1 API Idempotency

Critical commands such as shipment creation/confirmation may support:

```text
Idempotency-Key
```

Repeated requests with the same key and same logical request should not create duplicate business operations.

### 23.2 Carrier Event Idempotency

Carrier events should be deduplicated by:

1. Provider event ID when available.
2. A stable payload hash or equivalent fallback strategy when necessary.

### 23.3 Outbound Webhook Idempotency

Every outbound event should have a stable event ID.

Merchants should be able to use this value to deduplicate notifications.

---

## 24. Outbound Merchant Webhooks

Merchants may register webhook endpoints.

Possible events:

```text
shipment.confirmed
shipment.pickup_scheduled
shipment.picked_up
shipment.in_transit
shipment.out_for_delivery
shipment.delivery_failed
shipment.delivered
shipment.return_initiated
shipment.returned
shipment.cancelled
cod.collected
```

Payloads should include:

- Event ID.
- Event type.
- OccurredAtUtc.
- MerchantId/reference.
- Shipment ID.
- ExternalReference.
- TrackingNumber.
- Relevant status data.

### Delivery behavior

- Sign outbound payloads.
- Record each delivery attempt.
- Retry transient failures.
- Stop after a configured retry policy.
- Expose failed deliveries for operational review.
- Do not block the original shipment transaction while calling merchant webhooks.

---

## 25. Background Processing

Background processing should cover operations that must not block request-response flows.

Suggested workers/jobs:

### 25.1 Carrier Webhook Processing Worker

Processes persisted carrier events.

Responsibilities:

- Normalize event.
- Find shipment.
- Apply transition.
- Append tracking event.
- Publish internal event.
- Mark processing status.

### 25.2 Outbound Webhook Delivery Worker

Responsibilities:

- Find pending deliveries.
- Send signed HTTP request.
- Apply retry policy.
- Persist response/result.
- Schedule next attempt.

### 25.3 Tracking Synchronization Worker

Optional but recommended if scope remains manageable.

Purpose:

- Poll tracking for eligible active shipments when no recent webhook has arrived.
- Reuse carrier tracking provider abstraction.
- Apply the same normalization and deduplication pipeline.

Avoid creating background workers that duplicate domain logic.

---

## 26. Reliability and Consistency

Important workflows must consider transactional consistency.

Examples:

- Shipment confirmation.
- Carrier webhook persistence.
- Tracking event insertion.
- Shipment status update.
- Outbound webhook event creation.

Where an external carrier call occurs, avoid pretending a single SQL transaction can provide distributed atomicity.

Use explicit states and recoverable workflows.

An Outbox-style mechanism is recommended for reliable internal-to-outbound event publication if implemented without excessive complexity.

---

## 27. Authentication and Authorization

Authentication:

- JWT access tokens.
- Refresh tokens.
- Secure password hashing through ASP.NET Core Identity or a justified alternative.

Roles:

```text
Admin
Operator
Merchant
```

Authorization rules:

- Merchant users access only their merchant’s resources.
- Operators may access operational shipment data according to policy.
- Administrators may manage carriers/services and review cross-merchant operations.
- Sensitive configuration endpoints are administrator-only.

Authorization should use policies where that improves clarity.

---

## 28. Merchant Isolation

Merchant isolation is mandatory.

Every merchant-owned aggregate/query must enforce MerchantId at the persistence/query boundary.

Do not rely only on IDs supplied by the client.

Examples:

- Shipment lookup.
- Quote lookup.
- Pickup lookup.
- Reports.
- Webhook endpoints.

The project is not implementing full multi-tenant infrastructure, but merchant data isolation must be correct.

---

## 29. Validation

Use FluentValidation or equivalent application validation.

Validation should cover:

- Required fields.
- Length limits.
- Email/phone where applicable.
- Country codes.
- Positive monetary values.
- Weight and dimensions.
- Date ranges.
- Pagination.
- Sorting fields.
- Service identifiers.
- COD rules.
- Pickup windows.

Validation must distinguish between:

- Invalid request shape/input.
- Missing resource.
- Business conflict.
- Forbidden transition.
- Authorization failure.

---

## 30. Error Handling

Use centralized Problem Details responses.

Expected categories:

- 400 — Validation or malformed request.
- 401 — Missing/invalid authentication.
- 403 — Forbidden.
- 404 — Resource not found.
- 409 — Business conflict, duplicate reference, invalid transition, expired quote.
- 422 — Optional if used consistently for domain validation; otherwise prefer 409/400 according to documented policy.
- 500 — Safe generic server error.

Responses must not expose stack traces or sensitive infrastructure details.

---

## 31. Concurrency

Critical state transitions should be protected from concurrent updates.

Examples:

- Confirming the same shipment twice.
- Concurrent cancellation and pickup.
- Duplicate webhook processing.
- Multiple delivery-attempt updates.

Use:

- Database uniqueness constraints.
- Optimistic concurrency where appropriate.
- Idempotency.
- Transaction boundaries.
- Atomic update strategies where needed.

---

## 32. Persistence Design

Use SQL Server with EF Core.

Guidelines:

- Explicit entity configurations.
- Appropriate indexes.
- Unique constraints.
- Controlled delete behavior.
- UTC timestamps.
- Decimal precision for money and measurements.
- Normalized carrier/service codes.
- No cascade delete for historical operational data when unsafe.

Potential indexes include:

- MerchantId + ExternalReference.
- TrackingNumber.
- Shipment Status.
- CarrierId + Status.
- CreatedAtUtc.
- Carrier webhook external event key.
- Pending outbound webhook status + NextAttemptAtUtc.

---

## 33. API Conventions

General API rules:

- RESTful resource naming.
- Thin controllers.
- Async APIs.
- CancellationToken propagation.
- Consistent response DTOs.
- UTC date/time.
- Pagination metadata.
- Controlled filtering/sorting.
- No EF entities exposed directly.
- Stable public enums or documented string values.
- OpenAPI documentation.

Example resource areas:

```text
/api/auth
/api/merchants
/api/carriers
/api/carrier-services
/api/quotes
/api/shipments
/api/shipments/{id}/confirm
/api/shipments/{id}/cancel
/api/shipments/{id}/tracking
/api/shipments/{id}/delivery-attempts
/api/shipments/{id}/return-to-sender
/api/pickups
/api/webhooks/carriers/{carrierCode}
/api/webhook-endpoints
/api/reports
```

Exact endpoint design should be finalized per implementation phase.

---

## 34. Search, Filtering, Sorting, and Pagination

Shipment listing should support controlled filters such as:

- status
- carrierId
- carrierServiceId
- trackingNumber
- externalReference
- recipientName
- createdFrom
- createdTo
- isCod
- page
- pageSize
- sortBy
- sortDirection

Filtering and pagination must occur in SQL, not after loading full tables into memory.

Sorting must use a whitelist of supported fields.

---

## 35. Reporting

Keep reporting operational and testable.

Recommended initial reports:

- Total shipments.
- Shipments by status.
- Delivered shipments.
- In-transit shipments.
- Failed deliveries.
- Returned shipments.
- Delivery success rate.
- Average delivery time.
- Shipments by carrier.
- Shipments by service.
- COD pending amount.
- COD collected amount.

Filters may include:

- Date range.
- Merchant.
- Carrier.
- Service.
- Status.
- Country.

Avoid creating a full analytics warehouse.

---

## 36. Audit Trail

Audit important business operations such as:

- Shipment created.
- Shipment confirmed.
- Shipment cancelled.
- Pickup scheduled.
- Pickup cancelled.
- Delivery attempt recorded.
- Return initiated.
- Shipment returned.
- Carrier/service administration changes.
- Webhook endpoint changes.

Audit entries should answer:

- Who?
- What?
- When?
- Which resource?
- What important business action occurred?

Do not attempt to make AuditEntry a generic copy of every modified field unless there is a clear requirement.

---

## 37. Logging and Observability

Use structured logging.

Include useful properties such as:

- CorrelationId.
- Request path.
- MerchantId.
- ShipmentId.
- TrackingNumber.
- CarrierCode.
- WebhookEventId.
- OutboundWebhookDeliveryId.

Never log:

- Passwords.
- Refresh tokens.
- Webhook secrets.
- Authorization headers.
- Full sensitive customer payloads unless explicitly sanitized.

Recommended health checks:

- API health.
- SQL Server connectivity.
- Optional background worker health indicators where practical.

---

## 38. Security

Security requirements include:

- Secure JWT signing configuration.
- Refresh-token rotation/revocation.
- Role/policy authorization.
- Merchant isolation.
- Webhook signature verification.
- Outbound webhook signatures.
- Secret configuration outside source control.
- Safe error responses.
- Input validation.
- Request-size limits where relevant.
- Protection against insecure direct-object access.
- No secrets committed to Git.

---

## 39. Testing Strategy

Testing should focus heavily on business workflows.

### 39.1 Unit Tests

Prioritize:

- Shipment state transitions.
- Quote expiration rules.
- Confirmation rules.
- Cancellation rules.
- COD rules.
- Delivery attempts.
- Return-to-sender rules.
- Status normalization.
- Retry policy calculations.
- Signature helpers.

### 39.2 Integration Tests

Prioritize:

- Authentication.
- Merchant isolation.
- Quote creation.
- Shipment creation.
- Shipment confirmation.
- Duplicate confirmation/idempotency.
- Pickup scheduling.
- Carrier webhook ingestion.
- Duplicate carrier webhook handling.
- Tracking history.
- Delivery-failure workflow.
- Return-to-sender workflow.
- COD collection.
- Outbound webhook persistence/delivery behavior.
- Pagination/filtering.
- Reporting.

### 39.3 DemoCarrier Tests

Ensure deterministic behavior so tests do not depend on internet access or real third-party credentials.

---

# 40. Implementation Phases

Implementation must proceed phase by phase.

Do not implement future-phase functionality prematurely unless required as a minimal dependency.

Each phase should end with:

- Clean build.
- Passing relevant tests.
- Updated documentation when needed.
- No unrelated scope.
- Reviewable commit/branch.

---

## Phase 01 — Solution Foundation

### Goals

Establish a clean, production-style ASP.NET Core solution.

### Scope

- Create solution and projects.
- Add project references.
- Configure dependency injection.
- Configure environment-based settings.
- Configure EF Core SQL Server.
- Create initial DbContext.
- Add centralized Problem Details/error handling.
- Add logging.
- Add health endpoint.
- Add Swagger/OpenAPI.
- Add basic test projects.
- Add initial architecture documentation.
- Add `.gitignore`.
- Add configuration examples without secrets.

### Acceptance Criteria

- Solution builds successfully.
- API starts successfully.
- Swagger loads.
- Health endpoint returns successfully.
- SQL Server configuration is externally configurable.
- Unit/integration test projects execute.
- No domain features are prematurely implemented.

---

## Phase 02 — Identity, Authentication, and Merchant Access

### Goals

Implement secure access and merchant isolation foundations.

### Scope

- User model.
- Roles: Admin, Operator, Merchant.
- Merchant entity.
- Merchant-user relationship.
- JWT authentication.
- Refresh-token workflow.
- Login.
- Refresh.
- Revoke/logout.
- Initial seeded administrator for development.
- Authorization policies.
- Merchant context resolution.
- Tests for authentication and authorization.

### Acceptance Criteria

- Valid users can authenticate.
- Access tokens expire according to configuration.
- Refresh tokens rotate/revoke correctly.
- Merchant users cannot access another merchant’s data.
- Admin/Operator policies work as documented.
- Sensitive authentication data is not exposed.

---

## Phase 03 — Carriers and Carrier Services

### Goals

Create the carrier catalog and provider abstraction.

### Scope

- Carrier entity.
- CarrierService entity.
- Admin CRUD/activation operations.
- Carrier capability metadata.
- Carrier provider interfaces.
- Carrier provider resolver.
- DemoCarrier foundation.
- Standard and Express DemoCarrier services.
- Validation and uniqueness rules.
- Tests.

### Acceptance Criteria

- Admin can manage carriers/services.
- Merchant cannot manage carrier configuration.
- Services are correctly scoped to their carrier.
- Disabled services cannot be selected for new workflows.
- DemoCarrier is resolvable through abstraction.
- No controller depends on concrete DemoCarrier implementation.

---

## Phase 04 — Shipping Quotes and Rate Shopping

### Goals

Implement real shipping quote workflow.

### Scope

- Address request/value models.
- Package quote inputs.
- ShippingQuote.
- QuoteOption.
- Quote expiration.
- DemoCarrier rate generation.
- Quote request endpoint.
- Quote retrieval.
- Merchant ownership.
- Price/currency handling.
- Validation.
- Pagination/history where appropriate.
- Tests.

### Acceptance Criteria

- Merchant can request quote options.
- Multiple eligible options can be returned.
- Quote options persist immutably.
- Expired quotes are identified.
- Merchant cannot access another merchant’s quotes.
- Disabled carrier services do not appear in new quotes.

---

## Phase 05 — Shipments and Packages

### Goals

Implement draft shipment creation and management.

### Scope

- Shipment entity.
- Package entity.
- Sender/recipient address snapshotting.
- ExternalReference.
- Draft lifecycle.
- Shipment creation.
- Draft update rules.
- Shipment retrieval.
- Shipment listing.
- Filtering.
- Pagination.
- Sorting.
- Merchant isolation.
- Package validation.
- COD fields at shipment level, without collection workflow yet.
- Tests.

### Acceptance Criteria

- Merchant can create valid draft shipments.
- Shipment supports multiple packages.
- Duplicate ExternalReference per merchant is prevented.
- Filtering/pagination are database-side.
- Merchant cannot access another merchant’s shipments.
- Invalid package dimensions/weights are rejected.
- Draft-only fields cannot be modified after restricted states.

---

## Phase 06 — Shipment Confirmation, Tracking Number, and Labels

### Goals

Convert valid draft shipments into carrier-confirmed shipments.

### Scope

- Explicit confirmation command.
- Quote-option validation.
- DemoCarrier shipment creation.
- Tracking-number generation.
- Provider shipment reference.
- Label metadata.
- Confirmation idempotency.
- Confirmation audit entry.
- Cancellation for eligible pre-pickup states.
- Concurrency protection.
- Tests.

### Acceptance Criteria

- Valid shipment can be confirmed once.
- Confirmation returns stable tracking and label information.
- Expired quote cannot be confirmed.
- Duplicate confirmation does not create duplicate carrier shipments.
- Invalid cancellation state returns a business conflict.
- Confirmed shipment persists all required provider references.

---

## Phase 07 — Pickup Management

### Goals

Implement pickup scheduling lifecycle.

### Scope

- Pickup entity.
- Schedule pickup.
- Retrieve pickup.
- List pickups.
- Cancel pickup.
- DemoCarrier pickup integration.
- Pickup status transitions.
- Shipment transition to PickupScheduled.
- Pickup completion simulation/operation.
- Shipment transition to PickedUp.
- Tests.

### Acceptance Criteria

- Eligible shipment can schedule pickup.
- Past pickup windows are rejected.
- Pickup cannot be scheduled for invalid shipment state.
- Cancellation respects lifecycle rules.
- Pickup completion moves shipment correctly.
- Merchant ownership is enforced.

---

## Phase 08 — Tracking and Carrier Event Normalization

### Goals

Implement normalized shipment tracking.

### Scope

- TrackingEvent entity.
- Carrier tracking status mapping.
- DemoCarrier tracking retrieval.
- Shipment tracking endpoint.
- Tracking history.
- Normalized shipment transitions.
- Protection against invalid state regression.
- Unknown carrier statuses handled safely.
- Tests.

### Acceptance Criteria

- Shipment exposes ordered tracking history.
- Native carrier status is preserved.
- Normalized status is persisted.
- Old/out-of-order events cannot regress terminal or later states.
- Tracking polling and webhook processing can share normalization logic.

---

## Phase 09 — Delivery Attempts and Failed Delivery

### Goals

Model last-mile delivery failures explicitly.

### Scope

- DeliveryAttempt entity.
- FailureReason enum.
- Record failed attempt.
- Sequential attempt numbering.
- Next-attempt scheduling.
- Maximum-attempt policy.
- DeliveryFailed transition.
- Retry transition to OutForDelivery.
- Tests.

### Acceptance Criteria

- Delivery attempt can only occur in allowed state.
- Duplicate attempt behavior is safe.
- Attempt numbers are sequential.
- Maximum attempt policy is enforced.
- Shipment history preserves all attempts.
- Invalid state changes are rejected.

---

## Phase 10 — Return To Sender

### Goals

Implement failed-shipment return workflow.

### Scope

- ReturnToSender entity.
- Initiate return.
- Return reason.
- Returning state.
- Returned terminal state.
- Tracking integration.
- DemoCarrier return simulation/support.
- Tests.

### Acceptance Criteria

- Return may only start from allowed states.
- Delivered shipment cannot be returned through this workflow.
- Return lifecycle is persisted.
- Returned is terminal.
- Return tracking remains visible in shipment history.

---

## Phase 11 — Cash On Delivery Workflow

### Goals

Implement operational COD status management.

### Scope

- COD status model.
- COD collection record.
- Mark COD collected through valid operational/provider event.
- Failed/cancelled COD states.
- Idempotent updates.
- COD details in shipment responses.
- Tests.

### Acceptance Criteria

- COD validation is enforced.
- Non-COD shipment cannot receive COD collection.
- Duplicate collection does not duplicate financial records.
- Collected timestamp/value are preserved.
- COD data is available for reporting.

---

## Phase 12 — Carrier Webhooks and Idempotent Event Processing

### Goals

Implement production-style inbound carrier webhooks.

### Scope

- CarrierWebhookEvent entity.
- Carrier webhook endpoint.
- DemoCarrier signature validation.
- Event persistence.
- Provider event ID deduplication.
- Payload hash fallback.
- Processing status.
- Carrier webhook background processing.
- Tracking/state updates through shared pipeline.
- Failure/retry metadata.
- Tests.

### Acceptance Criteria

- Valid signed webhook is accepted.
- Invalid signature is rejected.
- Duplicate carrier event produces no duplicate side effects.
- Tracking event is created exactly once.
- Shipment status is updated through normal transition rules.
- Processing failures are persisted and retryable.

---

## Phase 13 — Outbound Merchant Webhooks

### Goals

Notify merchants reliably about shipment events.

### Scope

- MerchantWebhookEndpoint.
- Endpoint registration and activation.
- Event subscriptions.
- Signing secret.
- Outbound event model.
- OutboundWebhookDelivery.
- HTTP delivery worker.
- HMAC signature.
- Retry policy.
- Delivery history.
- Failed delivery visibility.
- Tests using mock HTTP server/handler.

### Acceptance Criteria

- Merchant can configure its own webhook endpoint.
- Relevant events create outbound deliveries.
- Original shipment operation does not wait for merchant HTTP endpoint.
- Payloads are signed.
- Transient failure retries.
- Permanent/max-retry failure is observable.
- Duplicate domain event does not create duplicate outbound notification.

---

## Phase 14 — Background Tracking Synchronization and Operational Reliability

### Goals

Improve resilience when carrier webhooks are delayed or unavailable.

### Scope

- Tracking synchronization worker.
- Eligible-shipment selection.
- Configurable polling interval.
- Last-sync metadata.
- Reuse tracking normalization/idempotency pipeline.
- Retry/backoff.
- Concurrency safeguards.
- Health/operational logging.
- Tests.

### Acceptance Criteria

- Eligible active shipments are synchronized.
- Delivered/Returned/Cancelled shipments are not continuously polled.
- Polling does not duplicate tracking events.
- Carrier failures do not crash worker.
- Retry behavior is observable.

---

## Phase 15 — Reporting, Search, and Operational Queries

### Goals

Expose useful shipping operational insight.

### Scope

- Shipment summary metrics.
- Status breakdown.
- Carrier/service breakdown.
- Delivery success rate.
- Average delivery duration.
- Failed-delivery counts.
- Return counts.
- COD pending/collected totals.
- Date/carrier/service filters.
- Admin cross-merchant reporting.
- Merchant-scoped reporting.
- Query optimization and indexes.
- Tests.

### Acceptance Criteria

- Reports are SQL-backed and do not load entire tables.
- Merchant reports are isolated.
- Admin reports support cross-merchant scope.
- Date filtering is UTC-safe.
- Metrics have documented definitions.
- Query plans/indexes are reviewed for main filters.

---

## Phase 16 — Hardening, Documentation, and Portfolio Polish

### Goals

Prepare the repository as a strong public portfolio project.

### Scope

- Review architecture.
- Review security.
- Review indexes.
- Review concurrency behavior.
- Review transactional consistency.
- Review retry behavior.
- Complete integration test suite.
- Seed/demo data strategy.
- Swagger examples.
- README.
- Architecture diagram.
- Business workflow documentation.
- Shipment lifecycle diagram.
- Carrier integration documentation.
- Webhook documentation.
- Setup instructions.
- SQL Server setup.
- Demo credentials.
- API usage examples.
- Screenshots/examples where useful.
- Final code cleanup.
- Remove dead code.
- Ensure no secrets or local paths are committed.

### Acceptance Criteria

- Clean clone can be configured and run using documented steps.
- All tests pass.
- README explains business value, not only technical stack.
- Core shipping workflow is easy to demonstrate.
- DemoCarrier enables full local testing without third-party accounts.
- Repository contains no secrets.
- Public API contracts are documented.
- Main architecture decisions are documented.
- Project is suitable for linking from Freelancer, Upwork, Workana, and GitHub profiles.

---

# 41. Recommended Phase Dependencies

```text
01 Foundation
    ↓
02 Identity & Merchants
    ↓
03 Carriers & Services
    ↓
04 Quotes
    ↓
05 Shipments & Packages
    ↓
06 Confirmation & Labels
    ↓
07 Pickups
    ↓
08 Tracking
    ↓
09 Delivery Attempts
    ↓
10 Return To Sender
    ↓
11 COD
    ↓
12 Carrier Webhooks
    ↓
13 Outbound Webhooks
    ↓
14 Tracking Sync / Reliability
    ↓
15 Reporting
    ↓
16 Hardening & Portfolio Polish
```

Some supporting infrastructure may be introduced earlier when strictly necessary, but the visible business implementation should stay aligned with the current phase.

---

# 42. Branching Strategy

Recommended per-phase workflow:

```text
master
  └── phase/01-foundation
  └── phase/02-identity-merchants
  └── phase/03-carriers-services
  └── ...
```

For each phase:

1. Start from updated `master`.
2. Create phase branch.
3. Implement only phase scope.
4. Run build/tests.
5. Review diff.
6. Commit.
7. Push branch.
8. Merge after review.
9. Delete phase branch.
10. Start next phase from updated `master`.

Avoid mixing multiple phases into one branch.

---

# 43. Commit Strategy

Prefer small, meaningful commits inside each phase when practical.

Examples:

```text
feat: add carrier and service domain models
feat: implement DemoCarrier rate provider
feat: add shipping quote workflow
test: cover quote expiration rules
docs: document carrier abstraction
```

Avoid generic commit messages such as:

```text
update
changes
fix
work
```

---

# 44. Definition of Done for Every Phase

A phase is complete only when:

- Required scope is implemented.
- Business rules are enforced.
- Validation is present.
- Authorization is correct.
- Database constraints support critical invariants.
- Relevant tests pass.
- Build is clean.
- No secrets are committed.
- No unrelated future features were added.
- Public API behavior is documented where needed.
- Code follows the established architecture.
- Edge cases defined in the phase have been reviewed.

---

# 45. Portfolio Demonstration Scenario

The final project should support a complete demo scenario:

1. Administrator configures DemoCarrier services.
2. Merchant authenticates.
3. Merchant requests shipping rates.
4. API returns Standard and Express options.
5. Merchant creates a shipment with multiple packages.
6. Merchant confirms a valid quote.
7. DemoCarrier generates tracking number and label metadata.
8. Merchant schedules pickup.
9. Pickup completes.
10. Shipment becomes PickedUp.
11. Carrier events move shipment through InTransit.
12. Shipment becomes OutForDelivery.
13. First delivery attempt fails because recipient is unavailable.
14. A retry is scheduled.
15. Shipment returns to OutForDelivery.
16. Shipment is Delivered.
17. COD is marked Collected when applicable.
18. Merchant receives signed outbound webhook events.
19. Reports show shipment status, delivery performance, and COD totals.

A second demonstration should show:

1. Repeated failed delivery attempts.
2. Return To Sender initiation.
3. Returning state.
4. Returned terminal state.

A third demonstration should show:

1. Duplicate carrier webhook received twice.
2. Only one TrackingEvent created.
3. Shipment status changed once.
4. Outbound webhook event not duplicated.

These scenarios are important portfolio evidence because they show business workflow, integration design, idempotency, reliability, and domain-state management.

---

# 46. Future Extension Ideas

Possible future extensions, intentionally excluded from the core implementation:

- Real DHL adapter.
- Real FedEx adapter.
- Real Aramex adapter.
- Carrier credential management.
- Address-validation providers.
- Object storage for label files.
- RabbitMQ / Azure Service Bus.
- Dedicated outbox/inbox infrastructure.
- Distributed caching.
- Rate limits per merchant.
- Webhook replay UI/API.
- Carrier invoice reconciliation.
- Multi-currency settlement.
- Customs documentation.
- Driver/last-mile application.
- Route optimization.
- Dedicated event bus.

Future extensions must not be introduced unless the core project is already complete and stable.

---

# 47. Final Project Positioning

The project should be presented as:

> A production-style ASP.NET Core multi-carrier shipping management API that models real shipping workflows including rate quotes, shipment confirmation, pickups, tracking, delivery failures, return-to-sender, COD, inbound carrier webhooks, outbound merchant webhooks, background processing, idempotency, and operational reporting.

The repository should demonstrate:

- Strong modern .NET backend skills.
- Shipping-domain understanding.
- API design.
- SQL Server and EF Core.
- Integration patterns.
- Webhooks.
- Background workers.
- Business workflow modeling.
- Security and authorization.
- Reliability and idempotency.
- Testable production-style architecture.

The primary objective is not code volume. The objective is to demonstrate credible real-world backend engineering and business workflow understanding.
