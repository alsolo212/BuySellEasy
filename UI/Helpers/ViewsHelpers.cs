namespace UI.Helpers
{
    public static class ViewsHelpers
    {
        public static string GetCallBackUrl(this HttpContext context, string defaultPath)
        {
            var referer = context.Request.Headers["Referer"].ToString();
            var callbackUrl = string.IsNullOrWhiteSpace(referer) ? defaultPath : referer;
            return callbackUrl;
        }
    }
}
