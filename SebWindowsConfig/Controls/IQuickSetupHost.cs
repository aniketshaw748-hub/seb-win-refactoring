using System.Collections.Generic;
using SebWindowsConfig.Entities;

namespace SebWindowsConfig.Controls
{
	internal enum PasswordKind
	{
		Quit,
		Administrator,
		File
	}

	/// <summary>
	/// What the quick setup needs from the configuration editor. The editor applies every change through its existing
	/// widgets, so the quick setup and the full settings view always show the same configuration.
	/// </summary>
	internal interface IQuickSetupHost
	{
		string StartUrl { get; set; }
		bool AllowQuit { get; set; }

		/// <summary>
		/// Whether SEB sends its Browser Exam Key and Configuration Key to the exam site, so e.g. Moodle can verify SEB.
		/// </summary>
		bool SendsExamKeys { get; set; }
		bool StartsExam { get; set; }

		/// <summary>
		/// True if the loaded settings contain this password (only its hash or the encrypted file is known).
		/// </summary>
		bool HasStoredPassword(PasswordKind kind);

		/// <summary>
		/// True if a password of this kind is set, either stored or newly typed.
		/// </summary>
		bool HasPassword(PasswordKind kind);

		/// <summary>
		/// Sets a new password. Two empty strings keep a stored password unchanged.
		/// </summary>
		void SetPassword(PasswordKind kind, string password, string confirmation);

		/// <summary>
		/// Removes a password of this kind, including a stored one.
		/// </summary>
		void RemovePassword(PasswordKind kind);

		bool GetOption(StudentOption option);
		void SetOption(StudentOption option, bool enabled);

		/// <summary>
		/// Which websites students can open (the URL filter).
		/// </summary>
		SiteAccess SiteAccess { get; }

		/// <summary>
		/// Turns the URL filter on (only the given sites, plus the start URL) or off (any website), replacing its rules.
		/// </summary>
		void SetSiteAccess(bool restricted, IEnumerable<string> allowedSites);

		SecurityPreset CurrentPreset { get; }
		void ApplyPreset(SecurityPreset preset);

		void SaveAs();
		void ShowAllSettings();
	}
}
