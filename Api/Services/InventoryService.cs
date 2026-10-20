using Domain.Enums;
using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public sealed class InventoryService
{
    private readonly ProductDbContext _dbContext;

    public InventoryService(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static int GetAvailableQuantity(int quantity, int reservedQuantity)
    {
        return Math.Max(0, quantity - reservedQuantity);
    }

    public async Task<bool> TryReserveAsync(
        Guid listingId,
        int quantity,
        decimal? expectedPrice = null,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 1)
        {
            return false;
        }

        var query = _dbContext.Listings
            .Where(listing =>
                listing.Id == listingId &&
                (listing.Status == ListingStatus.Active || listing.Status == ListingStatus.Published) &&
                listing.Quantity - listing.ReservedQuantity >= quantity)
            .AsQueryable();

        if (expectedPrice.HasValue)
        {
            query = query.Where(listing => listing.Price == expectedPrice.Value);
        }

        var affectedRows = await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(listing => listing.ReservedQuantity,
                    listing => listing.ReservedQuantity + quantity)
                .SetProperty(listing => listing.UpdatedAtUtc, DateTime.UtcNow), cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> TryReleaseAsync(
        Guid listingId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 1)
        {
            return true;
        }

        var affectedRows = await _dbContext.Listings
            .Where(listing =>
                listing.Id == listingId &&
                listing.ReservedQuantity >= quantity)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(listing => listing.ReservedQuantity,
                    listing => listing.ReservedQuantity - quantity)
                .SetProperty(listing => listing.UpdatedAtUtc, DateTime.UtcNow), cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> TryCompleteSaleAsync(
        Guid listingId,
        int quantity,
        bool reservationWasHeld,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 1)
        {
            return false;
        }

        var query = _dbContext.Listings.Where(listing =>
            listing.Id == listingId &&
            listing.Quantity >= quantity);

        if (reservationWasHeld)
        {
            query = query.Where(listing => listing.ReservedQuantity >= quantity);
        }

        var affectedRows = reservationWasHeld
            ? await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(listing => listing.Quantity,
                    listing => listing.Quantity - quantity)
                .SetProperty(listing => listing.ReservedQuantity,
                    listing => listing.ReservedQuantity - quantity)
                .SetProperty(listing => listing.UpdatedAtUtc, DateTime.UtcNow)
                .SetProperty(listing => listing.Status,
                    listing => listing.Quantity - quantity <= 0
                        ? ListingStatus.Inactive
                        : ListingStatus.Published), cancellationToken)
            : await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(listing => listing.Quantity,
                    listing => listing.Quantity - quantity)
                .SetProperty(listing => listing.UpdatedAtUtc, DateTime.UtcNow)
                .SetProperty(listing => listing.Status,
                    listing => listing.Quantity - quantity <= 0
                        ? ListingStatus.Inactive
                        : ListingStatus.Published), cancellationToken);

        return affectedRows == 1;
    }
}
