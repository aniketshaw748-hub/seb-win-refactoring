using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SebWindowsConfig.UserInterface;

namespace SebWindowsConfig.Controls
{
	/// <summary>
	/// An on/off switch with a title and an optional description underneath. It is a <see cref="CheckBox"/> underneath,
	/// so keyboard (space), screen readers and UI automation treat it like a normal check box.
	/// </summary>
	internal class ToggleSwitch : CheckBox
	{
		private const int TrackWidth = 40;
		private const int TrackHeight = 22;
		private const int TextGap = 12;

		private string description;

		internal ToggleSwitch(string title, string description, int width)
		{
			this.description = description;

			AutoSize = false;
			Cursor = Cursors.Hand;
			Font = Theme.Body;
			Margin = new Padding(0, 0, 0, 10);
			Text = title;
			Width = width;

			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			UpdateHeight();
		}

		internal string Description
		{
			get { return description; }
			set { description = value; UpdateHeight(); Invalidate(); }
		}

		protected override void OnFontChanged(EventArgs e)
		{
			base.OnFontChanged(e);
			UpdateHeight();
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			base.OnSizeChanged(e);

			if (IsHandleCreated)
			{
				UpdateHeight();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			var graphics = e.Graphics;
			var background = Parent?.BackColor ?? Theme.Surface;

			graphics.Clear(background);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;

			var track = new Rectangle(0, 2, TrackWidth, TrackHeight);
			var trackColor = !Enabled ? Theme.Border : Checked ? Theme.Accent : Color.FromArgb(160, 167, 177);

			using (var path = RoundedRectangle(track, TrackHeight / 2))
			using (var brush = new SolidBrush(trackColor))
			{
				graphics.FillPath(brush, path);
			}

			var knobSize = TrackHeight - 6;
			var knobX = Checked ? track.Right - knobSize - 3 : track.Left + 3;

			using (var brush = new SolidBrush(Color.White))
			{
				graphics.FillEllipse(brush, knobX, track.Top + 3, knobSize, knobSize);
			}

			var textLeft = TrackWidth + TextGap;
			var textWidth = Math.Max(10, Width - textLeft);
			var titleColor = Enabled ? Theme.Text : Theme.MutedText;
			var titleHeight = TextRenderer.MeasureText(graphics, Text, Theme.BodyBold, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height;

			TextRenderer.DrawText(graphics, Text, Theme.BodyBold, new Rectangle(textLeft, 3, textWidth, titleHeight), titleColor, TextFormatFlags.WordBreak);

			if (!string.IsNullOrEmpty(description))
			{
				var descriptionTop = 3 + titleHeight + 1;
				TextRenderer.DrawText(graphics, description, Theme.Small, new Rectangle(textLeft, descriptionTop, textWidth, Height - descriptionTop), Theme.MutedText, TextFormatFlags.WordBreak);
			}

			if (Focused && ShowFocusCues)
			{
				ControlPaint.DrawFocusRectangle(graphics, new Rectangle(textLeft - 3, 1, textWidth + 2, Height - 2));
			}
		}

		private void UpdateHeight()
		{
			var textWidth = Math.Max(10, Width - TrackWidth - TextGap);
			var height = TextRenderer.MeasureText(Text ?? "", Theme.BodyBold, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height + 6;

			if (!string.IsNullOrEmpty(description))
			{
				height += TextRenderer.MeasureText(description, Theme.Small, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height + 1;
			}

			Height = Math.Max(TrackHeight + 6, height);
		}

		internal static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
		{
			var path = new GraphicsPath();
			var diameter = radius * 2;

			path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
			path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
			path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
			path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
			path.CloseFigure();

			return path;
		}
	}
}
