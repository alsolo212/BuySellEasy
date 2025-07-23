namespace Domain.Entities
{
    public class Product
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? ProductName { get; set; }
        public string? Description { get; set; }
        public double? ProductPrice { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public string? Location { get; set; }
        public string? ProductCondition { get; set; }
    }
}
