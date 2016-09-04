namespace WopiHost.Abstractions
{
	public interface IWopiFolder
	{
		/// <summary>
		/// Name of the folder (for conclusive identification see the <see cref="Identifier"/>)
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Unique identifier of the folder.
		/// </summary>
		string Identifier { get; }
	}
}
