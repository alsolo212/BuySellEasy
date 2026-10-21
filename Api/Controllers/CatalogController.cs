using Api.Contracts.Listings;
using Api.Extensions;
using Api.Mappers;
using Domain.Enums;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly ProductDbContext _dbContext;

    public CatalogController(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("home")]
    public async Task<ActionResult<CatalogHomeResponse>> GetHome()
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ToListAsync();

        var featuredListingIds = await _dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == ListingStatus.Published || listing.Status == ListingStatus.Active)
            .OrderBy(_ => Guid.NewGuid())
            .Take(10)
            .Select(listing => listing.Id)
            .ToListAsync();

        var featuredListings = await _dbContext.Listings
            .AsNoTracking()
            .Where(listing => featuredListingIds.Contains(listing.Id))
            .Select(MarketplaceMapper.ListingSummaryProjection)
            .ToListAsync();

        var featuredListingOrder = featuredListingIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        featuredListings = featuredListings
            .OrderBy(listing => featuredListingOrder[listing.Id])
            .ToList();

        return Ok(new CatalogHomeResponse
        {
            Categories = categories.Select(MarketplaceMapper.MapCategory).ToArray(),
            FeaturedListings = featuredListings
        });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ToListAsync();

        return Ok(categories.Select(MarketplaceMapper.MapCategory).ToArray());
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? searchTerm,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? city,
        [FromQuery] ListingCondition? condition,
        [FromQuery] string? sortBy,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationExtensions.DefaultPageSize)
    {
        var query = _dbContext.Listings
            .AsNoTracking()
            .Where(listing => listing.Status == ListingStatus.Published || listing.Status == ListingStatus.Active);

        (page, pageSize) = PaginationExtensions.Normalize(page, pageSize);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalized = searchTerm.Trim().ToLower();
            query = query.Where(listing =>
                listing.Title.ToLower().Contains(normalized) ||
                listing.Description.ToLower().Contains(normalized) ||
                listing.City.ToLower().Contains(normalized));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(listing => listing.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var normalizedCity = city.Trim().ToLower();
            query = query.Where(listing => listing.City.ToLower() == normalizedCity);
        }

        if (condition.HasValue)
        {
            query = query.Where(listing => listing.Condition == condition);
        }

        query = sortBy?.ToLowerInvariant() switch
        {
            "cheapest" => query.OrderBy(listing => listing.Price),
            "expensive" => query.OrderByDescending(listing => listing.Price),
            _ => query.OrderByDescending(listing => listing.CreatedAtUtc)
        };

        var totalCount = await query.CountAsync();
        var listings = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MarketplaceMapper.ListingSummaryProjection)
            .ToArrayAsync();

        Response.AddPaginationHeaders(page, pageSize, totalCount);
        return Ok(listings);
    }

    [HttpGet("products/{id:guid}")]
    public async Task<IActionResult> GetProductById(Guid id)
    {
        var listing = await _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Category)
            .Include(listing => listing.Owner)
            .Include(listing => listing.Images)
            .FirstOrDefaultAsync(listing => listing.Id == id);

        if (listing is null)
        {
            throw new KeyNotFoundException("Listing was not found.");
        }

        if (!CanViewHiddenListing(listing))
        {
            throw new KeyNotFoundException("Listing was not found.");
        }

        return Ok(MarketplaceMapper.MapListingDetails(listing));
    }

    private bool CanViewHiddenListing(Domain.Entities.Listing listing)
    {
        if (listing.Status is not (ListingStatus.Draft or ListingStatus.Blocked or ListingStatus.Inactive or ListingStatus.Archived))
        {
            return true;
        }

        if (User.IsInRole(Infrastructure.DbContextt.IdentitySeed.Admin) ||
            User.IsInRole(Infrastructure.DbContextt.IdentitySeed.SuperAdmin))
        {
            return true;
        }

        return User.Identity?.IsAuthenticated == true &&
               Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId) &&
               listing.OwnerId == currentUserId;
    }
}
