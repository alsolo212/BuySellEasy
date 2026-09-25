using Api.Contracts.Cart;
using Api.Extensions;
using Api.Mappers;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly ProductDbContext _dbContext;

    public CartController(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<CartResponse>> GetCart()
    {
        var userId = User.GetRequiredUserId();
        var items = await LoadCartItemsQuery(userId).ToListAsync();

        return Ok(new CartResponse
        {
            Items = items.Select(MarketplaceMapper.MapCartItem).ToArray(),
            SelectedTotalAmount = items.Where(item => item.IsSelected).Sum(item => item.Listing!.Price * item.Quantity)
        });
    }

    [HttpPost]
    public async Task<ActionResult<CartResponse>> AddToCart([FromBody] AddCartItemRequest request)
    {
        var userId = User.GetRequiredUserId();
        var listing = await LoadListingAsync(request.ListingId);
        EnsureCanUseListing(listing, userId);

        var existing = await _dbContext.CartItems
            .FirstOrDefaultAsync(item => item.UserId == userId && item.ListingId == request.ListingId);

        if (existing is null)
        {
            _dbContext.CartItems.Add(new CartItem
            {
                UserId = userId,
                ListingId = request.ListingId,
                Quantity = Math.Min(request.Quantity, listing.Quantity),
                IsSelected = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.IsSelected = true;
            existing.Quantity = Math.Min(Math.Max(request.Quantity, 1), listing.Quantity);
        }

        await _dbContext.SaveChangesAsync();
        return await GetCart();
    }

    [HttpPatch("{itemId:guid}")]
    public async Task<ActionResult<CartResponse>> UpdateCartItem(Guid itemId, [FromBody] UpdateCartItemRequest request)
    {
        var userId = User.GetRequiredUserId();
        var cartItem = await _dbContext.CartItems
            .Include(item => item.Listing)
            .FirstOrDefaultAsync(item => item.Id == itemId && item.UserId == userId)
            ?? throw new KeyNotFoundException("Cart item was not found.");

        cartItem.IsSelected = request.IsSelected;
        if (request.Quantity.HasValue)
        {
            var availableQuantity = cartItem.Listing?.Quantity ?? 1;
            cartItem.Quantity = Math.Min(Math.Max(request.Quantity.Value, 1), Math.Max(availableQuantity, 1));
        }
        await _dbContext.SaveChangesAsync();

        return await GetCart();
    }

    [HttpDelete("{itemId:guid}")]
    public async Task<ActionResult<CartResponse>> RemoveCartItem(Guid itemId)
    {
        var userId = User.GetRequiredUserId();
        var cartItem = await _dbContext.CartItems
            .FirstOrDefaultAsync(item => item.Id == itemId && item.UserId == userId)
            ?? throw new KeyNotFoundException("Cart item was not found.");

        _dbContext.CartItems.Remove(cartItem);
        await _dbContext.SaveChangesAsync();

        return await GetCart();
    }

    private IQueryable<CartItem> LoadCartItemsQuery(Guid userId)
    {
        return _dbContext.CartItems
            .AsNoTracking()
            .Include(item => item.Listing!)
                .ThenInclude(listing => listing.Images)
            .Include(item => item.Listing!)
                .ThenInclude(listing => listing.Category)
            .Include(item => item.Listing!)
                .ThenInclude(listing => listing.Owner)
            .Where(item => item.UserId == userId && item.ListingId != null)
            .OrderByDescending(item => item.CreatedAtUtc);
    }

    private async Task<Listing> LoadListingAsync(Guid listingId)
    {
        return await _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Owner)
            .FirstOrDefaultAsync(listing => listing.Id == listingId)
            ?? throw new KeyNotFoundException("Listing was not found.");
    }

    private void EnsureCanUseListing(Listing listing, Guid currentUserId)
    {
        if (listing.OwnerId == currentUserId)
        {
            throw new InvalidOperationException("You cannot add your own listing to the cart.");
        }

        if (listing.Quantity < 1)
        {
            throw new InvalidOperationException("This listing is out of stock.");
        }

        if (listing.Status is ListingStatus.Sold or ListingStatus.InDelivery or ListingStatus.Arrived or ListingStatus.Archived or ListingStatus.Inactive or ListingStatus.Blocked)
        {
            throw new InvalidOperationException("This listing is not available for purchase.");
        }
    }
}
