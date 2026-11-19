using System.Collections.Generic;

namespace SebWindowsConfig.Entities
{
	/// <summary>
	/// Tools and permissions a teacher can switch on or off for students in the quick setup. Only options the Windows
	/// client actually reads are offered here.
	/// </summary>
	internal enum StudentOption
	{
		SpellCheck,
		Zoom,
		Find,
		Reload,
		BackForward,
		Print,
		Downloads,
		Uploads,
		VolumeControl,
		Clock
	}

	internal class StudentOptionInfo
	{
		internal StudentOption Option { get; private set; }
		internal string Group { get; private set; }
		internal string Title { get; private set; }
		internal string Description { get; private set; }

		private StudentOptionInfo(StudentOption option, string group, string title, string description)
		{
			Option = option;
			Group = group;
			Title = title;
			Description = description;
		}

		internal static IReadOnlyList<StudentOptionInfo> All { get; } = new[]
		{
			new StudentOptionInfo(StudentOption.SpellCheck, "Reading and writing", "Spell checker", "Underlines misspelled words while typing (English only on Windows)."),
			new StudentOptionInfo(StudentOption.Zoom, "Reading and writing", "Zoom in and out", "Make text and pages larger or smaller."),
			new StudentOptionInfo(StudentOption.Find, "Reading and writing", "Find on page", "Search for words on the exam page."),
			new StudentOptionInfo(StudentOption.Reload, "Browsing", "Reload button", "Lets students reload a page that stopped working."),
			new StudentOptionInfo(StudentOption.BackForward, "Browsing", "Back and forward", "Go back to the previous page and forward again."),
			new StudentOptionInfo(StudentOption.Print, "Browsing", "Printing", "Print exam pages."),
			new StudentOptionInfo(StudentOption.Downloads, "Files", "Download files", "Save files from the exam page, for example a PDF handout."),
			new StudentOptionInfo(StudentOption.Uploads, "Files", "Upload files", "Hand in files, for example an essay or a photo of their work."),
			new StudentOptionInfo(StudentOption.VolumeControl, "Taskbar", "Volume control", "Shows a volume control in SEB's taskbar."),
			new StudentOptionInfo(StudentOption.Clock, "Taskbar", "Clock", "Shows the time in SEB's taskbar.")
		};
	}
}
