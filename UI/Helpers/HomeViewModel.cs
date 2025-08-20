namespace UI.Helpers
{
    public class HomeViewModel
    {
        public List<Domain.Entities.Category> Categories { get; set; } = new();
        public List<Domain.Entities.Product> Products { get; set; } = new();
    }
}