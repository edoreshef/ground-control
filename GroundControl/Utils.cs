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
            return true;
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
        public static Dictionary<Keys, int> NumKeyToInt = new Dictionary<Keys, int>()
        {
            { Keys.D0, 0},
            { Keys.D1, 1},
            { Keys.D2, 2},
            { Keys.D3, 3},
            { Keys.D4, 4},
            { Keys.D5, 5},
            { Keys.D6, 6},
            { Keys.D7, 7},
            { Keys.D8, 8},
            { Keys.D9, 9},
        };

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

        public static double LogCeil(double value, double logBase)
        {
            var logValue = Math.Log(value, logBase);
            logValue = Math.Ceiling(Math.Round(logValue, 2));
            return Math.Pow(logBase, logValue);
        }

        public static int EnsureRange(int value, int minValue, int maxValue)
        {
            return Math.Max(minValue, Math.Min(maxValue, value));
        }
        public static float EnsureRange(float value, float minValue, float maxValue)
        {
            return Math.Max(minValue, Math.Min(maxValue, value));
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

        public static Rectangle SetWidthMoveLeft(this Rectangle rect, int newWidth)
        {
            return new Rectangle(rect.Right - newWidth, rect.Top, newWidth, rect.Height);
        }

        public static Rectangle SetWidthMoveRight(this Rectangle rect, int newWidth)
        {
            return new Rectangle(rect.Left, rect.Top, newWidth, rect.Height);
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
