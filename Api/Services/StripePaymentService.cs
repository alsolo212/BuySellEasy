using Api.Options;
using Domain.Entities;
using Domain.IdentityEntities;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Api.Services;

public sealed class StripePaymentService
{
    private readonly StripeOptions _options;

    public StripePaymentService(IOptions<StripeOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SecretKey);

    public async Task<(string Id, string ClientSecret)> CreatePaymentIntentAsync(
        PaymentSession paymentSession,
        CheckoutPaymentDraft draft,
        User buyer,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(buyer.StripeCustomerId))
        {
            buyer.StripeCustomerId = await CreateCustomerAsync(buyer, cancellationToken);
        }

        var paymentIntent = await new PaymentIntentService().CreateAsync(
            new PaymentIntentCreateOptions
            {
                Amount = ToStripeAmount(draft.Items.Sum(item => item.UnitPrice * item.Quantity)),
                Currency = "usd",
                Customer = buyer.StripeCustomerId,
                ReceiptEmail = buyer.Email,
                PaymentMethodTypes = new List<string> { "card" },
                SetupFutureUsage = draft.SaveCard ? "off_session" : null,
                Description = "Buy Sell Easy order",
                Metadata = new Dictionary<string, string>
                {
                    ["payment_session_id"] = paymentSession.Id.ToString()
                }
            },
            new RequestOptions
            {
                IdempotencyKey = $"bse-payment-intent-{paymentSession.Id}"
            },
            cancellationToken);

