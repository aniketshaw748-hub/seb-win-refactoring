using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SebWindowsConfig.Controls;
using SebWindowsConfig.Entities;
using SebWindowsConfig.UserInterface;
using SebWindowsConfig.Utilities;

namespace SebWindowsConfig
{
	/// <summary>
	/// The redesigned window around the existing settings pages: a header with the file actions, a switch between the
	/// guided quick setup and all settings, and a grouped sidebar with search instead of the row of tabs.
	/// The settings pages themselves and all their event handlers are unchanged.
	/// </summary>
	public partial class SebWindowsConfigForm : IQuickSetupHost
	{
		private const string PasswordPlaceholder = "0000000000000000";
		private const int SidebarWidth = 248;

		private static readonly string[][] NavigationGroups =
		{
			new[] { "Exam basics", "tabPageGeneral", "tabPageExam", "tabPageConfigFile" },
			new[] { "Browser", "tabPageAppearance", "tabPageBrowser", "tabPageDownUploads" },
			new[] { "Applications", "tabPageApplications", "tabPageAdditionalResources" },
			new[] { "Network and security", "tabPageNetwork", "tabPageSecurity" },
			new[] { "Windows", "tabPageRegistry", "tabPageHookedKeys" }
		};

		private readonly Dictionary<TabPage, NavigationButton> navigationButtons = new Dictionary<TabPage, NavigationButton>();
		private readonly Dictionary<PasswordKind, StoredPassword> storedPasswords = new Dictionary<PasswordKind, StoredPassword>();

		private QuickSetupControl quickSetup;
		private Panel allSettingsView;
		private Panel settingsContent;
		private FlowLayoutPanel navigationList;
		private TextBox searchBox;
		private ListBox searchResults;
		private Label searchEmpty;
		private ModeButton quickSetupModeButton;
		private ModeButton allSettingsModeButton;
		private Label headerFileLabel;
		private Timer highlightTimer;
		private Control highlightedControl;
		private Color highlightedControlColor;
		private bool highlightedControlUsedVisualStyle;
		private bool shellReady;
		private bool applyingQuickSetup;

		/// <summary>
		/// True while all widgets are updated from the settings (e.g. a file is opened), so widget events then aren't edits.
		/// </summary>
		private bool reloadingWidgets;

		private void InitialiseShell()
		{
			SuspendLayout();

			BackColor = Theme.Background;
			FormBorderStyle = FormBorderStyle.Sizable;
			MaximizeBox = true;

			var header = BuildHeader();
			var body = new Panel { BackColor = Theme.Background, Dock = DockStyle.Fill };

			allSettingsView = BuildAllSettingsView();
			quickSetup = new QuickSetupControl(this);

			// The allowed websites include the exam site, so they follow an exam link typed in All settings > General too.
			// Only a link the user types counts (the box has the focus), not one set while settings are loaded.
			textBoxStartURL.TextChanged += (o, e) =>
			{
				if (textBoxStartURL.Focused && !reloadingWidgets && !applyingQuickSetup)
				{
					quickSetup.OnStartUrlChangedElsewhere();
				}
			};

			body.Controls.Add(allSettingsView);
			body.Controls.Add(quickSetup);

			Controls.Add(body);
			Controls.Add(header);

			// Docking order, top to bottom: menu, header, then the body fills the rest above the status bar.
			header.SendToBack();
			menuStrip1.SendToBack();
			body.BringToFront();

			StartPosition = FormStartPosition.CenterScreen;

			// The form scales itself while loading, so the final size is set afterwards.
			Load += (o, e) => ApplyInitialSize();
			Shown += (o, e) => quickSetup.FocusCurrentStep();

			ResumeLayout(true);

			shellReady = true;
			OnSettingsReloaded();
			ShowQuickSetup();
		}

		private void ApplyInitialSize()
		{
			var workingArea = Screen.FromControl(this).WorkingArea;
			var preferred = new Size(SidebarWidth + tabControlSebWindowsConfig.Width + 64, 860);
			var size = new Size(Math.Min(preferred.Width, workingArea.Width), Math.Min(preferred.Height, workingArea.Height));

			MinimumSize = new Size(Math.Min(1100, workingArea.Width), Math.Min(660, workingArea.Height));
			Size = size;
			Location = new Point(workingArea.Left + (workingArea.Width - size.Width) / 2, workingArea.Top + (workingArea.Height - size.Height) / 2);
		}

