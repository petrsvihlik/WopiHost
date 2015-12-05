namespace WopiHost.Abstractions
{
	/// <summary>
	/// Represents a single WOPI file or folder.
	/// </summary>
	public interface IWopiItem
    {
		/// <summary>
		/// Name of the item (for conclusive identification see the <see cref="Identifier"/>)
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Unique identifier of the item.
		/// </summary>
		string Identifier { get; }

		/// <summary>
		/// Determines type of the item.
		/// </summary>
		WopiItemType WopiItemType { get; }
    }
}
