using Microsoft.AspNetCore.Mvc;

namespace WopiHost.Core.Results
{
	public class NotImplementedResult: StatusCodeResult
	{
		/// <summary>
		/// The server either does not recognize the request method, or it lacks the ability to fulfill the request.
		/// </summary>
		public NotImplementedResult() : base(501)
		{
		}
	}
}
