# Integrated main + email/AI deployment

The integration branch combines main at 2320d83 with email/AI at 4f683f7.
Keep the original customer, merchant, administrator and profile pages.
The canonical registration/login endpoints are now under /api/auth.
The old PortalAuthController is intentionally replaced by EmailConfirmationService
and the new role-aware AuthController.

## Configuration

Copy .env.example to .env on the server and supply the existing SQL password,
Gmail address and Gmail app password. Never overwrite an existing .env blindly.
PUBLIC_BASE_URL must be the public HTTPS origin.
Docker persists Identity keys, SQL data, Ollama models and uploaded images.
If the existing installation has uploaded files outside a volume, back them up
and copy them into the uploads volume before replacing its container.

The web port remains 127.0.0.1:8080 for host Apache.
ReverseProxy:Enabled is enabled only in Compose; Apache must overwrite forwarded
headers. Do not expose this trusted-proxy listener directly on a public address.
Ollama stays on the internal network and uses OLLAMA_NO_CLOUD=1.

## Database compatibility

Back up SQL Server before deploying the merge. Do not remove SQL volumes.

- **New database:** Compose enables Database:ApplyMigrations; EF creates the schema.
- **Group main database with EF history:** normal migrations add the AI tables.
- **Original email/AI database created by EnsureCreated:** it has Identity/AI
  tables but may not have the initial migration recorded. Before starting the
  integrated web service, inspect its migration history and run
  deployment/sql/baseline-legacy-identity.sql against that backed-up database.
  The script refuses non-legacy databases and records only the initial Identity
  baseline; it does not delete users or confirm email addresses.
  Start the integrated web service afterward to apply the remaining migrations.

Existing AI history is preserved by the new idempotent migration.
Startup assigns the Customer role to old roleless accounts, excluding accounts
with a merchant application. Existing confirmed accounts stay confirmed.
For non-Docker development, run dotnet ef database update before starting the app,
or explicitly enable Database:ApplyMigrations for the intended database.

This change has not connected to or deployed to the Debian NUC.
The branch must first be pushed to GitHub before the NUC can fetch it.

## Verification

- dotnet build CampusCoffeeSystem.csproj
- node --check wwwroot/js/coffee-agency.js
- node --check wwwroot/js/portal-auth.js
- node --check wwwroot/js/email-confirmation.js
- On Windows with LocalDB: create the dedicated test instance using
  sqllocaldb create CampusCoffeeIntegration -s, then run
  dotnet run --project CampusCoffeeSystem.Tests/CampusCoffeeSystem.Tests.csproj

The integration executable creates a randomly named database and removes only
that database afterward. It uses real Identity token validation, password hashes,
SQL migrations and controllers with simulated SMTP and Ollama responses.
It never sends actual email or calls a production database.

Deployment acceptance: register a fresh customer, receive the actual Gmail email,
confirm login is blocked before clicking, click the link, then sign in. Repeat
with a merchant and confirm approval is still required. Check recommendations
add real products to the cart and that current stock is enforced at checkout.
