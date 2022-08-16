using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MagnetWinForms
{
    public class MagnetWinForms : NativeWindow
    {
        #region Private Window API Related
        
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static private extern bool GetWindowRect(IntPtr hWnd, ref WinRect lpWinRect);

        // ReSharper disable InconsistentNaming
        const int WM_SIZING        = 0x214;
        const int WM_MOVING        = 0x216;
        const int WM_ENTERSIZEMOVE = 0x231;
        
        const int WMSZ_LEFT   = 1;
        const int WMSZ_RIGHT  = 2;
        const int WMSZ_TOP    = 3;
        const int WMSZ_BOTTOM = 6;
        // ReSharper restore InconsistentNaming

        #endregion

        #region Private global members

        private static readonly List<Form> Forms = new List<Form>();

        #endregion

        #region Private members

        private readonly Form m_OriginalForm;
        private Point m_MoveSum;
        private Point m_MovePrevPos;
        private List<Tuple<Form, EdgeDirection>> m_MoveLocks   = new List<Tuple<Form, EdgeDirection>>();
        private List<Tuple<Form, EdgeDirection>> m_ResizeLocks = new List<Tuple<Form, EdgeDirection>>();

        #endregion

        #region Public Constructor

        public MagnetWinForms(Form form)
        {
            // Store form referance
            m_OriginalForm = form;

            // Add to form list
            Forms.Add(form);

            // Assign handle
            AssignHandle(form.Handle);
        }

        #endregion

        #region Private/Protected methods

        private int FindClosestEdge(MagnetEdge refEdge)
        {
            if (refEdge == null)
                return int.MinValue;

            // get all forms edges list
            var edges = Forms
                .Where(frm => (frm != m_OriginalForm) && frm.Visible)
                .SelectMany(MagnetUtils.FormToEdges)
                .Where(t => !m_MoveLocks.Any(q => (q.Item1 == t.Window) && (q.Item2 == MagnetUtils.OppsiteEdge(t.EdgeDir))))
                .ToList();
            var corners = Forms
                .Where(frm => (frm != m_OriginalForm) && frm.Visible)
                .SelectMany(MagnetUtils.FormToCorners)
                .Where(t => !m_ResizeLocks.Any(q => (q.Item1 == t.Window) && (q.Item2 == t.AttractsEdge)))
                .ToList();

            var bestDist = int.MaxValue;
            foreach (var edge in edges)
            {
                var dist = edge.MeasureAttraction(refEdge);
                if (dist != int.MinValue)
                    if (Math.Abs(dist) < bestDist)
                        bestDist = dist;
            }

            foreach (var corner in corners)
            {
                var dist = corner.MeasureAttraction(refEdge);
                if (dist != int.MinValue)
                    if (Math.Abs(dist) < bestDist)
                        bestDist = dist;
            }

            return bestDist;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ENTERSIZEMOVE)
            {
                // Reset move tracking
                var currRect = new WinRect();
                GetWindowRect(Handle, ref currRect);
                m_MoveSum = new Point(currRect.Left, currRect.Top);
                m_MovePrevPos = new Point(currRect.Left, currRect.Top);

                // Scan for move locks
                m_MoveLocks.Clear();
                var otherEdges = Forms
                    .Where(frm => (frm != m_OriginalForm) && frm.Visible)
                    .Where(frm => Forms.IndexOf(m_OriginalForm) == 0)
                    .SelectMany(MagnetUtils.FormToEdges).ToList();

                var windowEdges = MagnetUtils.RectToEdges(currRect.ToRectangle());
                foreach (var windowEdge in windowEdges)
                    foreach (var otherEdge in otherEdges)
                        if (windowEdge.MeasureAttraction(otherEdge) == 0)
                            m_MoveLocks.Add(new Tuple<Form, EdgeDirection>(otherEdge.Window, windowEdge.EdgeDir));

                // Scan for resize locks
                m_ResizeLocks.Clear();
                foreach (var windowEdge in windowEdges)
                    foreach (var otherEdge in otherEdges)
                        if ((windowEdge.EdgeDir == otherEdge.EdgeDir) &&
                            ((windowEdge.Start == otherEdge.End) ||
                            (windowEdge.End == otherEdge.Start)))
                            m_ResizeLocks.Add(new Tuple<Form, EdgeDirection>(otherEdge.Window, otherEdge.EdgeDir));

                // Remove resize locks duplications
                m_ResizeLocks = m_ResizeLocks.GroupBy(a => a.Item1.GetHashCode() ^ a.Item2.GetHashCode()).Select(grp => grp.First()).ToList();
            }

            else if (m.Msg == WM_SIZING)
            {
                var rc = (WinRect)Marshal.PtrToStructure(m.LParam, typeof(WinRect));

                // Convert WinRect into edge list
                var resizeRect = rc.ToRectangle();
                var windowEdges = MagnetUtils.RectToEdges(resizeRect).ToDictionary(edge => edge.EdgeDir, edge => edge);

                // Create resize map
                var map = new Dictionary<int, EdgeDirection[]>
                {
                    {WMSZ_LEFT,                new[] {EdgeDirection.Left}},
                    {WMSZ_RIGHT,               new[] {EdgeDirection.Right}},
                    {WMSZ_TOP,                 new[] {EdgeDirection.Top}},
                    {WMSZ_BOTTOM,              new[] {EdgeDirection.Bottom}},
                    {WMSZ_LEFT + WMSZ_TOP,     new[] {EdgeDirection.Left,  EdgeDirection.Top}},
                    {WMSZ_LEFT + WMSZ_BOTTOM,  new[] {EdgeDirection.Left,  EdgeDirection.Bottom}},
                    {WMSZ_RIGHT + WMSZ_TOP,    new[] {EdgeDirection.Right, EdgeDirection.Top}},
                    {WMSZ_RIGHT + WMSZ_BOTTOM, new[] {EdgeDirection.Right, EdgeDirection.Bottom}},
                };

                // Edge list
                EdgeDirection[] edgeList;
                if (!map.TryGetValue(m.WParam.ToInt32(), out edgeList))
                    edgeList = new EdgeDirection[0];

                // Handle horizontal resize
                foreach (var edgeDir in edgeList)
                {
                    var movingEdge = windowEdges[edgeDir];
                    var magDistance = FindClosestEdge(movingEdge);
                    if (Math.Abs(magDistance) < 20)
                        resizeRect = MagnetUtils.ShiftRectangleEdge(resizeRect, edgeDir, magDistance);
                }

                // Rebuild new position edges
                windowEdges = MagnetUtils.RectToEdges(resizeRect).ToDictionary(a => a.EdgeDir, a => a);

                // Enforce resize locks
                foreach (var resizeLock in m_ResizeLocks)
                {
                    var otherRect = resizeLock.Item1.Bounds;
                    otherRect = MagnetUtils.SetRectangleEdge(otherRect, windowEdges[resizeLock.Item2]);
                    resizeLock.Item1.SetBounds(otherRect.X, otherRect.Y, otherRect.Width, otherRect.Height);
                }

                // Enfore move-on-resize windows
                foreach (var moveLock in m_MoveLocks)
                {
                    var otherRect = moveLock.Item1.Bounds;
                    otherRect = MagnetUtils.SetEntireRectangleEdge(otherRect, windowEdges[moveLock.Item2].GetOppsiteEdge());
                    moveLock.Item1.SetBounds(otherRect.X, otherRect.Y, otherRect.Width, otherRect.Height);
                }

                // Convert rect to LParam
                rc = WinRect.FromRectangle(resizeRect);
                Marshal.StructureToPtr(rc, m.LParam, true);

                m.Result = new IntPtr(1);
            }

            else if (m.Msg == WM_MOVING)
            {
                var rc = (WinRect)Marshal.PtrToStructure(m.LParam, typeof(WinRect));

                // Update MoveSum
                m_MoveSum.X += rc.Left - m_MovePrevPos.X;
                m_MoveSum.Y += rc.Top - m_MovePrevPos.Y;

                // Convert WinRect into edge list
                var moveRect = rc.ToRectangle();
                moveRect.Location = m_MoveSum;

                // Do magnetics twice, this has to be done so edge-to-edge works first and only then edge-to-corner works
                for (int i = 0; i < 2; i++)
                {
                    // Scan all edges directions and check if we can stick to other windows
                    foreach (EdgeDirection edgeType in Enum.GetValues(typeof(EdgeDirection)))
                    {
                        // Get edge info
                        var edge = MagnetUtils.RectToEdges(moveRect).First(t => t.EdgeDir == edgeType);

                        // Stick to other windows
                        var magDistance = FindClosestEdge(edge);
                        if (Math.Abs(magDistance) < 20)
                            moveRect = MagnetUtils.ShiftEntireRectangle(moveRect, edge.EdgeDir, magDistance);
                    }
                }

                // Move other windows
                foreach (var lockedWindows in m_MoveLocks)
                {
                    var otherRect = lockedWindows.Item1.Bounds;
                    otherRect.Offset(moveRect.Location.X - m_MovePrevPos.X, moveRect.Location.Y - m_MovePrevPos.Y);
                    lockedWindows.Item1.SetBounds(otherRect.X, otherRect.Y, otherRect.Width, otherRect.Height);
                }

                // Save position to track delta
                m_MovePrevPos = moveRect.Location;

                // Convert rect to LParam
                rc = WinRect.FromRectangle(moveRect);
                Marshal.StructureToPtr(rc, m.LParam, true);

                m.Result = new IntPtr(1);
            }

            base.WndProc(ref m);
        }

        #endregion
    }

    internal enum EdgeDirection
    {
        Top,
        Right,
        Bottom,
        Left
    };

    internal enum EdgeEnd
    {
        Near,
        Far
    }

    internal class MagnetCorner
    {
        public Form          Window;
        public Point         Position;
        public EdgeDirection AttractsEdge;
        public EdgeEnd       AttractsEdgeEnd;
        public int           OffsetDirection;

        public int MeasureAttraction(MagnetEdge other)
        {
            if (other == null)
                return int.MinValue;

            // Do not compare to self
            if (Window == other.Window)
                return int.MinValue;

            // Is edge effected by this corner?
            if (other.EdgeDir != AttractsEdge)
                return int.MinValue;

            // Select near/far
            var edgeEnd = AttractsEdgeEnd == EdgeEnd.Near ? other.Start : other.End;

            // Select axis
            var aboveOrBelow = (AttractsEdge == EdgeDirection.Left) || (AttractsEdge == EdgeDirection.Right);

            // Check if edge touchs the corner
            var otherPos = aboveOrBelow ? edgeEnd.Y : edgeEnd.X;
            var cornerPos = aboveOrBelow ? Position.Y : Position.X;
            if (otherPos != cornerPos)
                return int.MinValue;

            // Compute offset
            otherPos = aboveOrBelow ? edgeEnd.X : edgeEnd.Y;
            cornerPos = aboveOrBelow ? Position.X : Position.Y;
            return OffsetDirection*(otherPos - cornerPos);
        }
    }

    internal class MagnetEdge
    {
        public Form            Window;
        public Point           Start;
        public Point           End;
        public EdgeDirection   EdgeDir;

        public MagnetEdge GetOppsiteEdge()
        {
            return new MagnetEdge {EdgeDir = MagnetUtils.OppsiteEdge(EdgeDir), End = End, Start = Start, Window = null};
        }

        public int MeasureAttraction(MagnetEdge other)
        {
            if (other == null)
                return int.MinValue;

            // Do not compare to self
            if (Window == other.Window)
                return int.MinValue;

            // Top-to-Bottom match
            if ((EdgeDir == EdgeDirection.Top) && (other.EdgeDir == EdgeDirection.Bottom) &&
                (Start.X < other.End.X) && (End.X > other.Start.X))
                return Start.Y - other.Start.Y;

            // Bottom-to-Top match
            if ((EdgeDir == EdgeDirection.Bottom) && (other.EdgeDir == EdgeDirection.Top) &&
                (Start.X < other.End.X) && (End.X > other.Start.X))
                return other.Start.Y - Start.Y;

            // Left-to-Right match
            if ((EdgeDir == EdgeDirection.Left) && (other.EdgeDir == EdgeDirection.Right) &&
                (Start.Y < other.End.Y) && (End.Y > other.Start.Y))
                return Start.X - other.Start.X;

            // Right-to-Left match
            if ((EdgeDir == EdgeDirection.Right) && (other.EdgeDir == EdgeDirection.Left) &&
                (Start.Y < other.End.Y) && (End.Y > other.Start.Y))
                return other.Start.X - Start.X;

            return int.MinValue;
        }
    }

    internal class MagnetUtils
    {
        public static EdgeDirection OppsiteEdge(EdgeDirection edgeDir)
        {
            if (edgeDir == EdgeDirection.Bottom) return EdgeDirection.Top;
            if (edgeDir == EdgeDirection.Top) return EdgeDirection.Bottom;
            if (edgeDir == EdgeDirection.Left) return EdgeDirection.Right;
            if (edgeDir == EdgeDirection.Right) return EdgeDirection.Left;
            return EdgeDirection.Right;
        }

        public static Rectangle ShiftRectangleEdge(Rectangle rect, EdgeDirection edgeToMove, int distTomove)
        {
            // Find edge to move
            switch (edgeToMove)
            {
                case EdgeDirection.Left:
                    rect.X -= distTomove;
                    rect.Width += distTomove;
                    break;

                case EdgeDirection.Right:
                    rect.Width += distTomove;
                    break;

                case EdgeDirection.Top:
                    rect.Y -= distTomove;
                    rect.Height += distTomove;
                    break;

                case EdgeDirection.Bottom:
                    rect.Height += distTomove;
                    break;
            }

            return rect;
        }

        public static Rectangle ShiftEntireRectangle(Rectangle rect, EdgeDirection edgeToMove, int distTomove)
        {
            switch (edgeToMove)
            {
                case EdgeDirection.Left:
                    rect.X -= distTomove;
                    break;

                case EdgeDirection.Right:
                    rect.X += distTomove;
                    break;

                case EdgeDirection.Top:
                    rect.Y -= distTomove;
                    break;

                case EdgeDirection.Bottom:
                    rect.Y += distTomove;
                    break;
            }

            return rect;
        }

        public static Rectangle SetRectangleEdge(Rectangle rect, MagnetEdge edge)
        {
            var prevRight = rect.Right;
            var prevBottom = rect.Bottom;
            switch (edge.EdgeDir)
            {
                case EdgeDirection.Left:
                    rect.X = edge.Start.X;
                    rect.Width = prevRight - edge.Start.X;
                    break;

                case EdgeDirection.Right:
                    rect.Width = edge.Start.X - rect.Left;
                    break;

                case EdgeDirection.Top:
                    rect.Y = edge.Start.Y;
                    rect.Height = prevBottom - edge.Start.Y;
                    break;

                case EdgeDirection.Bottom:
                    rect.Height = edge.Start.Y - rect.Top;
                    break;
            }

            return rect;
        }

        public static Rectangle SetEntireRectangleEdge(Rectangle rect, MagnetEdge edge)
        {
            switch (edge.EdgeDir)
            {
                case EdgeDirection.Left:
                    rect.X = edge.Start.X;
                    break;

                case EdgeDirection.Right:
                    rect.X = edge.Start.X - rect.Width;
                    break;

                case EdgeDirection.Top:
                    rect.Y = edge.Start.Y;
                    break;

                case EdgeDirection.Bottom:
                    rect.Y = edge.Start.Y - rect.Height;
                    break;
            }
            return rect;
        }

        public static List<MagnetEdge> FormToEdges(Form frm)
        {
            var ret = RectToEdges(frm.Bounds);
            ret.ForEach(t => t.Window = frm);
            return ret;
        }

        public static List<MagnetEdge> RectToEdges(Rectangle rect)
        {
            var ret = new List<MagnetEdge>();

            // Left
            ret.Add(new MagnetEdge
            {
                Start = new Point(rect.Left, rect.Top),
                End = new Point(rect.Left, rect.Bottom),
                EdgeDir = EdgeDirection.Left
            });

            // Top
            ret.Add(new MagnetEdge
            {
                Start = new Point(rect.Left, rect.Top),
                End = new Point(rect.Right, rect.Top),
                EdgeDir = EdgeDirection.Top
            });

            // Right
            ret.Add(new MagnetEdge
            {
                Start = new Point(rect.Right, rect.Top),
                End = new Point(rect.Right, rect.Bottom),
                EdgeDir = EdgeDirection.Right,
            });

            // Bottom
            ret.Add(new MagnetEdge
            {
                Start = new Point(rect.Left, rect.Bottom),
                End = new Point(rect.Right, rect.Bottom),
                EdgeDir = EdgeDirection.Bottom,
            });

            return ret;
        }

        public static List<MagnetCorner> FormToCorners(Form frm)
        {
            var ret = RectToCorners(frm.Bounds);
            ret.ForEach(t => t.Window = frm);
            return ret;
        }

        public static List<MagnetCorner> RectToCorners(Rectangle rect)
        {
            var ret = new List<MagnetCorner>();
            ret.Add(new MagnetCorner { Position = new Point(rect.Left, rect.Top), AttractsEdge = EdgeDirection.Left, AttractsEdgeEnd = EdgeEnd.Far, OffsetDirection = +1 });
            ret.Add(new MagnetCorner { Position = new Point(rect.Left, rect.Top), AttractsEdge = EdgeDirection.Top, AttractsEdgeEnd = EdgeEnd.Far, OffsetDirection = +1 });
            ret.Add(new MagnetCorner { Position = new Point(rect.Right, rect.Top), AttractsEdge = EdgeDirection.Right, AttractsEdgeEnd = EdgeEnd.Far, OffsetDirection = -1 });
            ret.Add(new MagnetCorner { Position = new Point(rect.Right, rect.Top), AttractsEdge = EdgeDirection.Top, AttractsEdgeEnd = EdgeEnd.Near, OffsetDirection = +1 });
            ret.Add(new MagnetCorner { Position = new Point(rect.Left, rect.Bottom), AttractsEdge = EdgeDirection.Left, AttractsEdgeEnd = EdgeEnd.Near, OffsetDirection = +1 });
            ret.Add(new MagnetCorner { Position = new Point(rect.Left, rect.Bottom), AttractsEdge = EdgeDirection.Bottom, AttractsEdgeEnd = EdgeEnd.Far, OffsetDirection = -1 });
            ret.Add(new MagnetCorner { Position = new Point(rect.Right, rect.Bottom), AttractsEdge = EdgeDirection.Right, AttractsEdgeEnd = EdgeEnd.Near, OffsetDirection = -1 });
            ret.Add(new MagnetCorner { Position = new Point(rect.Right, rect.Bottom), AttractsEdge = EdgeDirection.Bottom, AttractsEdgeEnd = EdgeEnd.Near, OffsetDirection = -1 });
            return ret;
        }
    }

    internal struct WinRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public override string ToString()
        {
            return string.Format("{0} {1} {2} {3}", Left, Top, Right, Bottom);
        }

        public Rectangle ToRectangle()
        {
            return new Rectangle(Left, Top, Right - Left, Bottom - Top);
        }

        public static WinRect FromRectangle(Rectangle rect)
        {
            return new WinRect { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom };
        }
    }
}
