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
		public CultureInfo UiLlcc
		{
			get => new CultureInfo(this["UI_LLCC"]);
            set => this["UI_LLCC"] = value.Name;
        }

		/// <summary>
		/// Indicates that the WOPI server MAY include preferred data language in the format described in [RFC1766] for cases where language can affect data calculation.
		/// </summary>
		public CultureInfo DcLlcc
		{
			get => new CultureInfo(this["DC_LLCC"]);
            set => this["DC_LLCC"] = value.Name;
        }

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "true" to use the output of this action embedded in a web page.
		/// </summary>
		public bool Embedded
		{
			get => Convert.ToBoolean(this["EMBEDDED"], CultureInfo.InvariantCulture);
            set => this["EMBEDDED"] = value.ToString(CultureInfo.InvariantCulture);
        }

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "true" to prevent the attendee from navigating a file. For example, when using the attendee action (see st_wopi-action-values in section 3.1.5.1.1.2.3.1).
		/// </summary>
		public bool DisableAsync
		{
			get => Convert.ToBoolean(this["DISABLE_ASYNC"], CultureInfo.InvariantCulture);
            set => this["DISABLE_ASYNC"] = value.ToString(CultureInfo.InvariantCulture);
        }

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "true" to load a view of the document that does not create or join a broadcast session. This view looks and behaves like a regular broadcast frame.
		/// </summary>
		public bool DisableBroadcast
		{
			get => Convert.ToBoolean(this["DISABLE_BROADCAST"], CultureInfo.InvariantCulture);
            set => this["DISABLE_BROADCAST"] = value.ToString(CultureInfo.InvariantCulture);
        }

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "true" to load the file type in full-screen mode.
		/// </summary>
		public bool Fullscreen
		{
			get => Convert.ToBoolean(this["FULLSCREEN"], CultureInfo.InvariantCulture);
            set => this["FULLSCREEN"] = value.ToString(CultureInfo.InvariantCulture);
        }

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "true" to load the file type with a minimal user interface.
		/// </summary>
		public bool Recording
		{
			get => Convert.ToBoolean(this["RECORDING"], CultureInfo.InvariantCulture);
            set => this["RECORDING"] = value.ToString(CultureInfo.InvariantCulture);
        }

		/// <summary>
		/// Indicates that the WOPI server MAY include a value to designate the theme used. Current values are "1" to indicate a light-colored theme and "2" to indicate a darker colored theme.
		/// </summary>
		public int ThemeId
		{
			get => Convert.ToInt32(this["THEME_ID"], CultureInfo.InvariantCulture);
            set => this["THEME_ID"] = value.ToString(CultureInfo.InvariantCulture);
        }

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "1" to indicate that the user is a business user.
		/// </summary>
		public int BusinessUser
		{
			get => Convert.ToInt32(this["BUSINESS_USER"], CultureInfo.InvariantCulture);
            set => this["BUSINESS_USER"] = value.ToString(CultureInfo.InvariantCulture);
        }

		/// <summary>
		/// Indicates that the WOPI server MAY include the value "1" to load a view of the document that does not create or join a chat session.
		/// </summary>
		public int DisableChat
		{
			get => Convert.ToInt32(this["DISABLE_CHAT"], CultureInfo.InvariantCulture);
            set => this["DISABLE_CHAT"] = value.ToString(CultureInfo.InvariantCulture);
        }

		/// <summary>
		/// Sorry, this documentation hasn't been written yet. https://github.com/Microsoft/Office-Online-Test-Tools-and-Documentation/issues/52
		/// </summary>
		public string Perfstats
		{
			get => this["PERFSTATS"];
            set => this["PERFSTATS"] = value;
        }

		/// <summary>
		/// This value is used to run the WOPI Validation application in different modes.
		/// This value can be set to All, OfficeOnline or OfficeNativeClient to activate tests specific to Office Online and Office for iOS.If omitted, the default value is All.
		/// All: activates all WOPI Validation application tests.
		/// OfficeOnline: activates all tests necessary for Office Online integration.
		/// OfficeNativeClient: activates all tests necessary for Office for iOS integration.
		/// </summary>
		public string ValidatorTestCategory
		{
			get => this["VALIDATOR_TEST_CATEGORY"];
            set => this["VALIDATOR_TEST_CATEGORY"] = value;
        }

		public WopiUrlSettings()
		{

		}

		public WopiUrlSettings(IDictionary<string, string> settings)
		{
			if (settings != null)
			{
				foreach (var pair in settings)
				{
					Add(pair.Key, pair.Value);
				}
			}
		}
	}
}