		/// <summary>
		/// Called whenever all widgets were updated from the settings (file opened, saved, reverted ...).
		/// </summary>
		private void OnSettingsReloaded(bool keepStoredPasswords = false)
		{
			reloadingWidgets = false;

			if (!shellReady)
			{
				return;
			}

			headerFileLabel.Text = currentPathSebConfigFile;

			// While a preset is applied, its widget reloads must not replace the snapshots either.
			if (!keepStoredPasswords && !applyingQuickSetup)
			{
				RememberStoredPasswords();
			}

			if (!applyingQuickSetup)
			{
				// A preset reloads widgets but keeps the configuration; anything else may have replaced it.
				quickSetup.RefreshFromHost(settingsReplaced: !keepStoredPasswords);
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// Header
		// ------------------------------------------------------------------------------------------------------------

		private Control BuildHeader()
		{
			var header = new Panel { BackColor = Theme.Surface, Dock = DockStyle.Top, Height = 64 };
			var icon = new PictureBox { Image = LoadHeaderLogo(), Location = new Point(20, 16), Size = new Size(32, 32), SizeMode = PictureBoxSizeMode.Zoom };
			var title = Theme.Label("SEB Configuration Tool", Theme.AppTitle);

			title.Location = new Point(62, 12);
			headerFileLabel = Theme.Label("", Theme.Small, Theme.MutedText);
			headerFileLabel.AutoEllipsis = true;
			headerFileLabel.AutoSize = false;
			headerFileLabel.Location = new Point(63, 36);
			headerFileLabel.Size = new Size(330, 18);

			var modes = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Surface, FlowDirection = FlowDirection.LeftToRight, Location = new Point(410, 15), WrapContents = false };

			quickSetupModeButton = new ModeButton("Quick setup");
			allSettingsModeButton = new ModeButton("All settings");
			quickSetupModeButton.Click += (o, e) => ShowQuickSetup();
			allSettingsModeButton.Click += (o, e) => ShowAllSettings();
			modes.Controls.Add(quickSetupModeButton);
			modes.Controls.Add(allSettingsModeButton);

			var actions = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Surface, Dock = DockStyle.Right, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 15, 20, 0), WrapContents = false };
			var open = Theme.SecondaryButton("Open...");
			var save = Theme.SecondaryButton("Save");
			var saveAs = Theme.SecondaryButton("Save as...");

			open.Click += (o, e) => buttonOpenSettings_Click(o, e);
			save.Click += (o, e) => SaveToCurrentFile();
			saveAs.Click += (o, e) =>
			{
				if (!quickSetup.Visible || quickSetup.ConfirmExamLink())
				{
					buttonSaveSettingsAs_Click(o, e);
				}
			};
			open.Margin = save.Margin = new Padding(0, 0, 8, 0);
			saveAs.Margin = new Padding(0);
			toolTip1.SetToolTip(open, "Open a .seb file");
			toolTip1.SetToolTip(save, "Save the settings to the file shown on the left");
			toolTip1.SetToolTip(saveAs, "Save the settings as a new .seb file");
			actions.Controls.Add(open);
			actions.Controls.Add(save);
			actions.Controls.Add(saveAs);

			header.Controls.Add(actions);
			header.Controls.Add(icon);
			header.Controls.Add(title);
			header.Controls.Add(headerFileLabel);
			header.Controls.Add(modes);
			header.Controls.Add(new Panel { BackColor = Theme.Border, Dock = DockStyle.Bottom, Height = 1 });

			return header;
		}

