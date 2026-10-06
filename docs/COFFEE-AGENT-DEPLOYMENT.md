# Coffee Agent credentials and deployment

The repository contains the public model endpoint and model name, but no real model API credential. `CoffeeModel:RemoteApiKey` and `CoffeeModel:PublicApiKey` in `appsettings.json` must remain empty. Do not add credentials to source files, browser scripts, image build arguments, or configuration committed to Git.

## Ordinary remote website deployment

The default provider is `Remote`. Supply the credential privately before using Coffee Agent.

- When running ASP.NET directly, use the environment variable `CoffeeModel__RemoteApiKey`.
- For local development, ASP.NET's existing User Secrets support accepts `CoffeeModel:RemoteApiKey`. User Secrets are for development, are not encrypted, and are not a production vault.
- When using the supplied Docker Compose file, set `COFFEE_MODEL_REMOTE_API_KEY` in the server's untracked `.env` or deployment environment. Compose maps it to `CoffeeModel__RemoteApiKey` inside the web container.
- Keep `COFFEE_MODEL_PROVIDER=Remote` and `COFFEE_MODEL_PUBLIC_API_ENABLED=false`. Ordinary deployments do not host the gateway or require Ollama.

Copy `.env.example` only for a new deployment; never overwrite an existing server `.env` containing database and mail settings. Keep the actual `.env` outside version control, restrict it to its owner, and exclude it from Docker build context. The existing ignore files already exclude it. Environment variables are not an encrypted vault: restrict server and Docker administrative access and avoid printing container environment values or expanded Compose configuration. A managed secret service is preferable for a larger production deployment.

Without a configured client credential, Coffee Agent reports that the remote service is not configured. The normal website and the separate recommendation feature remain independent. Credentials must be distributed over a private channel, not chat transcripts, GitHub commits, or recordings.

## Model host only

The mini PC runs the `Local` provider and uses these private deployment settings:

```dotenv
COFFEE_MODEL_PROVIDER=Local
COFFEE_MODEL_PUBLIC_API_ENABLED=true
COMPOSE_PROFILES=model-host
COFFEE_MODEL_API_KEY=
```

The empty key above is a placeholder. The actual random gateway key is generated and stored on the server only. It authenticates public gateway callers; it is not a Gmail, SSH, database, or administrator credential. Local inference does not need the remote client credential. Ollama stays on the internal Docker network.

## Rotation and revocation

The previously embedded credential must be treated as exposed. Removing it in a later commit does not remove it from old commits, forks, clones, backup images, or backups.

Rotate `COFFEE_MODEL_API_KEY` on the model host and recreate the web container so the running process uses the new value. Verify that the old key returns HTTP 401 and that a newly configured key works. Distribute the new client credential privately and update each authorized client's runtime configuration. Do not restore an exposed key from an old server backup during rollback.

Alternatively, set `COFFEE_MODEL_PUBLIC_API_ENABLED=false` and recreate the web container to disable all public model access. Local Coffee Agent inference on the model host can continue. Do not delete database or model volumes during either operation.

This version has one shared gateway credential. Rotation revokes all clients using the previous key; individual client revocation would require a separate credential-management implementation. The key is not bound to a particular executable, so anyone possessing it can call the limited gateway.

## Existing protections

- Twenty public gateway requests per minute in one application instance, with no queue.
- One local inference at a time in the singleton web client, plus one Ollama parallel request.
- Separate website chat limits, authentication, anti-forgery checks and input limits.
- HTTPS-only remote calls, no automatic redirects, and safe handling of authentication failures.
- No direct internet exposure of Ollama's port; no model tools for changing accounts or sending email.

These controls limit abuse but do not replace secret management or guarantee model accuracy.

## Tests

Run `--recommendations-only` to verify remote credential injection with test-only values, missing-credential rejection, gateway revocation and the unchanged recommendation workflow. The live `--remote-coffee-model-smoke` check reads runtime environment configuration. Set the environment privately before running it; never commit a live credential to make the smoke check pass.
