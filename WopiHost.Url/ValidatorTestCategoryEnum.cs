#region Header
// ValidatorTestCategoryEnum.cs
// Developed by Taygun Savaş
// taygun.savas@triflesgames.com
#endregion

namespace WopiHost.Url
{
    /// <summary>
    /// This value is used to run the WOPI Validation application in different modes.
    /// This value can be set to All, OfficeOnline or OfficeNativeClient to activate tests specific to Office Online and Office for iOS.If omitted, the default value is All.
    /// </summary>
    public enum ValidatorTestCategoryEnum
    {
        /// <summary>
        /// All: activates all WOPI Validation application tests. 
        /// </summary>
        All,
        /// <summary>
        /// OfficeOnline: activates all tests necessary for Office Online integration.
        /// </summary>
        OfficeOnline,
        /// <summary>
        /// OfficeNativeClient: activates all tests necessary for Office for iOS integration.
        /// </summary>
        OfficeNativeClient
    }

}