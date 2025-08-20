namespace Application.DTO.FiltersDto
{
    public class ProductFilterDto
    {
        public Guid? CategoryId { get; set; }
        public string? SearchTerm { get; set; }
        public string? Condition { get; set; }
        public double? MinPrice { get; set; }
        public double? MaxPrice { get; set; }
        public string SortBy { get; set; } = "recommended";
        public Guid? UserId { get; set; }
    }
}
