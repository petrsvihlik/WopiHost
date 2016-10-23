using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Results
{
	public class ConflictResult: StatusCodeResult
	{
		/// <summary>
		/// Indicates that the request could not be processed because of conflict in the request, such as an edit conflict between multiple simultaneous updates.
		/// </summary>
		public ConflictResult() : base(409)
		{
		}
	}
}
