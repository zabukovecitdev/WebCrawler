using FluentAssertions;
using FluentResults;
using SamoBot.Utilities;

namespace SamoBot.Tests;

public class UrlNormalizerTests
{
    public class CleanTests
    {
        [Fact]
        public void Clean_WithNullUrl_ShouldReturnFailure()
        {
            string? url = null;
            var result = UrlNormalizer.Clean(url!);

            result.IsFailed.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Message == "Invalid URL");
        }

        [Fact]
        public void Clean_WithEmptyUrl_ShouldReturnFailure()
        {
            var url = string.Empty;

            var result = UrlNormalizer.Clean(url);

            result.IsFailed.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Message == "Invalid URL");
        }

        [Fact]
        public void Clean_WithWhitespaceUrl_ShouldReturnFailure()
        {
            var url = "   ";

            var result = UrlNormalizer.Clean(url);

            result.IsFailed.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Message == "Invalid URL");
        }

        [Fact]
        public void Clean_WithInvalidUrl_ShouldReturnFailure()
        {
            var url = "not-a-valid-url";
            var result = UrlNormalizer.Clean(url);

            result.IsFailed.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Message == "Invalid URL");
        }

        [Fact]
        public void Clean_WithValidHttpUrl_ShouldReturnSuccess()
        {
            var url = "http://example.com";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("http://example.com/");
        }

        [Fact]
        public void Clean_WithValidHttpsUrl_ShouldReturnSuccess()
        {
            var url = "https://example.com";
            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("https://example.com/");
        }

        [Fact]
        public void Clean_ShouldLowercaseScheme()
        {
            var url = "HTTP://example.com";
            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.AbsoluteUri.Should().StartWith("http://");
        }

        [Fact]
        public void Clean_ShouldLowercaseHost()
        {
            var url = "http://EXAMPLE.COM";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.AbsoluteUri.Should().Contain("example.com");
        }

        [Fact]
        public void Clean_ShouldRemoveFragment()
        {
            var url = "http://example.com/path#fragment";
            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.AbsoluteUri.Should().NotContain("#");
            result.Value.AbsoluteUri.Should().Be("http://example.com/path");
        }

        [Fact]
        public void Clean_ShouldRemoveDefaultHttpPort()
        {
            var url = "http://example.com:80/path";
            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.AbsoluteUri.Should().NotContain(":80");
            result.Value.AbsoluteUri.Should().Be("http://example.com/path");
        }

        [Fact]
        public void Clean_ShouldRemoveDefaultHttpsPort()
        {
            var url = "https://example.com:443/path";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.AbsoluteUri.Should().NotContain(":443");
            result.Value.AbsoluteUri.Should().Be("https://example.com/path");
        }

        [Fact]
        public void Clean_ShouldKeepNonDefaultPort()
        {
            var url = "http://example.com:8080/path";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.AbsoluteUri.Should().Contain(":8080");
            result.Value.AbsoluteUri.Should().Be("http://example.com:8080/path");
        }

        [Fact]
        public void Clean_ShouldKeepTrailingSlashFromPath()
        {
            var url = "http://example.com/path/";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.AbsoluteUri.Should().EndWith("/");
            result.Value.AbsoluteUri.Should().Be("http://example.com/path/");
        }

        [Fact]
        public void Clean_ShouldKeepRootSlash()
        {
            var url = "http://example.com/";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.AbsoluteUri.Should().EndWith("/");
            result.Value.AbsoluteUri.Should().Be("http://example.com/");
        }

        [Fact]
        public void Clean_ShouldSortQueryParameters()
        {
            var url = "http://example.com/path?zebra=1&apple=2&banana=3";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            var queryString = result.Value.AbsoluteUri.Split('?')[1];
            var parameters = queryString.Split('&');
            parameters[0].Should().StartWith("apple=");
            parameters[1].Should().StartWith("banana=");
            parameters[2].Should().StartWith("zebra=");
        }

        [Fact]
        public void Clean_ShouldSortQueryParametersCaseInsensitive()
        {
            var url = "http://example.com/path?Zebra=1&apple=2";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            var queryString = result.Value.AbsoluteUri.Split('?')[1];
            var parameters = queryString.Split('&');
            parameters[0].Should().StartWith("apple=");
            parameters[1].Should().StartWith("Zebra=");
        }

        [Fact]
        public void Clean_ShouldPreserveQueryParameterValues()
        {
            var url = "http://example.com/path?key1=value1&key2=value2";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.AbsoluteUri.Should().Contain("key1=value1");
            result.Value.AbsoluteUri.Should().Contain("key2=value2");
        }

        [Fact]
        public void Clean_WithComplexUrl_ShouldNormalizeAllAspects()
        {
            var url = "HTTPS://EXAMPLE.COM:443/path/to/resource/?zebra=1&apple=2#fragment";
            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.AbsoluteUri.Should().Be("https://example.com/path/to/resource/?apple=2&zebra=1");
        }

        [Fact]
        public void Clean_WithUrlContainingSpecialCharacters_ShouldPreserveThem()
        {
            var url = "http://example.com/path?key=value%20with%20spaces&other=test";
            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.AbsoluteUri.Should().Contain("key=value+with+spaces");
            result.Value.AbsoluteUri.Should().Contain("other=test");
        }
    }

    public class TryCleanTests
    {
        [Fact]
        public void TryClean_WithNullUrl_ShouldReturnFalse()
        {
            string? url = null;
            var result = UrlNormalizer.TryClean(url!, out var cleanedUrl);

            result.Should().BeFalse();
            cleanedUrl.Should().BeNull();
        }

        [Fact]
        public void TryClean_WithEmptyUrl_ShouldReturnFalse()
        {
            var url = string.Empty;
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeFalse();
            cleanedUrl.Should().BeNull();
        }

        [Fact]
        public void TryClean_WithWhitespaceUrl_ShouldReturnFalse()
        {
            var url = "   ";
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeFalse();
            cleanedUrl.Should().BeNull();
        }

        [Fact]
        public void TryClean_WithInvalidUrl_ShouldReturnFalse()
        {
            var url = "not-a-valid-url";
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeFalse();
            cleanedUrl.Should().BeNull();
        }

        [Fact]
        public void TryClean_WithValidHttpUrl_ShouldReturnTrue()
        {
            var url = "http://example.com";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl.Should().Be("http://example.com/");
        }

        [Fact]
        public void TryClean_WithValidHttpsUrl_ShouldReturnTrue()
        {
            var url = "https://example.com";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl.Should().Be("https://example.com/");
        }

        [Fact]
        public void TryClean_ShouldLowercaseScheme()
        {
            var url = "HTTP://example.com";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().StartWith("http://");
        }

        [Fact]
        public void TryClean_ShouldLowercaseHost()
        {
            var url = "http://EXAMPLE.COM";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().Contain("example.com");
        }

        [Fact]
        public void TryClean_ShouldRemoveFragment()
        {
            var url = "http://example.com/path#fragment";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("#");
            cleanedUrl.AbsoluteUri.Should().Be("http://example.com/path");
        }

        [Fact]
        public void TryClean_ShouldRemoveDefaultHttpPort()
        {
            var url = "http://example.com:80/path";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain(":80");
            cleanedUrl.AbsoluteUri.Should().Be("http://example.com/path");
        }

        [Fact]
        public void TryClean_ShouldRemoveDefaultHttpsPort()
        {
            var url = "https://example.com:443/path";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain(":443");
            cleanedUrl.AbsoluteUri.Should().Be("https://example.com/path");
        }

        [Fact]
        public void TryClean_ShouldKeepNonDefaultPort()
        {
            var url = "http://example.com:8080/path";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().Contain(":8080");
            cleanedUrl.AbsoluteUri.Should().Be("http://example.com:8080/path");
        }

        [Fact]
        public void TryClean_ShouldKeepTrailingSlashFromPath()
        {
            var url = "http://example.com/path/";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().EndWith("/");
            cleanedUrl.AbsoluteUri.Should().Be("http://example.com/path/");
        }

        [Fact]
        public void TryClean_ShouldKeepRootSlash()
        {
            var url = "http://example.com/";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().EndWith("/");
            cleanedUrl.AbsoluteUri.Should().Be("http://example.com/");
        }

        [Fact]
        public void TryClean_ShouldSortQueryParameters()
        {
            var url = "http://example.com/path?zebra=1&apple=2&banana=3";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            var queryString = cleanedUrl!.AbsoluteUri.Split('?')[1];
            var parameters = queryString.Split('&');
            parameters[0].Should().StartWith("apple=");
            parameters[1].Should().StartWith("banana=");
            parameters[2].Should().StartWith("zebra=");
        }

        [Fact]
        public void TryClean_WithComplexUrl_ShouldNormalizeAllAspects()
        {
            var url = "HTTPS://EXAMPLE.COM:443/path/to/resource/?zebra=1&apple=2#fragment";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().Be("https://example.com/path/to/resource/?apple=2&zebra=1");
        }

        [Fact]
        public void TryClean_AndClean_ShouldProduceSameResult()
        {
            var url = "HTTPS://EXAMPLE.COM:443/path/?zebra=1&apple=2#fragment";

            var tryResult = UrlNormalizer.TryClean(url, out var tryCleanedUrl);
            var cleanResult = UrlNormalizer.Clean(url);

            tryResult.Should().BeTrue();
            cleanResult.IsSuccess.Should().BeTrue();
            tryCleanedUrl!.AbsoluteUri.Should().Be(cleanResult.Value.AbsoluteUri);
        }
    }

    public class TrackingParameterRemovalTests
    {
        [Fact]
        public void TryClean_ShouldRemoveUtmSource()
        {
            var url = "http://example.com/page?utm_source=google&id=123";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_source");
            cleanedUrl.AbsoluteUri.Should().Contain("id=123");
            cleanedUrl.AbsoluteUri.Should().Be("http://example.com/page?id=123");
        }

        [Fact]
        public void TryClean_ShouldRemoveUtmMedium()
        {
            var url = "http://example.com/page?utm_medium=email&product=shoes";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_medium");
            cleanedUrl.AbsoluteUri.Should().Contain("product=shoes");
            cleanedUrl.AbsoluteUri.Should().Be("http://example.com/page?product=shoes");
        }

        [Fact]
        public void TryClean_ShouldRemoveUtmCampaign()
        {
            var url = "http://example.com/page?utm_campaign=summer_sale&category=electronics";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_campaign");
            cleanedUrl.AbsoluteUri.Should().Contain("category=electronics");
        }

        [Fact]
        public void TryClean_ShouldRemoveUtmTerm()
        {
            var url = "http://example.com/page?utm_term=keyword&search=test";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_term");
            cleanedUrl.AbsoluteUri.Should().Contain("search=test");
        }

        [Fact]
        public void TryClean_ShouldRemoveUtmContent()
        {
            var url = "http://example.com/page?utm_content=banner&page=1";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_content");
            cleanedUrl.AbsoluteUri.Should().Contain("page=1");
        }

        [Fact]
        public void TryClean_ShouldRemoveUtmId()
        {
            var url = "http://example.com/page?utm_id=12345&filter=active";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_id");
            cleanedUrl.AbsoluteUri.Should().Contain("filter=active");
        }

        [Fact]
        public void TryClean_ShouldRemoveAllUtmParameters()
        {
            var url = "http://example.com/page?utm_source=google&utm_medium=cpc&utm_campaign=test&utm_term=keyword&utm_content=ad&id=123";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_source");
            cleanedUrl.AbsoluteUri.Should().NotContain("utm_medium");
            cleanedUrl.AbsoluteUri.Should().NotContain("utm_campaign");
            cleanedUrl.AbsoluteUri.Should().NotContain("utm_term");
            cleanedUrl.AbsoluteUri.Should().NotContain("utm_content");
            cleanedUrl.AbsoluteUri.Should().Contain("id=123");
            cleanedUrl.AbsoluteUri.Should().Be("http://example.com/page?id=123");
        }

        [Fact]
        public void TryClean_ShouldRemoveUtmParametersCaseInsensitive()
        {
            var url = "http://example.com/page?UTM_SOURCE=google&Utm_Medium=cpc&id=123";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.ToLowerInvariant().Should().NotContain("utm_source");
            cleanedUrl.AbsoluteUri.ToLowerInvariant().Should().NotContain("utm_medium");
            cleanedUrl.AbsoluteUri.Should().Contain("id=123");
        }

        [Fact]
        public void TryClean_ShouldRemoveUtmCustomParameters()
        {
            var url = "http://example.com/page?utm_custom_param=value&id=123";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_custom_param");
            cleanedUrl.AbsoluteUri.Should().Contain("id=123");
        }

        [Fact]
        public void TryClean_ShouldRemoveFbclid()
        {
            var url = "http://example.com/page?fbclid=abc123&product=shoes";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("fbclid");
            cleanedUrl.AbsoluteUri.Should().Contain("product=shoes");
        }

        [Fact]
        public void TryClean_ShouldRemoveGclid()
        {
            var url = "http://example.com/page?gclid=xyz789&category=electronics";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("gclid");
            cleanedUrl.AbsoluteUri.Should().Contain("category=electronics");
        }

        [Fact]
        public void TryClean_ShouldRemoveIgshid()
        {
            var url = "http://example.com/page?igshid=insta123&page=home";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("igshid");
            cleanedUrl.AbsoluteUri.Should().Contain("page=home");
        }

        [Fact]
        public void TryClean_ShouldRemoveTwclid()
        {
            var url = "http://example.com/page?twclid=twitter456&filter=active";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("twclid");
            cleanedUrl.AbsoluteUri.Should().Contain("filter=active");
        }

        [Fact]
        public void TryClean_ShouldRemoveLiFatId()
        {
            var url = "http://example.com/page?li_fat_id=linkedin789&sort=price";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("li_fat_id");
            cleanedUrl.AbsoluteUri.Should().Contain("sort=price");
        }

        [Fact]
        public void TryClean_ShouldRemoveGoogleAnalyticsParameters()
        {
            var url = "http://example.com/page?_ga=GA1.2.123456&_gl=1.abc123&id=123";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("_ga");
            cleanedUrl.AbsoluteUri.Should().NotContain("_gl");
            cleanedUrl.AbsoluteUri.Should().Contain("id=123");
        }

        [Fact]
        public void TryClean_ShouldRemoveYclid()
        {
            var url = "http://example.com/page?yclid=yandex123&search=query";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("yclid");
            cleanedUrl.AbsoluteUri.Should().Contain("search=query");
        }

        [Fact]
        public void TryClean_ShouldRemoveMailchimpParameters()
        {
            var url = "http://example.com/page?mc_cid=campaign123&mc_eid=email456&product=shoes";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("mc_cid");
            cleanedUrl.AbsoluteUri.Should().NotContain("mc_eid");
            cleanedUrl.AbsoluteUri.Should().Contain("product=shoes");
        }

        [Fact]
        public void TryClean_ShouldRemoveMarketoParameter()
        {
            var url = "http://example.com/page?mkt_tok=marketo123&category=electronics";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("mkt_tok");
            cleanedUrl.AbsoluteUri.Should().Contain("category=electronics");
        }

        [Fact]
        public void TryClean_ShouldRemoveHubspotParameters()
        {
            var url = "http://example.com/page?_hsenc=hs123&_hsmi=hs456&hsCtaTracking=cta789&id=123";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("_hsenc");
            cleanedUrl.AbsoluteUri.Should().NotContain("_hsmi");
            cleanedUrl.AbsoluteUri.Should().NotContain("hsCtaTracking");
            cleanedUrl.AbsoluteUri.Should().Contain("id=123");
        }

        [Fact]
        public void TryClean_ShouldRemoveGeneralTrackingParameters()
        {
            var url = "http://example.com/page?trk=track123&trk_info=info456&ncid=ncid789&id=123";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("trk");
            cleanedUrl.AbsoluteUri.Should().NotContain("trk_info");
            cleanedUrl.AbsoluteUri.Should().NotContain("ncid");
            cleanedUrl.AbsoluteUri.Should().Contain("id=123");
        }

        [Fact]
        public void TryClean_ShouldRemoveClickTrackingParameters()
        {
            var url = "http://example.com/page?clickid=click123&clickId=click456&click_id=click789&product=shoes";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.ToLowerInvariant().Should().NotContain("clickid");
            cleanedUrl.AbsoluteUri.ToLowerInvariant().Should().NotContain("clickid");
            cleanedUrl.AbsoluteUri.ToLowerInvariant().Should().NotContain("click_id");
            cleanedUrl.AbsoluteUri.Should().Contain("product=shoes");
        }

        [Fact]
        public void TryClean_ShouldRemoveAffiliateParameters()
        {
            var url = "http://example.com/page?affiliate=aff123&affiliateId=aff456&affiliate_id=aff789&id=123";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.ToLowerInvariant().Should().NotContain("affiliate=");
            cleanedUrl.AbsoluteUri.ToLowerInvariant().Should().NotContain("affiliateid=");
            cleanedUrl.AbsoluteUri.ToLowerInvariant().Should().NotContain("affiliate_id=");
            cleanedUrl.AbsoluteUri.Should().Contain("id=123");
        }

        [Fact]
        public void TryClean_ShouldRemovePartnerParameters()
        {
            var url = "http://example.com/page?partner=part123&partnerId=part456&partner_id=part789&category=electronics";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.ToLowerInvariant().Should().NotContain("partner=");
            cleanedUrl.AbsoluteUri.ToLowerInvariant().Should().NotContain("partnerid=");
            cleanedUrl.AbsoluteUri.ToLowerInvariant().Should().NotContain("partner_id=");
            cleanedUrl.AbsoluteUri.Should().Contain("category=electronics");
        }

        [Fact]
        public void TryClean_ShouldRemoveMultipleTrackingParameters()
        {
            var url = "http://example.com/page?utm_source=google&fbclid=fb123&gclid=gc456&_ga=ga789&id=123&product=shoes";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_source");
            cleanedUrl.AbsoluteUri.Should().NotContain("fbclid");
            cleanedUrl.AbsoluteUri.Should().NotContain("gclid");
            cleanedUrl.AbsoluteUri.Should().NotContain("_ga");
            cleanedUrl.AbsoluteUri.Should().Contain("id=123");
            cleanedUrl.AbsoluteUri.Should().Contain("product=shoes");
            cleanedUrl.AbsoluteUri.Should().Be("http://example.com/page?id=123&product=shoes");
        }

        [Fact]
        public void TryClean_ShouldPreserveNonTrackingParameters()
        {
            var url = "http://example.com/page?utm_source=google&id=123&category=electronics&sort=price";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_source");
            cleanedUrl.AbsoluteUri.Should().Contain("id=123");
            cleanedUrl.AbsoluteUri.Should().Contain("category=electronics");
            cleanedUrl.AbsoluteUri.Should().Contain("sort=price");
        }

        [Fact]
        public void TryClean_ShouldRemoveAllTrackingParametersAndKeepOnlyNonTracking()
        {
            var url = "http://example.com/page?utm_source=google&utm_medium=email&fbclid=fb123&gclid=gc456&product=shoes&size=large";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_source");
            cleanedUrl.AbsoluteUri.Should().NotContain("utm_medium");
            cleanedUrl.AbsoluteUri.Should().NotContain("fbclid");
            cleanedUrl.AbsoluteUri.Should().NotContain("gclid");
            cleanedUrl.AbsoluteUri.Should().Contain("product=shoes");
            cleanedUrl.AbsoluteUri.Should().Contain("size=large");
            cleanedUrl.AbsoluteUri.Should().Be("http://example.com/page?product=shoes&size=large");
        }

        [Fact]
        public void TryClean_ShouldHandleUrlWithOnlyTrackingParameters()
        {
            var url = "http://example.com/page?utm_source=google&fbclid=fb123";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_source");
            cleanedUrl.AbsoluteUri.Should().NotContain("fbclid");
            cleanedUrl.AbsoluteUri.Should().NotContain("?");
            cleanedUrl.AbsoluteUri.Should().Be("http://example.com/page");
        }

        [Fact]
        public void TryClean_ShouldHandleTrackingParametersCaseInsensitive()
        {
            var url = "http://example.com/page?FBCLID=fb123&GCLID=gc456&UTM_SOURCE=google&id=123";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.ToLowerInvariant().Should().NotContain("fbclid=");
            cleanedUrl.AbsoluteUri.ToLowerInvariant().Should().NotContain("gclid=");
            cleanedUrl.AbsoluteUri.ToLowerInvariant().Should().NotContain("utm_source=");
            cleanedUrl.AbsoluteUri.Should().Contain("id=123");
        }

        [Fact]
        public void TryClean_ShouldSortRemainingParametersAfterRemovingTracking()
        {
            var url = "http://example.com/page?utm_source=google&zebra=1&apple=2&utm_medium=email&banana=3";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().NotContain("utm_source");
            cleanedUrl.AbsoluteUri.Should().NotContain("utm_medium");
            var queryString = cleanedUrl.AbsoluteUri.Split('?')[1];
            var parameters = queryString.Split('&');
            parameters[0].Should().StartWith("apple=");
            parameters[1].Should().StartWith("banana=");
            parameters[2].Should().StartWith("zebra=");
        }

        [Fact]
        public void TryClean_ShouldNormalizeUrlWhileRemovingTrackingParameters()
        {
            var url = "HTTPS://EXAMPLE.COM:443/page?utm_source=google&fbclid=fb123&ID=123#fragment";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl!.AbsoluteUri.Should().StartWith("https://example.com/page");
            cleanedUrl.AbsoluteUri.Should().NotContain(":443");
            cleanedUrl.AbsoluteUri.Should().NotContain("utm_source");
            cleanedUrl.AbsoluteUri.Should().NotContain("fbclid");
            cleanedUrl.AbsoluteUri.Should().NotContain("#");
            cleanedUrl.AbsoluteUri.Should().Contain("ID=123");
            cleanedUrl.AbsoluteUri.Should().Be("https://example.com/page?ID=123");
        }
    }
}
