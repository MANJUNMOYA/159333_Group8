# Campus Coffee and Catering Services Web Application

A campus coffee and catering services platform — group project (`159333_Group8`) built by a team of five.

The platform lets customers browse the menu, place pickup orders, track order history and leave reviews; merchants apply to join the platform, manage incoming orders and fulfil them; administrators review merchant applications, manage products and oversee platform data. It also integrates an AI coffee assistant chat and personalised menu recommendations.

## Team and Division of Work (Team of Five)

| Area | Member (Name / Student ID) | Responsibility | Main Files / Directories |
| --- | --- | --- | --- |
| Frontend | Yifan Dai(25008536)/Zhenning Zhu(25008537) | Page layout, interactions and visual design (Razor views + HTML/CSS/JavaScript) | `Views/`, `wwwroot/` |
| Backend | Zhuolun Li(24009223)/Yunhe Wei(24009354) | Business logic, controllers, service layer, authentication/authorisation and APIs | `Controllers/`, `Services/`, `Program.cs` |
| Database | Ziyi Yuan(23010525)/Zhuolun Li(24009223) | Data model design, EF Core migrations, seed data | `Data/`, `Models/`, `Data/Migrations/` |
| AI | Yifan Dai(25008536)/Ziyi Yuan(23010525) | AI coffee assistant (Coffee Agency), menu recommendations (local Ollama / remote model / Gemini) | CoffeeModel, Recommendation and CoffeeAgency classes under `Services/` |
| Testing | Zhenning Zhu(25008537)/Yunhe Wei(24009354) | Integration/API automated checks | `CampusCoffeeSystem.Tests/` |

## Features

- **Customer**: browse the menu (categories, dietary labels, allergen info), product details, shopping cart, checkout (pickup), order history, order reviews, profile; register/login with email verification
- **Merchant**: application to join the platform, order status workflow (pending / preparing / ready for pickup), view customer reviews
- **Administrator**: review merchant applications, create/edit/delete products, platform overview dashboard
- **AI capabilities**: Coffee Agency assistant chat (supports both a local Ollama provider and a shared remote model); "Recommended for you" personalised menu recommendations; optional Gemini recommendation service
- **Notifications**: SMTP email — verification, order confirmation, merchant application notifications
- **Security**: ASP.NET Core Identity with three roles (Customer / Merchant / Administrator), endpoint rate limiting, anti-forgery tokens, HSTS (production)

## Tech Stack

| Layer | Technology |
| --- | --- |
| Backend | ASP.NET Core MVC (.NET 8), C# |
| Database | SQL Server (local Windows auth / Docker SQL Server 2022), Entity Framework Core 8, Migrations |
| Frontend | Razor views + HTML/CSS/JavaScript (`wwwroot/`) |
| Auth | ASP.NET Core Identity + role-based authorisation |
| AI | Ollama (`qwen2.5:0.5b-instruct`, optional local) + remote Coffee Model API; optional Gemini menu recommendations |
| Testing | `CampusCoffeeSystem.Tests` (custom executable integration/API check suite) |
| Deployment | Docker + Docker Compose (web / db / optional ollama), Apache reverse proxy + HTTPS (duckdns) |

## Project Structure

```
159333_Group8/
├── Controllers/            # MVC controllers (Auth, Home, Orders, Products, Platform, AI-related, etc.)
├── Services/               # Business services: email, notifications, menu recommendations, Coffee Model clients, AI assistant
├── Models/                 # Entities and view models (Product, CustomerOrder, MerchantApplication, OrderReview, etc.)
├── Data/                   # EF Core DbContext, Migrations, startup seed data
├── Views/                  # Razor views (menu, cart, checkout, orders, reviews, role dashboards, login/register, etc.)
├── Areas/Identity/         # Identity account pages
├── wwwroot/                # Static assets (css / js / images)
├── CampusCoffeeSystem.Tests/  # Integration and API automated checks
├── docs/                   # Feature docs (email verification, Gemini recommendations, notifications)
├── appsettings.json        # Configuration (contains demo secrets — see Security Notes below)
├── compose.yaml / Dockerfile / .env.example   # Containerised deployment
└── Program.cs              # Application entry point, dependency injection and request pipeline
```