		/// <summary>
		/// "Save" in the header: while no file is open (the editor shows the default settings), or in the quick setup
		/// while this computer's local client settings are open, saving in place would write those local settings,
		/// which is not what someone setting up an exam expects, so it asks for a file name instead.
		/// File > Save Settings keeps the original behaviour.
		/// </summary>
		private void SaveToCurrentFile()
		{
			var isLocalClientSettings = string.Equals(currentPathSebConfigFile, SEBClientInfo.SebClientSettingsAppDataFile, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(currentPathSebConfigFile, SEBClientInfo.SebClientSettingsProgramDataFile, StringComparison.OrdinalIgnoreCase);

			if (quickSetup.Visible && !quickSetup.ConfirmExamLink())
			{
				return;
			}

			// In the quick setup, never overwrite this computer's own SEB settings in place: save a new exam file.
			if (currentPathSebConfigFile == SEBUIStrings.settingsTitleDefaultSettings || (quickSetup.Visible && isLocalClientSettings))
			{
				buttonSaveSettingsAs_Click(this, EventArgs.Empty);
			}
			else
			{
				buttonSaveSettings_Click(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// The window icon as a bitmap. Some frames of the icon are PNG compressed, which <see cref="Icon.ToBitmap"/>
		/// can't read, so the sizes are tried one by one.
		/// </summary>
		private Image LoadHeaderLogo()
		{
			try
			{
				using (var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
				{
					return icon?.ToBitmap();
				}
			}
			catch (Exception)
			{
				return null;
			}
		}

		private void ShowQuickSetup()
		{
			if (quickSetupModeButton.Selected)
			{
				// Already showing; refreshing now would turn passwords typed here into the "stored" ones.
				return;
			}

			// Like switching pages in All settings: unconfirmed passwords there must be confirmed first,
			// otherwise the quick setup would show "No password" while saving is still blocked.
			if (allSettingsView.Visible && ArePasswordsUnconfirmed())
			{
				return;
			}

			// Passwords may have been changed in All settings since the last reload.
			RememberStoredPasswords();
			quickSetup.RefreshFromHost();
			quickSetup.Visible = true;
			allSettingsView.Visible = false;
			quickSetupModeButton.Selected = true;
			allSettingsModeButton.Selected = false;
			quickSetup.FocusCurrentStep();
		}

		public void ShowAllSettings()
		{
			allSettingsView.Visible = true;
			quickSetup.Visible = false;
			quickSetupModeButton.Selected = false;
			allSettingsModeButton.Selected = true;
			UpdateNavigationSelection();
			searchBox.Focus();
		}

		// ------------------------------------------------------------------------------------------------------------
		// All settings: sidebar, search and the existing pages
		// ------------------------------------------------------------------------------------------------------------

		private Panel BuildAllSettingsView()
		{
			var view = new Panel { BackColor = Theme.Background, Dock = DockStyle.Fill };
			var sidebar = new Panel { BackColor = Theme.Surface, Dock = DockStyle.Left, Padding = new Padding(14, 16, 14, 12), Width = SidebarWidth };

			searchBox = Theme.TextBox(SidebarWidth - 28);
			searchBox.AccessibleName = "Search settings";
			searchBox.Dock = DockStyle.Top;
			searchBox.TextChanged += (o, e) => UpdateSearch();
			searchBox.KeyDown += SearchBox_KeyDown;
			searchBox.HandleCreated += (o, e) => SendMessage(searchBox.Handle, EM_SETCUEBANNER, (IntPtr) 1, "Search settings, e.g. clipboard");

			var gap = new Panel { Dock = DockStyle.Top, Height = 12 };

			navigationList = new FlowLayoutPanel { AutoScroll = true, BackColor = Theme.Surface, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };

			foreach (var group in NavigationGroups)
			{
				var header = Theme.Label(group[0].ToUpperInvariant(), Theme.SectionHeader, Theme.MutedText);
				header.Margin = new Padding(8, navigationList.Controls.Count == 0 ? 0 : 12, 0, 6);
				navigationList.Controls.Add(header);

				foreach (var name in group.Skip(1))
				{
					var page = tabControlSebWindowsConfig.TabPages[name];

					if (page == null)
					{
						continue;
					}

					var image = page.ImageIndex >= 0 && page.ImageIndex < imageListTabIcons.Images.Count ? imageListTabIcons.Images[page.ImageIndex] : null;
					var button = new NavigationButton(page.Text.Trim(), image, SidebarWidth - 32);

					button.Click += (o, e) => SelectSettingsPage(page);
					navigationButtons[page] = button;
					navigationList.Controls.Add(button);
				}
			}

			searchResults = new ListBox
			{
				BorderStyle = BorderStyle.None,
				Dock = DockStyle.Fill,
				DrawMode = DrawMode.OwnerDrawFixed,
				Font = Theme.Body,
				IntegralHeight = false,
				ItemHeight = 44,
				Visible = false
			};
			searchResults.DrawItem += SearchResults_DrawItem;
			searchResults.MouseClick += (o, e) => RevealSelectedSearchResult();
			searchResults.KeyDown += (o, e) =>
			{
				if (e.KeyCode == Keys.Enter)
				{
					RevealSelectedSearchResult();
					e.Handled = true;
				}
			};

			searchEmpty = Theme.WrappingLabel("No settings match your search.", SidebarWidth - 40, Theme.Body, Theme.MutedText);
			searchEmpty.Dock = DockStyle.Top;
			searchEmpty.Visible = false;

			sidebar.Controls.Add(navigationList);
			sidebar.Controls.Add(searchResults);
			sidebar.Controls.Add(searchEmpty);
			sidebar.Controls.Add(gap);
			sidebar.Controls.Add(searchBox);
			sidebar.Paint += (o, e) =>
			{
				using (var pen = new Pen(Theme.Border))
				{
					e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
				}
			};

			settingsContent = new Panel { AutoScroll = true, BackColor = Theme.Background, Dock = DockStyle.Fill, Padding = new Padding(12) };

			// The pages keep their layout; only the row of tab headers is hidden, the sidebar replaces it.
			// InitialiseGUIWidgets() docks the tab control to fill the form. Here it keeps the size it had there
			// instead, so the pages (laid out for that size) scroll in a smaller window rather than being squeezed.
			var pageAreaSize = tabControlSebWindowsConfig.Size;

			Controls.Remove(tabControlSebWindowsConfig);
			tabControlSebWindowsConfig.Dock = DockStyle.None;
			tabControlSebWindowsConfig.Anchor = AnchorStyles.Top | AnchorStyles.Left;
			tabControlSebWindowsConfig.Appearance = TabAppearance.FlatButtons;
			tabControlSebWindowsConfig.ItemSize = new Size(0, 1);
			tabControlSebWindowsConfig.SizeMode = TabSizeMode.Fixed;
			tabControlSebWindowsConfig.Location = new Point(12, 4);
			tabControlSebWindowsConfig.Size = pageAreaSize;
			tabControlSebWindowsConfig.SelectedIndexChanged += (o, e) => UpdateNavigationSelection();
			settingsContent.Controls.Add(tabControlSebWindowsConfig);

			view.Controls.Add(settingsContent);
			view.Controls.Add(sidebar);

			return view;
		}

		private void SelectSettingsPage(TabPage page)
		{
			// Selecting a page runs the existing tab change checks, e.g. for unconfirmed passwords.
			tabControlSebWindowsConfig.SelectedTab = page;
			UpdateNavigationSelection();
		}

		private void UpdateNavigationSelection()
		{
			foreach (var entry in navigationButtons)
			{
				entry.Value.Selected = entry.Key == tabControlSebWindowsConfig.SelectedTab;
			}
		}

		private void UpdateSearch()
		{
			var query = searchBox.Text.Trim();

			if (query.Length < 2)
			{
				searchResults.Visible = false;
				searchEmpty.Visible = false;
				navigationList.Visible = true;

				return;
			}

			// Built per search: which controls are shown can change while editing (e.g. a selected resource).
			var results = new SettingsSearchIndex(tabControlSebWindowsConfig.TabPages.Cast<TabPage>(), toolTip1).Search(query);

			searchResults.BeginUpdate();
			searchResults.Items.Clear();

			foreach (var result in results)
			{
				searchResults.Items.Add(result);
			}

			searchResults.EndUpdate();

			navigationList.Visible = false;
			searchResults.Visible = results.Count > 0;
			searchEmpty.Visible = results.Count == 0;
		}

		private void SearchBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
			{
				searchBox.Clear();
				e.SuppressKeyPress = true;
			}
			else if ((e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter) && searchResults.Visible && searchResults.Items.Count > 0)
			{
				searchResults.Focus();

				if (searchResults.SelectedIndex < 0)
				{
					searchResults.SelectedIndex = 0;
				}

				if (e.KeyCode == Keys.Enter)
				{
					RevealSelectedSearchResult();
				}

				e.SuppressKeyPress = true;
			}
		}

		private void SearchResults_DrawItem(object sender, DrawItemEventArgs e)
		{
			if (e.Index < 0)
			{
				return;
			}

			var entry = (SettingsSearchEntry) searchResults.Items[e.Index];
			var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

			using (var brush = new SolidBrush(selected ? Theme.AccentSoft : Theme.Surface))
			{
				e.Graphics.FillRectangle(brush, e.Bounds);
			}

			var textBounds = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 4, e.Bounds.Width - 12, 20);
			var locationBounds = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 23, e.Bounds.Width - 12, 18);

			TextRenderer.DrawText(e.Graphics, entry.Text, Theme.Body, textBounds, Theme.Text, TextFormatFlags.EndEllipsis);
			TextRenderer.DrawText(e.Graphics, entry.Location, Theme.Small, locationBounds, Theme.MutedText, TextFormatFlags.EndEllipsis);
		}

		private void RevealSelectedSearchResult()
		{
			if (searchResults.SelectedItem is SettingsSearchEntry entry)
			{
				// Also re-check the text: some labels change with other settings (e.g. the proxy protocol).
				if (SettingsSearchIndex.IsShownOnPage(entry.Control, entry.Page) && (entry.Control is TabPage || SettingsSearchIndex.Normalize(entry.Control.Text) == entry.Text))
				{
					Reveal(entry);
				}
				else
				{
					// The setting was hidden or renamed since the search ran (e.g. another proxy type): refresh the list.
					UpdateSearch();
				}
			}
		}

		private void Reveal(SettingsSearchEntry entry)
		{
			SelectSettingsPage(entry.Page);

			if (tabControlSebWindowsConfig.SelectedTab != entry.Page)
			{
				return;
			}

			foreach (var nested in entry.NestedPages)
			{
				if (nested.Parent is TabControl tabs)
				{
					tabs.SelectedTab = nested;
				}
			}

			settingsContent.ScrollControlIntoView(entry.Control);
			Highlight(entry.Control);
		}

		/// <summary>
		/// Briefly tints a control found by the search, so the eye lands on it.
		/// </summary>
		private void Highlight(Control control)
		{
			if (control is TabPage)
			{
				return;
			}

			RestoreHighlightedControl();
			highlightTimer?.Dispose();

			highlightedControl = control;
			highlightedControlColor = control.BackColor;
			highlightedControlUsedVisualStyle = control is ButtonBase button && button.UseVisualStyleBackColor;

			highlightTimer = new Timer { Interval = 2000 };
			highlightTimer.Tick += (o, e) => RestoreHighlightedControl();

			control.BackColor = Theme.Highlight;
			highlightTimer.Start();

			if (control.CanFocus && !(control is Label) && !(control is GroupBox))
			{
				control.Focus();
			}
		}

		private void RestoreHighlightedControl()
		{
			highlightTimer?.Stop();

			if (highlightedControl != null)
			{
				highlightedControl.BackColor = highlightedControlColor;

				if (highlightedControlUsedVisualStyle && highlightedControl is ButtonBase button)
				{
					button.UseVisualStyleBackColor = true;
				}

				highlightedControl = null;
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// IQuickSetupHost: every change goes through the existing widgets, so their handlers update the settings.
		// ------------------------------------------------------------------------------------------------------------

		public string StartUrl
		{
			get { return textBoxStartURL.Text; }
			set { if (textBoxStartURL.Text != value) textBoxStartURL.Text = value; }
		}

		public bool AllowQuit
		{
			get { return checkBoxAllowQuit.Checked; }
			set { checkBoxAllowQuit.Checked = value; }
		}

		public bool SendsExamKeys
		{
			get { return checkBoxSendBrowserExamKey.Checked; }
			set { checkBoxSendBrowserExamKey.Checked = value; }
		}

		public bool StartsExam
		{
			get { return radioButtonStartingAnExam.Checked; }
			set
			{
				if (value)
				{
					radioButtonStartingAnExam.Checked = true;

					// A file password loaded from a client configuration file is only known as a hash. An exam file
					// would be encrypted with that hash as its password, which nobody can type, so ask for it again.
					var hashStored = storedPasswords.TryGetValue(PasswordKind.File, out var storedFilePassword) && storedFilePassword.ContainsHash;

					if (hashStored)
					{
						// Never restore that hash later, e.g. when a replacement being typed is cleared again.
						storedPasswords.Remove(PasswordKind.File);
					}

					if (settingsPasswordFieldsContainHash)
					{
						((IQuickSetupHost) this).RemovePassword(PasswordKind.File);
						quickSetup.RefreshPassword(PasswordKind.File);
						MessageBox.Show(this, "Please type the file password again. It came from a file for configuring computers, where only a scrambled copy of it is stored, so it can't be reused for an exam file.", "File password", MessageBoxButtons.OK, MessageBoxIcon.Information);
					}
				}
				else
				{
					radioButtonConfiguringAClient.Checked = true;
				}
			}
		}

		SiteAccess IQuickSetupHost.SiteAccess => SiteAccess.Read(SEBSettings.settingsCurrent);

		void IQuickSetupHost.SetSiteAccess(bool restricted, IEnumerable<string> allowedSites)
		{
			var rules = SEBSettings.settingsCurrent[SEBSettings.KeyURLFilterRules] as List<object>;

			if (rules == null)
			{
				rules = new List<object>();
				SEBSettings.settingsCurrent[SEBSettings.KeyURLFilterRules] = rules;
			}

			rules.Clear();

			foreach (var site in allowedSites.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				rules.Add(FilterRule.ToConfig(new FilterRule { IsActive = true, IsRegex = false, Expression = site, Action = FilterAction.Allow }));
			}

			// Show the new rules in All settings > Network > Filter; the check box handlers store the on/off states.
			LoadUrlFilterRules();
			checkBoxEnableURLFilter.Checked = restricted;
			checkBoxEnableURLContentFilter.Checked = false;
		}

		bool IQuickSetupHost.IsNewConfiguration => currentPathSebConfigFile == SEBUIStrings.settingsTitleDefaultSettings;

		SecurityPreset IQuickSetupHost.CurrentPreset => SecurityPreset.Detect(SEBSettings.settingsCurrent);

		bool IQuickSetupHost.HasStoredPassword(PasswordKind kind)
		{
			return storedPasswords.TryGetValue(kind, out var stored) && stored.IsStillStored(this);
		}

		bool IQuickSetupHost.HasPassword(PasswordKind kind)
		{
			var fields = PasswordFieldsOf(kind);

			return fields.Item1.Text.Length > 0 && fields.Item1.Text == fields.Item2.Text;
		}

		void IQuickSetupHost.SetPassword(PasswordKind kind, string password, string confirmation)
		{
			if (password.Length == 0 && confirmation.Length == 0 && storedPasswords.TryGetValue(kind, out var stored))
			{
				// Nothing typed (any more): keep the password that came with the settings.
				stored.Restore(this);

				return;
			}

			var fields = PasswordFieldsOf(kind);

			fields.Item1.Text = password;
			fields.Item2.Text = confirmation;
		}

		void IQuickSetupHost.RemovePassword(PasswordKind kind)
		{
			storedPasswords.Remove(kind);
			SetPasswordContainsHash(kind, false);

			var fields = PasswordFieldsOf(kind);

			fields.Item1.Text = "";
			fields.Item2.Text = "";
		}

		bool IQuickSetupHost.GetOption(StudentOption option)
		{
			switch (option)
			{
				case StudentOption.SpellCheck:
					return checkBoxAllowSpellCheck.Checked;
				case StudentOption.Zoom:
					return checkBoxEnableZoomPage.Checked || checkBoxEnableZoomText.Checked;
				case StudentOption.Find:
					return checkBoxAllowFind.Checked;
				case StudentOption.Reload:
					return checkBoxAllowReload.Checked && checkBoxShowReloadButton.Checked;
				case StudentOption.BackForward:
					return checkBoxAllowBrowsingBackForward.Checked;
				case StudentOption.Print:
					return checkBoxAllowPrint.Checked;
				case StudentOption.Downloads:
					return checkBoxAllowDownloads.Checked;
				case StudentOption.Uploads:
					return checkBoxAllowUploads.Checked;
				case StudentOption.VolumeControl:
					return checkBoxEnableAudioControl.Checked;
				case StudentOption.Clock:
					return checkBoxShowTime.Checked;
				default:
					return false;
			}
		}

		void IQuickSetupHost.SetOption(StudentOption option, bool enabled)
		{
			switch (option)
			{
				case StudentOption.SpellCheck:
					checkBoxAllowSpellCheck.Checked = enabled;
					break;
				case StudentOption.Zoom:
					checkBoxEnableZoomPage.Checked = enabled;
					checkBoxEnableZoomText.Checked = enabled;
					break;
				case StudentOption.Find:
					checkBoxAllowFind.Checked = enabled;
					break;
				case StudentOption.Reload:
					checkBoxAllowReload.Checked = enabled;
					checkBoxShowReloadButton.Checked = enabled;
					break;
				case StudentOption.BackForward:
					checkBoxAllowBrowsingBackForward.Checked = enabled;
					break;
				case StudentOption.Print:
					checkBoxAllowPrint.Checked = enabled;
					break;
				case StudentOption.Downloads:
					checkBoxAllowDownloads.Checked = enabled;
					break;
				case StudentOption.Uploads:
					checkBoxAllowUploads.Checked = enabled;
					break;
				case StudentOption.VolumeControl:
					checkBoxEnableAudioControl.Checked = enabled;
					break;
				case StudentOption.Clock:
					checkBoxShowTime.Checked = enabled;
					break;
			}
		}

		void IQuickSetupHost.ApplyPreset(SecurityPreset preset)
		{
			applyingQuickSetup = true;

			try
			{
				switch (preset.KioskMode)
				{
					case KioskMode.CreateNewDesktop:
						radioCreateNewDesktop.Checked = true;
						break;
					case KioskMode.DisableExplorerShell:
						radioKillExplorerShell.Checked = true;
						break;
					default:
						radioNoKiosMode.Checked = true;
						break;
				}

				radioButtonClipboardPolicyAllow.Checked = preset.ClipboardPolicy == 0;
				radioButtonClipboardPolicyBlock.Checked = preset.ClipboardPolicy == 1;
				radioButtonClipboardPolicyIsolated.Checked = preset.ClipboardPolicy == 2;
				checkBoxAllowVirtualMachine.Checked = preset.AllowVirtualMachine;
				checkBoxEnableScreenCapture.Checked = preset.AllowScreenSharing;
				checkBoxEnablePrintScreen.Checked = preset.EnablePrintScreen;
				comboBoxAllowedDisplaysMaxNumber.Text = preset.AllowedDisplays.ToString();
				listBoxSebServicePolicy.SelectedIndex = preset.SebServicePolicy;

				// The check box is labelled "Enable SEB Service Component" (checked = service is not ignored).
				checkBoxSebServiceIgnore.Checked = preset.UseSebService;
			}
			finally
			{
				applyingQuickSetup = false;
			}

			// A preset may reload all widgets, which turns newly typed passwords into "stored" ones. Keep the snapshots
			// of what was stored before, so clearing a newly typed password still goes back to the previous state.
			OnSettingsReloaded(keepStoredPasswords: true);
		}

		public void SaveAs()
		{
			buttonSaveSettingsAs_Click(this, EventArgs.Empty);
		}

		private Tuple<TextBox, TextBox> PasswordFieldsOf(PasswordKind kind)
		{
			switch (kind)
			{
				case PasswordKind.Quit:
					return Tuple.Create(textBoxQuitPassword, textBoxConfirmQuitPassword);
				case PasswordKind.Administrator:
					return Tuple.Create(textBoxAdminPassword, textBoxConfirmAdminPassword);
				default:
					return Tuple.Create(textBoxSettingsPassword, textBoxConfirmSettingsPassword);
			}
		}

		private bool PasswordContainsHash(PasswordKind kind)
		{
			switch (kind)
			{
				case PasswordKind.Quit:
					return quitPasswordFieldsContainHash;
				case PasswordKind.Administrator:
					return adminPasswordFieldsContainHash;
				default:
					return settingsPasswordFieldsContainHash;
			}
		}

		private void SetPasswordContainsHash(PasswordKind kind, bool value)
		{
			switch (kind)
			{
				case PasswordKind.Quit:
					quitPasswordFieldsContainHash = value;
					break;
				case PasswordKind.Administrator:
					adminPasswordFieldsContainHash = value;
					break;
				default:
					settingsPasswordFieldsContainHash = value;
					break;
			}
		}

		private static string HashKeyOf(PasswordKind kind)
		{
			switch (kind)
			{
				case PasswordKind.Quit:
					return SEBSettings.KeyHashedQuitPassword;
				case PasswordKind.Administrator:
					return SEBSettings.KeyHashedAdminPassword;
				default:
					return null;
			}
		}

		/// <summary>
		/// Remembers the passwords that came with the loaded settings, so the quick setup can show "a password is
		/// already set" and put it back when a newly typed password is deleted again.
		/// </summary>
		private void RememberStoredPasswords()
		{
			storedPasswords.Clear();

			foreach (PasswordKind kind in Enum.GetValues(typeof(PasswordKind)))
			{
				var fields = PasswordFieldsOf(kind);
				var hashKey = HashKeyOf(kind);
				var isSet = fields.Item1.Text.Length > 0 && fields.Item1.Text == fields.Item2.Text;

				if (isSet)
				{
					storedPasswords[kind] = new StoredPassword
					{
						Kind = kind,
						ContainsHash = PasswordContainsHash(kind),
						FieldText = fields.Item1.Text,
						Hash = hashKey != null ? SEBSettings.settingsCurrent[hashKey] as string : null,
						SettingsPassword = settingsPassword
					};
				}
			}
		}

		private class StoredPassword
		{
			internal PasswordKind Kind;
			internal bool ContainsHash;
			internal string FieldText;
			internal string Hash;
			internal string SettingsPassword;

			internal bool IsStillStored(SebWindowsConfigForm form)
			{
				var fields = form.PasswordFieldsOf(Kind);
				var confirmed = fields.Item1.Text.Length > 0 && fields.Item1.Text == fields.Item2.Text;

				// Compared by the effective password, not the field text: a widget reload shows the same password
				// as a hash placeholder instead of the typed text.
				if (Kind == PasswordKind.File)
				{
					return confirmed && form.settingsPassword == SettingsPassword;
				}

				return confirmed && SEBSettings.settingsCurrent[HashKeyOf(Kind)] as string == Hash;
			}

			/// <summary>
			/// Puts the stored password back, in the same order as UpdateAllWidgetsOfProgram() fills the fields,
			/// so the placeholder is never hashed as if it were a new password.
			/// </summary>
			internal void Restore(SebWindowsConfigForm form)
			{
				if (IsStillStored(form))
				{
					return;
				}

				var fields = form.PasswordFieldsOf(Kind);

				form.SetPasswordContainsHash(Kind, false);
				fields.Item1.Text = FieldText;
				form.SetPasswordContainsHash(Kind, ContainsHash);
				fields.Item2.Text = FieldText;

				if (Kind == PasswordKind.File)
				{
					form.settingsPassword = SettingsPassword;
				}
				else if (Hash != null)
				{
					SEBSettings.settingsCurrent[HashKeyOf(Kind)] = Hash;
				}
			}
		}

		private const int EM_SETCUEBANNER = 0x1501;

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

		/// <summary>
		/// One half of the "Quick setup | All settings" switch in the header.
		/// </summary>
		private class ModeButton : Button
		{
			private bool selected;

			internal ModeButton(string text)
			{
				AutoSize = false;
				Cursor = Cursors.Hand;
				FlatStyle = FlatStyle.Flat;
				Font = Theme.Body;
				Margin = new Padding(0);
				Size = new Size(116, 34);
				Text = text;

				FlatAppearance.BorderSize = 0;
				SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
			}

			internal bool Selected
			{
				get { return selected; }
				set { selected = value; AccessibleDescription = value ? "Selected" : null; Invalidate(); }
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				var graphics = e.Graphics;

				graphics.Clear(Theme.Surface);
				graphics.SmoothingMode = SmoothingMode.AntiAlias;

				using (var path = ToggleSwitch.RoundedRectangle(new Rectangle(1, 1, Width - 3, Height - 3), 8))
				using (var brush = new SolidBrush(selected ? Theme.AccentSoft : Theme.Surface))
				using (var pen = new Pen(selected ? Theme.Accent : Theme.Border))
				{
					graphics.FillPath(brush, path);
					graphics.DrawPath(pen, path);
				}

				TextRenderer.DrawText(graphics, Text, selected ? Theme.BodyBold : Theme.Body, ClientRectangle, selected ? Theme.Accent : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

				if (Focused && ShowFocusCues)
				{
					ControlPaint.DrawFocusRectangle(graphics, new Rectangle(4, 4, Width - 9, Height - 9));
				}
			}
		}

		/// <summary>
		/// A settings page in the sidebar: the page's icon and name; the selected page gets a tinted row.
		/// </summary>
		private class NavigationButton : Button
		{
			private readonly Image image;
			private bool selected;
			private bool hovering;

			internal NavigationButton(string text, Image image, int width)
			{
				this.image = image;

				Cursor = Cursors.Hand;
				FlatStyle = FlatStyle.Flat;
				Font = Theme.Body;
				Margin = new Padding(0, 0, 0, 2);
				Size = new Size(width, 36);
				Text = text;

				FlatAppearance.BorderSize = 0;
				SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
			}

			internal bool Selected
			{
				get { return selected; }
				set { selected = value; Invalidate(); }
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

				graphics.Clear(Theme.Surface);
				graphics.SmoothingMode = SmoothingMode.AntiAlias;

				if (selected || hovering)
				{
					using (var path = ToggleSwitch.RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 6))
					using (var brush = new SolidBrush(selected ? Theme.AccentSoft : Theme.Background))
					{
						graphics.FillPath(brush, path);
					}
				}

				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

				if (image != null)
				{
					graphics.DrawImage(image, new Rectangle(8, (Height - 22) / 2, 22, 22));
				}

				TextRenderer.DrawText(graphics, Text, selected ? Theme.BodyBold : Theme.Body, new Rectangle(40, 0, Width - 44, Height), selected ? Theme.Accent : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

				if (Focused && ShowFocusCues)
				{
					ControlPaint.DrawFocusRectangle(graphics, new Rectangle(2, 2, Width - 5, Height - 5));
				}
			}
		}
	}
}