        return (paymentIntent.Id, paymentIntent.ClientSecret!);
    }

    public async Task<(string Id, string Url)> CreateCheckoutSessionAsync(
        PaymentSession paymentSession,
        CheckoutPaymentDraft draft,
        User buyer,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(buyer.StripeCustomerId))
        {
            buyer.StripeCustomerId = await CreateCustomerAsync(buyer, cancellationToken);
        }

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            Customer = buyer.StripeCustomerId,
            CustomerUpdate = new SessionCustomerUpdateOptions
            {
                Name = "auto",
                Address = "auto"
            },
            SuccessUrl = $"{_options.FrontendBaseUrl.TrimEnd('/')}/profile?tab=orders&payment=success",
            CancelUrl = draft.CancelUrl,
            ClientReferenceId = paymentSession.Id.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["payment_session_id"] = paymentSession.Id.ToString()
            },
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = draft.Items.Select(item => new SessionLineItemOptions
            {
                Quantity = item.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "usd",
                    UnitAmount = ToStripeAmount(item.UnitPrice),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = item.Title
                    }
                }
            }).ToList(),
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                ReceiptEmail = buyer.Email,
                SetupFutureUsage = draft.SaveCard ? "off_session" : null,
                Metadata = new Dictionary<string, string>
                {
                    ["payment_session_id"] = paymentSession.Id.ToString()
                }
            }
        };

        var session = await new SessionService().CreateAsync(options, cancellationToken: cancellationToken);
        return (session.Id, session.Url);
    }

    public async Task<long> GetPaymentFeeInCentsAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var paymentIntent = await new PaymentIntentService().GetAsync(
            paymentIntentId,
            new PaymentIntentGetOptions
            {
                Expand = new List<string> { "latest_charge.balance_transaction" }
            },
            cancellationToken: cancellationToken);

        if (paymentIntent.LatestCharge is Charge charge && charge.BalanceTransaction is BalanceTransaction balanceTransaction)
        {
            return balanceTransaction.Fee;
        }

        var chargeId = paymentIntent.LatestChargeId;
        if (string.IsNullOrWhiteSpace(chargeId))
        {
            return 0;
        }

        var chargeResult = await new ChargeService().GetAsync(
            chargeId,
            new ChargeGetOptions { Expand = new List<string> { "balance_transaction" } },
            cancellationToken: cancellationToken);

        return chargeResult.BalanceTransaction is BalanceTransaction chargeBalanceTransaction
            ? chargeBalanceTransaction.Fee
            : 0;
    }

    public async Task KeepOnlyLatestSavedCardAsync(
        User buyer,
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(buyer.StripeCustomerId))
        {
            return;
        }

        EnsureConfigured();
        var paymentIntent = await new PaymentIntentService().GetAsync(paymentIntentId, cancellationToken: cancellationToken);
        var latestPaymentMethodId = paymentIntent.PaymentMethodId;
        if (string.IsNullOrWhiteSpace(latestPaymentMethodId))
        {
            return;
        }

        var paymentMethods = await new PaymentMethodService().ListAsync(
            new PaymentMethodListOptions
            {
                Customer = buyer.StripeCustomerId,
                Type = "card"
            },
            cancellationToken: cancellationToken);

        foreach (var paymentMethod in paymentMethods.Data.Where(item => item.Id != latestPaymentMethodId))
        {
            await new PaymentMethodService().DetachAsync(paymentMethod.Id, cancellationToken: cancellationToken);
        }
    }

    public async Task<string> CreateTransferAsync(
        Order order,
        string connectedAccountId,
        long amountInCents,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var transfer = await new TransferService().CreateAsync(
            new TransferCreateOptions
            {
                Amount = amountInCents,
                Currency = "usd",
                Destination = connectedAccountId,
                Metadata = new Dictionary<string, string>
                {
                    ["order_id"] = order.Id.ToString()
                }
            },
            new RequestOptions { IdempotencyKey = $"bse-transfer-{order.Id}" },
            cancellationToken);

        return transfer.Id;
    }

    public async Task<string> CreateRefundAsync(
        Order order,
        long amountInCents,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (string.IsNullOrWhiteSpace(order.StripePaymentIntentId))
        {
            throw new InvalidOperationException("The order has no Stripe payment intent.");
        }

        var refund = await new RefundService().CreateAsync(
            new RefundCreateOptions
            {
                PaymentIntent = order.StripePaymentIntentId,
                Amount = amountInCents,
                Metadata = new Dictionary<string, string>
                {
                    ["order_id"] = order.Id.ToString()
                }
            },
            new RequestOptions { IdempotencyKey = $"bse-refund-{order.Id}" },
            cancellationToken);

        return refund.Id;
    }

    public async Task<string> EnsureConnectAccountAsync(User seller, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (!string.IsNullOrWhiteSpace(seller.StripeConnectAccountId))
        {
            return seller.StripeConnectAccountId;
        }

        // Stripe has deprecated Accounts v1 creation for new Connect integrations.
        // BSE collects the payment on the platform and later creates a transfer, so
        // sellers are recipient accounts rather than merchants of record.
        var account = await new StripeClient(_options.SecretKey).V2.Core.Accounts.CreateAsync(
            new Stripe.V2.Core.AccountCreateOptions
            {
                Configuration = new Stripe.V2.Core.AccountCreateConfigurationOptions
                {
                    Recipient = new Stripe.V2.Core.AccountCreateConfigurationRecipientOptions
                    {
                        Capabilities = new Stripe.V2.Core.AccountCreateConfigurationRecipientCapabilitiesOptions
                        {
                            StripeBalance = new Stripe.V2.Core.AccountCreateConfigurationRecipientCapabilitiesStripeBalanceOptions
                            {
                                StripeTransfers = new Stripe.V2.Core.AccountCreateConfigurationRecipientCapabilitiesStripeBalanceStripeTransfersOptions
                                {
                                    Requested = true
                                }
                            }
                        }
                    }
                },
                ContactEmail = seller.Email,
                Dashboard = "express",
                Defaults = new Stripe.V2.Core.AccountCreateDefaultsOptions
                {
                    Currency = "usd",
                    Responsibilities = new Stripe.V2.Core.AccountCreateDefaultsResponsibilitiesOptions
                    {
                        FeesCollector = "application",
                        LossesCollector = "application"
                    }
                },
                DisplayName = seller.Email,
                Identity = new Stripe.V2.Core.AccountCreateIdentityOptions
                {
                    Country = _options.ConnectCountry,
                    EntityType = "individual"
                },
                Metadata = new Dictionary<string, string>
                {
                    ["bse_user_id"] = seller.Id.ToString()
                }
            },
            new RequestOptions
            {
                IdempotencyKey = $"bse-connect-account-{seller.Id}"
            },
            cancellationToken: cancellationToken);

        seller.StripeConnectAccountId = account.Id;
        return account.Id;
    }

    public async Task<(string ClientSecret, bool IsComplete)> CreateConnectOnboardingSessionAsync(
        User seller,
        CancellationToken cancellationToken = default)
    {
        var accountId = await EnsureConnectAccountAsync(seller, cancellationToken);
        var account = await new AccountService().GetAsync(accountId, cancellationToken: cancellationToken);
        var accountSession = await new AccountSessionService().CreateAsync(
            new AccountSessionCreateOptions
            {
                Account = accountId,
                Components = new AccountSessionComponentsOptions
                {
                    AccountOnboarding = new AccountSessionComponentsAccountOnboardingOptions
                    {
                        Enabled = true,
                        Features = new AccountSessionComponentsAccountOnboardingFeaturesOptions
                        {
                            ExternalAccountCollection = true
                        }
                    }
                }
            },
            cancellationToken: cancellationToken);

        // Recipient accounts do not need the card_payments capability.
        return (accountSession.ClientSecret, account.DetailsSubmitted && account.PayoutsEnabled);
    }

    public async Task<bool> CanReceiveTransfersAsync(User seller, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(seller.StripeConnectAccountId))
        {
            return false;
        }

        EnsureConfigured();
        var account = await new AccountService().GetAsync(seller.StripeConnectAccountId, cancellationToken: cancellationToken);
        // The V1 representation is used here because Account Sessions remain a V1 API,
        // while the connected account itself was created through Accounts v2.
        return account.DetailsSubmitted && account.PayoutsEnabled;
    }

    public static long ToStripeAmount(decimal amount)
    {
        return checked((long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero));
    }

    private async Task<string> CreateCustomerAsync(User buyer, CancellationToken cancellationToken)
    {
        var customer = await new CustomerService().CreateAsync(
            new CustomerCreateOptions
            {
                Email = buyer.Email,
                Name = $"{buyer.UserName ?? buyer.Email}",
                Metadata = new Dictionary<string, string>
                {
                    ["bse_user_id"] = buyer.Id.ToString()
                }
            },
            cancellationToken: cancellationToken);

        return customer.Id;
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Stripe is not configured. Add Stripe secrets to .NET User Secrets.");
        }

        StripeConfiguration.ApiKey = _options.SecretKey;
    }
}

public sealed class CheckoutPaymentDraft
{
    public Guid BuyerId { get; init; }

    public string BuyerFirstName { get; init; } = string.Empty;

    public string BuyerLastName { get; init; } = string.Empty;

    public string BuyerEmail { get; init; } = string.Empty;

    public string BuyerPhone { get; init; } = string.Empty;

    public string DeliveryAddress { get; init; } = string.Empty;

    public string DeliveryCity { get; init; } = string.Empty;

    public bool SaveCard { get; init; }

    public string CancelUrl { get; init; } = string.Empty;

    public List<Guid> CartItemIds { get; init; } = new();

    public List<CheckoutPaymentDraftItem> Items { get; init; } = new();
}

public sealed class CheckoutPaymentDraftItem
{
    public Guid ListingId { get; init; }

    public Guid SellerId { get; init; }

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? ImageUrl { get; init; }
}
