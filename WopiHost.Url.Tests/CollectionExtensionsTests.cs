using System.Collections.Generic;
using Xunit;

namespace WopiHost.Url.Tests
{
    public class CollectionExtensionsTests
    {
        [Fact]
        public void MergeWorksWithNulls()
        {
            // Arrange
            Dictionary<string, string> full = new Dictionary<string, string>();
            Dictionary<string, string> empty = null;

            // Act
            var one = full.Merge(empty);
            var two = empty.Merge(full);

            // Assert
            Assert.Equal(full, one);
            Assert.Equal(full, two);
        }

        [Fact]
        public void MergeTwoDictionaries()
        {
            // Arrange
            Dictionary<string, string> a = new Dictionary<string, string> { { "A", "B"}, { "C", "D" } };
            Dictionary<string, string> b = new Dictionary<string, string> { { "G", "H" }, { "I", "J" } };

            // Act
            var result = a.Merge(b);

            // Assert
            Assert.Contains("A", result);
            Assert.Contains("G", result);
        }
    }
}
