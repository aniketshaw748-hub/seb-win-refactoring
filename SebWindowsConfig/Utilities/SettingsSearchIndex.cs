using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SebWindowsConfig.Utilities
{
	/// <summary>
	/// One searchable setting: the visible text of a control on one of the settings pages, and where it lives.
	/// </summary>
	internal class SettingsSearchEntry
	{
		internal string Text { get; set; }
		internal string Location { get; set; }
		internal string Keywords { get; set; }
		internal Control Control { get; set; }
		internal TabPage Page { get; set; }

		/// <summary>
		/// The nested tab pages (outermost first) that have to be selected to show the control, e.g. Network > Filter.
		/// </summary>
		internal IList<TabPage> NestedPages { get; set; }
	}

	/// <summary>
	/// Indexes the labels, check boxes, radio buttons, group boxes and nested tabs of the settings pages, so a setting
	/// can be found by typing part of its name.
	/// </summary>
	internal class SettingsSearchIndex
	{
		private const int MaximumLabelLength = 70;

		private readonly List<SettingsSearchEntry> entries = new List<SettingsSearchEntry>();

		internal SettingsSearchIndex(IEnumerable<TabPage> pages, ToolTip toolTip)
		{
			foreach (var page in pages)
			{
				Collect(page, page, new List<TabPage>(), toolTip);
			}
		}

		internal IList<SettingsSearchEntry> Search(string query, int limit = 60)
		{
			var words = Normalize(query).ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

			if (words.Length == 0)
			{
				return new List<SettingsSearchEntry>();
			}

			var phrase = string.Join(" ", words);

			return entries
				.Select(e => new { Entry = e, Text = e.Text.ToLowerInvariant(), Location = (e.Location + " " + e.Keywords).ToLowerInvariant() })
				.Where(e => words.All(w => e.Text.Contains(w) || e.Location.Contains(w)))
				.Select(e => new { e.Entry, Rank = Rank(e.Text, phrase, words) })
				.OrderBy(e => e.Rank)
				.ThenBy(e => e.Entry.Text.Length)
				.Take(limit)
				.Select(e => e.Entry)
				.ToList();
		}

		private static int Rank(string text, string phrase, string[] words)
		{
			if (text.StartsWith(phrase))
			{
				return 0;
			}

			if (text.Contains(phrase))
			{
				return 1;
			}

			if (words.All(w => text.Contains(w)))
			{
				return 2;
			}

			return 3;
		}

		private void Collect(Control parent, TabPage page, List<TabPage> nested, ToolTip toolTip)
		{
			foreach (Control control in parent.Controls)
			{
				// Controls the editor hides on purpose (e.g. retired features) must not show up as results.
				if (!IsShownByItself(control))
				{
					continue;
				}

				if (control is TabControl tabs)
				{
					foreach (TabPage nestedPage in tabs.TabPages)
					{
						var path = new List<TabPage>(nested) { nestedPage };

						Add(nestedPage, Normalize(nestedPage.Text), page, path);
						Collect(nestedPage, page, path, toolTip);
					}

					continue;
				}

				if (IsSearchable(control))
				{
					var text = Normalize(control.Text);
					var tip = Normalize(toolTip?.GetToolTip(control));

					// Long labels are explanations under a setting, not setting names; listing them only adds noise.
					if (text.Length > 1 && !(control is Label && text.Length > MaximumLabelLength))
					{
						Add(control, text, page, nested, tip);
					}
				}

				if (control.HasChildren)
				{
					Collect(control, page, nested, toolTip);
				}
			}
		}

		private void Add(Control control, string text, TabPage page, IList<TabPage> nested, string tip = null)
		{
			var location = Normalize(page.Text) + string.Concat(nested.Select(p => " > " + Normalize(p.Text)));

			// Several labels repeat the same text on one page (e.g. "Port"); one result per page is enough.
			if (entries.Any(e => e.Page == page && e.Text == text))
			{
				return;
			}

			entries.Add(new SettingsSearchEntry
			{
				Text = text,
				Location = location,
				Keywords = tip ?? "",
				Control = control,
				Page = page,
				NestedPages = nested.ToList()
			});
		}

		/// <summary>
		/// <see cref="Control.Visible"/> is also false for every control on a page that isn't selected, so the control's
		/// own visibility flag is read instead (what <c>Visible = false</c> sets).
		/// </summary>
		/// <summary>
		/// True if neither the control nor any of its parents up to the settings page is hidden on purpose.
		/// </summary>
		internal static bool IsShownOnPage(Control control, TabPage page)
		{
			for (var current = control; current != null && current != page; current = current.Parent)
			{
				if (!IsShownByItself(current))
				{
					return false;
				}
			}

			return true;
		}

		private static bool IsShownByItself(Control control)
		{
			const int STATE_VISIBLE = 0x2;

			// A tab control hides every page that isn't selected; that is not hiding a setting on purpose.
			if (control is TabPage)
			{
				return true;
			}

			return GetState == null || (bool) GetState.Invoke(control, new object[] { STATE_VISIBLE });
		}

		private static readonly MethodInfo GetState = typeof(Control).GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);

		private static bool IsSearchable(Control control)
		{
			return control is CheckBox || control is RadioButton || control is Label || control is GroupBox || control is Button;
		}

		internal static string Normalize(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return "";
			}

			return Regex.Replace(text.Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&"), @"\s+", " ").Trim().TrimEnd(':').Trim();
		}
	}
}
