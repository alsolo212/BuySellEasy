using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using Domain.IdentityEntities;
using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StripeApiConfiguration = Stripe.StripeConfiguration;
using StripeEventUtility = Stripe.EventUtility;
using Xunit;

namespace Api.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection
{
    public const string Name = "integration-tests";
}

[Collection(IntegrationTestCollection.Name)]
public sealed class SecurityIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public SecurityIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Foreign_user_cannot_read_another_users_order_or_chat()
    {
        var scenario = await SeedScenarioAsync(includeOrder: true);
        using var attacker = CreateAuthenticatedClient(scenario.IntruderId);

        var orderResponse = await attacker.GetAsync($"/api/orders/{scenario.OrderId}");
        var chatResponse = await attacker.GetAsync($"/api/chats/{scenario.ChatId}/messages");

        Assert.Equal(HttpStatusCode.Forbidden, orderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, chatResponse.StatusCode);
    }

    [Fact]
    public async Task Forged_target_user_id_does_not_expose_another_users_orders()
    {
        var scenario = await SeedScenarioAsync(includeOrder: true);
        using var attacker = CreateAuthenticatedClient(scenario.IntruderId);

        var response = await attacker.GetAsync($"/api/orders/mine?userId={scenario.BuyerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(0, document.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Forged_review_target_user_id_is_rejected()
    {
        var scenario = await SeedScenarioAsync(includeOrder: true);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            var order = await db.Orders.SingleAsync(item => item.Id == scenario.OrderId);
            order.Status = OrderStatus.Delivered;
            await db.SaveChangesAsync();
        }

        using var buyer = CreateAuthenticatedClient(scenario.BuyerId);
        var response = await buyer.PostAsJsonAsync("/api/reviews", new
        {
            orderId = scenario.OrderId,
            targetUserId = scenario.IntruderId,
            rating = 5,
            comment = "Attempt to forge the review target."
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ProductDbContext>();
        Assert.Equal(0, await verificationDb.Reviews.CountAsync(item => item.OrderId == scenario.OrderId));
    }

    [Fact]
    public async Task Second_purchase_is_rejected_after_the_only_item_is_reserved()
    {
        var scenario = await SeedScenarioAsync(includeOrder: false, listingQuantity: 1);
        using var firstBuyer = CreateAuthenticatedClient(scenario.BuyerId);
        using var secondBuyer = CreateAuthenticatedClient(scenario.IntruderId);

        var firstResponse = await firstBuyer.PostAsJsonAsync("/api/orders", CreateOrderRequest(scenario.ListingId));
        var secondResponse = await secondBuyer.PostAsJsonAsync("/api/orders", CreateOrderRequest(scenario.ListingId));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var listing = await db.Listings.SingleAsync(item => item.Id == scenario.ListingId);
        var orders = await db.Orders.CountAsync(item => item.ListingId == scenario.ListingId);

        Assert.Equal(1, listing.ReservedQuantity);
        Assert.Equal(1, orders);
    }

    [Fact]
    public async Task Duplicate_stripe_webhook_does_not_create_duplicate_orders()
    {
        var scenario = await SeedScenarioAsync(includeOrder: true);
        var paymentSessionId = Guid.NewGuid();
        const string paymentIntentId = "pi_duplicate_webhook_test";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            var existingOrder = await db.Orders.SingleAsync(item => item.Id == scenario.OrderId);
            existingOrder.PaymentMethod = PaymentMethod.Card;
            existingOrder.PaymentState = PaymentState.Paid;
            existingOrder.StripePaymentIntentId = paymentIntentId;
            db.PaymentSessions.Add(new PaymentSession
            {
                Id = paymentSessionId,
                BuyerId = scenario.BuyerId,
                StripeCheckoutSessionId = "cs_processed_webhook_test",
                StripePaymentIntentId = paymentIntentId,
                Status = PaymentSessionStatus.Processed,
                TotalAmount = 100m,
                DraftJson = "{}",
                ProcessedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var payload = CreatePaymentIntentEvent(paymentSessionId, paymentIntentId);
        var firstResponse = await SendSignedWebhookAsync(payload);
        var secondResponse = await SendSignedWebhookAsync(payload);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ProductDbContext>();
        Assert.Equal(1, await verificationDb.PaymentSessions.CountAsync(item => item.Id == paymentSessionId));
        Assert.Equal(PaymentSessionStatus.Processed, await verificationDb.PaymentSessions
            .Where(item => item.Id == paymentSessionId)
            .Select(item => item.Status)
            .SingleAsync());
        Assert.Equal(1, await verificationDb.Orders.CountAsync(item => item.StripePaymentIntentId == paymentIntentId));
    }

    [Fact]
    public async Task Private_chat_attachment_is_not_available_to_a_nonparticipant()
    {
        var scenario = await SeedScenarioAsync(includeOrder: false);
        var directory = Path.Combine(_factory.WebRootPath, "uploads", "chats", scenario.ChatId.ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "private-note.txt"), "private attachment");

        using var attacker = CreateAuthenticatedClient(scenario.IntruderId);
        using var participant = CreateAuthenticatedClient(scenario.BuyerId);
        var url = $"/uploads/chats/{scenario.ChatId:N}/private-note.txt";

        var denied = await attacker.GetAsync(url);
        var allowed = await participant.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal("nosniff", allowed.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Global_rate_limit_rejects_anonymous_request_floods()
    {
        using var rateLimitedFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DdosProtection:RequestsPerMinute"] = "60",
                    ["DdosProtection:BurstSize"] = "1"
                })));
        using var client = rateLimitedFactory.CreateClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => client.GetAsync("/api/catalog/categories")));

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        var rejected = responses.First(response =>
            response.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        return client;
    }

    private async Task<HttpResponseMessage> SendSignedWebhookAsync(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = StripeEventUtility.GenerateSignatureHeader(
            payload,
            TestApplicationFactory.StripeWebhookSecret,
            timestamp);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", signature);

        var client = _factory.CreateClient();
        return await client.SendAsync(request);
    }

    private async Task<SecurityScenario> SeedScenarioAsync(bool includeOrder, int listingQuantity = 2)
    {
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();
        var listingId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var category = new Category { Id = Guid.NewGuid(), Name = $"Tests-{Guid.NewGuid():N}" };
        var seller = CreateUser(sellerId, "seller");
        var buyer = CreateUser(buyerId, "buyer");
        var intruder = CreateUser(intruderId, "intruder");
        var listing = new Listing
        {
            Id = listingId,
            OwnerId = sellerId,
            CategoryId = category.Id,
            Title = "Integration-test listing",
            Description = "Listing used only by the security integration suite.",
            Price = 100m,
            Quantity = listingQuantity,
            Status = ListingStatus.Published,
            City = "Kyiv",
            Condition = ListingCondition.New,
            PublishedAtUtc = DateTime.UtcNow
        };
        var chat = new Chat
        {
            Id = chatId,
            ListingId = listingId,
            BuyerId = buyerId,
            SellerId = sellerId,
            CreatedAtUtc = DateTime.UtcNow,
            LastMessageAtUtc = DateTime.UtcNow
        };

        db.AddRange(category, seller, buyer, intruder, listing, chat);
        if (includeOrder)
        {
            db.Orders.Add(new Order
            {
                Id = orderId,
                ListingId = listingId,
                BuyerId = buyerId,
                SellerId = sellerId,
                Quantity = 1,
                InventoryReserved = true,
                PaymentMethod = PaymentMethod.CashOnDelivery,
                PaymentState = PaymentState.NotApplicable,
                TotalAmount = 100m,
                ListingTitleSnapshot = listing.Title,
                BuyerFirstName = "Buyer",
                BuyerLastName = "Integration",
                BuyerEmail = buyer.Email!,
                BuyerPhone = "+380501234567",
                DeliveryAddress = "1 Integration Street",
                DeliveryCity = "Kyiv",
                SellerDecisionDeadlineUtc = DateTime.UtcNow.AddDays(4)
            });
        }

        await db.SaveChangesAsync();
        return new SecurityScenario(sellerId, buyerId, intruderId, listingId, chatId, orderId);
    }

    private static User CreateUser(Guid id, string prefix)
    {
        var email = $"{prefix}-{id:N}@example.test";
        var userName = $"{prefix}-{id:N}";
        return new User
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            PhoneNumber = "+380501234567",
            CreatedAt = DateTime.UtcNow
        };
    }

    private static object CreateOrderRequest(Guid listingId) => new
    {
        listingId,
        quantity = 1,
        paymentMethod = "CashOnDelivery",
        buyerFirstName = "Buyer",
        buyerLastName = "Integration",
        buyerEmail = "buyer@example.test",
        buyerPhone = "+380501234567",
        deliveryAddress = "1 Integration Street",
        deliveryCity = "Kyiv"
    };

    private static string CreatePaymentIntentEvent(Guid paymentSessionId, string paymentIntentId)
    {
        var apiVersion = StripeApiConfiguration.ApiVersion;
        return $$"""
            {
              "id": "evt_duplicate_webhook_test",
              "object": "event",
              "api_version": "{{apiVersion}}",
              "created": 1743465600,
              "data": {
                "object": {
                  "id": "{{paymentIntentId}}",
                  "object": "payment_intent",
                  "metadata": { "payment_session_id": "{{paymentSessionId}}" }
                }
              },
              "livemode": false,
              "pending_webhooks": 1,
              "type": "payment_intent.succeeded"
            }
            """;
    }

    private sealed record SecurityScenario(
        Guid SellerId,
        Guid BuyerId,
        Guid IntruderId,
        Guid ListingId,
        Guid ChatId,
        Guid OrderId);
}
