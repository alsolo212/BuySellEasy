using Api.Contracts.Reviews;
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
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly ProductDbContext _dbContext;

    public ReviewsController(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<ReviewResponse>>> GetUserReviews(Guid userId)
    {
        var reviews = await _dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.Order)
            .Include(review => review.Listing)
            .Include(review => review.Author)
            .Include(review => review.TargetUser)
            .Where(review =>
                review.TargetUserId == userId &&
                review.Status != ReviewStatus.Removed &&
                review.Status != ReviewStatus.Hidden)
            .OrderByDescending(review => review.CreatedAtUtc)
            .ToListAsync();

        return Ok(reviews.Select(MarketplaceMapper.MapReview).ToArray());
    }

    [HttpGet("listings/{listingId:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<ReviewResponse>>> GetListingReviews(Guid listingId)
    {
        var reviews = await _dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.Order)
            .Include(review => review.Listing)
            .Include(review => review.Author)
            .Include(review => review.TargetUser)
            .Where(review =>
                review.ListingId == listingId &&
                review.Status != ReviewStatus.Removed &&
                review.Status != ReviewStatus.Hidden)
            .OrderByDescending(review => review.CreatedAtUtc)
            .ToListAsync();

        return Ok(reviews.Select(MarketplaceMapper.MapReview).ToArray());
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ReviewResponse>> CreateReview([FromBody] CreateReviewRequest request)
    {
        var authorId = User.GetRequiredUserId();
        var order = await _dbContext.Orders
            .Include(item => item.Listing)
            .FirstOrDefaultAsync(item => item.Id == request.OrderId)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.Status != OrderStatus.Delivered)
        {
            throw new InvalidOperationException("Reviews can be created only for delivered orders.");
        }

        if (order.BuyerId != authorId && order.SellerId != authorId)
        {
            throw new UnauthorizedAccessException("You cannot review this order.");
        }

        var targetUserId = request.TargetUserId ?? (order.BuyerId == authorId ? order.SellerId : order.BuyerId);

        var alreadyExists = await _dbContext.Reviews.AnyAsync(review =>
            review.OrderId == order.Id &&
            review.AuthorId == authorId &&
            review.TargetUserId == targetUserId);

        if (alreadyExists)
        {
            throw new InvalidOperationException("You have already submitted a review for this order.");
        }

        var review = new Review
        {
            OrderId = order.Id,
            ListingId = order.ListingId,
            AuthorId = authorId,
            TargetUserId = targetUserId,
            Rating = request.Rating,
            Comment = request.Comment.Trim(),
            Status = ReviewStatus.Published,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync();

        var created = await LoadReviewAsync(review.Id);
        return Ok(MarketplaceMapper.MapReview(created));
    }

    [Authorize]
    [HttpPost("{id:guid}/dispute")]
    public async Task<ActionResult<ReviewResponse>> DisputeReview(Guid id, [FromBody] DisputeReviewRequest request)
    {
        var userId = User.GetRequiredUserId();
        var review = await _dbContext.Reviews.FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Review was not found.");

        if (review.TargetUserId != userId && !User.IsElevatedAdmin())
        {
            throw new UnauthorizedAccessException("You cannot dispute this review.");
        }

        review.Status = ReviewStatus.Disputed;
        review.DisputeReason = request.Reason.Trim();
        await _dbContext.SaveChangesAsync();

        var updated = await LoadReviewAsync(review.Id);
        return Ok(MarketplaceMapper.MapReview(updated));
    }

    [Authorize(Roles = $"{IdentitySeed.Admin},{IdentitySeed.SuperAdmin}")]
    [HttpGet("admin/disputes")]
    public async Task<ActionResult<IReadOnlyCollection<ReviewResponse>>> GetDisputedReviews()
    {
        var reviews = await _dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.Order)
            .Include(review => review.Listing)
            .Include(review => review.Author)
            .Include(review => review.TargetUser)
            .Where(review => review.Status == ReviewStatus.Disputed)
            .OrderByDescending(review => review.CreatedAtUtc)
            .ToListAsync();

        return Ok(reviews.Select(MarketplaceMapper.MapReview).ToArray());
    }

    [Authorize(Roles = $"{IdentitySeed.Admin},{IdentitySeed.SuperAdmin}")]
    [HttpPatch("admin/{id:guid}")]
    public async Task<ActionResult<ReviewResponse>> ModerateReview(Guid id, [FromBody] UpdateReviewModerationRequest request)
    {
        if (request.Status is not ReviewStatus.Published and not ReviewStatus.Hidden and not ReviewStatus.Removed)
        {
            throw new InvalidOperationException("Admin can set review only to Published, Hidden, or Removed.");
        }

        var review = await _dbContext.Reviews.FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Review was not found.");

        review.Status = request.Status;
        if (request.Status != ReviewStatus.Disputed)
        {
            review.DisputeReason = request.Status == ReviewStatus.Published
                ? null
                : review.DisputeReason;
        }

        await _dbContext.SaveChangesAsync();

        var updated = await LoadReviewAsync(review.Id);
        return Ok(MarketplaceMapper.MapReview(updated));
    }

    private async Task<Review> LoadReviewAsync(Guid id)
    {
        return await _dbContext.Reviews
            .AsNoTracking()
            .Include(review => review.Order)
            .Include(review => review.Listing)
            .Include(review => review.Author)
            .Include(review => review.TargetUser)
            .FirstOrDefaultAsync(review => review.Id == id)
            ?? throw new KeyNotFoundException("Review was not found.");
    }
}
