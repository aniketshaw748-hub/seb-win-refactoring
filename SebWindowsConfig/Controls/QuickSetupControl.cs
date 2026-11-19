using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SebWindowsConfig.Entities;
using SebWindowsConfig.UserInterface;

namespace SebWindowsConfig.Controls
{
	/// <summary>
	/// The guided five step setup shown when the configuration tool starts. It covers what most exams need: the exam link,
	/// how students may leave, which tools they get, how locked down the computer is and saving the file. Every change is
	/// applied through <see cref="IQuickSetupHost"/>, so the full settings view always reflects the same configuration.
	/// </summary>
	internal class QuickSetupControl : UserControl
	{
		private const int RailWidth = 280;
		private const int PageWidth = 640;

		private static readonly string[] StepTitles = { "Exam link", "Leaving the exam", "Tools for students", "Lockdown level", "Save and share" };

		private readonly IQuickSetupHost host;
		private readonly List<StepButton> stepButtons = new List<StepButton>();
		private readonly List<Control> pages = new List<Control>();
		private readonly Dictionary<StudentOption, ToggleSwitch> optionSwitches = new Dictionary<StudentOption, ToggleSwitch>();
		private readonly Dictionary<SecurityPreset, CardRadioButton> presetCards = new Dictionary<SecurityPreset, CardRadioButton>();

		private Panel pageHost;
		private Button backButton;
		private Button nextButton;
		private Label stepCounter;
		private int currentStep;
		private bool refreshing;

		private TextBox urlTextBox;
		private Label urlStatus;
		private Button urlFixButton;
		private Button urlOpenButton;

		private ToggleSwitch allowQuitSwitch;
		private PasswordFields quitPassword;
		private PasswordFields adminPassword;

		private Label customPresetNote;

		private CardRadioButton startExamCard;
		private CardRadioButton configureClientCard;
		private PasswordFields filePassword;
		private FlowLayoutPanel summary;

		internal QuickSetupControl(IQuickSetupHost host)
		{
			this.host = host;

			BackColor = Theme.Background;
			Dock = DockStyle.Fill;
			Font = Theme.Body;

			BuildLayout();
			ShowStep(0);
		}

		/// <summary>
		/// Reads the current configuration from the host into all steps, e.g. after a file was opened.
		/// </summary>
		internal void RefreshFromHost()
		{
			refreshing = true;

			try
			{
				urlTextBox.Text = host.StartUrl ?? "";
				allowQuitSwitch.Checked = host.AllowQuit;
				quitPassword.Refresh(host);
				adminPassword.Refresh(host);
				filePassword.Refresh(host);

				foreach (var entry in optionSwitches)
				{
					entry.Value.Checked = host.GetOption(entry.Key);
				}

				RefreshPresetCards();

				startExamCard.Checked = host.StartsExam;
				configureClientCard.Checked = !host.StartsExam;
			}
			finally
			{
				refreshing = false;
			}

			UpdateUrlStatus();
			UpdateQuitFields();
			UpdateSummary();
			UpdateRail();
		}

		/// <summary>
		/// Reads one password from the host again, leaving the other password fields as they are.
		/// </summary>
		internal void RefreshPassword(PasswordKind kind)
		{
			var fields = kind == PasswordKind.Quit ? quitPassword : kind == PasswordKind.Administrator ? adminPassword : filePassword;
			var wasRefreshing = refreshing;

			refreshing = true;

			try
			{
				fields.Refresh(host);
			}
			finally
			{
				refreshing = wasRefreshing;
			}

			UpdateSummary();
			UpdateRail();
		}

		/// <summary>
		/// Puts the keyboard focus on the first thing to fill in on the current step.
		/// </summary>
		internal void FocusCurrentStep()
		{
			if (currentStep == 0)
			{
				urlTextBox.Focus();
				urlTextBox.SelectionStart = urlTextBox.TextLength;
			}
			else
			{
				pages[currentStep].SelectNextControl(null, true, true, true, false);
			}
		}

