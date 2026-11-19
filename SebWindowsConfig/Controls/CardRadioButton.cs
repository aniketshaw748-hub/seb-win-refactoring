using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SebWindowsConfig.UserInterface;

namespace SebWindowsConfig.Controls
{
	/// <summary>
	/// A radio button drawn as a selectable card with a title, a description and optional bullet points.
	/// Cards with the same parent form one group, exactly like normal radio buttons.
	/// </summary>
	internal class CardRadioButton : RadioButton
	{
		private const int Pad = 14;
		private const int DotSize = 18;
		private const int TextLeft = Pad + DotSize + 12;

		private readonly string description;
		private readonly string[] bullets;
		private string badge;
		private bool hovering;

		internal CardRadioButton(string title, string description, string[] bullets, int width)
		{
			this.bullets = bullets ?? new string[0];
			this.description = description;

			AccessibleDescription = description;
			AutoSize = false;
			Cursor = Cursors.Hand;
			Font = Theme.Body;
			Margin = new Padding(0, 0, 0, 10);
			Text = title;
			Width = width;

			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			UpdateHeight();
		}

		/// <summary>
		/// A short tag shown next to the title, e.g. "Recommended".
		/// </summary>
		internal string Badge
		{
			get { return badge; }
			set { badge = value; Invalidate(); }
		}

		protected override void OnCheckedChanged(EventArgs e)
		{
			base.OnCheckedChanged(e);
			Invalidate();
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
			var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
			var fill = Checked ? Theme.AccentSoft : hovering ? Theme.Background : Theme.Surface;
			var border = Checked ? Theme.Accent : Theme.Border;

			graphics.Clear(Parent?.BackColor ?? Theme.Surface);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;

			using (var path = ToggleSwitch.RoundedRectangle(bounds, 8))
			using (var brush = new SolidBrush(fill))
			using (var pen = new Pen(border, Checked ? 2 : 1))
			{
				graphics.FillPath(brush, path);
				graphics.DrawPath(pen, path);
			}

			var dot = new Rectangle(Pad, Pad + 1, DotSize, DotSize);

			using (var pen = new Pen(Checked ? Theme.Accent : Color.FromArgb(160, 167, 177), 2))
			{
				graphics.DrawEllipse(pen, dot);
			}

			if (Checked)
			{
				using (var brush = new SolidBrush(Theme.Accent))
				{
					graphics.FillEllipse(brush, dot.X + 4, dot.Y + 4, dot.Width - 8, dot.Height - 8);
				}
			}

			var textWidth = Width - TextLeft - Pad;
			var y = Pad;
			var titleSize = TextRenderer.MeasureText(graphics, Text, Theme.Subtitle);

			TextRenderer.DrawText(graphics, Text, Theme.Subtitle, new Point(TextLeft, y), Theme.Text);

			if (!string.IsNullOrEmpty(badge))
			{
				var badgeSize = TextRenderer.MeasureText(graphics, badge, Theme.SectionHeader);
				var badgeBounds = new Rectangle(TextLeft + titleSize.Width + 4, y + 3, badgeSize.Width + 8, badgeSize.Height + 2);

				using (var path = ToggleSwitch.RoundedRectangle(badgeBounds, (badgeBounds.Height - 1) / 2))
				using (var brush = new SolidBrush(Theme.SuccessSoft))
				{
					graphics.FillPath(brush, path);
				}

				TextRenderer.DrawText(graphics, badge, Theme.SectionHeader, badgeBounds, Theme.Success, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			}

			y += titleSize.Height + 2;

			if (!string.IsNullOrEmpty(description))
			{
				var height = Measure(description, Theme.Body, textWidth);
				TextRenderer.DrawText(graphics, description, Theme.Body, new Rectangle(TextLeft, y, textWidth, height), Theme.MutedText, TextFormatFlags.WordBreak);
				y += height + 4;
			}

			foreach (var bullet in bullets)
			{
				var height = Measure(bullet, Theme.Small, textWidth - 14);

				using (var brush = new SolidBrush(Theme.MutedText))
				{
					graphics.FillEllipse(brush, TextLeft + 2, y + 7, 4, 4);
				}

				TextRenderer.DrawText(graphics, bullet, Theme.Small, new Rectangle(TextLeft + 14, y, textWidth - 14, height), Theme.Text, TextFormatFlags.WordBreak);
				y += height + 2;
			}

			if (Focused && ShowFocusCues)
			{
				ControlPaint.DrawFocusRectangle(graphics, new Rectangle(4, 4, Width - 9, Height - 9));
			}
		}

		private void UpdateHeight()
		{
			var textWidth = Math.Max(20, Width - TextLeft - Pad);
			var height = Pad + TextRenderer.MeasureText(Text ?? "", Theme.Subtitle).Height + 2;

			if (!string.IsNullOrEmpty(description))
			{
				height += Measure(description, Theme.Body, textWidth) + 4;
			}

			foreach (var bullet in bullets)
			{
				height += Measure(bullet, Theme.Small, textWidth - 14) + 2;
			}

			Height = height + Pad;
		}

		private static int Measure(string text, Font font, int width)
		{
			return TextRenderer.MeasureText(text, font, new Size(Math.Max(10, width), int.MaxValue), TextFormatFlags.WordBreak).Height;
		}
	}
}
