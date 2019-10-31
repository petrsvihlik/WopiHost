namespace WopiHost.Core
{
    public class WopiOverrideHeaderAttribute : HttpHeaderAttribute
    {
        public WopiOverrideHeaderAttribute(string[] values) : base(WopiHeaders.WopiOverride, values)
        {
        }
    }
}
