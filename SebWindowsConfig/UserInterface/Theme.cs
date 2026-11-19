using System.Drawing;
using System.Windows.Forms;

namespace SebWindowsConfig.UserInterface
{
	/// <summary>
	/// Colours, fonts and small styling helpers shared by the redesigned shell, the quick setup and the settings navigation.
	/// </summary>
	internal static class Theme
	{
		internal static readonly Color Background = Color.FromArgb(245, 246, 248);
		internal static readonly Color Surface = Color.White;
		internal static readonly Color Border = Color.FromArgb(217, 221, 227);
		internal static readonly Color Text = Color.FromArgb(31, 35, 40);
		internal static readonly Color MutedText = Color.FromArgb(91, 100, 112);
		internal static readonly Color Accent = Color.FromArgb(37, 99, 235);
		internal static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
		internal static readonly Color AccentSoft = Color.FromArgb(232, 240, 254);
		internal static readonly Color Success = Color.FromArgb(22, 128, 61);
		internal static readonly Color SuccessSoft = Color.FromArgb(230, 245, 236);
		internal static readonly Color Warning = Color.FromArgb(180, 83, 9);
		internal static readonly Color WarningSoft = Color.FromArgb(254, 243, 226);
		internal static readonly Color Highlight = Color.FromArgb(255, 236, 153);

		internal const string FontFamily = "Segoe UI";

		internal static readonly Font Body = new Font(FontFamily, 9.75F);
		internal static readonly Font BodyBold = new Font(FontFamily, 9.75F, FontStyle.Bold);
		internal static readonly Font Small = new Font(FontFamily, 8.75F);
		internal static readonly Font SectionHeader = new Font(FontFamily, 8.25F, FontStyle.Bold);
		internal static readonly Font Subtitle = new Font(FontFamily, 11F, FontStyle.Bold);
		internal static readonly Font Title = new Font(FontFamily, 16F, FontStyle.Bold);
		internal static readonly Font AppTitle = new Font(FontFamily, 12F, FontStyle.Bold);

		internal static Button PrimaryButton(string text)
		{
			var button = BaseButton(text);

			button.BackColor = Accent;
			button.ForeColor = Color.White;
			button.FlatAppearance.BorderColor = Accent;
			button.FlatAppearance.MouseOverBackColor = AccentHover;
			button.FlatAppearance.MouseDownBackColor = AccentHover;
			button.Font = BodyBold;

			return button;
		}

		internal static Button SecondaryButton(string text)
		{
			var button = BaseButton(text);

			button.BackColor = Surface;
			button.ForeColor = Text;
			button.FlatAppearance.BorderColor = Border;
			button.FlatAppearance.MouseOverBackColor = Background;
			button.FlatAppearance.MouseDownBackColor = Border;

			return button;
		}

		/// <summary>
		/// A button that looks like a text link. Being a real button, it works with Enter/Space and screen readers.
		/// </summary>
		internal static Button LinkButton(string text, Font font = null)
		{
			var button = new Button
			{
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				BackColor = Color.Transparent,
				Cursor = Cursors.Hand,
				FlatStyle = FlatStyle.Flat,
				Font = new Font(font ?? Body, FontStyle.Underline),
				ForeColor = Accent,
				Margin = new Padding(0),
				Padding = new Padding(0),
				Text = text,
				TextAlign = ContentAlignment.MiddleLeft,
				UseVisualStyleBackColor = false
			};

			button.FlatAppearance.BorderSize = 0;
			button.FlatAppearance.MouseDownBackColor = Color.Transparent;
			button.FlatAppearance.MouseOverBackColor = Color.Transparent;

			return button;
		}

		internal static Label Label(string text, Font font = null, Color? color = null)
		{
			return new Label
			{
				AutoSize = true,
				BackColor = Color.Transparent,
				Font = font ?? Body,
				ForeColor = color ?? Text,
				Margin = new Padding(0),
				Text = text
			};
		}

		/// <summary>
		/// A label that wraps inside the given width.
		/// </summary>
		internal static Label WrappingLabel(string text, int width, Font font = null, Color? color = null)
		{
			var label = Label(text, font, color);

			label.MaximumSize = new Size(width, 0);

			return label;
		}

		internal static TextBox TextBox(int width)
		{
			return new TextBox
			{
				BorderStyle = BorderStyle.FixedSingle,
				Font = new Font(FontFamily, 10.5F),
				Margin = new Padding(0),
				Width = width
			};
		}

		private static Button BaseButton(string text)
		{
			var button = new Button
			{
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowOnly,
				Cursor = Cursors.Hand,
				FlatStyle = FlatStyle.Flat,
				Font = Body,
				Height = 34,
				MinimumSize = new Size(96, 34),
				Padding = new Padding(10, 0, 10, 0),
				Text = text,
				UseVisualStyleBackColor = false
			};

			button.FlatAppearance.BorderSize = 1;

			return button;
		}
	}
}
