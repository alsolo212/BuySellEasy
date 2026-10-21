# Security integration tests

The suite starts the actual API in the `Testing` environment, replaces PostgreSQL
with an isolated temporary SQLite database, disables the background order worker,
and uses a test-only authentication handler. It never reads or modifies the local
development database, uploads, Stripe account, or Ollama service.

Run all backend tests from the backend repository directory:

```bash
dotnet test BSE.sln
```

Run only the security suite:

```bash
dotnet test Api.IntegrationTests/Api.IntegrationTests.csproj
```

The tests cover foreign order/chat access, forged `userId` filtering and review
recipient IDs, inventory reservation/oversell protection, duplicate Stripe
webhook delivery, and private chat-upload access. The Stripe webhook test signs
a local fixture with a test secret; it does not call Stripe.
