# Gemini menu recommendations

Authenticated customers receive up to five menu recommendations after the Menu page loads. The page calls the application API; the browser never calls Gemini directly and never receives the Gemini API key.

## Flow

1. `POST /api/menu-recommendations` identifies the signed-in Customer from the Identity cookie.
2. The server aggregates that customer's non-cancelled order items and active numeric product ratings.
3. Only active, in-stock menu candidates and anonymous aggregate history are sent to Gemini.
4. Gemini returns structured `productId + reason` values.
5. The server rejects unknown, duplicate, inactive or unavailable products, fills missing places with a deterministic fallback, and supplies trusted product names from SQL Server.

Names, email addresses, delivery addresses, phone numbers, passwords, delivery notes, review comments and complete orders are not sent to Gemini. Price, stock and product identity remain controlled by the application database.

## Browser API contract

Request:

```http
POST /api/menu-recommendations
RequestVerificationToken: <page antiforgery token>
```

There is no request body and no browser-supplied user ID.

Response:

```json
{
  "source": "gemini",
  "isPersonalized": true,
  "generatedAtUtc": "2026-09-29T05:30:00Z",
  "items": [
    {
      "rank": 1,
      "productId": 1,
      "productName": "Campus Flat White",
      "reason": "You often choose smooth milk-based coffee, so this is a familiar match."
    }
  ]
}
```

`source` is `gemini` when at least one valid Gemini selection was used and `fallback` when the local ranking supplied the complete result.
The Menu page displays this honestly as `AI-assisted` or `Local fallback`; fallback results are never labelled as AI-generated.

## Local setup

Keep the key out of `appsettings.json` and Git. From the project directory:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_KEY"
dotnet user-secrets set "Gemini:Enabled" "true"
```

The model is configurable:

```powershell
dotnet user-secrets set "Gemini:Model" "gemini-3.1-flash-lite"
```

For Docker, set `GEMINI_ENABLED=true`, `GEMINI_API_KEY`, and optionally `GEMINI_MODEL` in the existing server `.env`. If Gemini is disabled, unconfigured, unavailable, rate-limited, times out or returns invalid JSON, customers still receive fallback recommendations.

The REST integration follows Google's Generate Content and structured-output documentation:

- <https://ai.google.dev/api/generate-content>
- <https://ai.google.dev/gemini-api/docs/generate-content/structured-output>
- <https://ai.google.dev/gemini-api/docs/troubleshooting>

## Verification

Run the recommendation and API pipeline checks without calling Gemini:

```powershell
dotnet run --project .\CampusCoffeeSystem.Tests\CampusCoffeeSystem.Tests.csproj -- --recommendations-only
```

The checks cover structured model output, Product ID validation, deterministic fallback,
customer-only authorization, antiforgery protection, successful customer responses and HTTP 429 rate limiting.
