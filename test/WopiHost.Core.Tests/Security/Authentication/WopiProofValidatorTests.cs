using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Security.Authentication;
using WopiHost.Discovery;
using WopiHost.Discovery.Models;

namespace WopiHost.Core.Tests.Security.Authentication;

/// <summary>
/// Tests for the WopiProofValidator class.
/// </summary>
public class WopiProofValidatorTests
{
    /// <summary>
    /// Tests for the timestamp validation logic within the WopiProofValidator class.
    /// </summary>
    public class TimestampValidationTests
    {
        private readonly Mock<IDiscoverer> _mockDiscoverer;
        private readonly Mock<ILogger<WopiProofValidator>> _mockLogger;
        private readonly FakeTimeProvider _timeProvider;
        private readonly WopiProofValidator _validator;
        
        // Fixed reference time for consistent testing
        private readonly DateTimeOffset _referenceTime = new DateTimeOffset(2023, 6, 15, 12, 0, 0, TimeSpan.Zero);

        public TimestampValidationTests()
        {
            _mockDiscoverer = new Mock<IDiscoverer>();
            _mockLogger = new Mock<ILogger<WopiProofValidator>>();
            _timeProvider = new FakeTimeProvider(_referenceTime);
            _validator = new WopiProofValidator(_mockDiscoverer.Object, _mockLogger.Object, _timeProvider);
            
            // Setup discoverer to return empty proof keys
            _mockDiscoverer.Setup(d => d.GetProofKeysAsync())
                .ReturnsAsync(new WopiProofKeys());
        }

        [Fact]
        public void ValidateTimestamp_WithExactly20MinutesOld_ShouldReturnTrue()
        {
            // Arrange
            // Exactly 20 minutes old
            var timestamp = _referenceTime.AddMinutes(-20).ToUnixTimeMilliseconds().ToString();

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.True(result, "Timestamp exactly 20 minutes old should be accepted");
        }

        [Fact]
        public void ValidateTimestamp_With19MinutesOld_ShouldReturnTrue()
        {
            // Arrange
            var timestamp = _referenceTime.AddMinutes(-19).ToUnixTimeMilliseconds().ToString();

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.True(result, "Timestamp less than 20 minutes old should be accepted");
        }

        [Fact]
        public void ValidateTimestamp_WithAlmost20MinutesOld_ShouldReturnTrue()
        {
            // Arrange - 19 minutes and 59 seconds old
            var timestamp = _referenceTime.AddMinutes(-19).AddSeconds(-59).ToUnixTimeMilliseconds().ToString();

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.True(result, "Timestamp just under 20 minutes old should be accepted");
        }

        [Fact]
        public void ValidateTimestamp_With21MinutesOld_ShouldReturnFalse()
        {
            // Arrange
            var timestamp = _referenceTime.AddMinutes(-21).ToUnixTimeMilliseconds().ToString();

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.False(result, "Timestamp more than 20 minutes old should be rejected");
        }

        [Fact]
        public void ValidateTimestamp_With20MinutesAnd1SecondOld_ShouldReturnFalse()
        {
            // Arrange - Just over 20 minutes old
            var timestamp = _referenceTime.AddMinutes(-20).AddSeconds(-1).ToUnixTimeMilliseconds().ToString();

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.False(result, "Timestamp just over 20 minutes old should be rejected");
        }

        [Fact]
        public void ValidateTimestamp_WithFutureTimestamp_ShouldReturnTrue()
        {
            // Arrange - 1 minute in the future
            var timestamp = _referenceTime.AddMinutes(1).ToUnixTimeMilliseconds().ToString();

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.True(result, "Future timestamp should be accepted");
        }

        [Fact]
        public void ValidateTimestamp_WithInvalidFormat_ShouldReturnFalse()
        {
            // Arrange
            string timestamp = "not-a-number";

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.False(result, "Invalid timestamp format should be rejected");
        }

        [Fact]
        public void ValidateTimestamp_WithEmptyString_ShouldReturnFalse()
        {
            // Arrange
            string timestamp = string.Empty;

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.False(result, "Empty timestamp should be rejected");
        }
    }

    /// <summary>
    /// Tests for the ValidateProofAsync method which integrates with timestamp validation.
    /// </summary>
    public class ProofValidationTests
    {
        private readonly Mock<IDiscoverer> _mockDiscoverer;
        private readonly Mock<ILogger<WopiProofValidator>> _mockLogger;
        private readonly FakeTimeProvider _timeProvider;
        private readonly WopiProofValidator _validator;
        
        // Fixed reference time for consistent testing
        private readonly DateTimeOffset _referenceTime = new DateTimeOffset(2023, 6, 15, 12, 0, 0, TimeSpan.Zero);
        
        public ProofValidationTests()
        {
            _mockDiscoverer = new Mock<IDiscoverer>();
            _mockLogger = new Mock<ILogger<WopiProofValidator>>();
            _timeProvider = new FakeTimeProvider(_referenceTime);
            _validator = new WopiProofValidator(_mockDiscoverer.Object, _mockLogger.Object, _timeProvider);
            
            // Setup discoverer to return empty proof keys by default
            _mockDiscoverer.Setup(d => d.GetProofKeysAsync())
                .ReturnsAsync(new WopiProofKeys());
        }
        
        [Fact]
        public async Task ValidateProofAsync_WithMissingProofHeader_ShouldReturnFalse()
        {
            // Arrange
            var request = new DefaultHttpContext().Request;
            const string accessToken = "test-access-token";
            
            // Add only timestamp, missing proof header
            request.Headers[WopiHeaders.TIMESTAMP] = _referenceTime.AddMinutes(-10).ToUnixTimeMilliseconds().ToString();
            
            // Act
            bool result = await _validator.ValidateProofAsync(request, accessToken);
            
            // Assert
            Assert.False(result, "Validation should fail when proof header is missing");
        }
        
        [Fact]
        public async Task ValidateProofAsync_WithMissingTimestampHeader_ShouldReturnFalse()
        {
            // Arrange
            var request = new DefaultHttpContext().Request;
            const string accessToken = "test-access-token";
            
            // Add only proof, missing timestamp header
            request.Headers[WopiHeaders.PROOF] = "valid-proof";
            
            // Act
            bool result = await _validator.ValidateProofAsync(request, accessToken);
            
            // Assert
            Assert.False(result, "Validation should fail when timestamp header is missing");
        }
        
        [Fact]
        public async Task ValidateProofAsync_WithOldTimestamp_ShouldReturnFalse()
        {
            // Arrange
            var request = new DefaultHttpContext().Request;
            const string accessToken = "test-access-token";
            
            // Add headers with old timestamp (21 minutes)
            request.Headers[WopiHeaders.PROOF] = "valid-proof";
            request.Headers[WopiHeaders.TIMESTAMP] = _referenceTime.AddMinutes(-21).ToUnixTimeMilliseconds().ToString();
            
            // Act
            bool result = await _validator.ValidateProofAsync(request, accessToken);
            
            // Assert
            Assert.False(result, "Validation should fail with timestamp older than 20 minutes");
        }
    }
    
    /// <summary>
    /// A simple implementation of TimeProvider for use in testing.
    /// </summary>
    private class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
} 