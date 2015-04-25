using JWC;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace GroundControl
{
    public partial class MainForm : Form
    {
        const int Column0Width = 100;
        const int ColumnWidth = 70;
        const int Row0Height = 25;
        const int RowHeight = 15;

        // Document related
        private string ProjectFilename;
        private bool Modified;
        private RocketProject Project;
        private List<TrackInfo> Tracks;
        private List<TrackInfo> ColumnToTrack;
        private int RowsCount;
        private Dictionary<KeyInfo, TrackInfo> KeyToTrack;
        private List<KeyInfo>[] KeysInRow = new List<KeyInfo>[0];

        // Current view related members
        private Point ViewTopLeftOffset;
        private int ViewTopRowNr;
        private int ViewBotRowNr;

        // Audio view related
        private Bitmap     AudioTrack;
        private float[]    AudioBuffer;
        private WaveFormat AudioWaveFormat;

        // Selection related members
        private Rectangle Selection = Rectangle.Empty;
        private Point SelectionStart = Point.Empty;
        private Point Cursor = new Point(0, 0);
        private bool MousePressed;

        // Editing related
        private Point EditInKey = new Point(-1, -1);

        // Rocket communication
        private RocketServer server;

        // Undo/redo related
        private bool         UndoInProcess = false;
        private int          UndoSnapIndex = -1;
        private List<Stream> UndoSnaps = new List<Stream>();

        // Mru 
        private MruStripMenu mruMenu;

        // Track manager
        private FormTrackEditor TrackEditor;

        private enum Direction { Up, Down, Left, Right };

        public MainForm()
        {
            InitializeComponent();

            // Make sure handle is created
            CreateHandle();

            // Hookup to UI input events
            pnlDraw.KeyDown += pnlDraw_KeyDown;
            pnlDraw.KeyPress += pnlDraw_KeyPress;
            pnlDraw.MouseWheel += pnlDraw_MouseWheel;
            pnlAudioView.TabStop = false;
            pnlAudioView.GotFocus += pnlAudioView_GotFocus;

            // Create a new empty project
            newToolStripMenuItem_Click(null, null);

            // Setup Rocket server
            server = new RocketServer();
            server.GetTrack += server_GetTrack;
            server.RowSet += server_RowSet;

            // Force resize to reposition all controls
            MainForm_Resize(null, null);

            // Load MRU options
            mruMenu = new MruStripMenuInline(fileToolStripMenuItem, recentToolStripMenuItem, OnMruFile, @"SOFTWARE\Rocket\Rocket\MRU", 16);
            mruMenu.LoadFromRegistry();

            // Create Track editor form
            TrackEditor = new FormTrackEditor();
            TrackEditor.StartPosition = FormStartPosition.Manual;
            TrackEditor.SetBounds(Bounds.Right + 10, Bounds.Top, 250, Bounds.Height);
            TrackEditor.BeforeChange += TrackEditor_BeforeChange;
            TrackEditor.TracksChanged += TrackEditor_TracksChanged;
            TrackEditor.TracksRemoved += TrackEditor_TracksRemoved;
            TrackEditor.Show(this);
        }

        #region Resizing related

        private void MainForm_Resize(object sender, EventArgs e)
        {
            // Set scroll bars and draw panel positions
            var rect = pnlMainArea.ClientRectangle;
            vScrollBar1.SetBounds(rect.Right - vScrollBar1.Width, 0, vScrollBar1.Width, rect.Height - hScrollBar1.Height);
            hScrollBar1.SetBounds(0, rect.Bottom - hScrollBar1.Height, rect.Width - vScrollBar1.Width, hScrollBar1.Height);
            pnlEditor.SetBounds(0, 0, rect.Width - vScrollBar1.Width, rect.Height - hScrollBar1.Height);
            pnlDraw.Refresh();
            pnlAudioView.Refresh();
        }

        private void pnlAudioView_SizeChanged(object sender, EventArgs e)
        {
            BuildAudioTrack();
        }

        #endregion

        #region Keyboard/Mouse interactions

        private void ScrollBar_ValueChanged(object sender, EventArgs e)
        {
            pnlDraw.Invalidate();
            pnlAudioView.Invalidate();
        }

        private void pnlAudioView_GotFocus(object sender, EventArgs e)
        {
            pnlDraw.Focus();
        }

        private void pnlDraw_MouseDown(object sender, MouseEventArgs e)
        {
            // Do we need to exit edit mode?
            if (textEdit.Visible)
                ExitEditMode();

            // Move cursor to newlocation
            MoveCursor(ViewXYtoCell(e.Location));

            // Remember that mouse was pressed
            MousePressed = true;
        }

        private void pnlDraw_MouseMove(object sender, MouseEventArgs e)
        {
            if (MousePressed)
            {
                MoveCursor(ViewXYtoCell(e.Location));
            }
        }

        private void pnlDraw_MouseUp(object sender, MouseEventArgs e)
        {
            MousePressed = false;
        }

        private void pnlDraw_MouseWheel(object sender, MouseEventArgs e)
        {
            // Select key type
            var KeyType = (e.Delta > 0) ? Keys.Up : Keys.Down;
            if (ModifierKeys.HasFlag(Keys.Control))
                KeyType = (e.Delta > 0) ? Keys.Left : Keys.Right;

            // Simulate presses
            for (int i = Math.Abs(e.Delta / 120); i > 0; i--)
                pnlDraw_KeyDown(this, new KeyEventArgs(KeyType));
        }

        private void pnlDraw_KeyDown(object sender, KeyEventArgs e)
        {
            // Are we chaning values
            if ((e.Modifiers & Keys.Alt) != 0)
            {
                // Select step size
                var step = 0.0f;
                if (e.KeyCode == Keys.Up) step = (e.Modifiers & Keys.Shift) != 0 ? +0.1f : +1.0f;
                if (e.KeyCode == Keys.Down) step = (e.Modifiers & Keys.Shift) != 0 ? -0.1f : -1.0f;
                if (e.KeyCode == Keys.PageUp) step = (e.Modifiers & Keys.Shift) == 0 ? +10f : +100f;
                if (e.KeyCode == Keys.PageDown) step = (e.Modifiers & Keys.Shift) == 0 ? -10f : -100f;

                // Update all keys
                foreach (var key in GetSelectedKeys())
                {
                    // Update key
                    key.Value += step;

                    // Update client
                    server.SetKey(KeyToTrack[key].Name, key.Row, key.Value, key.Interpolation);
                }

                // Redraw
                pnlDraw.Invalidate();

                // Exit?
                if (e.KeyCode == Keys.F4)
                    Application.Exit();

                return;
            }

            // Move cursor (without Alt)
            if ((e.Modifiers & Keys.Alt) == 0)
            {
                // Update cursor position
                var newPos = Cursor;

                if ((e.Modifiers & Keys.Control) == 0)
                {
                    if (e.KeyCode == Keys.Up)       newPos.Y--;
                    if (e.KeyCode == Keys.Down)     newPos.Y++;
                    if (e.KeyCode == Keys.PageUp)   newPos.Y -= (ViewBotRowNr - ViewTopRowNr - 1);
                    if (e.KeyCode == Keys.PageDown) newPos.Y += (ViewBotRowNr - ViewTopRowNr - 1);
                    if (e.KeyCode == Keys.Left)     newPos.X--;
                    if (e.KeyCode == Keys.Right)    newPos.X++;
                    if (e.KeyCode == Keys.Home)     newPos.Y = 0;
                    if (e.KeyCode == Keys.End)      newPos.Y = RowsCount - 1;
                    if (e.KeyCode == Keys.Tab)
                        if ((e.Modifiers & Keys.Shift) == 0) 
                            newPos.X++;
                        else
                            newPos.X--;

                    // Move the cursor
                    MoveCursor(newPos);
                }
                else
                {
                    if (e.KeyCode == Keys.Up)    JumpToNextKey(Direction.Up);
                    if (e.KeyCode == Keys.Down)  JumpToNextKey(Direction.Down);
                    if (e.KeyCode == Keys.Left)  JumpToNextKey(Direction.Left);
                    if (e.KeyCode == Keys.Right) JumpToNextKey(Direction.Right);
                    if (e.KeyCode == Keys.Tab)
                        if ((e.Modifiers & Keys.Shift) == 0)
                            JumpToNextKey(Direction.Right);
                        else
                            JumpToNextKey(Direction.Left);

                }
            }

            // Region shifting
            if (e.KeyCode == Keys.A) MoveKeys(Direction.Left, 1);
            if (e.KeyCode == Keys.D) MoveKeys(Direction.Right, 1);
            if (e.KeyCode == Keys.W) MoveKeys(Direction.Up, 1);
            if (e.KeyCode == Keys.S) MoveKeys(Direction.Down, 1);

            // Handle delete key
            if (e.KeyCode == Keys.Delete)
            {
                // Save before change
                SaveUndoSnapshot();

                // Remove keys
                DeleteKeys(GetSelectedKeys());

                // Redraw things
                pnlDraw.Invalidate();
            }

            // Bookmarks
            if (((e.Modifiers & Keys.Control) != 0) && (Utils.NumKeyToInt.ContainsKey(e.KeyCode)))
            {
                // Set?
                if ((e.Modifiers & Keys.Shift) != 0)
                    SetBookmark(Utils.NumKeyToInt[e.KeyCode]);
                else
                    GotoBookmark(Utils.NumKeyToInt[e.KeyCode]);
            }

            if (e.KeyCode == Keys.K) SetBookmark(-1);

            
            e.Handled = true;
        }

        private void pnlDraw_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Check if valid edit key
            if (char.IsDigit(e.KeyChar) || (e.KeyChar == '.') || (e.KeyChar == '-') || (e.KeyChar == '\r'))
            {
                // Should we start editing?
                if (EditInKey == new Point(-1, -1))
                {
                    // Ignore if we're outside of valid range
                    if (Cursor.X >= ColumnToTrack.Count)
                        return;
                    
                    // Setup editing
                    textEdit.Bounds = CellRect(Cursor.X, Cursor.Y);
                    textEdit.Font = new Font("Courier New", 10, FontStyle.Bold);

                    // Set start text
                    if (e.KeyChar == '\r')
                    {
                        // Get current value
                        var track = ColumnToTrack[Cursor.X];
                        textEdit.Text = track.GetValue(Cursor.Y).ToString("0.00");
                    }
                    else
                    {
                        textEdit.Text = "" + e.KeyChar;
                        textEdit.SelectionStart = textEdit.Text.Length;
                    }

                    // Show edit
                    EditInKey = Cursor;
                    textEdit.Show();
                    textEdit.Focus();

                    // Redraw screen
                    pnlDraw.Invalidate();
                }
            }

            // Play/Stop
            if (e.KeyChar == ' ')
            {
                if (server.PlayMode)
                    server.Pause();
                else
                    server.Play();
            }

            // Is it interpolation change?
            if (("" + e.KeyChar).ToLower() == "i")
            {
                var keysInRange = GetSelectedKeys(true);

                // Get current value
                var currentInter = keysInRange.Count == 0 ? 0 : keysInRange[0].Interpolation;

                // Increment value
                currentInter = (currentInter + 1) % 4;

                // Change interpolation type
                foreach (var key in keysInRange)
                {
                    // Update key
                    key.Interpolation = currentInter;

                    // Update client
                    server.SetKey(KeyToTrack[key].Name, key.Row, key.Value, key.Interpolation);
                }

                pnlDraw.Invalidate();
            }
        }

        private void textEdit_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Block anything that is not a valid input
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.') && (e.KeyChar != '-'))
                e.Handled = true;

            // only allow one decimal point
            if ((e.KeyChar == '.') && ((sender as TextBox).Text.IndexOf('.') > -1))
                e.Handled = true;

            // only allow one decimal point
            if ((e.KeyChar == '-') && (((sender as TextBox).SelectionStart != 0) || (sender as TextBox).Text.IndexOf('-') > -1))
                e.Handled = true;

            // Enter pressed?
            if (e.KeyChar == '\r')
            {
                ExitEditMode();
                e.Handled = true;
            }
        }

        private void textEdit_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Tab) e.IsInputKey = true;
        }

        private void textEdit_KeyDown(object sender, KeyEventArgs e)
        {
            // Cancel editing?
            if (e.KeyCode == Keys.Escape)
            {
                // Empty text
                textEdit.Text = "";

                // Exit edit move
                ExitEditMode();

                // For some reason "ESC" causes a windows "beep", this cancels it
                e.SuppressKeyPress = true;
            }

            // Keys that will cause end of editing and move cursor
            if ((e.KeyCode == Keys.Down) || (e.KeyCode == Keys.Up) || (e.KeyCode == Keys.PageDown) || (e.KeyCode == Keys.PageUp) || (e.KeyCode == Keys.Tab))
            {
                // Stop editing
                ExitEditMode();

                // Forward key to panel to move cursor
                pnlDraw_KeyDown(null, e);
            }
        }

        private void ExitEditMode()
        {
            // Do we have a valid input
            if (textEdit.Text.Length > 0)
            {
                // Before changing - save undo snapshot
                SaveUndoSnapshot();

                // Get (or create) a key
                var track = ColumnToTrack[EditInKey.X];
                var key = GetKeyFromCell(EditInKey.X, EditInKey.Y);
                if (key == null)
                {
                    // Add key to track
                    key = new KeyInfo() { Row = EditInKey.Y };
                    AddKeyToTrack(key, track);
                }

                // Save value in key
                key.Value = float.Parse(textEdit.Text);

                // Update client
                server.SetKey(track.Name, key.Row, key.Value, key.Interpolation);
            }

            // Terminate editing
            EditInKey = new Point(-1, -1);
            textEdit.Hide();

            // Redraw screen
            pnlDraw.Invalidate();
        }

        private void MoveCursor(Point newPosition)
        {
            // Check if cursor has moved
            if (newPosition.Equals(Cursor))
                return;

            // Make sure cursor is in range
            var PrevCursor = Cursor;
            Cursor.X = Math.Max(0, Math.Min(newPosition.X, ColumnToTrack.Count - 1));
            Cursor.Y = Math.Max(0, Math.Min(newPosition.Y, RowsCount - 1));

            // Is selection mod is on?
            var selectionActive = ModifierKeys.HasFlag(Keys.Shift) || (MousePressed);

            // Handle selection selection - Moved without an active selection? Cancel old selection
            if (!selectionActive)
            {
                // Cancel selection
                Selection = Rectangle.Empty;
            }
            else
            {
                // Selection just started?
                if (Selection == Rectangle.Empty)
                    SelectionStart = PrevCursor;

                // Update selection
                Selection = new Rectangle(Math.Min(SelectionStart.X, Cursor.X),
                                            Math.Min(SelectionStart.Y, Cursor.Y),
                                            Math.Abs(SelectionStart.X - Cursor.X) + 1,
                                            Math.Abs(SelectionStart.Y - Cursor.Y) + 1);
            }

            // Update client
            server.SetRow(Cursor.Y);

            // During playback, make sure cursor does not go below mid screen
            var maxViewHeight = server.PlayMode ? pnlDraw.ClientSize.Height / 2 : pnlDraw.ClientSize.Height;

            // Make sure cursor is in view
            if (RowToViewY(Cursor.Y) < Row0Height)
                vScrollBar1.Value = Cursor.Y * RowHeight;
            if (RowToViewY(Cursor.Y + 1) > maxViewHeight)
                vScrollBar1.Value = Math.Max(0, (Cursor.Y + 1) * RowHeight + Row0Height - maxViewHeight);

            // Make sure cursor is in view
            if (ColumnToViewX(Cursor.X) < Column0Width)
                hScrollBar1.Value = Cursor.X * ColumnWidth;
            if (ColumnToViewX(Cursor.X + 1) > pnlDraw.ClientSize.Width)
                hScrollBar1.Value = Math.Max(0, (Cursor.X + 1) * ColumnWidth + Column0Width - pnlDraw.ClientSize.Width);

            // Update status bar
            toolStripCurrentRow.Text = "Row: " + Cursor.Y;
            if (Cursor.X < ColumnToTrack.Count)
            {
                // Display current track/row value
                var track = ColumnToTrack[Cursor.X];
                toolStripCurrentValue.Text = track.GetValue(Cursor.Y).ToString("0.00");

                // Display interpolation
                var keyIndex = track.FindKeyByRow(Cursor.Y, true);
                if (keyIndex >= 0)
                    toolStripInterpolation.Text = track.Keys[keyIndex].Interpolation.ToString();
                else
                    toolStripInterpolation.Text = "";
            }
            else
            {
                toolStripCurrentValue.Text = "";
                toolStripInterpolation.Text = "";
            }

            // redraw window
            pnlDraw.Invalidate();
            pnlAudioView.Invalidate();
        }

        #endregion

        #region Client Callbacks

        private void server_RowSet(object sender, int rowNr)
        {
            // Move cursor
            MoveCursor(new Point(Cursor.X, rowNr));
        }

        private void server_GetTrack(object sender, string trackName)
        {
            // Find track
            var track = Tracks.FirstOrDefault(t => t.Name == trackName);

            // Create new track if doesn't exists
            if (track == null)
            {
                Tracks.Add(track = new TrackInfo() { Name = trackName });

                RebuildKeyMaps();

                pnlDraw.Invalidate();
            }

            // Send all keys
            foreach (var key in track.Keys)
                server.SetKey(trackName, key.Row, key.Value, key.Interpolation);
        }

        #endregion

        #region Drawing

        private void pnlDraw_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var titleFont = new Font("Courier New", 8, FontStyle.Regular);
            var rowFont = new Font("Courier New", 10, FontStyle.Bold);
            var keysTipFont = new Font("Courier New", 8, FontStyle.Bold);
            var bookmarkFont = new Font("Tahoma", 7, FontStyle.Regular);
            
            var sfNear = new StringFormat() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            var sfFar = new StringFormat() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            var sfCenter = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            var InterColors = new Pen[] 
            {
                null, 
                new Pen(Color.Red, 3),
                new Pen(Utils.RGB(0x69FF24), 3),
                new Pen(Utils.RGB(0x2491FF), 3)
            };

            // Compute grid size
            var columnsCount = ColumnToTrack.Count;
            var totalWidth = Column0Width + columnsCount * ColumnWidth;
            var totalHeight = RowsCount * RowHeight;

            // Update scroll bars
            vScrollBar1.LargeChange = pnlDraw.ClientSize.Height;
            vScrollBar1.SmallChange = 1;
            vScrollBar1.Maximum = totalHeight;
            hScrollBar1.LargeChange = pnlDraw.ClientSize.Width;
            hScrollBar1.SmallChange = 1;
            hScrollBar1.Maximum = totalWidth;
            if (hScrollBar1.LargeChange > hScrollBar1.Maximum) hScrollBar1.Value = 0;

            // Get screen 0,0 offset
            ViewTopLeftOffset = new Point(hScrollBar1.Value, vScrollBar1.Value);

            // Build view rows info
            ViewTopRowNr = (ViewTopLeftOffset.Y) / RowHeight;
            ViewBotRowNr = (ViewTopLeftOffset.Y + pnlDraw.ClientSize.Height - Row0Height) / RowHeight;

            // Draw horizontal lines
            int viewLastColumnRight = Math.Min(columnsCount * ColumnWidth - ViewTopLeftOffset.X + Column0Width, pnlDraw.ClientSize.Width);
            g.Clip = new Region(CellRect(-1, -1, columnsCount, RowsCount).Expand(left: -Column0Width, top: -Row0Height));
            for (int iRow = ViewTopRowNr; iRow < ViewBotRowNr; iRow++)
            {
                // Select row back color
                var color = (iRow % 2) == 0 ? Utils.Gray(10) : Utils.Gray(0);
                if (iRow % 8 == 0)
                    color = Utils.Gray(25);

                // Is it the "selected" row?
                if (iRow == Cursor.Y)
                    color = Utils.Gray(60);

                // Draw rect 
                g.FillRectangle(new SolidBrush(color), CellRect(-1, iRow, columnsCount + 1, 1).Expand(bottom: 1));

                // Background of "Cursor" cell
                if (iRow == Cursor.Y)
                    g.FillRectangle(new SolidBrush(Utils.RGB(0xFFFFFF)), CellRect(Cursor.X, iRow).Expand(bottom: 1));
            }

            // Draw column0 header 
            var titleRect = CellRect(-1, -1);
            //var titleBrush = new LinearGradientBrush(titleRect, Utils.RGB(0x0963BD), Utils.RGB(0x05294D), LinearGradientMode.Vertical);
            var titleBrush = new SolidBrush(Utils.RGB(0x00));
            var titlePen = new Pen(Color.FromArgb(255, 0, 0, 0));
            g.FillRectangle(titleBrush, titleRect);

            // Build formatting string
            var format = Project.TimeFormat;
            format = format.Replace("row", "0");
            format = format.Replace("seconds", "1");
            format = format.Replace("time", "2");

            // Draw rows index labels 
            g.Clip = new Region(CellRect(-1, -1, 1, RowsCount).Expand(top: -Row0Height));
            for (int iRow = ViewTopRowNr; iRow < ViewBotRowNr; iRow++)
            {
                // Select row back color
                var color = (iRow % 8 == 0) ? Utils.Gray(200) : Utils.Gray(150);

                // Draw row number
                var rowsPerSecond = Project.BPM * Project.RowsPerBeat / 60.0;
                var seconds = iRow / rowsPerSecond;
                var time = TimeSpan.FromSeconds(seconds);
                try
                {
                    g.DrawString(string.Format(format, iRow, seconds, time), rowFont, new SolidBrush(color), CellRect(-1, iRow).Expand(right: -20), sfFar);
                }
                catch (Exception)
                {
                }

                // Draw key count
                if (KeysInRow[iRow].Count > 0)
                {
                    var keyCountRect = CellRect(0, iRow, 0, 1).Expand(left: 20).Expand(0, -3, -3, -3);
                    g.FillRectangle(Brushes.LightGray, keyCountRect);
                    g.DrawString(KeysInRow[iRow].Count.ToString(), keysTipFont, Brushes.Black, keyCountRect.Pan(top:1), sfCenter);
                }
            }

            // Draw bookmarks
            foreach (var bookmark in Project.Bookmarks)
            {
                var bookmarkRect = CellRect(-1, bookmark.Row, 0, 1).Expand(right: RowHeight).Expand(right: -1, bottom: -1).Pan(right: 2);
                g.FillEllipse(Brushes.DarkRed, bookmarkRect);
                g.DrawEllipse(Pens.Red, bookmarkRect);
                g.DrawString(bookmark.Number.ToString(), bookmarkFont, Brushes.White, bookmarkRect.Pan(bottom: 1, right: 0), sfCenter);
            }


            // Draw column0 vertical seperators
            g.ResetClip();
            g.DrawLine(new Pen(Utils.Gray(180)), CellRect(-1, -1, 0, RowsCount + 1).Pan(right: Column0Width - 1));

            // Draw column headers 
            g.Clip = new Region(CellRect(-1, -1, columnsCount, RowsCount + 1).Expand(left: -Column0Width));
            for (int iColumn = 0; iColumn < columnsCount; iColumn++)
            {
                // Skip invisible columns
                var column = ColumnToTrack[iColumn];
                if ((ColumnToViewX(iColumn + 1) < 0) || (ColumnToViewX(iColumn) > pnlDraw.ClientSize.Width))
                    continue;

                // Draw vertical seperators
                g.DrawLine(new Pen(Utils.ARGB(0x20ffffff)), CellRect(iColumn + 1, -1, 0, RowsCount));

                // Draw column header 
                titleRect = CellRect(iColumn, -1).Expand(left: -1);
                g.FillRectangle(titleBrush, titleRect);

                // Draw column name
                g.DrawString(column.Name, titleFont, Brushes.White, titleRect.Expand(top: +0, left: -3), sfNear);
            }

            // Draw last vertical seperator
            g.ResetClip();
            g.DrawLine(new Pen(Utils.ARGB(0x20ffffff)), CellRect(columnsCount, -1, 0, RowsCount));

            // Draw header horizontal seperator
            g.DrawLine(new Pen(Utils.Gray(180)), new Rectangle(0, Row0Height, pnlDraw.ClientSize.Width, 0));

            // Draw column keys
            g.Clip = new Region(CellRect(-1, -1, columnsCount + 1, RowsCount).Expand(top: -Row0Height, left: -Column0Width));
            for (int iColumn = 0; iColumn < columnsCount; iColumn++)
            {
                // Skip invisible columns
                var column = ColumnToTrack[iColumn];
                if ((ColumnToViewX(iColumn + 1) < 0) || (ColumnToViewX(iColumn) > pnlDraw.ClientSize.Width))
                    continue;

                // Find first value key to show
                for (int iKey = 0; iKey < column.Keys.Count; iKey++)
                {
                    var key = column.Keys[iKey];

                    // Select color
                    var color = (key.Row == Cursor.Y && iColumn == Cursor.X) ? Brushes.Black : Brushes.White;

                    // Draw value
                    g.DrawString(key.Value.ToString("0.00"), rowFont, color, CellRect(iColumn, key.Row).Expand(right: -4), sfFar);

                    // Draw Interpolation
                    if (key.Interpolation != 0)
                    {
                        // find next key row
                        var nextKeyRow = (iKey == column.Keys.Count - 1) ? RowsCount : column.Keys[iKey + 1].Row;

                        // var compute line rect
                        g.DrawLine(InterColors[key.Interpolation],
                            CellRect(iColumn + 1, key.Row, 0, nextKeyRow - key.Row)
                            .Pan(left: 2)
                            .Expand(top: -2, bottom: -1));
                    }
                }
            }

            // Draw selection
            if (Selection != Rectangle.Empty)
            {
                var selectionRect = CellRect(Selection.X, Selection.Y, Selection.Width, Selection.Height);
                g.FillRectangle(new SolidBrush(Color.FromArgb(70, 148, 198, 255)), selectionRect);
            }
        }

        private void pnlAudioView_Paint(object sender, PaintEventArgs e)
        {
            if (AudioTrack != null)
            {
                // Compute dest rect
                var destRect = pnlAudioView.ClientRectangle.Expand(top: -Row0Height);

                // Draw image
                e.Graphics.DrawImage(AudioTrack,
                    destRect,
                    new Rectangle(0, vScrollBar1.Value, destRect.Width, destRect.Height),
                    GraphicsUnit.Pixel);
            }

            // Draw cursor
            var cursorViewY = RowToViewY(Cursor.Y) + RowHeight / 2;
            e.Graphics.DrawLine(Pens.Yellow, 0, cursorViewY, pnlAudioView.ClientSize.Width, cursorViewY);
        }

        #endregion

        #region Coordinate Conversion

        private int RowToViewY(int row)
        {
            return row * RowHeight + Row0Height - ViewTopLeftOffset.Y;
        }

        private int ViewYToRow(int viewY)
        {
            return (viewY - Row0Height + ViewTopLeftOffset.Y) / RowHeight;
        }

        private int ColumnToViewX(int column)
        {
            return column * ColumnWidth - ViewTopLeftOffset.X + Column0Width;
        }

        private int ViewXToColumn(int viewX)
        {
            return (viewX - Column0Width + ViewTopLeftOffset.X) / ColumnWidth;
        }

        private Point ViewXYtoCell(Point view)
        {
            return new Point(ViewXToColumn(view.X), ViewYToRow(view.Y));
        }

        #endregion

        #region Track manager related

        private void TrackEditor_BeforeChange(object sender, EventArgs e)
        {
            SaveUndoSnapshot();
        }

        private void TrackEditor_TracksChanged(object sender, EventArgs e)
        {
            RebuildVisibleColumnList();

            pnlDraw.Invalidate();
        }

        private void TrackEditor_TracksRemoved(object sender, EventArgs e)
        {
            RebuildKeyMaps();

            RebuildVisibleColumnList();

            pnlDraw.Invalidate();
        }

        #endregion

        #region Key/Cell related functions

        private Rectangle CellRect(int column, int row, int columnSpan = 1, int rowSpan = 1)
        {
            var left   = column == -1 ? 0                                             : ColumnToViewX(column);
            var top    = row    == -1 ? 0                                             : RowToViewY   (row);
            var width  = column == -1 ? Column0Width + (columnSpan - 1) * ColumnWidth : columnSpan * ColumnWidth;
            var height = row    == -1 ? Row0Height   + (rowSpan    - 1) * RowHeight   : rowSpan    * RowHeight;
            
            if (columnSpan == 0) width  = 0;
            if (rowSpan    == 0) height = 0;
            
            return new Rectangle(left, top, width, height);
        }

        private KeyInfo GetKeyFromCell(int column, int row, bool includePrevKey = false)
        {
            // Out of range?
            if (column >= ColumnToTrack.Count)
                return null;

            // Get key
            var track = ColumnToTrack[column];
            var index = track.FindKeyByRow(row, includePrevKey);
            return index == -1 ? null : track.Keys[index];
        }

        private Point GetCellFromKey(KeyInfo key)
        {
            return new Point(ColumnToTrack.IndexOf(KeyToTrack[key]), key.Row);
        }

        private List<KeyInfo> KeysInRect(int column, int row, int columnSpan, int rowSpan, bool includePrevKey = false)
        {
            // Scan a columns
            var keys = new List<KeyInfo>();
            for (; columnSpan > 0; column++, columnSpan--)
            {
                // Ignore out-of-range columns
                if (column < 0) continue;
                if (column >= ColumnToTrack.Count) break;

                // Add keys in range
                keys.AddRange(
                    ColumnToTrack[column].Keys
                    .Where(t => (t.Row >= row) && (t.Row < row + rowSpan)));

                if (includePrevKey)
                {
                    // Search for key above rect
                    var topKey = ColumnToTrack[column].Keys.Where(t => t.Row < row).LastOrDefault();
                    if (topKey != null)
                        keys.Add(topKey);
                }
            }

            return keys;
        }

        private List<KeyInfo> GetSelectedKeys(bool includePrevKey = false)
        {
            // Create return value
            var keys = new List<KeyInfo>();

            // get cursor key
            var key = GetKeyFromCell(Cursor.X, Cursor.Y, includePrevKey);
            if (key != null) 
                keys.Add(key);

            // Get region keys
            if (Selection != Rectangle.Empty)
                keys.AddRange(KeysInRect(Selection.X, Selection.Y, Selection.Width, Selection.Height, includePrevKey).Where(k => k != key));

            return keys;
        }

        private void JumpToNextKey(Direction direction)
        {
            // Is cursor is valid position?
            if (Cursor.X >= ColumnToTrack.Count)
                return;

            switch (direction)
            {
                case Direction.Up:
                {
                    // find track
                    var track = ColumnToTrack[Cursor.X];

                    // Select get
                    var Key = track.Keys.Where(k => k.Row < Cursor.Y).LastOrDefault();

                    // Move cursor
                    if (Key != null)
                        MoveCursor(new Point(Cursor.X, Key.Row));
                    break;
                }

                case Direction.Down:
                {
                    // find track
                    var track = ColumnToTrack[Cursor.X];

                    // Select get
                    var Key = track.Keys.Where(k => k.Row > Cursor.Y).FirstOrDefault();

                    // Move cursor
                    if (Key != null)
                        MoveCursor(new Point(Cursor.X, Key.Row));
                    break;
                }

                case Direction.Left:
                {
                    for (var iColumn = Cursor.X - 1; iColumn >= 0; iColumn--)
                    {
                        var key = GetKeyFromCell(iColumn, Cursor.Y, true);
                        if ((key != null) && ((key.Row == Cursor.Y) || (key.Interpolation != 0)))
                        {
                            MoveCursor(new Point(iColumn, Cursor.Y));
                            break;
                        }
                    }
                    
                    break;
                }

                case Direction.Right:
                {
                    for (var iColumn = Cursor.X + 1; iColumn < ColumnToTrack.Count; iColumn++)
                    {
                        var key = GetKeyFromCell(iColumn, Cursor.Y, true);
                        if ((key != null) && ((key.Row == Cursor.Y) || (key.Interpolation != 0)))
                        {
                            MoveCursor(new Point(iColumn, Cursor.Y));
                            break;
                        }
                    }

                    break;
                }
            }
        }

        private void AddKeyToTrack(KeyInfo key, TrackInfo track)
        {
            // Add key to track
            track.Keys.AddSorted(key);

            // Add to key-to-track map
            KeyToTrack.Add(key, track);

            // Add to row map
            KeysInRow[key.Row].Add(key);

            // Update client
            server.SetKey(KeyToTrack[key].Name, key.Row, key.Value, key.Interpolation);
        }

        private void DeleteKeys(List<KeyInfo> keys)
        {
            // Remove keys
            foreach (var key in keys)
            {
                // Report key remove to client
                server.DeleteKey(KeyToTrack[key].Name, key.Row);

                // Remove from track
                KeyToTrack[key].Keys.Remove(key);
                KeyToTrack.Remove(key);

                // Remove from row map
                KeysInRow[key.Row].Remove(key);
            }
        }

        private void RebuildVisibleColumnList()
        {
            ColumnToTrack = Tracks.Where(t => t.Visible).ToList();
        }

        private void RebuildKeyMaps()
        {
            // Apply Row count
            RowsCount = Project.Rows;

            // Remove keys outside of RowCount
            Tracks.ForEach(t => t.Keys = t.Keys.Where(k => k.Row < RowsCount).ToList());

            // Sort all tracks
            foreach (var track in Tracks)
                track.Keys.Sort((a, b) => a.Row.CompareTo(b.Row));

            // Build the key to track
            KeyToTrack = Tracks
                .SelectMany(t =>
                    t.Keys.Select(k => new Tuple<KeyInfo, TrackInfo>(k, t)))
                    .ToDictionary(t => t.Item1, t => t.Item2);

            // Update KeysInRow
            KeysInRow = Enumerable.Range(0, RowsCount).Select(t => new List<KeyInfo>()).ToArray();
            Tracks.ForEach(t => t.Keys.ForEach(k => KeysInRow[k.Row].Add(k)));

            // Update Track Editor
            if (TrackEditor != null)
                TrackEditor.SetTracks(Tracks);

            // Rebuild visible column list
            RebuildVisibleColumnList();
        }

        #endregion

        #region Copy/Paste/Move

        private void CopyToClipboard()
        {
            // Find range
            var region = Selection;
            if (region == Rectangle.Empty)
                region = new Rectangle(Cursor, new Size(1, 1));

            // Build table
            var sb = new StringBuilder();
            for (int iRow = region.Top; iRow < region.Bottom; iRow++)
            {
                for (int iColumn = region.Left; iColumn < region.Right; iColumn++)
                {
                    var key = GetKeyFromCell(iColumn, iRow, false);
                    if (key != null)
                        sb.AppendFormat("{0} {1}", (int)key.Interpolation, key.Value);

                    // Add space
                    if (iColumn != region.Right - 1)
                        sb.AppendFormat("\t");
                }


                // New table row
                sb.AppendLine();
            }

            // Save string to clipboard
            Clipboard.SetText(sb.ToString());
        }

        private void PasteFromClipboard()
        {
            try
            {
                // Get data from clipboard
                var clipboardData = Clipboard.GetText(TextDataFormat.Text);
                var table = clipboardData.TrimEnd('\r', '\n').Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None).Select(t => t.Split('\t').ToArray()).ToArray();

                // Make sure table is not empty
                if (table.Length == 0) return;
                if (table[0].Length == 0) return;

                // Make sure all rows have the same values count
                if (!table.All(t => t.Length == table[0].Length))
                    return;

                // Convert data
                var newKeys = Enumerable.Range(0, table[0].Length).Select(t=> new List<KeyInfo>()).ToArray();
                for (int iRow = 0; iRow < table.Length; iRow++)
                {
                    for (int iColumn = 0; iColumn < table[0].Length; iColumn++)
                    {
                        if (table[iRow][iColumn] != "")
                        {
                            // Break string into parts
                            var parts = table[iRow][iColumn].Split(' ');

                            // Create key and add it to the correct column
                            var key = new KeyInfo();
                            newKeys[iColumn].Add(key);

                            // Set fields
                            key.Row           = Cursor.Y + iRow;
                            key.Value         = float.Parse(parts.Last());
                            key.Interpolation = parts.Length > 1 ? int.Parse(parts[0]) : 0;
                        }
                    }
                }

                // Save before change
                SaveUndoSnapshot();

                // Get keys to remove from paste region
                var oldKeys = KeysInRect(Cursor.X, Cursor.Y, table[0].Length, table.Length);
                DeleteKeys(oldKeys);

                // Add new keys
                for (int iColumn = 0; iColumn < newKeys.Length; iColumn++)
                {
                    // Make sure we're in a valid column
                    if (Cursor.X + iColumn >= ColumnToTrack.Count)
                        continue;

                    // Get track
                    var track = ColumnToTrack[Cursor.X + iColumn];

                    // Add all keys
                    foreach (var key in newKeys[iColumn])
                        AddKeyToTrack(key, track);
                }
            }
            catch(Exception)
            {
            }

            // Refresh screen
            pnlDraw.Invalidate();
        }

        private void MoveKeys(Direction direction, int steps)
        {
            // Find region
            var region = Selection;
            if (region == Rectangle.Empty)
                region = new Rectangle(Cursor, new Size(1, 1));

            // Get a list of keys region
            var keysToMove = KeysInRect(region.X, region.Y, region.Width, region.Height);
            var keyPositions = keysToMove.ToDictionary(k => k, k => GetCellFromKey(k));

            // define new region
            var xshift = 0;
            var yshift = 0;
            switch (direction)
            {
                case Direction.Left:  xshift = -steps; break;
                case Direction.Right: xshift = +steps; break;
                case Direction.Up:    yshift = -steps; break;
                case Direction.Down:  yshift = +steps; break;
            }

            // Get keys to remove
            Rectangle destRegion = region.Pan(right: xshift, bottom: yshift);
            var keysToRemove = KeysInRect(destRegion.X, destRegion.Y, destRegion.Width, destRegion.Height);

            // Save snapshot before change
            SaveUndoSnapshot();

            // Remove keys from both lists
            keysToRemove = keysToRemove.Concat(keysToMove).Distinct().ToList();
            DeleteKeys(keysToRemove);

            // Add all keys
            foreach (var key in keysToMove)
            {
                // Find new track
                var trackIndex = destRegion.X + (keyPositions[key].X - region.X);
                if ((trackIndex < 0) || (trackIndex >= ColumnToTrack.Count))
                    continue;

                // update row number
                key.Row += yshift;
                if ((key.Row < 0) || (key.Row >= RowsCount))
                    continue;

                // Add key to track
                AddKeyToTrack(key, ColumnToTrack[trackIndex]);
            }

            // Store selection state
            var storedSelection      = Selection;
            var storedSelectionStart = SelectionStart;

            // Move cursor (this cancels selection)
            MoveCursor(new Point(Cursor.X + xshift, Cursor.Y + yshift)); 

            // Restore selection
            if (storedSelection != Rectangle.Empty)
            {
                SelectionStart = storedSelectionStart;
                SelectionStart.Offset(xshift, yshift);

                // Update selection
                Selection = storedSelection.Pan(right: xshift, bottom: yshift);
            }

            // Redraw screen
            pnlDraw.Invalidate();
        }

        #endregion

        #region Save and load

        private void OpenFile(string filename)
        {
            try
            {
                // Open file
                using (var reader = new FileStream(filename, FileMode.Open))
                {
                    // Load data
                    XmlSerializer ser = new XmlSerializer(typeof(RocketProject));
                    Project = ser.Deserialize(reader) as RocketProject;

                    // Cache variables
                    Tracks = Project.Tracks;
                    RowsCount = Project.Rows;

                    // Update project file status
                    ProjectFilename = filename;
                    Modified = false;
                    UpdateApplicationTitle();

                    // Fix bookmarks
                    foreach (var bookmark in Project.Bookmarks)
                        if (bookmark.Number == -1)
                            bookmark.Number = Enumerable.Range(1, 9).FirstOrDefault(index => !Project.Bookmarks.Any(b => b.Number == index));

                    // Rebuild all key maps
                    RebuildKeyMaps();

                    // Update MRU
                    mruMenu.AddFile(filename);
                    mruMenu.SaveToRegistry();

                    // Clear undo buffer
                    UndoSnapIndex = -1;
                    UndoSnaps = new List<Stream>();

                    // Reload audio
                    LoadAudio();

                    // Refresh screen
                    pnlDraw.Refresh();
                    pnlAudioView.Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error while loading the file.\n" + ex.Message, "Loading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void SaveProject(string filename)
        {
            try
            {
                using (var writer = new FileStream(filename, FileMode.Create))
                {
                    // Load data
                    var ser = new XmlSerializer(typeof(RocketProject));
                    ser.Serialize(writer, Project);
                }

                // Update project file status
                ProjectFilename = filename;
                Modified = false;
                UpdateApplicationTitle();

                // Update MRU
                mruMenu.AddFile(filename);
                mruMenu.SaveToRegistry();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error while saving file.\n" + ex.Message, "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateApplicationTitle()
        {
            // Base title
            var title = "Ground Control";

            // Add file name
            if (string.IsNullOrEmpty(ProjectFilename))
                title += " [Unsaved Project]";
            else
                title += " [" + Path.GetFileNameWithoutExtension(ProjectFilename) + (Modified ? "*]" : "]");

            // Update form property    
            Text = title;
        }

        #endregion

        #region Bookmarks related

        private void SetBookmark(int number)
        {
            // Remove bookmark from current row if already exists
            var eraseBookmark = false;
            var bookmark = Project.Bookmarks.FirstOrDefault(b => b.Row == Cursor.Y);
            if (bookmark != null)
            {
                // Check if this is a bookmark erase (toggling of bookmark)
                eraseBookmark = (bookmark.Number == number) || (number == -1);

                // Anyway, remove bookmark
                Project.Bookmarks.Remove(bookmark);
            }

            // Remove bookmark if number already exists somewhere else
            bookmark = Project.Bookmarks.FirstOrDefault(b => b.Number == number);
            if (bookmark != null)
                Project.Bookmarks.Remove(bookmark);

            // Create new bookmark (if we need to)
            if (!eraseBookmark)
            {
                // create a new bookmark
                bookmark = new Bookmark() { Number = number, Row = Cursor.Y };
                Project.Bookmarks.Add(bookmark);

                // If this is an automatic index finding bookmark, find index
                if (bookmark.Number == -1)
                    bookmark.Number = Enumerable.Range(1, 9).FirstOrDefault(index => !Project.Bookmarks.Any(b => b.Number == index));
            }

            // Refresh screen
            pnlDraw.Invalidate();
        }

        private void GotoBookmark(int number)
        {
            Bookmark target = null;

            // is it "Next bookmark"?
            if (number == -1)
            {
                // get next bookmark
                target = Project.Bookmarks.OrderBy(t => t.Row).Where(t => t.Row > Cursor.Y).FirstOrDefault();

                // if next could not be found, go to first
                if (target == null)
                    target = Project.Bookmarks.FirstOrDefault();
            }
            else
            {
                // Find relevent bookmark
                target = Project.Bookmarks.FirstOrDefault(b => b.Number == number);
            }

            // Did we actually manage to find a bookmark?
            if (target != null)
            {
                // Move cursor
                MoveCursor(new Point(Cursor.X, target.Row));

                // Make sure we're centered in view
                vScrollBar1.Value = Math.Max(0, (Cursor.Y + 1) * RowHeight + Row0Height - (pnlDraw.ClientSize.Height / 2));
            }
        }

        #endregion

        #region Undo/Redo related

        private void SaveUndoSnapshot()
        {
            // Everytime a snapshot is saved, it means that we're not doing undo right now
            UndoInProcess = false;

            // Remember that there was a change
            Modified = true;
            UpdateApplicationTitle();

            // Remove all snaps after UndoSnapIndex
            while (UndoSnaps.Count - 1 > UndoSnapIndex)
                UndoSnaps.RemoveAt(UndoSnaps.Count - 1);

            // Create snapshot
            var snapStream = new MemoryStream();
            var ser = new XmlSerializer(typeof(RocketProject));
            ser.Serialize(snapStream, Project);

            // Add stream
            UndoSnaps.Add(snapStream);
            UndoSnapIndex = UndoSnaps.Count - 1;
        }

        private void RestoreUndoSnapshot()
        {
            // Can we undo?
            if (UndoSnapIndex == -1)
                return;

            // Have we started an undo sequance?
            if (!UndoInProcess)
            {
                // if yes, save a snapshot so we can redo into current position
                SaveUndoSnapshot();
                UndoSnapIndex--;

                // Remember that we're in an undo sequance
                UndoInProcess = true;
            }

            // Restore snapshot
            var snapStream = UndoSnaps[UndoSnapIndex];
            snapStream.Seek(0, SeekOrigin.Begin);
            var ser = new XmlSerializer(typeof(RocketProject));
            Project = ser.Deserialize(snapStream) as RocketProject;

            // Rebuild things...
            Tracks = Project.Tracks;
            RowsCount = Project.Rows;
            RebuildKeyMaps();
            RebuildVisibleColumnList();

            // Move restore point
            UndoSnapIndex--;
        }

        #endregion

        #region Audio track related

        private void LoadAudio()
        {
            // Do we have anything to load
            if (string.IsNullOrEmpty(Project.AudioFile))
                return;

            try
            {
                // Open file
                var reader = new AudioFileReader(Project.AudioFile);

                // read data
                AudioWaveFormat = reader.WaveFormat;
                AudioBuffer = new float[reader.Length / 4];
                reader.Read(AudioBuffer, 0, AudioBuffer.Length);

                // Start building the audio track image
                BuildAudioTrack();
            }
            catch(Exception ex)
            {
                MessageBox.Show(this, "Error while loading audio file.\n" + ex.Message, "Loading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildAudioTrack()
        {
            // Do we have a loaded audio?
            if (AudioWaveFormat == null)
                return;

            // Make sure all scroll values are updated
            var g = Graphics.FromHwnd(pnlDraw.Handle);
            pnlDraw_Paint(this, new PaintEventArgs(g, pnlDraw.ClientRectangle));
            g.Dispose();

            // Set bitmap size
            AudioTrack = new Bitmap(pnlAudioView.ClientSize.Width, vScrollBar1.Maximum);

            // Compute samples per pixel
            var rowsPerSecond = Project.BPM * Project.RowsPerBeat / 60.0;

            // Get bits
            BitmapData bmpData = AudioTrack.LockBits(
                    new Rectangle(0, 0, AudioTrack.Width, AudioTrack.Height),
                    ImageLockMode.ReadWrite,
                    AudioTrack.PixelFormat);

            for (long y = 0; y < AudioTrack.Height; y++) // pixel-time = pixel-y / pixels-per-second
            {
                // Compute first and last samples to include
                var firstSample = (long)(AudioWaveFormat.SampleRate * (y - 0) / (RowHeight * rowsPerSecond));
                var lastSample  = (long)(AudioWaveFormat.SampleRate * (y + 1) / (RowHeight * rowsPerSecond));
                if (firstSample < 0) firstSample = 0;
                if (lastSample < 0)  lastSample = 0;
                if (firstSample * AudioWaveFormat.Channels >= AudioBuffer.Length) firstSample = AudioBuffer.Length / AudioWaveFormat.Channels - 1;
                if (lastSample  * AudioWaveFormat.Channels >= AudioBuffer.Length) lastSample  = AudioBuffer.Length / AudioWaveFormat.Channels - 1;

                // Create pixel counter projection
                var midPoint = bmpData.Width / 2;
                var pixelCounter = new int[bmpData.Width];
                for (long iSample = firstSample; iSample < lastSample; iSample++)
                {
                    // Find sample position
                    var sample = midPoint + (int)(AudioBuffer[iSample * AudioWaveFormat.Channels] * bmpData.Width);
                    if (sample > bmpData.Width - 1) sample = bmpData.Width - 1;
                    if (sample < 0) sample = 0;

                    // Increment scanline
                    var step = sample > midPoint ? -1 : +1;
                    while (true)
                    {
                        pixelCounter[sample]++;
                        pixelCounter[bmpData.Width - 1 - sample]++;
                        if (sample == midPoint)
                            break;

                        sample += step;
                    }
                }

                // Fix center pixel
                pixelCounter[midPoint  ] /= 2;
                pixelCounter[midPoint-1] /= 2;

                // Convert to color
                var scanLine = new byte[bmpData.Width * 4];
                var maxCount = lastSample - firstSample;
                if (maxCount > 0)
                {
                    for (int i = 0; i < bmpData.Width; i++)
                    {
                        var value = (byte)(30 + pixelCounter[i] * 200 / maxCount);
                        if (pixelCounter[i] == 0)
                            value = 0;
                        scanLine[i * 4 + 0] = value;
                        scanLine[i * 4 + 1] = value;
                        scanLine[i * 4 + 2] = value;
                        scanLine[i * 4 + 3] = 0xff;
                    }
                }

                // Copy to image
                Marshal.Copy(scanLine, 0, (IntPtr)((long)bmpData.Scan0 + bmpData.Stride * y), scanLine.Length);
            }

            // Release lock
            AudioTrack.UnlockBits(bmpData);
            
            // Refresh screen
            pnlAudioView.Invalidate();
        }

        #endregion

        #region Menu Events

        private void tmrUpdateUI_Tick(object sender, EventArgs e)
        {
            // Refresh trackManager option
            trackManagerToolStripMenuItem.Checked = TrackEditor.Visible;
        }

        private void OnMruFile(int number, String filename)
        {
            OpenFile(filename);
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ProjectFilename = "";
            Project = new RocketProject();
            Tracks = Project.Tracks;
            UndoSnaps = new List<Stream>();
            UndoSnapIndex = -1;
            Modified = false;

            // Clean key maps
            RebuildKeyMaps();

            BuildAudioTrack();

            pnlDraw.Invalidate();
            pnlAudioView.Invalidate();
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Multiselect = false;
            dlg.CheckFileExists = true;
            dlg.Title = "Open Rocket Project";
            dlg.DefaultExt = "rocket";
            dlg.Filter = "Rocket Files (*.rocket)|*.rocket|All Files (*.*)|*.*";

            if (dlg.ShowDialog(this) == DialogResult.OK)
                OpenFile(dlg.FileName);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Is it the first save? if yes, run saveAs to give a file name
            if (string.IsNullOrEmpty(ProjectFilename))
                saveAsToolStripMenuItem_Click(null, null);
            else
                SaveProject(ProjectFilename);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.Title = "Save Rocket Project";
            dlg.DefaultExt = "rocket";
            dlg.Filter = "Rocket Files (*.rocket)|*.rocket|All Files (*.*)|*.*";

            if (dlg.ShowDialog(this) == DialogResult.OK)
                SaveProject(dlg.FileName);
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyToClipboard();
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PasteFromClipboard();
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RestoreUndoSnapshot();

            pnlDraw.Invalidate();
        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Is redo allowed?
            if (UndoSnapIndex >= UndoSnaps.Count - 2)
                return;

            // Jump two-steps forward (and restore undosnap goes one step back)
            UndoSnapIndex += 2;

            // Restore point
            RestoreUndoSnapshot();

            // Redraw screen
            pnlDraw.Invalidate();
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dlg = new FormSettings(Project);
            if (dlg.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                // Rebuild all keys maps
                RebuildKeyMaps();

                // Reload audio
                LoadAudio();
            }
        }

        private void trackManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TrackEditor.Visible = !TrackEditor.Visible;
        }

        #endregion
    }
}