		internal void ShowStep(int step)
		{
			currentStep = Math.Max(0, Math.Min(step, pages.Count - 1));

			for (var i = 0; i < pages.Count; i++)
			{
				pages[i].Visible = i == currentStep;
			}

			pageHost.AutoScrollPosition = new Point(0, 0);
			backButton.Visible = currentStep > 0;
			nextButton.Text = currentStep == pages.Count - 1 ? "Save exam file..." : "Next: " + StepTitles[currentStep + 1];
			stepCounter.Text = $"Step {currentStep + 1} of {pages.Count}";

			if (currentStep == pages.Count - 1)
			{
				UpdateSummary();
			}

			UpdateRail();
		}

		private void BuildLayout()
		{
			var rail = BuildRail();
			var footer = BuildFooter();

			pageHost = new Panel
			{
				AutoScroll = true,
				BackColor = Theme.Background,
				Dock = DockStyle.Fill,
				Padding = new Padding(40, 28, 24, 24)
			};

			pages.Add(BuildExamLinkPage());
			pages.Add(BuildLeavingPage());
			pages.Add(BuildToolsPage());
			pages.Add(BuildLockdownPage());
			pages.Add(BuildSavePage());

			foreach (var page in pages)
			{
				page.Location = new Point(40, 28);
				pageHost.Controls.Add(page);
			}

			Controls.Add(pageHost);
			Controls.Add(footer);
			Controls.Add(rail);
		}

		private Control BuildRail()
		{
			var rail = new Panel
			{
				BackColor = Theme.Surface,
				Dock = DockStyle.Left,
				Padding = new Padding(20, 24, 20, 20),
				Width = RailWidth
			};

			var list = new FlowLayoutPanel
			{
				AutoSize = true,
				BackColor = Theme.Surface,
				FlowDirection = FlowDirection.TopDown,
				Location = new Point(20, 24),
				WrapContents = false
			};

			list.Controls.Add(Theme.Label("Quick setup", Theme.Subtitle));
			list.Controls.Add(Spacer(4));
			list.Controls.Add(Theme.WrappingLabel("Set up an exam in five steps. You can change anything later.", RailWidth - 40, Theme.Small, Theme.MutedText));
			list.Controls.Add(Spacer(18));

			for (var i = 0; i < StepTitles.Length; i++)
			{
				var index = i;
				var button = new StepButton(i + 1, StepTitles[i], RailWidth - 40);

				button.Click += (o, e) => ShowStep(index);
				stepButtons.Add(button);
				list.Controls.Add(button);
			}

			var allSettings = Theme.LinkButton("Show all settings");

			allSettings.Click += (o, e) => host.ShowAllSettings();

			var allSettingsHint = Theme.WrappingLabel("Every SEB option, for experienced users.", RailWidth - 40, Theme.Small, Theme.MutedText);

			list.Controls.Add(Spacer(14));
			list.Controls.Add(allSettings);
			list.Controls.Add(Spacer(2));
			list.Controls.Add(allSettingsHint);

			rail.Controls.Add(list);
			rail.Paint += (o, e) =>
			{
				using (var pen = new Pen(Theme.Border))
				{
					e.Graphics.DrawLine(pen, rail.Width - 1, 0, rail.Width - 1, rail.Height);
				}
			};

			return rail;
		}

		private Control BuildFooter()
		{
			var footer = new Panel
			{
				BackColor = Theme.Surface,
				Dock = DockStyle.Bottom,
				Height = 60,
				Padding = new Padding(24, 12, 24, 12)
			};

			stepCounter = Theme.Label("", Theme.Body, Theme.MutedText);
			stepCounter.Location = new Point(24, 20);

			backButton = Theme.SecondaryButton("Back");
			nextButton = Theme.PrimaryButton("Next");

			backButton.Click += (o, e) => ShowStep(currentStep - 1);
			nextButton.Click += (o, e) => Next();

			var buttons = new FlowLayoutPanel
			{
				AutoSize = true,
				BackColor = Theme.Surface,
				Dock = DockStyle.Right,
				FlowDirection = FlowDirection.LeftToRight,
				WrapContents = false
			};

			backButton.Margin = new Padding(0, 0, 8, 0);
			nextButton.Margin = new Padding(0);
			buttons.Controls.Add(backButton);
			buttons.Controls.Add(nextButton);

			footer.Controls.Add(buttons);
			footer.Controls.Add(stepCounter);
			footer.Paint += (o, e) =>
			{
				using (var pen = new Pen(Theme.Border))
				{
					e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
				}
			};

			return footer;
		}

