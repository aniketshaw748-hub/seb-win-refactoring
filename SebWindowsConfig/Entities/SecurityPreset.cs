using System;
using System.Collections.Generic;

namespace SebWindowsConfig.Entities
{
	internal enum KioskMode
	{
		None,
		CreateNewDesktop,
		DisableExplorerShell
	}

	/// <summary>
	/// A named bundle of lockdown settings offered by the quick setup ("How locked down should the computer be?").
	/// Every preset sets the same group of settings, so the active preset can be recognised again from any loaded file.
	/// </summary>
	internal class SecurityPreset
	{
		internal string Title { get; private set; }
		internal string UseFor { get; private set; }
		internal string[] Effects { get; private set; }

		internal KioskMode KioskMode { get; private set; }
		internal int ClipboardPolicy { get; private set; }
		internal bool AllowVirtualMachine { get; private set; }
		internal bool AllowScreenSharing { get; private set; }
		internal bool EnablePrintScreen { get; private set; }
		internal int AllowedDisplays { get; private set; }
		internal int SebServicePolicy { get; private set; }

		/// <summary>
		/// Whether the SEB Service component is used; the service policy only has an effect while it is.
		/// </summary>
		internal bool UseSebService { get; private set; }

		internal static readonly SecurityPreset Relaxed = new SecurityPreset
		{
			Title = "Relaxed",
			UseFor = "Practice quizzes and open-book tests in a supervised room.",
			Effects = new[]
			{
				"No kiosk mode: the Windows desktop and taskbar stay available",
				"Clipboard shared with other apps",
				"Screen capture, remote screen sharing and virtual machines allowed",
				"Up to 2 connected displays",
				"SEB Service not used"
			},
			KioskMode = KioskMode.None,
			ClipboardPolicy = 0,
			AllowVirtualMachine = true,
			AllowScreenSharing = true,
			EnablePrintScreen = true,
			AllowedDisplays = 2,
			SebServicePolicy = 1,
			UseSebService = false
		};

		internal static readonly SecurityPreset Standard = new SecurityPreset
		{
			Title = "Standard",
			UseFor = "Most exams. These are the recommended SEB defaults.",
			Effects = new[]
			{
				"Kiosk mode: SEB runs on a separate desktop",
				"Clipboard works only inside SEB",
				"Screen capture, remote screen sharing and virtual machines not allowed",
				"Only 1 connected display",
				"SEB Service not used"
			},
			KioskMode = KioskMode.CreateNewDesktop,
			ClipboardPolicy = 2,
			AllowVirtualMachine = false,
			AllowScreenSharing = false,
			EnablePrintScreen = false,
			AllowedDisplays = 1,
			SebServicePolicy = 1,
			UseSebService = false
		};

		internal static readonly SecurityPreset Strict = new SecurityPreset
		{
			Title = "Strict",
			UseFor = "High-stakes exams on managed computers.",
			Effects = new[]
			{
				"Kiosk mode: SEB runs on a separate desktop",
				"Clipboard blocked completely",
				"Screen capture, remote screen sharing and virtual machines not allowed",
				"Only 1 connected display",
				"SEB Service required: SEB doesn't start unless it is running"
			},
			KioskMode = KioskMode.CreateNewDesktop,
			ClipboardPolicy = 1,
			AllowVirtualMachine = false,
			AllowScreenSharing = false,
			EnablePrintScreen = false,
			AllowedDisplays = 1,
			SebServicePolicy = 2,
			UseSebService = true
		};

		internal static IReadOnlyList<SecurityPreset> All { get; } = new[] { Relaxed, Standard, Strict };

		/// <summary>
		/// Returns the preset whose values all match the given settings, or <c>null</c> if the settings were customised.
		/// </summary>
		internal static SecurityPreset Detect(IDictionary<string, object> settings)
		{
			foreach (var preset in All)
			{
				if (preset.Matches(settings))
				{
					return preset;
				}
			}

			return null;
		}

		internal bool Matches(IDictionary<string, object> settings)
		{
			return CurrentKioskMode(settings) == KioskMode
				&& Int(settings, SEBSettings.KeyClipboardPolicy) == ClipboardPolicy
				&& Bool(settings, SEBSettings.KeyAllowVirtualMachine) == AllowVirtualMachine
				&& Bool(settings, SEBSettings.KeyAllowScreenSharing) == AllowScreenSharing
				&& Bool(settings, SEBSettings.KeyEnablePrintScreen) == EnablePrintScreen
				&& Int(settings, SEBSettings.KeyAllowedDisplaysMaxNumber) == AllowedDisplays
				&& Int(settings, SEBSettings.KeySebServicePolicy) == SebServicePolicy
				&& !Bool(settings, SEBSettings.KeySebServiceIgnore) == UseSebService;
		}

		internal static KioskMode CurrentKioskMode(IDictionary<string, object> settings)
		{
			if (Bool(settings, SEBSettings.KeyCreateNewDesktop))
			{
				return KioskMode.CreateNewDesktop;
			}

			if (Bool(settings, SEBSettings.KeyKillExplorerShell))
			{
				return KioskMode.DisableExplorerShell;
			}

			return KioskMode.None;
		}

		private static bool Bool(IDictionary<string, object> settings, string key)
		{
			return settings.TryGetValue(key, out var value) && value is bool b && b;
		}

		private static int Int(IDictionary<string, object> settings, string key)
		{
			if (settings.TryGetValue(key, out var value) && value != null)
			{
				try
				{
					return Convert.ToInt32(value);
				}
				catch (Exception)
				{
				}
			}

			return -1;
		}
	}
}
