using System;

namespace WopiHost.Core.Models
{
	public class LockInfo
	{
		public string Lock { get; set; }

		public DateTime DateCreated { get; set; }

        public bool Expired => DateCreated.AddMinutes(30) < DateTime.UtcNow;
    }
}
