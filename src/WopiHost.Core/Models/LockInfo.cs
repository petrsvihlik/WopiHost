namespace WopiHost.Core.Models;

/// <summary>
/// Represents an editing file lock.
/// </summary>
public class LockInfo
	{
		/// <summary>
		/// Lock identifier.
		/// </summary>
		public string Lock { get; set; }

		/// <summary>
		/// Lock timestamp.
		/// </summary>
		public DateTime DateCreated { get; set; }

		/// <summary>
		/// Determines whether the lock is expired.
		/// </summary>
    public bool Expired => DateCreated.AddMinutes(30) < DateTime.UtcNow;
}
