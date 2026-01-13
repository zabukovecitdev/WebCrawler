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
            // Arrange
            string? url = null;

            // Act
            var result = UrlNormalizer.Clean(url!);

            // Assert
            result.IsFailed.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Message == "Invalid URL");
        }

        [Fact]
        public void Clean_WithEmptyUrl_ShouldReturnFailure()
        {
            // Arrange
            var url = string.Empty;

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsFailed.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Message == "Invalid URL");
        }

        [Fact]
        public void Clean_WithWhitespaceUrl_ShouldReturnFailure()
        {
            // Arrange
            var url = "   ";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsFailed.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Message == "Invalid URL");
        }

        [Fact]
        public void Clean_WithInvalidUrl_ShouldReturnFailure()
        {
            // Arrange
            var url = "not-a-valid-url";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsFailed.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e.Message == "Invalid URL");
        }

        [Fact]
        public void Clean_WithValidHttpUrl_ShouldReturnSuccess()
        {
            // Arrange
            var url = "http://example.com";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("http://example.com/");
        }

        [Fact]
        public void Clean_WithValidHttpsUrl_ShouldReturnSuccess()
        {
            // Arrange
            var url = "https://example.com";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("https://example.com/");
        }

        [Fact]
        public void Clean_ShouldLowercaseScheme()
        {
            // Arrange
            var url = "HTTP://example.com";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().StartWith("http://");
        }

        [Fact]
        public void Clean_ShouldLowercaseHost()
        {
            // Arrange
            var url = "http://EXAMPLE.COM";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("example.com");
        }

        [Fact]
        public void Clean_ShouldRemoveFragment()
        {
            // Arrange
            var url = "http://example.com/path#fragment";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotContain("#");
            result.Value.Should().Be("http://example.com/path");
        }

        [Fact]
        public void Clean_ShouldRemoveDefaultHttpPort()
        {
            // Arrange
            var url = "http://example.com:80/path";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotContain(":80");
            result.Value.Should().Be("http://example.com/path");
        }

        [Fact]
        public void Clean_ShouldRemoveDefaultHttpsPort()
        {
            // Arrange
            var url = "https://example.com:443/path";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotContain(":443");
            result.Value.Should().Be("https://example.com/path");
        }

        [Fact]
        public void Clean_ShouldKeepNonDefaultPort()
        {
            // Arrange
            var url = "http://example.com:8080/path";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain(":8080");
            result.Value.Should().Be("http://example.com:8080/path");
        }

        [Fact]
        public void Clean_ShouldRemoveTrailingSlashFromPath()
        {
            // Arrange
            var url = "http://example.com/path/";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotEndWith("/");
            result.Value.Should().Be("http://example.com/path");
        }

        [Fact]
        public void Clean_ShouldKeepRootSlash()
        {
            // Arrange
            var url = "http://example.com/";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().EndWith("/");
            result.Value.Should().Be("http://example.com/");
        }

        [Fact]
        public void Clean_ShouldSortQueryParameters()
        {
            // Arrange
            var url = "http://example.com/path?zebra=1&apple=2&banana=3";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
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
            // Arrange
            var url = "http://example.com/path?Zebra=1&apple=2";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var queryString = result.Value.Split('?')[1];
            var parameters = queryString.Split('&');
            parameters[0].Should().StartWith("apple=");
            parameters[1].Should().StartWith("Zebra=");
        }

        [Fact]
        public void Clean_ShouldPreserveQueryParameterValues()
        {
            // Arrange
            var url = "http://example.com/path?key1=value1&key2=value2";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("key1=value1");
            result.Value.Should().Contain("key2=value2");
        }

        [Fact]
        public void Clean_WithComplexUrl_ShouldNormalizeAllAspects()
        {
            // Arrange
            var url = "HTTPS://EXAMPLE.COM:443/path/to/resource/?zebra=1&apple=2#fragment";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be("https://example.com/path/to/resource?apple=2&zebra=1");
        }

        [Fact]
        public void Clean_WithUrlContainingSpecialCharacters_ShouldPreserveThem()
        {
            // Arrange
            var url = "http://example.com/path?key=value%20with%20spaces&other=test";

            // Act
            var result = UrlNormalizer.Clean(url);

            // Assert
            result.IsSuccess.Should().BeTrue();
            // HttpUtility.ParseQueryString normalizes %20 to + in query strings
            result.Value.Should().Contain("key=value+with+spaces");
            result.Value.Should().Contain("other=test");
        }
    }

    public class TryCleanTests
    {
        [Fact]
        public void TryClean_WithNullUrl_ShouldReturnFalse()
        {
            // Arrange
            string? url = null;

            // Act
            var result = UrlNormalizer.TryClean(url!, out var cleanedUrl);

            // Assert
            result.Should().BeFalse();
            cleanedUrl.Should().BeNull();
        }

        [Fact]
        public void TryClean_WithEmptyUrl_ShouldReturnFalse()
        {
            // Arrange
            var url = string.Empty;

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeFalse();
            cleanedUrl.Should().BeNull();
        }

        [Fact]
        public void TryClean_WithWhitespaceUrl_ShouldReturnFalse()
        {
            // Arrange
            var url = "   ";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeFalse();
            cleanedUrl.Should().BeNull();
        }

        [Fact]
        public void TryClean_WithInvalidUrl_ShouldReturnFalse()
        {
            // Arrange
            var url = "not-a-valid-url";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeFalse();
            cleanedUrl.Should().BeNull();
        }

        [Fact]
        public void TryClean_WithValidHttpUrl_ShouldReturnTrue()
        {
            // Arrange
            var url = "http://example.com";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeTrue();
            cleanedUrl.Should().Be("http://example.com/");
        }

        [Fact]
        public void TryClean_WithValidHttpsUrl_ShouldReturnTrue()
        {
            // Arrange
            var url = "https://example.com";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeTrue();
            cleanedUrl.Should().Be("https://example.com/");
        }

        [Fact]
        public void TryClean_ShouldLowercaseScheme()
        {
            // Arrange
            var url = "HTTP://example.com";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeTrue();
            cleanedUrl.Should().StartWith("http://");
        }

        [Fact]
        public void TryClean_ShouldLowercaseHost()
        {
            // Arrange
            var url = "http://EXAMPLE.COM";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeTrue();
            cleanedUrl.Should().Contain("example.com");
        }

        [Fact]
        public void TryClean_ShouldRemoveFragment()
        {
            // Arrange
            var url = "http://example.com/path#fragment";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeTrue();
            cleanedUrl.Should().NotContain("#");
            cleanedUrl.Should().Be("http://example.com/path");
        }

        [Fact]
        public void TryClean_ShouldRemoveDefaultHttpPort()
        {
            // Arrange
            var url = "http://example.com:80/path";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeTrue();
            cleanedUrl.Should().NotContain(":80");
            cleanedUrl.Should().Be("http://example.com/path");
        }

        [Fact]
        public void TryClean_ShouldRemoveDefaultHttpsPort()
        {
            // Arrange
            var url = "https://example.com:443/path";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeTrue();
            cleanedUrl.Should().NotContain(":443");
            cleanedUrl.Should().Be("https://example.com/path");
        }

        [Fact]
        public void TryClean_ShouldKeepNonDefaultPort()
        {
            // Arrange
            var url = "http://example.com:8080/path";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeTrue();
            cleanedUrl.Should().Contain(":8080");
            cleanedUrl.Should().Be("http://example.com:8080/path");
        }

        [Fact]
        public void TryClean_ShouldRemoveTrailingSlashFromPath()
        {
            // Arrange
            var url = "http://example.com/path/";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeTrue();
            cleanedUrl.Should().NotEndWith("/");
            cleanedUrl.Should().Be("http://example.com/path");
        }

        [Fact]
        public void TryClean_ShouldKeepRootSlash()
        {
            // Arrange
            var url = "http://example.com/";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeTrue();
            cleanedUrl.Should().EndWith("/");
            cleanedUrl.Should().Be("http://example.com/");
        }

        [Fact]
        public void TryClean_ShouldSortQueryParameters()
        {
            // Arrange
            var url = "http://example.com/path?zebra=1&apple=2&banana=3";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
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
            // Arrange
            var url = "HTTPS://EXAMPLE.COM:443/path/to/resource/?zebra=1&apple=2#fragment";

            // Act
            var result = UrlNormalizer.TryClean(url, out var cleanedUrl);

            // Assert
            result.Should().BeTrue();
            cleanedUrl.Should().Be("https://example.com/path/to/resource?apple=2&zebra=1");
        }

        [Fact]
        public void TryClean_AndClean_ShouldProduceSameResult()
        {
            // Arrange
            var url = "HTTPS://EXAMPLE.COM:443/path/?zebra=1&apple=2#fragment";

            // Act
            var tryResult = UrlNormalizer.TryClean(url, out var tryCleanedUrl);
            var cleanResult = UrlNormalizer.Clean(url);

            // Assert
            tryResult.Should().BeTrue();
            cleanResult.IsSuccess.Should().BeTrue();
            tryCleanedUrl.Should().Be(cleanResult.Value);
        }
    }
}
