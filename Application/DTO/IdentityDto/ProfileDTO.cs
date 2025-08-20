namespace Application.DTO.IdentityDto
{
    public class ProfileDTO
    {
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? ProfileImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsVerified { get; set; }
        public int ActiveListingsCount { get; set; }
    }
}
