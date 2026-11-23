using WopiHost.Discovery.Enumerations;
using WopiHost.Discovery.Models;

namespace WopiHost.Discovery;

/// <summary>
/// Provides information about the capabilities of a WOPI client.
/// </summary>
public interface IDiscoverer
	{
		/// <summary>
		/// Gets the URL template for the given file extension and action.
		/// </summary>
		/// <param name="extension">The file extension to get the URL for (without the leading dot).</param>
		/// <param name="action">The action to be performed with the file.</param>
		/// <returns>The URL template with query parameter placeholders that need to be resolved, or <see langword="null"/> if the action is not available for the extension.</returns>
		Task<string?> GetUrlTemplateAsync(string extension, WopiActionEnum action);

		/// <summary>
		/// Determines whether files with the given extension are supported by the WOPI client.
		/// </summary>
		/// <param name="extension">The file extension to evaluate (without the leading dot).</param>
		/// <returns><see langword="true"/> if files with the extension can be handled by the WOPI client; otherwise, <see langword="false"/>.</returns>
		Task<bool> SupportsExtensionAsync(string extension);

		/// <summary>
		/// Determines whether the action is supported for files with the given extension.
		/// </summary>
		/// <param name="extension">The file extension in question (without the leading dot).</param>
		/// <param name="action">The action to evaluate.</param>
		/// <returns><see langword="true"/> if the action is supported for the given file extension; otherwise, <see langword="false"/>.</returns>
		Task<bool> SupportsActionAsync(string extension, WopiActionEnum action);

		/// <summary>
		/// Gets WOPI host requirements for the combination of action and file extension.
		/// </summary>
		/// <param name="extension">The file extension to evaluate (without the leading dot).</param>
		/// <param name="action">The WOPI action to evaluate.</param>
		/// <returns>The values of the action's <c>requires</c> attribute (for example <c>locks</c>, <c>update</c>, <c>cobalt</c>), or an empty collection if there are none or the action is not available.</returns>
		Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action);

		/// <summary>
		/// Gets the name of the application that handles files with the given extension.
		/// </summary>
		/// <param name="extension">The file extension to get the app name for (without the leading dot).</param>
		/// <returns>The name of the app, or <see langword="null"/> if no app handles the extension.</returns>
		Task<string?> GetApplicationNameAsync(string extension);

		/// <summary>
		/// Gets the icon of the application that handles files with the given extension.
		/// </summary>
		/// <param name="extension">The file extension to get the icon for (without the leading dot).</param>
		/// <returns>The URL of the app's favicon, or <see langword="null"/> if no app handles the extension or it has no favicon.</returns>
		Task<Uri?> GetApplicationFavIconAsync(string extension);
		
		/// <summary>
		/// Gets the proof keys from the WOPI discovery XML.
		/// </summary>
		/// <returns>The current and old proof keys; properties are <see langword="null"/> when the discovery XML omits them.</returns>
		Task<WopiProofKeys> GetProofKeysAsync();
}