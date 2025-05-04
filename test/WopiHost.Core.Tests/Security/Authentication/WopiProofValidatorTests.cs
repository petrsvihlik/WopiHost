using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Core.Security.Authentication;
using WopiHost.Discovery;
using WopiHost.Discovery.Models;
using Xunit;

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
        private readonly WopiProofValidator _validator;

        public TimestampValidationTests()
        {
            _mockDiscoverer = new Mock<IDiscoverer>();
            _mockLogger = new Mock<ILogger<WopiProofValidator>>();
            _validator = new WopiProofValidator(_mockDiscoverer.Object, _mockLogger.Object);
            
            // Setup discoverer to return empty proof keys
            _mockDiscoverer.Setup(d => d.GetProofKeysAsync())
                .ReturnsAsync(new WopiProofKeys());
        }

        [Fact]
        public void ValidateTimestamp_WithExactly20MinutesOld_ShouldReturnTrue()
        {
            // Arrange
            // Adding a small buffer (200ms) to ensure we're just slightly under 20 minutes
            // to account for execution time between timestamp creation and validation
            string timestamp = DateTimeOffset.UtcNow.AddMinutes(-20).AddMilliseconds(200).ToUnixTimeMilliseconds().ToString();

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.True(result, "Timestamp exactly 20 minutes old should be accepted");
        }

        [Fact]
        public void ValidateTimestamp_With19MinutesOld_ShouldReturnTrue()
        {
            // Arrange
            string timestamp = DateTimeOffset.UtcNow.AddMinutes(-19).ToUnixTimeMilliseconds().ToString();

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.True(result, "Timestamp less than 20 minutes old should be accepted");
        }

        [Fact]
        public void ValidateTimestamp_WithAlmost20MinutesOld_ShouldReturnTrue()
        {
            // Arrange - 19 minutes and 59 seconds old
            string timestamp = DateTimeOffset.UtcNow.AddMinutes(-19).AddSeconds(-59).ToUnixTimeMilliseconds().ToString();

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.True(result, "Timestamp just under 20 minutes old should be accepted");
        }

        [Fact]
        public void ValidateTimestamp_With21MinutesOld_ShouldReturnFalse()
        {
            // Arrange
            string timestamp = DateTimeOffset.UtcNow.AddMinutes(-21).ToUnixTimeMilliseconds().ToString();

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.False(result, "Timestamp more than 20 minutes old should be rejected");
        }

        [Fact]
        public void ValidateTimestamp_With20MinutesAnd1SecondOld_ShouldReturnFalse()
        {
            // Arrange - Just over 20 minutes old
            string timestamp = DateTimeOffset.UtcNow.AddMinutes(-20).AddSeconds(-1).ToUnixTimeMilliseconds().ToString();

            // Act
            bool result = _validator.ValidateTimestamp(timestamp);

            // Assert
            Assert.False(result, "Timestamp just over 20 minutes old should be rejected");
        }

        [Fact]
        public void ValidateTimestamp_WithFutureTimestamp_ShouldReturnTrue()
        {
            // Arrange - 1 minute in the future
            string timestamp = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds().ToString();

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
        private readonly WopiProofValidator _validator;
        
        public ProofValidationTests()
        {
            _mockDiscoverer = new Mock<IDiscoverer>();
            _mockLogger = new Mock<ILogger<WopiProofValidator>>();
            _validator = new WopiProofValidator(_mockDiscoverer.Object, _mockLogger.Object);
            
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
            request.Headers[WopiHeaders.TIMESTAMP] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            
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
            request.Headers[WopiHeaders.TIMESTAMP] = DateTimeOffset.UtcNow.AddMinutes(-21).ToUnixTimeMilliseconds().ToString();
            
            // Act
            bool result = await _validator.ValidateProofAsync(request, accessToken);
            
            // Assert
            Assert.False(result, "Validation should fail with timestamp older than 20 minutes");
        }
    }
} 