		private void Next()
		{
			if (currentStep < pages.Count - 1)
			{
				ShowStep(currentStep + 1);
			}
			else
			{
				Save();
			}
		}

		private void Save()
		{
			if (ConfirmExamLink())
			{
				host.SaveAs();
			}
		}

		/// <summary>
		/// Before saving: warns if the exam link is missing or invalid. Returns false if the user wants to fix it first.
		/// </summary>
		internal bool ConfirmExamLink()
		{
			if (!IsValidUrl(urlTextBox.Text))
			{
				var answer = MessageBox.Show(this, "The exam link is missing or doesn't look like a web address, so students may not reach the exam.\n\nSave the file anyway?", "Check the exam link", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

				if (answer != DialogResult.Yes)
				{
					ShowStep(0);
					urlTextBox.Focus();

					return false;
				}
			}

			return true;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Step 1: Exam link
		// ------------------------------------------------------------------------------------------------------------

		private Control BuildExamLinkPage()
		{
			var page = Page("Where is the exam?", "Paste the link students should open first, for example your Moodle quiz, your exam system's login page or a Google Form.");

			page.Controls.Add(FieldLabel("Exam link (start URL)"));

			var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 8), WrapContents = false };

			urlTextBox = Theme.TextBox(470);
			urlTextBox.AccessibleName = "Exam link";
			urlTextBox.Margin = new Padding(0, 2, 8, 0);
			urlTextBox.TextChanged += (o, e) =>
			{
				if (!refreshing)
				{
					host.StartUrl = urlTextBox.Text.Trim();
					UpdateRail();
				}

				UpdateUrlStatus();
			};

			urlOpenButton = Theme.SecondaryButton("Open link");
			urlOpenButton.Margin = new Padding(0);
			urlOpenButton.Click += (o, e) => OpenUrl();

			row.Controls.Add(urlTextBox);
			row.Controls.Add(urlOpenButton);
			page.Controls.Add(row);

			var statusRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 20), WrapContents = false };

			urlStatus = Theme.Label("", Theme.Body);
			urlStatus.Margin = new Padding(0, 6, 8, 0);

			urlFixButton = Theme.SecondaryButton("Fix it");
			urlFixButton.MinimumSize = new Size(70, 30);
			urlFixButton.Height = 30;
			urlFixButton.Click += (o, e) =>
			{
				urlTextBox.Text = SuggestUrl(urlTextBox.Text);
				urlTextBox.Focus();
			};

			statusRow.Controls.Add(urlStatus);
			statusRow.Controls.Add(urlFixButton);
			page.Controls.Add(statusRow);

			page.Controls.Add(Tip("Tip: open the link once with \"Open link\" to check it is the right page before you save the file."));

