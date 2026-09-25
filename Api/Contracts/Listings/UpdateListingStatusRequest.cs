using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Api.Contracts.Listings;

public class UpdateListingStatusRequest
{
    [Required]
    public ListingStatus Status { get; init; }
}
