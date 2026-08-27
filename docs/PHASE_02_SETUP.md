# Phase 02 local setup and manual verification

Phase 02 adds ASP.NET Core Identity, JWT access tokens, rotating hashed refresh tokens, roles, merchant provisioning, and tenant-scoped merchant reads. Migrations remain an explicit deployment step. Access tokens remain valid until expiry after logout; logout prevents future refreshes and no access-token deny-list is used.

## Configure and start

From the repository root, choose strong development-only values and run:

```powershell
dotnet tool restore
dotnet user-secrets set "Jwt:SigningKey" "<at-least-32-byte-strong-development-key>" --project src/ShippingManagementApi.Api
dotnet user-secrets set "DevelopmentSeed:AdminEmail" "admin@example.local" --project src/ShippingManagementApi.Api
dotnet user-secrets set "DevelopmentSeed:AdminPassword" "<development-admin-password>" --project src/ShippingManagementApi.Api
dotnet ef database update --project src/ShippingManagementApi.Infrastructure --startup-project src/ShippingManagementApi.Api
dotnet run --project src/ShippingManagementApi.Api --launch-profile http
```

The API base URL is `http://localhost:5196`. Development startup creates the configured administrator idempotently after the migration has been applied. Missing seed settings fail startup clearly; no production account is seeded. OpenAPI is at `/openapi/v1.json` and Scalar is at `/scalar/v1` only in Development.

## Authentication

```powershell
$baseUrl = "http://localhost:5196"
$login = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/auth/login" -ContentType "application/json" -Body (@{
  email = "admin@example.local"
  password = "<development-admin-password>"
} | ConvertTo-Json)
$adminAccess = $login.accessToken
$refresh1 = $login.refreshToken
Invoke-RestMethod -Uri "$baseUrl/api/auth/me" -Headers @{ Authorization = "Bearer $adminAccess" }
```

Expected: login and authenticated `/api/auth/me` return `200`; a wrong password returns a generic `401`; `/api/auth/me` without the Authorization header returns `401`. In Scalar, select Bearer authentication, paste the access token, and call `/api/auth/me`.

## Provision two merchants and verify isolation

```powershell
$headers = @{ Authorization = "Bearer $adminAccess" }
$merchantA = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/admin/merchants" -Headers $headers -ContentType "application/json" -Body (@{
  name = "Merchant A"; code = "MERCHANT-A"; initialUserEmail = "merchant-a@example.local"; initialUserPassword = "<merchant-a-password>"
} | ConvertTo-Json)
$merchantB = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/admin/merchants" -Headers $headers -ContentType "application/json" -Body (@{
  name = "Merchant B"; code = "MERCHANT-B"; initialUserEmail = "merchant-b@example.local"; initialUserPassword = "<merchant-b-password>"
} | ConvertTo-Json)

$loginA = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/auth/login" -ContentType "application/json" -Body (@{
  email = "merchant-a@example.local"; password = "<merchant-a-password>"
} | ConvertTo-Json)
$merchantHeaders = @{ Authorization = "Bearer $($loginA.accessToken)" }
Invoke-RestMethod -Uri "$baseUrl/api/merchants/$($merchantA.id)" -Headers $merchantHeaders
try { Invoke-WebRequest -Uri "$baseUrl/api/merchants/$($merchantB.id)" -Headers $merchantHeaders } catch { $_.Exception.Response.StatusCode.value__ }
try { Invoke-WebRequest -Method Post -Uri "$baseUrl/api/admin/merchants" -Headers $merchantHeaders -ContentType "application/json" -Body '{}' } catch { $_.Exception.Response.StatusCode.value__ }
```

Expected: Admin provisioning returns `201`; Merchant A's own read returns `200`; changing the ID to Merchant B returns safe `404`; a Merchant calling the Admin-only provisioning endpoint returns `403`. Duplicate code or email returns `409` in the normal workflow.

## Refresh rotation and logout

```powershell
$rotated = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/auth/refresh" -ContentType "application/json" -Body (@{ refreshToken = $refresh1 } | ConvertTo-Json)
$refresh2 = $rotated.refreshToken
try { Invoke-WebRequest -Method Post -Uri "$baseUrl/api/auth/refresh" -ContentType "application/json" -Body (@{ refreshToken = $refresh1 } | ConvertTo-Json) } catch { $_.Exception.Response.StatusCode.value__ }
$rotatedAgain = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/auth/refresh" -ContentType "application/json" -Body (@{ refreshToken = $refresh2 } | ConvertTo-Json)
$refresh3 = $rotatedAgain.refreshToken
Invoke-WebRequest -Method Post -Uri "$baseUrl/api/auth/logout" -ContentType "application/json" -Body (@{ refreshToken = $refresh3 } | ConvertTo-Json)
try { Invoke-WebRequest -Method Post -Uri "$baseUrl/api/auth/refresh" -ContentType "application/json" -Body (@{ refreshToken = $refresh3 } | ConvertTo-Json) } catch { $_.Exception.Response.StatusCode.value__ }
```

Expected: each valid refresh returns `200` and a different token; reused and logged-out tokens return `401`; logout returns `204`.

## Foundation and security checks

- `GET /health` returns `200` and includes `X-Correlation-ID`.
- `/openapi/v1.json` and `/scalar/v1` load in Development and return `404` in Production.
- API responses never contain passwords, Identity internals, token hashes, or signing keys.
- `RefreshTokens.TokenHash` contains a 64-character SHA-256 hex hash, never the raw refresh token.
- Tracked appsettings and launch settings contain no signing key or password.
- Default logging does not enable sensitive EF data and application code never logs passwords, tokens, or Authorization headers.