			return page;
		}

		private void UpdateUrlStatus()
		{
			var text = urlTextBox.Text.Trim();
			var suggestion = SuggestUrl(text);

			urlFixButton.Visible = false;
			urlOpenButton.Enabled = IsValidUrl(text);

			if (text.Length == 0)
			{
				SetStatus(urlStatus, "Add the link students should open first.", Theme.MutedText);
			}
			else if (IsValidUrl(text))
			{
				SetStatus(urlStatus, "✓  Looks good", Theme.Success);
			}
			else if (suggestion != text && IsValidUrl(suggestion))
			{
				SetStatus(urlStatus, $"Did you mean {Shorten(suggestion, 60)}?", Theme.Warning);
				urlFixButton.Visible = true;
			}
			else
			{
				SetStatus(urlStatus, "This doesn't look like a web address. It should start with https://", Theme.Warning);
			}
		}

		private void OpenUrl()
		{
			var url = urlTextBox.Text.Trim();

			if (!IsValidUrl(url))
			{
				return;
			}

			try
			{
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			catch (Exception e)
			{
				MessageBox.Show(this, "The link couldn't be opened: " + e.Message, "Open link", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		internal static bool IsValidUrl(string text)
		{
			if (string.IsNullOrWhiteSpace(text) || text.Any(char.IsWhiteSpace))
			{
				return false;
			}

			if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
			{
				return false;
			}

			if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
			{
				return false;
			}

			return uri.Host.Contains('.') || uri.IsLoopback;
		}

		internal static string SuggestUrl(string text)
		{
			var trimmed = (text ?? "").Trim();

			if (trimmed.Length == 0 || trimmed.Contains("://"))
			{
				return trimmed;
			}

			return "https://" + trimmed.TrimStart('/');
		}

		// ------------------------------------------------------------------------------------------------------------
		// Step 2: Leaving the exam
		// ------------------------------------------------------------------------------------------------------------

		private Control BuildLeavingPage()
		{
			var page = Page("How can students leave the exam?", "SEB stays open until the exam is over. Decide who can close it.");

			allowQuitSwitch = new ToggleSwitch("Students can quit SEB", "When this is off, students can't close SEB themselves. Put a quit link on the last page of your exam instead (All settings > Exam).", PageWidth);
			allowQuitSwitch.CheckedChanged += (o, e) =>
			{
				if (!refreshing)
				{
					host.AllowQuit = allowQuitSwitch.Checked;
				}

				UpdateQuitFields();
				UpdateRail();
			};

			page.Controls.Add(allowQuitSwitch);
			page.Controls.Add(Spacer(6));

			quitPassword = new PasswordFields(PasswordKind.Quit, "Quit password (optional)", "Anyone quitting SEB has to type this. Give it only to supervisors, so students can't leave the exam early.", PageWidth, OnPasswordChanged);
			adminPassword = new PasswordFields(PasswordKind.Administrator, "Administrator password (optional)", "Protects SEB's settings on the exam computers. It is needed to change or replace them later.", PageWidth, OnPasswordChanged);

			page.Controls.Add(quitPassword.Panel);
			page.Controls.Add(Spacer(18));
			page.Controls.Add(adminPassword.Panel);

			return page;
		}

		private void UpdateQuitFields()
		{
			quitPassword.Panel.Enabled = allowQuitSwitch.Checked;
		}

		private void OnPasswordChanged(PasswordFields fields)
		{
			if (!refreshing)
			{
				host.SetPassword(fields.Kind, fields.Password, fields.Confirmation);
			}

			fields.UpdateStatus(host);
			UpdateRail();
			UpdateSummary();
		}

		// ------------------------------------------------------------------------------------------------------------
		// Step 3: Tools for students
		// ------------------------------------------------------------------------------------------------------------

		private Control BuildToolsPage()
		{
			var page = Page("What can students use?", "Switch on only what the exam needs. Everything else stays blocked.");

			foreach (var group in StudentOptionInfo.All.GroupBy(o => o.Group))
			{
				page.Controls.Add(SectionHeader(group.Key));

				foreach (var info in group)
				{
					var toggle = new ToggleSwitch(info.Title, info.Description, PageWidth);

					toggle.AccessibleName = info.Title;
					toggle.CheckedChanged += (o, e) =>
					{
						if (!refreshing)
						{
							host.SetOption(info.Option, toggle.Checked);
						}

						UpdateRail();
					};

					optionSwitches[info.Option] = toggle;
					page.Controls.Add(toggle);
				}

				page.Controls.Add(Spacer(8));
			}

			return page;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Step 4: Lockdown level
		// ------------------------------------------------------------------------------------------------------------

		private Control BuildLockdownPage()
		{
			var page = Page("How locked down should the computer be?", "Pick a level. It sets the kiosk mode, clipboard, screen capture and display rules for you.");

			var cards = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0), WrapContents = false };

			foreach (var preset in SecurityPreset.All)
			{
				var card = new CardRadioButton(preset.Title, preset.UseFor, preset.Effects, PageWidth);

				if (preset == SecurityPreset.Standard)
				{
					card.Badge = "Recommended";
				}

				card.CheckedChanged += (o, e) =>
				{
					if (!refreshing && card.Checked && host.CurrentPreset != preset)
					{
						// Some preset changes reload every widget from the settings, which drops passwords that are
						// typed but not confirmed yet. Keep what was typed and apply it again afterwards.
						var typed = new[] { quitPassword, adminPassword, filePassword }.Select(f => Tuple.Create(f, f.Password, f.Confirmation)).ToList();

						host.ApplyPreset(preset);

						foreach (var entry in typed.Where(t => t.Item2.Length > 0 || t.Item3.Length > 0))
						{
							entry.Item1.SetTyped(entry.Item2, entry.Item3);
						}

						RefreshPresetCards();
					}

					UpdateRail();
				};

				presetCards[preset] = card;
				cards.Controls.Add(card);
			}

			page.Controls.Add(cards);

			customPresetNote = Theme.WrappingLabel("", PageWidth, Theme.Body, Theme.Warning);
			customPresetNote.Margin = new Padding(0, 4, 0, 0);
			page.Controls.Add(customPresetNote);

			return page;
		}

		private void RefreshPresetCards()
		{
			var current = host.CurrentPreset;
			var wasRefreshing = refreshing;

			refreshing = true;

			try
			{
				foreach (var entry in presetCards)
				{
					entry.Value.Checked = entry.Key == current;
				}
			}
			finally
			{
				refreshing = wasRefreshing;
			}

			customPresetNote.Text = current == null
				? "These settings were customised in \"All settings\", so they don't match a level. Picking a level here replaces those security settings."
				: "Fine-tune any of this in All settings > Security.";
			customPresetNote.ForeColor = current == null ? Theme.Warning : Theme.MutedText;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Step 5: Save and share
		// ------------------------------------------------------------------------------------------------------------

		private Control BuildSavePage()
		{
			var page = Page("Save and share", "Save the settings as a .seb file and share it with your students or install it on the exam computers.");

			page.Controls.Add(FieldLabel("What is this file for?"));

			var purpose = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 0, 0, 10), WrapContents = false };

			startExamCard = new CardRadioButton("Starting an exam", "Students double-click the file, or open it from a link, to start the exam. Their computer's own SEB settings are not changed.", null, PageWidth);
			configureClientCard = new CardRadioButton("Configuring exam computers", "Installs these settings as the default SEB settings on each computer that opens the file, e.g. in a computer lab.", null, PageWidth);

			startExamCard.CheckedChanged += (o, e) =>
			{
				if (!refreshing && startExamCard.Checked)
				{
					host.StartsExam = true;
				}

				UpdateRail();
				UpdateSummary();
			};
			configureClientCard.CheckedChanged += (o, e) =>
			{
				if (!refreshing && configureClientCard.Checked)
				{
					host.StartsExam = false;
				}

				UpdateRail();
				UpdateSummary();
			};

			purpose.Controls.Add(startExamCard);
			purpose.Controls.Add(configureClientCard);
			page.Controls.Add(purpose);

			filePassword = new PasswordFields(PasswordKind.File, "File password (recommended)", "Encrypts the file so nobody can read or change its settings without the password. SEB asks for it when the file is opened, so give it to everyone who opens the file.", PageWidth, OnPasswordChanged);
			page.Controls.Add(filePassword.Panel);
			page.Controls.Add(Spacer(18));

			page.Controls.Add(SectionHeader("Your exam at a glance"));

			summary = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0, 0, 0, 16), WrapContents = false };
			page.Controls.Add(summary);

			var actions = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 6), WrapContents = false };
			var saveButton = Theme.PrimaryButton("Save exam file...");

			saveButton.Margin = new Padding(0);
			saveButton.Click += (o, e) => Save();

			actions.Controls.Add(saveButton);
			page.Controls.Add(actions);

			return page;
		}

		private void UpdateSummary()
		{
			if (summary == null)
			{
				return;
			}

			var url = urlTextBox.Text.Trim();
			var preset = host.CurrentPreset;
			var tools = StudentOptionInfo.All.Where(o => optionSwitches[o.Option].Checked).Select(o => o.Title.ToLowerInvariant()).ToList();
			var rows = new List<Tuple<bool, string>>
			{
				Tuple.Create(IsValidUrl(url), IsValidUrl(url) ? "Opens " + Shorten(url, 70) : "No valid exam link yet"),
				Tuple.Create(true, QuitSummary()),
				Tuple.Create(true, tools.Count == 0 ? "No extra tools for students" : "Students can use: " + string.Join(", ", tools)),
				Tuple.Create(true, preset == null ? "Custom lockdown settings" : preset.Title + " lockdown"),
				Tuple.Create(true, host.HasPassword(PasswordKind.File) ? "The file is encrypted with a password" : "The file is not password protected")
			};

			summary.SuspendLayout();
			summary.Controls.Clear();

			foreach (var row in rows)
			{
				var color = row.Item1 ? Theme.Text : Theme.Warning;
				var line = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 6), WrapContents = false };
				var mark = Theme.Label(row.Item1 ? "✓" : "!", Theme.BodyBold, row.Item1 ? Theme.Success : Theme.Warning);
				var text = Theme.WrappingLabel(row.Item2, PageWidth - 24, Theme.Body, color);

				mark.AutoSize = false;
				mark.Size = new Size(24, 20);
				line.Controls.Add(mark);
				line.Controls.Add(text);
				summary.Controls.Add(line);
			}

			summary.ResumeLayout();
		}

		private string QuitSummary()
		{
			if (!host.AllowQuit)
			{
				return "Students can't quit SEB themselves";
			}

			return host.HasPassword(PasswordKind.Quit) ? "Quitting SEB needs the quit password" : "Students can quit SEB without a password";
		}

		// ------------------------------------------------------------------------------------------------------------
		// Rail
		// ------------------------------------------------------------------------------------------------------------

		private void UpdateRail()
		{
			if (stepButtons.Count == 0 || urlTextBox == null || startExamCard == null)
			{
				return;
			}

			var url = urlTextBox.Text.Trim();
			var preset = host.CurrentPreset;
			var toolCount = optionSwitches.Values.Count(t => t.Checked);

			stepButtons[0].Summary = IsValidUrl(url) ? HostOf(url) : url.Length == 0 ? "Not set yet" : "Check the link";
			stepButtons[0].NeedsAttention = !IsValidUrl(url);
			stepButtons[1].Summary = !host.AllowQuit ? "Students can't quit" : host.HasPassword(PasswordKind.Quit) ? "Quit password set" : "No quit password";
			stepButtons[2].Summary = toolCount == 1 ? "1 tool on" : $"{toolCount} tools on";
			stepButtons[3].Summary = preset == null ? "Custom" : preset.Title;
			stepButtons[4].Summary = host.StartsExam ? "Starts an exam" : "Configures computers";

			for (var i = 0; i < stepButtons.Count; i++)
			{
				stepButtons[i].Current = i == currentStep;
			}
		}

		private static string HostOf(string url)
		{
			return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Building blocks
		// ------------------------------------------------------------------------------------------------------------

		private static FlowLayoutPanel Page(string title, string description)
		{
			var page = new FlowLayoutPanel
			{
				AutoSize = true,
				BackColor = Theme.Background,
				FlowDirection = FlowDirection.TopDown,
				Margin = new Padding(0),
				MaximumSize = new Size(PageWidth, 0),
				WrapContents = false
			};

			var titleLabel = Theme.Label(title, Theme.Title);
			var descriptionLabel = Theme.WrappingLabel(description, PageWidth, Theme.Body, Theme.MutedText);

			titleLabel.Margin = new Padding(0, 0, 0, 6);
			descriptionLabel.Margin = new Padding(0, 0, 0, 24);

			page.Controls.Add(titleLabel);
			page.Controls.Add(descriptionLabel);

			return page;
		}

		private static Label FieldLabel(string text)
		{
			var label = Theme.Label(text, Theme.BodyBold);
			label.Margin = new Padding(0, 0, 0, 6);
			return label;
		}

		private static Label SectionHeader(string text)
		{
			var label = Theme.Label(text.ToUpperInvariant(), Theme.SectionHeader, Theme.MutedText);
			label.Margin = new Padding(0, 4, 0, 10);
			return label;
		}

		private static Control Tip(string text)
		{
			var label = Theme.WrappingLabel(text, PageWidth - 24, Theme.Small, Theme.MutedText);
			var panel = new Panel { AutoSize = true, BackColor = Theme.AccentSoft, Margin = new Padding(0), Padding = new Padding(12, 10, 12, 10) };

			label.BackColor = Theme.AccentSoft;
			panel.Controls.Add(label);

			return panel;
		}

		private static Control Spacer(int height)
		{
			return new Panel { Height = height, Margin = new Padding(0), Width = 1 };
		}

		private static void SetStatus(Label label, string text, Color color)
		{
			label.Text = text;
			label.ForeColor = color;
		}

		private static string Shorten(string text, int length)
		{
			return text.Length <= length ? text : text.Substring(0, length - 3) + "...";
		}

		/// <summary>
		/// A password with its confirmation, a show/hide switch and a status line, as used for the quit, administrator
		/// and file passwords.
		/// </summary>
		private class PasswordFields
		{
			private readonly TextBox password;
			private readonly TextBox confirmation;
			private readonly Label status;
			private readonly Button remove;
			private IQuickSetupHost host;

			internal PasswordFields(PasswordKind kind, string title, string description, int width, Action<PasswordFields> changed)
			{
				Kind = kind;

				Panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(0), WrapContents = false };

				var titleLabel = Theme.Label(title, Theme.BodyBold);
				var descriptionLabel = Theme.WrappingLabel(description, width, Theme.Small, Theme.MutedText);

				titleLabel.Margin = new Padding(0, 0, 0, 2);
				descriptionLabel.Margin = new Padding(0, 0, 0, 8);

				password = Theme.TextBox(240);
				confirmation = Theme.TextBox(240);
				password.AccessibleName = title.Replace(" (optional)", "");
				confirmation.AccessibleName = "Confirm " + password.AccessibleName.ToLowerInvariant();
				password.UseSystemPasswordChar = true;
				confirmation.UseSystemPasswordChar = true;
				password.Margin = new Padding(0, 0, 12, 0);
				confirmation.Margin = new Padding(0, 0, 12, 0);

				var show = new CheckBox { AutoSize = true, Font = Theme.Small, ForeColor = Theme.MutedText, Margin = new Padding(0, 4, 0, 0), Text = "Show", AccessibleName = "Show " + password.AccessibleName.ToLowerInvariant() };
				show.CheckedChanged += (o, e) =>
				{
					password.UseSystemPasswordChar = !show.Checked;
					confirmation.UseSystemPasswordChar = !show.Checked;
				};

				var labels = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 4), WrapContents = false };
				var passwordLabel = Theme.Label("Password", Theme.Small, Theme.MutedText);
				var confirmLabel = Theme.Label("Type it again", Theme.Small, Theme.MutedText);

				passwordLabel.AutoSize = false;
				passwordLabel.Size = new Size(252, 18);
				labels.Controls.Add(passwordLabel);
				labels.Controls.Add(confirmLabel);

				var fields = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 0, 0, 6), WrapContents = false };
				fields.Controls.Add(password);
				fields.Controls.Add(confirmation);
				fields.Controls.Add(show);

				var statusRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0), WrapContents = false };

				status = Theme.Label("", Theme.Small, Theme.MutedText);
				status.Margin = new Padding(0, 0, 10, 0);

				remove = Theme.LinkButton("Remove password", Theme.Small);
				remove.Margin = new Padding(0, -4, 0, 0);
				remove.Visible = false;
				remove.Click += (o, e) =>
				{
					host?.RemovePassword(Kind);
					Refresh(host);
					changed(this);
				};

				statusRow.Controls.Add(status);
				statusRow.Controls.Add(remove);

				password.TextChanged += (o, e) => changed(this);
				confirmation.TextChanged += (o, e) => changed(this);

				Panel.Controls.Add(titleLabel);
				Panel.Controls.Add(descriptionLabel);
				Panel.Controls.Add(labels);
				Panel.Controls.Add(fields);
				Panel.Controls.Add(statusRow);
			}

			internal PasswordKind Kind { get; }
			internal FlowLayoutPanel Panel { get; }
			internal string Password => password.Text;
			internal string Confirmation => confirmation.Text;

			/// <summary>
			/// Clears the fields; a stored password is never shown, only reported in the status line.
			/// </summary>
			/// <summary>
			/// Puts typed text back into both fields; each change is applied to the settings as if typed.
			/// </summary>
			internal void SetTyped(string newPassword, string newConfirmation)
			{
				password.Text = newPassword;
				confirmation.Text = newConfirmation;
			}

			internal void Refresh(IQuickSetupHost host)
			{
				this.host = host;
				password.Text = "";
				confirmation.Text = "";
				UpdateStatus(host);
			}

			internal void UpdateStatus(IQuickSetupHost host)
			{
				this.host = host;

				var typed = password.Text.Length > 0 || confirmation.Text.Length > 0;
				var stored = host.HasStoredPassword(Kind);

				remove.Visible = stored && !typed;

				if (typed && password.Text != confirmation.Text)
				{
					status.Text = confirmation.Text.Length == 0 ? "Type the password again to confirm it." : "The two passwords don't match yet.";
					status.ForeColor = Theme.Warning;
				}
				else if (typed)
				{
					status.Text = "✓  Password set";
					status.ForeColor = Theme.Success;
				}
				else if (stored)
				{
					status.Text = "A password is already set. Type a new one to replace it.";
					status.ForeColor = Theme.MutedText;
				}
				else
				{
					status.Text = "No password";
					status.ForeColor = Theme.MutedText;
				}
			}
		}

		/// <summary>
		/// One step in the left rail: a number (or a tick for finished steps), the step title and a one line summary.
		/// It is a button, so it works with the keyboard and screen readers.
		/// </summary>
		private class StepButton : Button
		{
			private readonly int number;
			private string summary = "";
			private bool current;
			private bool needsAttention;
			private bool hovering;

			internal StepButton(int number, string title, int width)
			{
				this.number = number;

				Cursor = Cursors.Hand;
				FlatStyle = FlatStyle.Flat;
				Font = Theme.Body;
				Margin = new Padding(0, 0, 0, 4);
				Size = new Size(width, 56);
				Text = title;
				AccessibleName = $"Step {number}: {title}";

				FlatAppearance.BorderSize = 0;
				SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
			}

			internal string Summary
			{
				get { return summary; }
				set { summary = value ?? ""; AccessibleDescription = summary; Invalidate(); }
			}

			internal bool Current
			{
				get { return current; }
				set { current = value; Invalidate(); }
			}

			internal bool NeedsAttention
			{
				get { return needsAttention; }
				set { needsAttention = value; Invalidate(); }
			}

			protected override void OnMouseEnter(EventArgs e)
			{
				base.OnMouseEnter(e);
				hovering = true;
				Invalidate();
			}

			protected override void OnMouseLeave(EventArgs e)
			{
				base.OnMouseLeave(e);
				hovering = false;
				Invalidate();
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				var graphics = e.Graphics;
				var background = current ? Theme.AccentSoft : hovering ? Theme.Background : Theme.Surface;

				graphics.Clear(Theme.Surface);
				graphics.SmoothingMode = SmoothingMode.AntiAlias;

				using (var path = ToggleSwitch.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 8))
				using (var brush = new SolidBrush(background))
				{
					graphics.FillPath(brush, path);
				}

				var circle = new Rectangle(12, (Height - 28) / 2, 28, 28);
				var circleColor = needsAttention && !current ? Theme.Warning : current ? Theme.Accent : Color.FromArgb(160, 167, 177);

				using (var brush = new SolidBrush(current ? Theme.Accent : Theme.Surface))
				using (var pen = new Pen(circleColor, 2))
				{
					graphics.FillEllipse(brush, circle);
					graphics.DrawEllipse(pen, circle);
				}

				var mark = needsAttention && !current ? "!" : number.ToString();
				TextRenderer.DrawText(graphics, mark, Theme.BodyBold, circle, current ? Color.White : circleColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

				var textLeft = circle.Right + 12;
				var textWidth = Width - textLeft - 8;

				TextRenderer.DrawText(graphics, Text, Theme.BodyBold, new Rectangle(textLeft, 9, textWidth, 20), Theme.Text, TextFormatFlags.EndEllipsis);
				TextRenderer.DrawText(graphics, summary, Theme.Small, new Rectangle(textLeft, 29, textWidth, 18), needsAttention ? Theme.Warning : Theme.MutedText, TextFormatFlags.EndEllipsis);

				if (Focused && ShowFocusCues)
				{
					ControlPaint.DrawFocusRectangle(graphics, new Rectangle(2, 2, Width - 5, Height - 5));
				}
			}
		}
	}
}