## Getting Started

### Local Development

Prerequisites: .NET 8 SDK, SQL Server (local instance or LocalDB).

```bash
dotnet restore
dotnet ef database update   # apply migrations (requires the dotnet-ef tool)
dotnet run
```

- The default connection string is `Server=.;Database=CampusCoffeeSystem;Trusted_Connection=True;...` (see `appsettings.json`), using Windows authentication against a local SQL Server
- On startup the application seeds data automatically: the three roles, sample products (coffee / bakery / light meals) and demo accounts

**Demo accounts** (`FrontendDemoSeed`, password `123456`):

| Account | Role |
| --- | --- |
| customer@test.com | Customer |
| merchant@test.com | Merchant |
| admin@test.com | Administrator |

**Development administrator** (`SeedAdmin`, only in `appsettings.Development.json`): `admin@campuscoffee.local` / `Campus123!`

### Running the Tests

```bash
cd CampusCoffeeSystem.Tests
dotnet run
```

`CampusCoffeeSystem.Tests` is a custom executable check suite (not xUnit). It covers the coffee assistant, menu recommendations, the remote Coffee Model API, and email verification/notification integration. Each run uses its own temporary LocalDB database and never sends real emails.

### Docker Deployment

1. Copy `.env.example` to `.env` and fill in variables such as `MSSQL_SA_PASSWORD`
2. Start the services:

```bash
docker compose up -d --build
```

3. To enable the local Ollama model (`CoffeeModel__Provider=Local`):

```bash
docker compose --profile model-host up -d --build
```

- Production settings (SMTP, Gemini, CoffeeModel, PublicBaseUrl, database connection, etc.) are injected via Compose environment variables, so the committed configuration files do not need to be modified
- Persistent volumes: `sqlserver-data`, `data-protection-keys`, `uploads-data`

## Configuration

Key sections in `appsettings.json`:

| Section | Description |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Database connection string |
| `CoffeeModel` | AI model: `Provider` (`Local` = Ollama / `Remote` = shared remote), `BaseUrl`, `RemoteBaseUrl`, `Model`, `TimeoutSeconds`, etc. |
| `Gemini` | Optional Gemini menu recommendations (disabled by default) |
| `Smtp` | Email sending (Host / Port / credentials / sender) |
| `Notifications` | Toggles for order confirmation and merchant application notifications |
| `DataProtection` | Data protection key persistence path |
| `SeedAdmin` / `FrontendDemoSeed` | Development seed accounts (in `appsettings.Development.json`) |

## Security Notes (Important)

⚠️ **In a real project, secrets — API keys, passwords, connection strings — must never be committed inside `appsettings.json`.** Use `dotnet user-secrets`, environment variables or a managed key vault (e.g. Azure Key Vault / AWS Secrets Manager) instead.

For **demonstration convenience**, this project deliberately writes some sensitive values into the configuration files:

- `CoffeeModel:RemoteApiKey` (shared key for the remote AI service) in `appsettings.json`
- Development seed account passwords in `appsettings.Development.json`

These values are for demo purposes only. **Do not reuse them in production**; inject real secrets via `.env` / Compose environment variables and rotate any key that has been exposed. The current Docker deployment already supports overriding all sensitive configuration through environment variables, so it can run securely without editing the committed files.

## Related Docs

- `docs/EMAIL-VERIFICATION.md` — email verification flow
- `docs/GEMINI-RECOMMENDATIONS.md` — Gemini menu recommendations
- `docs/NOTIFICATIONS.md` — email notifications (order confirmation, merchant applications)
