using System.Collections.Generic;
using System.Threading.Tasks;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery
{
	public interface IDiscoverer
	{
		Task<string> GetUrlTemplateAsync(string extension, WopiActionEnum action);
		Task<bool> SupportsExtensionAsync(string extension);
		Task<bool> SupportsActionAsync(string extension, WopiActionEnum action);
		Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action);
		Task<bool> RequiresCobaltAsync(string extension, WopiActionEnum action);
		Task<string> GetApplicationNameAsync(string extension);
	}
}