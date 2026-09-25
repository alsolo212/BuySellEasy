using Api.Contracts.Listings;
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
[Route("api/listings")]
public class ListingsController : ControllerBase
{
    private const int MaxListingImagesCount = 8;
    private const long MaxListingImageSizeBytes = 8 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly ProductDbContext _dbContext;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ListingsController(ProductDbContext dbContext, IWebHostEnvironment webHostEnvironment)
    {
        _dbContext = dbContext;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ListingSummaryResponse>>> GetListings(
        [FromQuery] string? searchTerm,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? city,
        [FromQuery] ListingCondition? condition,
        [FromQuery] ListingStatus? status,
        [FromQuery] Guid? ownerId,
        [FromQuery] string? sortBy,
        [FromQuery] bool includeHidden = false)
    {
        var currentUserId = User.Identity?.IsAuthenticated == true ? User.GetRequiredUserId() : (Guid?)null;
        var canSeeHidden = includeHidden && User.IsElevatedAdmin();
        var query = _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Category)
            .Include(listing => listing.Owner)
            .Include(listing => listing.Images)
            .Include(listing => listing.Orders)
            .AsQueryable();

        if (!canSeeHidden)
        {
            query = query.Where(listing =>
                listing.Status != ListingStatus.Inactive &&
                listing.Status != ListingStatus.Blocked &&
                listing.Status != ListingStatus.Archived &&
                listing.Status != ListingStatus.Draft);
        }

        if (ownerId.HasValue)
        {
            query = query.Where(listing => listing.OwnerId == ownerId.Value);
        }

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
            query = query.Where(listing => listing.Condition == condition.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(listing => listing.Status == status.Value);
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

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyCollection<ListingSummaryResponse>>> GetMyListings()
    {
        var userId = User.GetRequiredUserId();
        var listings = await _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Category)
            .Include(listing => listing.Owner)
            .Include(listing => listing.Images)
            .Include(listing => listing.Orders)
            .Where(listing => listing.OwnerId == userId && listing.Status != ListingStatus.Inactive)
            .OrderByDescending(listing => listing.CreatedAtUtc)
            .ToListAsync();

        return Ok(listings.Select(MarketplaceMapper.MapListingSummary).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ListingDetailsResponse>> GetListingById(Guid id)
    {
        var currentUserId = User.Identity?.IsAuthenticated == true ? User.GetRequiredUserId() : (Guid?)null;
        var listing = await _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Category)
            .Include(listing => listing.Owner)
            .Include(listing => listing.Images)
            .Include(listing => listing.Orders)
            .FirstOrDefaultAsync(listing => listing.Id == id)
            ?? throw new KeyNotFoundException("Listing was not found.");

        var canAccessHiddenListing =
            listing.OwnerId == currentUserId ||
            User.IsElevatedAdmin();
        if (!canAccessHiddenListing &&
            listing.Status is ListingStatus.Inactive or ListingStatus.Blocked or ListingStatus.Archived or ListingStatus.Draft)
        {
            throw new KeyNotFoundException("Listing was not found.");
        }

        return Ok(MarketplaceMapper.MapListingDetails(listing));
    }

    [Authorize]
    [HttpPost("{id:guid}/report")]
    public async Task<IActionResult> ReportListing(Guid id, [FromBody] CreateListingReportRequest request)
    {
        var userId = User.GetRequiredUserId();
        var listing = await _dbContext.Listings.FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Listing was not found.");

        if (listing.OwnerId == userId)
        {
            throw new InvalidOperationException("You cannot report your own listing.");
        }

        var alreadyReported = await _dbContext.ListingReports.AnyAsync(report =>
            report.ListingId == id &&
            report.ReporterId == userId &&
            report.Status == Domain.Enums.ListingReportStatus.Pending);

        if (alreadyReported)
        {
            throw new InvalidOperationException("You have already reported this listing.");
        }

        _dbContext.ListingReports.Add(new ListingReport
        {
            ListingId = id,
            ReporterId = userId,
            Reason = request.Reason.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ListingDetailsResponse>> CreateListing([FromBody] CreateListingRequest request)
    {
        var userId = User.GetRequiredUserId();
        var categoryExists = await _dbContext.Categories.AnyAsync(category => category.Id == request.CategoryId);
        if (!categoryExists)
        {
            throw new KeyNotFoundException("Category was not found.");
        }

        var listing = new Listing
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Price = request.Price,
            Quantity = request.Quantity,
            IsNegotiable = request.IsNegotiable,
            City = request.City.Trim(),
            CategoryId = request.CategoryId,
            Condition = request.Condition,
            Status = request.Status == ListingStatus.Draft ? ListingStatus.Draft : ListingStatus.Published,
            OwnerId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            PublishedAtUtc = request.Status == ListingStatus.Draft ? null : DateTime.UtcNow,
            Images = request.ImageUrls
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select((url, index) => new ListingImage
                {
                    Url = url.Trim(),
                    SortOrder = index
                })
                .ToList()
        };

        _dbContext.Listings.Add(listing);
        await _dbContext.SaveChangesAsync();
        await UpdateOwnerListingCountAsync(userId);

        var created = await LoadListingAsync(listing.Id);
        return CreatedAtAction(nameof(GetListingById), new { id = listing.Id }, MarketplaceMapper.MapListingDetails(created));
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ListingDetailsResponse>> UpdateListing(Guid id, [FromBody] UpdateListingRequest request)
    {
        var userId = User.GetRequiredUserId();
        var categoryExists = await _dbContext.Categories.AnyAsync(category => category.Id == request.CategoryId);
        if (!categoryExists)
        {
            throw new KeyNotFoundException("Category was not found.");
        }

        var listing = await _dbContext.Listings
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Listing was not found.");

        EnsureCanManageListing(listing, userId);
        await EnsureOwnerCanEditListingContentAsync(listing, userId);
        var previousManagedImageUrls = listing.Images
            .Select(image => image.Url)
            .Where(IsManagedListingImageUrl)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        listing.Title = request.Title.Trim();
        listing.Description = request.Description.Trim();
        listing.Price = request.Price;
        listing.Quantity = request.Quantity;
        listing.IsNegotiable = request.IsNegotiable;
        listing.City = request.City.Trim();
        listing.CategoryId = request.CategoryId;
        listing.Condition = request.Condition;
        listing.UpdatedAtUtc = DateTime.UtcNow;

        var requestedImageUrls = request.ImageUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .ToList();

        var requestedImageSet = requestedImageUrls.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingImages = listing.Images.ToList();
        var imagesToRemove = existingImages
            .Where(image => !requestedImageSet.Contains(image.Url))
            .ToList();

        _dbContext.ListingImages.RemoveRange(imagesToRemove);

        var existingImagesByUrl = existingImages
            .Except(imagesToRemove)
            .ToDictionary(image => image.Url, StringComparer.OrdinalIgnoreCase);

        var imagesToAdd = new List<ListingImage>();

        for (var index = 0; index < requestedImageUrls.Count; index++)
        {
            var imageUrl = requestedImageUrls[index];
            if (existingImagesByUrl.TryGetValue(imageUrl, out var existingImage))
            {
                existingImage.SortOrder = index;
                continue;
            }

            imagesToAdd.Add(new ListingImage
            {
                ListingId = listing.Id,
                Url = imageUrl,
                SortOrder = index
            });
        }

        if (imagesToAdd.Count > 0)
        {
            _dbContext.ListingImages.AddRange(imagesToAdd);
        }

        await _dbContext.SaveChangesAsync();
        DeleteManagedListingFiles(previousManagedImageUrls.Except(requestedImageUrls, StringComparer.OrdinalIgnoreCase));

        var updated = await LoadListingAsync(listing.Id);
        return Ok(MarketplaceMapper.MapListingDetails(updated));
    }

    [Authorize]
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ListingDetailsResponse>> UpdateStatus(Guid id, [FromBody] UpdateListingStatusRequest request)
    {
        var userId = User.GetRequiredUserId();
        var listing = await _dbContext.Listings.FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Listing was not found.");

        EnsureCanManageListing(listing, userId);
        if (!User.IsElevatedAdmin() && request.Status == ListingStatus.Blocked)
        {
            throw new UnauthorizedAccessException("Only admins can block listings.");
        }

        if (!User.IsElevatedAdmin() && listing.Status == ListingStatus.Blocked)
        {
            throw new UnauthorizedAccessException("Blocked listings can only be changed by admins.");
        }

        listing.Status = request.Status;
        if (request.Status == ListingStatus.Published && listing.PublishedAtUtc is null)
        {
            listing.PublishedAtUtc = DateTime.UtcNow;
        }
        listing.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        await UpdateOwnerListingCountAsync(listing.OwnerId);

        var updated = await LoadListingAsync(listing.Id);
        return Ok(MarketplaceMapper.MapListingDetails(updated));
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteListing(Guid id)
    {
        var userId = User.GetRequiredUserId();
        var listing = await _dbContext.Listings
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Listing was not found.");

        EnsureCanManageListing(listing, userId);
        var managedImageUrls = listing.Images
            .Select(image => image.Url)
            .Where(IsManagedListingImageUrl)
            .ToArray();

        var hasAnyOrders = await _dbContext.Orders.AnyAsync(order => order.ListingId == listing.Id);
        if (hasAnyOrders)
        {
            listing.Status = ListingStatus.Inactive;
            listing.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            await UpdateOwnerListingCountAsync(listing.OwnerId);
            return NoContent();
        }

        _dbContext.Listings.Remove(listing);
        await _dbContext.SaveChangesAsync();
        await UpdateOwnerListingCountAsync(listing.OwnerId);
        DeleteManagedListingFiles(managedImageUrls);

        return NoContent();
    }

    [Authorize]
    [HttpPost("images")]
    public async Task<ActionResult<ListingImageUploadResponse>> UploadImages([FromForm] List<IFormFile>? files)
    {
        if (files is null || files.Count == 0)
        {
            return ValidationProblem(CreateValidationProblem("files", "At least one image is required."));
        }

        if (files.Count > MaxListingImagesCount)
        {
            return ValidationProblem(CreateValidationProblem("files", $"You can upload up to {MaxListingImagesCount} images."));
        }

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                return ValidationProblem(CreateValidationProblem("files", "One of the selected files is empty."));
            }

            if (file.Length > MaxListingImageSizeBytes)
            {
                return ValidationProblem(CreateValidationProblem("files", "Each image must be smaller than 8 MB."));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(extension))
            {
                return ValidationProblem(CreateValidationProblem("files", "Only JPG, PNG, and WEBP images are supported."));
            }
        }

        var uploadsDirectory = Path.Combine(ResolveWebRootPath(), "uploads", "listings");
        Directory.CreateDirectory(uploadsDirectory);

        var uploadedImages = new List<ListingImageUploadItemResponse>();
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsDirectory, fileName);

            await using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            uploadedImages.Add(new ListingImageUploadItemResponse
            {
                FileName = fileName,
                Url = $"/uploads/listings/{fileName}"
            });
        }

        return Ok(new ListingImageUploadResponse
        {
            Images = uploadedImages
        });
    }

    private async Task<Listing> LoadListingAsync(Guid id)
    {
        return await _dbContext.Listings
            .AsNoTracking()
            .Include(listing => listing.Category)
            .Include(listing => listing.Owner)
            .Include(listing => listing.Images)
            .Include(listing => listing.Orders)
            .FirstOrDefaultAsync(listing => listing.Id == id)
            ?? throw new KeyNotFoundException("Listing was not found.");
    }

    private async Task UpdateOwnerListingCountAsync(Guid ownerId)
    {
        var activeListingsCount = await _dbContext.Listings.CountAsync(listing =>
            listing.OwnerId == ownerId &&
            (listing.Status == ListingStatus.Published || listing.Status == ListingStatus.Active));

        var owner = await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == ownerId);
        if (owner is null)
        {
            return;
        }

        owner.ActiveListingsCount = activeListingsCount;
        await _dbContext.SaveChangesAsync();
    }

    private void EnsureCanManageListing(Listing listing, Guid currentUserId)
    {
        if (listing.OwnerId != currentUserId && !User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("You do not have access to manage this listing.");
        }
    }

    private async Task EnsureOwnerCanEditListingContentAsync(Listing listing, Guid currentUserId)
    {
        if (listing.OwnerId != currentUserId)
        {
            return;
        }

        if (listing.Status == ListingStatus.Blocked)
        {
            throw new UnauthorizedAccessException("Blocked listings can only be deleted or left unchanged.");
        }

        var hasActiveOrders = await _dbContext.Orders.AnyAsync(order =>
            order.ListingId == listing.Id &&
            order.Status != OrderStatus.Declined &&
            order.Status != OrderStatus.Cancelled &&
            order.Status != OrderStatus.Delivered);

        if (hasActiveOrders)
        {
            throw new UnauthorizedAccessException("Listings with active orders cannot be edited right now.");
        }
    }

    private static ValidationProblemDetails CreateValidationProblem(string key, string message)
    {
        return new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [key] = [message]
        });
    }

    private static bool IsManagedListingImageUrl(string imageUrl)
    {
        return imageUrl.StartsWith("/uploads/listings/", StringComparison.OrdinalIgnoreCase);
    }

    private void DeleteManagedListingFiles(IEnumerable<string> imageUrls)
    {
        var webRootPath = ResolveWebRootPath();
        foreach (var imageUrl in imageUrls.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!IsManagedListingImageUrl(imageUrl))
            {
                continue;
            }

            var fileName = Path.GetFileName(imageUrl);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var filePath = Path.Combine(webRootPath, "uploads", "listings", fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }

    private string ResolveWebRootPath()
    {
        return string.IsNullOrWhiteSpace(_webHostEnvironment.WebRootPath)
            ? Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot")
            : _webHostEnvironment.WebRootPath;
    }
}
