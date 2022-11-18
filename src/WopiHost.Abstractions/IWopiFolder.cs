namespace WopiHost.Abstractions;

	/// <summary>
	/// Object that represents a container with files.
	/// </summary>
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
