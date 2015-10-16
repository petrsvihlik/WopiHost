namespace WopiHost.Abstractions
{
	/// <summary>
	/// Performs security-related actions.
	/// </summary>
	public interface IWopiSecurityHandler
	{
		/// <summary>
		/// Validates the given value against the authroization token.
		/// </summary>
		/// <param name="value">Value to validate</param>
		/// <param name="token">Authorization token</param>
		/// <returns>TRUE if the token is valid.</returns>
		bool ValidateAccessToken(string value, string token);

		/// <summary>
		/// Generates authorization token for the given value.
		/// </summary>
		/// <param name="value"></param>
		/// <returns>Authorization token</returns>
		string GenerateAccessToken(string value);
	}
}
