using System.Collections.Generic;

namespace WopiHost.Contracts
{
	public interface IWopiFileProvider
	{
        /// <summary>
        /// Returns a concrete instance of an implementation of the <see cref="IWopiFile"/>.
        /// </summary>
        /// <param name="identifier">Generic string identifier of a file (typically some kind of a path).</param>
        /// <returns>Instance of a file.</returns>
		IWopiFile GetWopiFile(string identifier);

	    List<IWopiFile> GetWopiFiles();
	}
}