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
		/// <param name="extension">File extension to get the URL for (without the leading dot).</param>
		/// <param name="action">Action to be performed with the file.</param>
		/// <returns>URL template with query parameter placeholders that need to be resolved.</returns>
		Task<string?> GetUrlTemplateAsync(string extension, WopiActionEnum action);

		/// <summary>
		/// Determines whether files with the given extension are supported by the WOPI client.
		/// </summary>
		/// <param name="extension">File extension to evaluate (without the leading dot).</param>
		/// <returns>True if files with the extension can be handled by the WOPI client.</returns>
		Task<bool> SupportsExtensionAsync(string extension);

		/// <summary>
		/// Determines whether the action is supported for files with the given extension.
		/// </summary>
		/// <param name="extension">File extension in question (without the leading dot).</param>
		/// <param name="action">Action to be evaluated.</param>
		/// <returns>True if the action is supported for the given file extension.</returns>
		Task<bool> SupportsActionAsync(string extension, WopiActionEnum action);

		/// <summary>
		/// Gets WOPI host requirements for the combination of action and file extension.
		/// </summary>
		/// <param name="extension">File extension to evaluate (without the leading dot).</param>
		/// <param name="action">WOPI action to evaluate.</param>
		/// <returns>A collection of requirements as strings.</returns>
		Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action);

		/// <summary>
		/// Gets the name of the application that handles files with the given extension.
		/// </summary>
		/// <param name="extension">File extension to get the app name for (without the leading dot).</param>
		/// <returns>Name of the app.</returns>
		Task<string?> GetApplicationNameAsync(string extension);

		/// <summary>
		/// Gets the icon of the application that handles files with the given extension.
		/// </summary>
		/// <param name="extension">File extension to get the icon for (without the leading dot).</param>
		/// <returns>Icon of the app.</returns>
		Task<Uri?> GetApplicationFavIconAsync(string extension);
		
		/// <summary>
		/// Gets the proof keys from the WOPI discovery XML.
		/// </summary>
		/// <returns>An object containing all proof key information.</returns>
		Task<WopiProofKeys> GetProofKeysAsync();
}