using System.Web;

namespace SamoBot.Extensions;

public static class UrlExtensions
{
    extension(Uri url)
    {
        public int GetUrlSegmentsLength() => url.Segments.Length - 1;
        public int GetQueryParameterCount() => string.IsNullOrEmpty(url.Query) ? 0 : HttpUtility.ParseQueryString(url.Query).Count;
    }
}