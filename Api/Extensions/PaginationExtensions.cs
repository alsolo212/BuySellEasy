namespace Api.Extensions;

public static class PaginationExtensions
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int page, int pageSize)
    {
        return (
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, MaxPageSize));
    }

    public static void AddPaginationHeaders(this HttpResponse response, int page, int pageSize, int totalCount)
    {
        response.Headers["X-Total-Count"] = totalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        response.Headers["X-Page"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture);
        response.Headers["X-Page-Size"] = pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
