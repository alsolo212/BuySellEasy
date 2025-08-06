using Microsoft.AspNetCore.Mvc.Filters;

namespace UI.Filters
{
    public class ProductListActionFilter : IActionFilter
    {
        private readonly ILogger<ProductListActionFilter> _logger;

        public ProductListActionFilter(ILogger<ProductListActionFilter> logger)
        {  _logger = logger;
        }
        public void OnActionExecuted(ActionExecutedContext context)
        {
            
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            
        }
    }
}
