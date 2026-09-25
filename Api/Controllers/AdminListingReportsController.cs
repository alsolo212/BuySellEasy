using Api.Contracts.Admin;
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
[Authorize(Roles = $"{IdentitySeed.Admin},{IdentitySeed.SuperAdmin}")]
[Route("api/admin/listing-reports")]
public class AdminListingReportsController : ControllerBase
{
    private readonly ProductDbContext _dbContext;

    public AdminListingReportsController(ProductDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ListingReportResponse>>> GetReports()
    {
        var reports = await _dbContext.ListingReports
            .AsNoTracking()
            .Include(report => report.Listing)
                .ThenInclude(listing => listing.Images)
            .Include(report => report.Listing)
                .ThenInclude(listing => listing.Owner)
            .Include(report => report.Reporter)
            .Where(report => report.Status == ListingReportStatus.Pending)
            .OrderByDescending(report => report.CreatedAtUtc)
            .ToListAsync();

        return Ok(reports.Select(MarketplaceMapper.MapListingReport).ToArray());
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ListingReportResponse>> Moderate(Guid id, [FromBody] UpdateListingReportRequest request)
    {
        var report = await _dbContext.ListingReports
            .Include(item => item.Listing)
                .ThenInclude(listing => listing.Images)
            .Include(item => item.Listing)
                .ThenInclude(listing => listing.Owner)
            .Include(item => item.Reporter)
            .FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Listing report was not found.");

        report.Status = request.BlockListing ? ListingReportStatus.Blocked : ListingReportStatus.Dismissed;
        report.ResolvedAtUtc = DateTime.UtcNow;
        report.ResolvedById = User.GetRequiredUserId();

        if (request.BlockListing)
        {
            report.Listing.Status = ListingStatus.Blocked;
            report.Listing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(MarketplaceMapper.MapListingReport(report));
    }
}
