using System.Collections.Generic;
using Xunit;

namespace WopiHost.Url.Tests
{
    public class CollectionExtensionsTests
    {
        [Fact]
        public void MergeWorksWithNulls()
        {
            Dictionary<string, string> full = new Dictionary<string, string>();
            Dictionary<string, string> empty = null;

            var one = full.Merge(empty);
            var two = empty.Merge(full);

            Assert.Equal(full, one);
            Assert.Equal(full, two);
        }
    }
}
