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
            result.Value.Should().StartWith("http://");
        }

        [Fact]
        public void Clean_ShouldLowercaseHost()
        {
            var url = "http://EXAMPLE.COM";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("example.com");
        }

        [Fact]
        public void Clean_ShouldRemoveFragment()
        {
            var url = "http://example.com/path#fragment";
            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotContain("#");
            result.Value.Should().Be("http://example.com/path");
        }

        [Fact]
        public void Clean_ShouldRemoveDefaultHttpPort()
        {
            var url = "http://example.com:80/path";
            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotContain(":80");
            result.Value.Should().Be("http://example.com/path");
        }

        [Fact]
        public void Clean_ShouldRemoveDefaultHttpsPort()
        {
            var url = "https://example.com:443/path";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotContain(":443");
            result.Value.Should().Be("https://example.com/path");
        }

        [Fact]
        public void Clean_ShouldKeepNonDefaultPort()
        {
            var url = "http://example.com:8080/path";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain(":8080");
            result.Value.Should().Be("http://example.com:8080/path");
        }

        [Fact]
        public void Clean_ShouldKeepTrailingSlashFromPath()
        {
            var url = "http://example.com/path/";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().EndWith("/");
            result.Value.Should().Be("http://example.com/path/");
        }

        [Fact]
        public void Clean_ShouldKeepRootSlash()
        {
            var url = "http://example.com/";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().EndWith("/");
            result.Value.Should().Be("http://example.com/");
        }

        [Fact]
        public void Clean_ShouldSortQueryParameters()
        {
            var url = "http://example.com/path?zebra=1&apple=2&banana=3";

            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            var queryString = result.Value.Split('?')[1];
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
            var queryString = result.Value.Split('?')[1];
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
            result.Value.Should().Contain("key1=value1");
            result.Value.Should().Contain("key2=value2");
        }

        [Fact]
        public void Clean_WithComplexUrl_ShouldNormalizeAllAspects()
        {
            var url = "HTTPS://EXAMPLE.COM:443/path/to/resource/?zebra=1&apple=2#fragment";
            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("https://example.com/path/to/resource/?apple=2&zebra=1");
        }

        [Fact]
        public void Clean_WithUrlContainingSpecialCharacters_ShouldPreserveThem()
        {
            var url = "http://example.com/path?key=value%20with%20spaces&other=test";
            var result = UrlNormalizer.Clean(url);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("key=value+with+spaces");
            result.Value.Should().Contain("other=test");
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
            cleanedUrl.Should().StartWith("http://");
        }

        [Fact]
        public void TryClean_ShouldLowercaseHost()
        {
            var url = "http://EXAMPLE.COM";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl.Should().Contain("example.com");
        }

        [Fact]
        public void TryClean_ShouldRemoveFragment()
        {
            var url = "http://example.com/path#fragment";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl.Should().NotContain("#");
            cleanedUrl.Should().Be("http://example.com/path");
        }

        [Fact]
        public void TryClean_ShouldRemoveDefaultHttpPort()
        {
            var url = "http://example.com:80/path";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl.Should().NotContain(":80");
            cleanedUrl.Should().Be("http://example.com/path");
        }

        [Fact]
        public void TryClean_ShouldRemoveDefaultHttpsPort()
        {
            var url = "https://example.com:443/path";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl.Should().NotContain(":443");
            cleanedUrl.Should().Be("https://example.com/path");
        }

        [Fact]
        public void TryClean_ShouldKeepNonDefaultPort()
        {
            var url = "http://example.com:8080/path";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl.Should().Contain(":8080");
            cleanedUrl.Should().Be("http://example.com:8080/path");
        }

        [Fact]
        public void TryClean_ShouldKeepTrailingSlashFromPath()
        {
            var url = "http://example.com/path/";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl.Should().EndWith("/");
            cleanedUrl.Should().Be("http://example.com/path/");
        }

        [Fact]
        public void TryClean_ShouldKeepRootSlash()
        {
            var url = "http://example.com/";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            cleanedUrl.Should().EndWith("/");
            cleanedUrl.Should().Be("http://example.com/");
        }

        [Fact]
        public void TryClean_ShouldSortQueryParameters()
        {
            var url = "http://example.com/path?zebra=1&apple=2&banana=3";

            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            result.Should().BeTrue();
            var queryString = cleanedUrl!.Split('?')[1];
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
            cleanedUrl.Should().Be("https://example.com/path/to/resource/?apple=2&zebra=1");
        }

        [Fact]
        public void TryClean_AndClean_ShouldProduceSameResult()
        {
            var url = "HTTPS://EXAMPLE.COM:443/path/?zebra=1&apple=2#fragment";

            var tryResult = UrlNormalizer.TryClean(url, out var tryCleanedUrl);
            var cleanResult = UrlNormalizer.Clean(url);

            tryResult.Should().BeTrue();
            cleanResult.IsSuccess.Should().BeTrue();
            tryCleanedUrl.Should().Be(cleanResult.Value);
        }
    }
}
