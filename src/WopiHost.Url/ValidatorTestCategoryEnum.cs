namespace WopiHost.Url;

/// <summary>
/// Specifies which test suite the WOPI Validation application runs.
/// </summary>
/// <remarks>
/// Passed through the <c>VALIDATOR_TEST_CATEGORY</c> URL placeholder. When omitted, the validator uses <see cref="All"/>.
/// </remarks>
public enum ValidatorTestCategoryEnum
{
    /// <summary>
    /// Activates all WOPI Validation application tests.
    /// </summary>
    All,
    /// <summary>
    /// Activates all tests necessary for Office Online integration.
    /// </summary>
    OfficeOnline,
    /// <summary>
    /// Activates all tests necessary for Office for iOS integration.
    /// </summary>
    OfficeNativeClient
}