// src/windows/GamepadCanvas.cs
// Renders an imported input-overlay skin image plus clickable hotspots for each
// bindable element, so the user can click a button on the picture of their
// controller instead of picking it from a plain list.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DualKey
{
    public class GamepadCanvas : Panel
    {
        private Image skinImage;
        private List<OverlayElement> elements = new List<OverlayElement>();
        private List<List<OverlayElement>> groups = new List<List<OverlayElement>>();

        private (int, int, int, int)? hoverKey;
        private (int, int, int, int)? selectedKey;
        private string hoverLabel;

        private float scale = 1f;
        private float offsetX;
        private float offsetY;

        /// <summary>Raised when the user picks a specific control (resolving any overlap via a menu).</summary>
        public event EventHandler<OverlayElement> ElementSelected;

        public GamepadCanvas()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(250, 250, 250);
            Cursor = Cursors.Default;
        }

        public void SetSkin(Image image, IEnumerable<OverlayElement> bindableElements)
        {
            if (skinImage != null && !ReferenceEquals(skinImage, image))
                skinImage.Dispose();

            skinImage = image;
            elements = bindableElements.ToList();
            groups = BuildGroups(elements);
            hoverKey = null;
            selectedKey = null;
            hoverLabel = null;
            Invalidate();
        }

        public void Highlight(OverlayElement element)
        {
            if (element == null)
                selectedKey = null;
            else
                selectedKey = KeyOf(element);
            Invalidate();
        }

        private static (int, int, int, int) KeyOf(OverlayElement el)
        {
            return (el.X, el.Y, el.Width, el.Height);
        }

        private static List<List<OverlayElement>> BuildGroups(List<OverlayElement> els)
        {
            var map = new Dictionary<(int, int, int, int), List<OverlayElement>>();
            var order = new List<(int, int, int, int)>();

            foreach (var el in els)
            {
                var key = KeyOf(el);
                List<OverlayElement> list;
                if (!map.TryGetValue(key, out list))
                {
                    list = new List<OverlayElement>();
                    map[key] = list;
                    order.Add(key);
                }
                list.Add(el);
            }

            var result = new List<List<OverlayElement>>();
            foreach (var key in order)
                result.Add(map[key]);
            return result;
        }

        private void RecalcTransform()
        {
            if (skinImage == null)
            {
                scale = 1f;
                offsetX = 0;
                offsetY = 0;
                return;
            }

            float availW = Math.Max(1, ClientSize.Width - 16);
            float availH = Math.Max(1, ClientSize.Height - 16);
            float s = Math.Min(availW / skinImage.Width, availH / skinImage.Height);
            if (s <= 0) s = 1f;
            s = Math.Min(s, 1.5f); // don't blow tiny skins up too aggressively

            scale = s;
            offsetX = (ClientSize.Width - skinImage.Width * scale) / 2f;
            offsetY = (ClientSize.Height - skinImage.Height * scale) / 2f;
        }

        private RectangleF GroupScreenRect(List<OverlayElement> group)
        {
            var el = group[0];
            return new RectangleF(
                offsetX + el.X * scale,
                offsetY + el.Y * scale,
                el.Width * scale,
                el.Height * scale);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RecalcTransform();

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (skinImage == null)
            {
                using (var f = new Font("Segoe UI", 10f))
                using (var b = new SolidBrush(Color.Gray))
                {
                    string text = "Import a gamepad layout (.png + .json) to begin.";
                    SizeF size = g.MeasureString(text, f);
                    g.DrawString(text, f, b, (ClientSize.Width - size.Width) / 2, (ClientSize.Height - size.Height) / 2);
                }
                return;
            }

            g.DrawImage(skinImage, offsetX, offsetY, skinImage.Width * scale, skinImage.Height * scale);

            foreach (var group in groups)
            {
                RectangleF rect = GroupScreenRect(group);
                var key = KeyOf(group[0]);
                bool isSelected = selectedKey.HasValue && selectedKey.Value == key;
                bool isHover = hoverKey.HasValue && hoverKey.Value == key;

                Color fill = isSelected ? Color.FromArgb(90, 30, 144, 255)
                    : isHover ? Color.FromArgb(70, 255, 205, 0)
                    : Color.FromArgb(45, 50, 205, 50);
                Color border = isSelected ? Color.DeepSkyBlue
                    : isHover ? Color.Gold
                    : Color.FromArgb(160, 34, 139, 34);

                using (var brush = new SolidBrush(fill))
                    g.FillRectangle(brush, rect);
                using (var pen = new Pen(border, isSelected ? 2.5f : 1.5f))
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            if (!string.IsNullOrEmpty(hoverLabel))
            {
                using (var f = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                {
                    SizeF size = g.MeasureString(hoverLabel, f);
                    float pad = 4f;
                    var box = new RectangleF(8, 8, size.Width + pad * 2, size.Height + pad * 2);
                    using (var bg = new SolidBrush(Color.FromArgb(230, 30, 30, 30)))
                        g.FillRectangle(bg, box);
                    using (var tb = new SolidBrush(Color.White))
                        g.DrawString(hoverLabel, f, tb, box.X + pad, box.Y + pad);
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            RecalcTransform();

            List<OverlayElement> group = HitTest(e.Location);
            (int, int, int, int)? key = null;
            if (group != null) key = KeyOf(group[0]);

            if (!Equals(key, hoverKey))
            {
                hoverKey = key;
                hoverLabel = group != null ? string.Join(" / ", group.Select(x => x.Id)) : null;
                Cursor = group != null ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverKey != null)
            {
                hoverKey = null;
                hoverLabel = null;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            RecalcTransform();

            List<OverlayElement> group = HitTest(e.Location);
            if (group == null) return;

            if (group.Count == 1)
            {
                var handler = ElementSelected;
                if (handler != null) handler(this, group[0]);
                return;
            }

            // Several elements share the exact same screen position (e.g. the four
            // frames of a d-pad) - let the user pick which one they mean.
            var menu = new ContextMenuStrip();
            foreach (var el in group)
            {
                var captured = el;
                menu.Items.Add(el.Id, null, (s, args) =>
                {
                    var handler = ElementSelected;
                    if (handler != null) handler(this, captured);
                });
            }
            menu.Show(this, e.Location);
        }

        private List<OverlayElement> HitTest(Point clientPoint)
        {
            // Later groups are drawn on top, so test them first.
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                RectangleF rect = GroupScreenRect(groups[i]);
                if (rect.Contains(clientPoint))
                    return groups[i];
            }
            return null;
        }
    }
}
