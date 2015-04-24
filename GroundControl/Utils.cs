using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Windows.Forms
{
    public class PanelEx : Forms.Panel
    {
        public PanelEx()
            : base()
        {
            this.SetStyle(ControlStyles.Selectable, true);
            this.TabStop = true;
            this.DoubleBuffered = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            this.Focus();
            base.OnMouseDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData != Keys.Tab;
            //if (keyData == Keys.Up || keyData == Keys.Down) return true;
            //if (keyData == Keys.Left || keyData == Keys.Right) return true;
            //return base.IsInputKey(keyData);
        }

        protected override void OnEnter(EventArgs e)
        {
            this.Invalidate();
            base.OnEnter(e);
        }

        protected override void OnLeave(EventArgs e)
        {
            this.Invalidate();
            base.OnLeave(e);
        }

        /*protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);
            if (this.Focused)
            {
                var rc = this.ClientRectangle;
                rc.Inflate(-2, -2);
                ControlPaint.DrawFocusRectangle(pe.Graphics, rc);
            }
        }*/
    }

    public class Utils
    {
        public static Color Gray(byte level)
        {
            unchecked
            {
                return Color.FromArgb((Int32)0xFF000000 | (level << 16) | (level << 8) | (level << 0));
            }
        }

        public static Color RGB(int rgb)
        {
            unchecked
            {
                return Color.FromArgb((Int32)0xFF000000 | rgb);
            }
        }

        public static Color ARGB(uint argb)
        {
            unchecked
            {
                return Color.FromArgb((int)argb);
            }
        }

        public static int DivRoundUp(int num, int divider)
        {
            return (num + divider - 1) / divider;
        }

    }

    public static class MyExtensions
    {
        public static Rectangle Expand(this Rectangle rect, int left = 0, int right = 0, int top = 0, int bottom = 0)
        {
            return new Rectangle(rect.X - left, rect.Y - top, rect.Width + left + right, rect.Height + top + bottom);
        }

        public static Rectangle Pan(this Rectangle rect, int left = 0, int right = 0, int top = 0, int bottom = 0)
        {
            return new Rectangle(rect.X - left + right, rect.Y - top + bottom, rect.Width, rect.Height);
        }

        public static void DrawLine(this Graphics g, Pen pen, Rectangle rect)
        {
            g.DrawLine(pen, rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
    }

    public static class ListExt
    {
        public static void AddSorted<T>(this List<T> @this, T item) where T : IComparable<T>
        {
            if (@this.Count == 0)
            {
                @this.Add(item);
                return;
            }
            if (@this[@this.Count - 1].CompareTo(item) <= 0)
            {
                @this.Add(item);
                return;
            }
            if (@this[0].CompareTo(item) >= 0)
            {
                @this.Insert(0, item);
                return;
            }
            int index = @this.BinarySearch(item);
            if (index < 0)
                index = ~index;
            @this.Insert(index, item);
        }
    }
}
