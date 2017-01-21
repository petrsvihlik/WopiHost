using System;
using System.Collections.Generic;
using System.Globalization;

namespace WopiHost.Url
{
	/// <summary>
	/// Additional URL settings influencing behavior of the WOPI client (as defined in http://wopi.readthedocs.io/en/latest/discovery.html#placeholder-values)
	/// </summary>
	public class WopiUrlSettings : Dictionary<string, string>
	{
		/// <summary>
		/// Indicates that the WOPI server MAY include the preferred UI language in the format described in [RFC1766].
		/// </summary>
		public CultureInfo UI_LLCC
		{
			get { return new CultureInfo(this["UI_LLCC"]); }
			set { this["UI_LLCC"] = value.Name; }
		}

		/// <summary>
		/// Indicates that the WOPI server MAY include preferred data language in the format described in [RFC1766] for cases where language can affect data calculation.
		/// </summary>
		public CultureInfo DC_LLCC
		{
			get { return new CultureInfo(this["DC_LLCC"]); }
			set { this["DC_LLCC"] = value.Name; }
		}

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "true" to use the output of this action embedded in a web page.
		/// </summary>
		public bool EMBEDDED
		{
			get { return Convert.ToBoolean(this["EMBEDDED"]); }
			set { this["EMBEDDED"] = value.ToString(); }
		}

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "true" to prevent the attendee from navigating a file. For example, when using the attendee action (see st_wopi-action-values in section 3.1.5.1.1.2.3.1).
		/// </summary>
		public bool DISABLE_ASYNC
		{
			get { return Convert.ToBoolean(this["DISABLE_ASYNC"]); }
			set { this["DISABLE_ASYNC"] = value.ToString(); }
		}

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "true" to load a view of the document that does not create or join a broadcast session. This view looks and behaves like a regular broadcast frame.
		/// </summary>
		public bool DISABLE_BROADCAST
		{
			get { return Convert.ToBoolean(this["DISABLE_BROADCAST"]); }
			set { this["DISABLE_BROADCAST"] = value.ToString(); }
		}

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "true" to load the file type in full-screen mode.
		/// </summary>
		public bool FULLSCREEN
		{
			get { return Convert.ToBoolean(this["FULLSCREEN"]); }
			set { this["FULLSCREEN"] = value.ToString(); }
		}

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "true" to load the file type with a minimal user interface.
		/// </summary>
		public bool RECORDING
		{
			get { return Convert.ToBoolean(this["RECORDING"]); }
			set { this["RECORDING"] = value.ToString(); }
		}

		/// <summary>
		/// Indicates that the WOPI server MAY include a value to designate the theme used. Current values are "1" to indicate a light-colored theme and "2" to indicate a darker colored theme.
		/// </summary>
		public int THEME_ID
		{
			get { return Convert.ToInt32(this["THEME_ID"]); }
			set { this["THEME_ID"] = value.ToString(); }
		}

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "1" to indicate that the user is a business user.
		/// </summary>
		public int BUSINESS_USER
		{
			get { return Convert.ToInt32(this["BUSINESS_USER"]); }
			set { this["BUSINESS_USER"] = value.ToString(); }
		}

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "1" to load a view of the document that does not create or join a chat session.
		/// </summary>
		public int DISABLE_CHAT
		{
			get { return Convert.ToInt32(this["DISABLE_CHAT"]); }
			set { this["DISABLE_CHAT"] = value.ToString(); }
		}

		/// <summary>
		/// Sorry, this documentation hasn’t been written yet. https://github.com/Microsoft/Office-Online-Test-Tools-and-Documentation/issues/52
		/// </summary>
		public string PERFSTATS
		{
			get { return this["PERFSTATS"]; }
			set { this["PERFSTATS"] = value; }
		}

		/// <summary>
		/// This value is used to run the WOPI Validation application in different modes.
		/// This value can be set to All, OfficeOnline or OfficeNativeClient to activate tests specific to Office Online and Office for iOS.If omitted, the default value is All.
		/// All: activates all WOPI Validation application tests.
		/// OfficeOnline: activates all tests necessary for Office Online integration.
		/// OfficeNativeClient: activates all tests necessary for Office for iOS integration.
		/// </summary>
		public string VALIDATOR_TEST_CATEGORY
		{
			get { return this["VALIDATOR_TEST_CATEGORY"]; }
			set { this["VALIDATOR_TEST_CATEGORY"] = value; }
		}

		public WopiUrlSettings()
		{

		}

		public WopiUrlSettings(IDictionary<string, string> settings)
		{
			if (settings != null)
			{
				foreach (KeyValuePair<string, string> pair in settings)
				{
					Add(pair.Key, pair.Value);
				}
			}
		}
	}
}
