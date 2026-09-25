using Api.Contracts.Listings;
using Api.Mappers;
using Domain.Enums;
using Infrastructure.DbContextt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        var featuredListings = await _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Category)
            .Include(listing => listing.Owner)
            .Include(listing => listing.Images)
            .Where(listing => listing.Status == ListingStatus.Published || listing.Status == ListingStatus.Active)
            .OrderBy(_ => Guid.NewGuid())
            .Take(10)
            .ToListAsync();

        return Ok(new CatalogHomeResponse
        {
            Categories = categories.Select(MarketplaceMapper.MapCategory).ToArray(),
            FeaturedListings = featuredListings.Select(MarketplaceMapper.MapListingSummary).ToArray()
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
        [FromQuery] string? sortBy)
    {
        var query = _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Category)
            .Include(listing => listing.Owner)
            .Include(listing => listing.Images)
            .Where(listing => listing.Status == ListingStatus.Published || listing.Status == ListingStatus.Active);

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

        var listings = await query.ToListAsync();
        return Ok(listings.Select(MarketplaceMapper.MapListingSummary).ToArray());
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

        return Ok(MarketplaceMapper.MapListingDetails(listing));
    }
}
