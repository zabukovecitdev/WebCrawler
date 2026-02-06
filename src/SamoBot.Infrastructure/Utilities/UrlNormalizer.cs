using System.Web;
using FluentResults;

namespace SamoBot.Infrastructure.Utilities;

public static class UrlNormalizer
{
    // TODO: This Url normalization should be done with some sort of pipeline which can be easily extended and configured
    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "utm_source",
        "utm_medium",
        "utm_campaign",
        "utm_term",
        "utm_content",
        "utm_id",
        "fbclid",
        "gclid",
        "igshid",
        "twclid",
        "li_fat_id",
        "_ga",
        "_gl",
        "yclid",
        "mc_cid",
        "mc_eid",
        "mkt_tok",
        "_hsenc",
        "_hsmi",
        "hsCtaTracking",
        "trk",
        "trk_info",
        "ncid",
        "clickid",
        "clickId",
        "click_id",
        "affiliate",
        "affiliateId",
        "affiliate_id",
        "partner",
        "partnerId",
        "partner_id",
    };

    public static Result<Uri> Clean(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result.Fail("Invalid URL");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var cleanedUri))
        {
            return Result.Fail("Invalid URL");
        }

        return Result.Ok(cleanedUri.Normalize());
    }

    public static bool TryClean(string url, out Uri? normalizedUrl)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            normalizedUrl = null;
            return false;
        }

        normalizedUrl = uri.NormalizeAndRemoveTracking();
        return true;
    }

    private static Uri Normalize(this Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty
        };

        if ((builder.Scheme == "http" && builder.Port == 80) ||
            (builder.Scheme == "https" && builder.Port == 443))
        {
            builder.Port = -1;
        }

        // Keep trailing slashes as-is for web crawler accuracy
        // Some sites treat /page and /page/ as different resources

        if (!string.IsNullOrEmpty(builder.Query))
        {
            var queryParameters = HttpUtility.ParseQueryString(builder.Query);

            var keys = queryParameters.AllKeys
                .Where(key => key != null)
                .OrderBy(key => key, StringComparer.InvariantCultureIgnoreCase);

            var normalized = HttpUtility.ParseQueryString(string.Empty);
            foreach (var key in keys)
            {
                normalized[key] = queryParameters[key];
            }

            builder.Query = normalized.ToString();
        }

        return builder.Uri;
    }

    private static Uri NormalizeAndRemoveTracking(this Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty
        };

        if ((builder.Scheme == "http" && builder.Port == 80) ||
            (builder.Scheme == "https" && builder.Port == 443))
        {
            builder.Port = -1;
        }

        if (!string.IsNullOrEmpty(builder.Query))
        {
            var queryParameters = HttpUtility.ParseQueryString(builder.Query);

            var keys = queryParameters.AllKeys
                .Where(key => key != null && !IsTrackingParameter(key))
                .OrderBy(key => key, StringComparer.InvariantCultureIgnoreCase);

            var normalized = HttpUtility.ParseQueryString(string.Empty);
            foreach (var key in keys)
            {
                normalized[key] = queryParameters[key];
            }

            builder.Query = normalized.ToString();
        }

        return builder.Uri;
    }

    private static bool IsTrackingParameter(string? parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        if (TrackingParameters.Contains(parameterName))
        {
            return true;
        }

        if (parameterName.StartsWith("utm_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
