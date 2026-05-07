using WopiHost.Abstractions;

namespace WopiHost.Core.Tests.Abstractions;

public class WopiLockComparerTests
{
    public class Ordinal
    {
        private readonly OrdinalWopiLockComparer sut = new();

        [Fact]
        public void IdenticalStrings_AreEqual()
            => Assert.True(sut.AreEqual("abc", "abc"));

        [Fact]
        public void DifferentStrings_AreNotEqual()
            => Assert.False(sut.AreEqual("abc", "abd"));

        [Fact]
        public void CaseDiffers_AreNotEqual()
            => Assert.False(sut.AreEqual("abc", "ABC"));

        [Fact]
        public void BothNull_AreEqual()
            => Assert.True(sut.AreEqual(null, null));

        [Fact]
        public void OneNull_AreNotEqual()
        {
            Assert.False(sut.AreEqual(null, "abc"));
            Assert.False(sut.AreEqual("abc", null));
        }

        [Fact]
        public void Instance_IsSingleton()
            => Assert.Same(OrdinalWopiLockComparer.Instance, OrdinalWopiLockComparer.Instance);
    }

    public class JsonShaped
    {
        private readonly JsonShapedWopiLockComparer sut = new();

        [Fact]
        public void IdenticalStrings_AreEqual()
            => Assert.True(sut.AreEqual("""{"S":"abc","F":1}""", """{"S":"abc","F":1}"""));

        [Fact]
        public void SameSField_DifferentExtraProperties_AreEqual()
        {
            // The OOS quirk we're absorbing: stored lock has the original payload, the client
            // sends back the same logical lock with an extra property added.
            var stored = """{"S":"abc-123","F":4}""";
            var fromClient = """{"S":"abc-123","F":4,"V":1}""";

            Assert.True(sut.AreEqual(stored, fromClient));
        }

        [Fact]
        public void DifferentSField_AreNotEqual()
        {
            var stored = """{"S":"abc-123","F":4}""";
            var fromClient = """{"S":"different-session","F":4}""";

            Assert.False(sut.AreEqual(stored, fromClient));
        }

        [Fact]
        public void NonJsonInputs_FallBackToOrdinal()
        {
            Assert.True(sut.AreEqual("plain-token", "plain-token"));
            Assert.False(sut.AreEqual("plain-token", "different"));
        }

        [Fact]
        public void OneJsonOneNonJson_AreNotEqual()
        {
            // Asymmetric input: don't accidentally call them equivalent.
            Assert.False(sut.AreEqual("""{"S":"abc"}""", "abc"));
        }

        [Fact]
        public void MalformedJson_FallsBackToOrdinal()
        {
            // Looks like JSON but isn't parseable — must not throw.
            var malformed = "{not really json";
            Assert.True(sut.AreEqual(malformed, malformed));
            Assert.False(sut.AreEqual(malformed, "{also broken"));
        }

        [Fact]
        public void JsonWithoutSField_FallsBackToOrdinal()
        {
            var noS = """{"F":1}""";
            Assert.True(sut.AreEqual(noS, noS));
            Assert.False(sut.AreEqual(noS, """{"F":2}"""));
        }
    }
}
