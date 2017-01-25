using JWC;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Globalization;

namespace GroundControl
{
    public partial class MainForm : Form
    {
        private const int Column0Width = 100;
        private const int ColumnWidth = 70;
        private const int Row0Height = 25;
        private const int RowHeight = 15;
        private const int InterpolationBarWidth = 3;

        // Document related
        private string m_ProjectFilename;
        private bool m_Modified;
        private RocketProject m_Project;
        private List<TrackInfo> m_Tracks;
        private List<TrackInfo> m_ColumnToTrack;
        private int m_RowsCount;
        private Dictionary<KeyInfo, TrackInfo> m_KeyToTrack;
        private List<KeyInfo>[] m_KeysInRow = new List<KeyInfo>[0];

        // Current view related members
        private Point m_ViewTopLeftOffset;
        private int m_ViewTopRowNr;
        private int m_ViewBotRowNr;

        // Audio view related
        private Bitmap     m_AudioTrack;
        private float[]    m_AudioBuffer;
        private WaveFormat m_AudioWaveFormat;

        // Scroll view related
        private const int m_VScroll_XMargin = 1;
        private const int m_VScroll_YMargin = 1;
        private float m_VScroll_YScale;
        private int m_VSCroll_XScale;

        // Selection related members
        private Rectangle m_Selection = Rectangle.Empty;
        private Point m_SelectionStart = Point.Empty;
        private Point m_Cursor = new Point(0, 0);
        private bool m_MousePressed;

        // Editing related
        private Point m_EditInKey = new Point(-1, -1);

        // Rocket communication
        private RocketServer m_Server;

        // Undo/redo related
        private bool         m_UndoInProcess;
        private int          m_UndoSnapIndex = -1;
        private List<Stream> m_UndoSnaps = new List<Stream>();

        // Mru 
        private MruStripMenu m_MruMenu;

        // Track manager
        private FormTrackEditor m_TrackEditor;

        private enum Direction
        {
            Up,
            Down,
            Left,
            Right
        };

        // LUTs
        private string[] m_InterpolationToString = {"Step", "Linear", "Smooth", "Ramp"};

        public MainForm()
        {
            InitializeComponent();

            // Make sure handle is created
            CreateHandle();

            new MagnetWinForms.MagnetWinForms(this);

            // Hookup to UI input events
            pnlDraw.KeyDown += pnlDraw_KeyDown;
            pnlDraw.KeyPress += pnlDraw_KeyPress;
            pnlDraw.MouseWheel += pnlDraw_MouseWheel;
            pnlAudioView.TabStop = false;
            pnlAudioView.GotFocus += panel_GotFocus;
            pnlVScroll.TabStop = false;
            pnlVScroll.GotFocus += panel_GotFocus;

            // Create a new empty project
            newToolStripMenuItem_Click(null, null);

            // Setup Rocket server
            m_Server = new RocketServer();
            m_Server.GetTrack += server_GetTrack;
            m_Server.RowSet += server_RowSet;
            m_Server.ClientConnected += server_ClientConnected;

            // Force resize to reposition all controls
            MainForm_Resize(null, null);

            // Load MRU options
            m_MruMenu = new MruStripMenuInline(fileToolStripMenuItem, recentToolStripMenuItem, OnMruFile, @"SOFTWARE\Rocket\Rocket\MRU", 16);
            m_MruMenu.LoadFromRegistry();

            // Create Track editor form
            m_TrackEditor = new FormTrackEditor();
            m_TrackEditor.StartPosition = FormStartPosition.Manual;
            m_TrackEditor.SetBounds(Bounds.Right, Bounds.Top, 250, Bounds.Height);
            m_TrackEditor.BeforeChange += TrackEditor_BeforeChange;
            m_TrackEditor.TracksChanged += TrackEditor_TracksChanged;
            m_TrackEditor.TracksRemoved += TrackEditor_TracksRemoved;
            m_TrackEditor.Show(this);
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
            pnlVScroll.Refresh();
        }

        private void pnlAudioView_SizeChanged(object sender, EventArgs e)
        {
            BuildAudioTrack();
        }

        #endregion

        #region Keyboard/Mouse interactions

        private void ScrollBar_ValueChanged(object sender, EventArgs e)
        {
            pnlDraw.Refresh();
            pnlAudioView.Refresh();
            pnlVScroll.Refresh();
        }

        private void panel_GotFocus(object sender, EventArgs e)
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
            m_MousePressed = true;
        }

        private void pnlDraw_MouseMove(object sender, MouseEventArgs e)
        {
            if (m_MousePressed)
            {
                MoveCursor(ViewXYtoCell(e.Location));
            }
        }

        private void pnlDraw_MouseUp(object sender, MouseEventArgs e)
        {
            m_MousePressed = false;
        }

        private void pnlDraw_MouseWheel(object sender, MouseEventArgs e)
        {
            // Select key type
            var keyType = (e.Delta > 0) ? Keys.Up : Keys.Down;
            if (ModifierKeys.HasFlag(Keys.Control))
                keyType = (e.Delta > 0) ? Keys.Left : Keys.Right;

            // Simulate presses
            for (int i = Math.Abs(e.Delta / 120); i > 0; i--)
                pnlDraw_KeyDown(this, new KeyEventArgs(keyType));
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
                    m_Server.SetKey(m_KeyToTrack[key].Name, key.Row, key.Value, key.Interpolation);
                }

                // Update view
                UpdateView();

                // Exit?
                if (e.KeyCode == Keys.F4)
                    Application.Exit();

                return;
            }

            // Move cursor (without Alt)
            if ((e.Modifiers & Keys.Alt) == 0)
            {
                // Update cursor position
                var newPos = m_Cursor;

                if ((e.Modifiers & Keys.Control) == 0)
                {
                    if (e.KeyCode == Keys.Up)       newPos.Y--;
                    if (e.KeyCode == Keys.Down)     newPos.Y++;
                    if (e.KeyCode == Keys.PageUp)   newPos.Y -= (m_ViewBotRowNr - m_ViewTopRowNr - 1);
                    if (e.KeyCode == Keys.PageDown) newPos.Y += (m_ViewBotRowNr - m_ViewTopRowNr - 1);
                    if (e.KeyCode == Keys.Left)     newPos.X--;
                    if (e.KeyCode == Keys.Right)    newPos.X++;
                    if (e.KeyCode == Keys.Home)     newPos.Y = 0;
                    if (e.KeyCode == Keys.End)      newPos.Y = m_RowsCount - 1;
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
                if (m_EditInKey == new Point(-1, -1))
                {
                    // Ignore if we're outside of valid range
                    if (m_Cursor.X >= m_ColumnToTrack.Count)
                        return;

                    // Setup editing
                    textEdit.Bounds = CellRect(m_Cursor.X, m_Cursor.Y);
                    textEdit.Font = new Font("Courier New", 10, FontStyle.Bold);

                    // Set start text
                    if (e.KeyChar == '\r')
                    {
                        // Get current value
                        var track = m_ColumnToTrack[m_Cursor.X];
                        textEdit.Text = track.GetValue(m_Cursor.Y).ToString("0.00", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        textEdit.Text = "" + e.KeyChar;
                        textEdit.SelectionStart = textEdit.Text.Length;
                    }

                    // Show edit
                    m_EditInKey = m_Cursor;
                    textEdit.Show();
                    textEdit.Focus();

                    // Redraw screen
                    pnlDraw.Invalidate();
                }
            }

            // Play/Stop
            if (e.KeyChar == ' ')
            {
                if (m_Server.PlayMode)
                    m_Server.Pause();
                else
                    m_Server.Play();
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
                    m_Server.SetKey(m_KeyToTrack[key].Name, key.Row, key.Value, key.Interpolation);
                }

                // Redraw grid and status bar
                UpdateView();
            }
        }

        private void textEdit_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Block anything that is not a valid input
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.') && (e.KeyChar != '-'))
                e.Handled = true;

            // only allow one decimal point
            var textbox = (TextBox)sender;
            if ((e.KeyChar == '.') && (textbox.Text.IndexOf('.') > -1))
                e.Handled = true;

            // only allow one decimal point
            if ((e.KeyChar == '-') && ((textbox.SelectionStart != 0) || (textbox.Text.IndexOf('-') > -1)))
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
                var track = m_ColumnToTrack[m_EditInKey.X];
                var key = GetKeyFromCell(m_EditInKey.X, m_EditInKey.Y);
                if (key == null)
                {
                    // Get prev key (before adding the key)
                    var prevKey = GetKeyFromCell(m_EditInKey.X, m_EditInKey.Y, true);

                    // Add key to track
                    key = new KeyInfo { Row = m_EditInKey.Y };
                    AddKeyToTrack(key, track);

                    // Copy interpolation mode from previusKey
                    if (prevKey != null)
                        key.Interpolation = prevKey.Interpolation;
                }

                // Save value in key
                key.Value = float.Parse(textEdit.Text, CultureInfo.InvariantCulture);

                // Update client
                m_Server.SetKey(track.Name, key.Row, key.Value, key.Interpolation);
            }

            // Terminate editing
            m_EditInKey = new Point(-1, -1);
            textEdit.Hide();

            // Redraw screen
            UpdateView();
        }

        private void MoveCursor(Point newPosition, bool remoteSetRow = false)
        {
            // Check if cursor has moved
            if (newPosition.Equals(m_Cursor))
                return;

            // Make sure cursor is in range
            var prevCursor = m_Cursor;
            m_Cursor.X = Math.Max(0, Math.Min(newPosition.X, m_ColumnToTrack.Count - 1));
            m_Cursor.Y = Math.Max(0, Math.Min(newPosition.Y, m_RowsCount - 1));

            // Is selection mod is on?
            var selectionActive = ModifierKeys.HasFlag(Keys.Shift) || (m_MousePressed);

            // Handle selection selection - Moved without an active selection? Cancel old selection
            if (!selectionActive)
            {
                // Cancel selection
                m_Selection = Rectangle.Empty;
            }
            else
            {
                // Selection just started?
                if (m_Selection == Rectangle.Empty)
                    m_SelectionStart = prevCursor;

                // Update selection
                m_Selection = new Rectangle(Math.Min(m_SelectionStart.X, m_Cursor.X),
                                            Math.Min(m_SelectionStart.Y, m_Cursor.Y),
                                            Math.Abs(m_SelectionStart.X - m_Cursor.X) + 1,
                                            Math.Abs(m_SelectionStart.Y - m_Cursor.Y) + 1);
            }

            // Update client (only if cursor change was caused by UI
            if (!remoteSetRow)
                m_Server.SetRow(m_Cursor.Y);

            // During playback, make sure cursor does not go below mid screen
            var maxViewHeight = m_Server.PlayMode ? pnlDraw.ClientSize.Height / 2 : pnlDraw.ClientSize.Height;

            // Make sure cursor is in view
            if (RowToViewY(m_Cursor.Y) < Row0Height)
                vScrollBar1.Value = m_Cursor.Y * RowHeight;
            if (RowToViewY(m_Cursor.Y + 1) > maxViewHeight)
                vScrollBar1.Value = Math.Max(0, (m_Cursor.Y + 1) * RowHeight + Row0Height - maxViewHeight);

            // Make sure cursor is in view
            if (ColumnToViewX(m_Cursor.X) < Column0Width)
                hScrollBar1.Value = m_Cursor.X * ColumnWidth;
            if (ColumnToViewX(m_Cursor.X + 1) > pnlDraw.ClientSize.Width)
                hScrollBar1.Value = Math.Max(0, (m_Cursor.X + 1) * ColumnWidth + Column0Width - pnlDraw.ClientSize.Width + InterpolationBarWidth);

            // Refresh everything
            UpdateView();
        }

        private void UpdateView()
        {
            // Update status bar
            toolStripCurrentRow.Text = @"Row: " + m_Cursor.Y;
            if (m_Cursor.X < m_ColumnToTrack.Count)
            {
                // Display current track/row value
                var track = m_ColumnToTrack[m_Cursor.X];
                toolStripCurrentValue.Text = track.GetValue(m_Cursor.Y).ToString("0.00", CultureInfo.InvariantCulture);

                // Display interpolation
                var keyIndex = track.FindKeyByRow(m_Cursor.Y, true);
                if (keyIndex >= 0)
                    toolStripInterpolation.Text = m_InterpolationToString[track.Keys[keyIndex].Interpolation];
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
            pnlVScroll.Invalidate();
        }

        #endregion

        #region Client Callbacks

        private void server_RowSet(object sender, int rowNr)
        {
            // Move cursor
            MoveCursor(new Point(m_Cursor.X, rowNr), true);
        }

        private void server_GetTrack(object sender, string trackName)
        {
            // Find track
            var track = m_Tracks.FirstOrDefault(t => t.Name == trackName);

            // Create new track if doesn't exists
            if (track == null)
            {
                m_Tracks.Add(track = new TrackInfo() { Name = trackName });

                RebuildKeyMaps();

                pnlDraw.Invalidate();
            }

            // Send all keys
            foreach (var key in track.Keys)
                m_Server.SetKey(trackName, key.Row, key.Value, key.Interpolation);
        }

        private void server_ClientConnected(object sender, EventArgs e)
        {
            // Client just got connected, send him the current position
            m_Server.SetRow(m_Cursor.Y);
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

            var interColors = new[] 
            {
                null, 
                new Pen(Color.Red, 3),
                new Pen(Utils.RGB(0x69FF24), 3),
                new Pen(Utils.RGB(0x2491FF), 3)
            };

            // Compute grid size
            var columnsCount = m_ColumnToTrack.Count;
            var totalWidth = Column0Width + columnsCount * ColumnWidth + InterpolationBarWidth;
            var totalHeight = m_RowsCount * RowHeight;

            // Update scroll bars
            vScrollBar1.LargeChange = pnlDraw.ClientSize.Height;
            vScrollBar1.SmallChange = 1;
            vScrollBar1.Maximum = totalHeight;
            hScrollBar1.LargeChange = pnlDraw.ClientSize.Width;
            hScrollBar1.SmallChange = 1;
            hScrollBar1.Maximum = totalWidth;
            if (hScrollBar1.LargeChange > hScrollBar1.Maximum) hScrollBar1.Value = 0;

            // Get screen 0,0 offset
            m_ViewTopLeftOffset = new Point(hScrollBar1.Value, vScrollBar1.Value);

            // Compute visible top/bottom rows
            m_ViewTopRowNr = (m_ViewTopLeftOffset.Y) / RowHeight;
            m_ViewBotRowNr = (m_ViewTopLeftOffset.Y + pnlDraw.ClientSize.Height - Row0Height) / RowHeight;

            // Trim visible rows by total row count
            m_ViewTopRowNr = Math.Min(m_ViewTopRowNr, m_RowsCount);
            m_ViewBotRowNr = Math.Min(m_ViewBotRowNr, m_RowsCount);

            // Draw horizontal lines
            g.Clip = new Region(CellRect(-1, -1, columnsCount + 1, m_RowsCount + 1).SetWidthMoveRight(pnlDraw.ClientSize.Width));
            for (var iRow = m_ViewTopRowNr; iRow < m_ViewBotRowNr; iRow++)
            {
                // Select row back color
                var color = (iRow % 2) == 0 ? Utils.Gray(10) : Utils.Gray(0);
                if (iRow % 8 == 0)
                    color = Utils.Gray(25);

                // Is it the "selected" row?
                if (iRow == m_Cursor.Y)
                    color = Utils.Gray(60);

                // Draw rect 
                var rowBGRect = CellRect(-1, iRow, columnsCount + 1, 1).SetWidthMoveRight(pnlDraw.ClientSize.Width).Expand(bottom: 1);
                g.FillRectangle(new SolidBrush(color), rowBGRect);

                // Background of "m_Cursor" cell
                if (iRow == m_Cursor.Y)
                    g.FillRectangle(new SolidBrush(Utils.RGB(0xFFFFFF)), CellRect(m_Cursor.X, iRow).Expand(bottom: 1));
            }

            // Draw column0 header 
            var titleRect = CellRect(-1, -1);
            var titleBrush = new SolidBrush(Utils.RGB(0x00));
            g.FillRectangle(titleBrush, titleRect);

            // Build formatting string
            var format = m_Project.TimeFormat;
            format = format.Replace("row", "0");
            format = format.Replace("seconds", "1");
            format = format.Replace("time", "2");

            // Draw rows index labels 
            g.Clip = new Region(CellRect(-1, -1, 1, m_RowsCount));
            for (var iRow = m_ViewTopRowNr; iRow < m_ViewBotRowNr; iRow++)
            {
                // Select row back color
                var color = (iRow % 8 == 0) ? Utils.Gray(200) : Utils.Gray(150);

                // Draw row number
                var rowsPerSecond = m_Project.BPM * m_Project.RowsPerBeat / 60.0;
                var seconds = iRow / rowsPerSecond;
                var time = TimeSpan.FromSeconds(seconds);
                try
                {
                    g.DrawString(string.Format(format, iRow, seconds, time), rowFont, new SolidBrush(color), CellRect(-1, iRow).Expand(right: -20), sfFar);
                }
                catch (Exception)
                {
                    // ignored
                }

                // Draw key count
                if (m_KeysInRow[iRow].Count > 0)
                {
                    var keyCountRect = CellRect(-1, iRow).SetWidthMoveLeft(20).Expand(0, -3, -3, -3);
                    g.FillRectangle(Brushes.LightGray, keyCountRect);
                    g.DrawString(m_KeysInRow[iRow].Count.ToString(), keysTipFont, Brushes.Black, keyCountRect.Pan(top:1), sfCenter);
                }
            }

            // Draw bookmarks
            foreach (var bookmark in m_Project.Bookmarks)
            {
                var bookmarkRect = CellRect(-1, bookmark.Row, 0, 1).Expand(right: RowHeight).Expand(right: -1, bottom: -1).Pan(right: 2);
                g.FillEllipse(Brushes.DarkRed, bookmarkRect);
                g.DrawEllipse(Pens.Red, bookmarkRect);
                g.DrawString(bookmark.Number.ToString(), bookmarkFont, Brushes.White, bookmarkRect.Pan(bottom: 1, right: 0), sfCenter);
            }

            // Draw column0 vertical seperators
            g.ResetClip();
            g.DrawLine(new Pen(Utils.Gray(180)), CellRect(-1, -1, 0, m_RowsCount + 1).Pan(right: Column0Width - 1));

            // Draw column headers 
            g.Clip = new Region(CellRect(-1, -1, columnsCount + 1, m_RowsCount + 1));
            for (int iColumn = 0; iColumn < columnsCount; iColumn++)
            {
                // Skip invisible columns
                var column = m_ColumnToTrack[iColumn];
                if ((ColumnToViewX(iColumn + 1) < 0) || (ColumnToViewX(iColumn) > pnlDraw.ClientSize.Width))
                    continue;

                // Draw vertical seperators
                g.DrawLine(new Pen(Utils.ARGB(0x20ffffff)), CellRect(iColumn + 1, -1, 0, m_RowsCount));

                // Draw column header 
                titleRect = CellRect(iColumn, -1).Expand(left: -1);
                g.FillRectangle(titleBrush, titleRect);

                // Draw column name
                g.DrawString(column.Name, titleFont, Brushes.White, titleRect.Expand(top: +0, left: -3), sfNear);
            }

            // Draw last vertical seperator
            g.ResetClip();
            g.DrawLine(new Pen(Utils.ARGB(0x20ffffff)), CellRect(columnsCount, -1, 0, m_RowsCount));

            // Draw header horizontal seperator
            g.DrawLine(new Pen(Utils.Gray(180)), new Rectangle(0, Row0Height, pnlDraw.ClientSize.Width, 0));

            // Draw column keys
            g.Clip = new Region(CellRect(-1, -1, columnsCount + 1, m_RowsCount).Expand(top: -Row0Height, left: -Column0Width));
            for (int iColumn = 0; iColumn < columnsCount; iColumn++)
            {
                // Skip invisible columns
                var column = m_ColumnToTrack[iColumn];
                if ((ColumnToViewX(iColumn + 1) < 0) || (ColumnToViewX(iColumn) > pnlDraw.ClientSize.Width))
                    continue;

                // Find first value key to show
                for (int iKey = 0; iKey < column.Keys.Count; iKey++)
                {
                    var key = column.Keys[iKey];

                    // Select color
                    var color = (key.Row == m_Cursor.Y && iColumn == m_Cursor.X) ? Brushes.Black : Brushes.White;

                    // Draw value
                    g.DrawString(key.Value.ToString("0.00", CultureInfo.InvariantCulture), rowFont, color, CellRect(iColumn, key.Row).Expand(right: -4), sfFar);

                    // Draw Interpolation
                    if (key.Interpolation != 0)
                    {
                        // find next key row
                        var nextKeyRow = (iKey == column.Keys.Count - 1) ? m_RowsCount : column.Keys[iKey + 1].Row;

                        // var compute line rect
                        g.DrawLine(interColors[key.Interpolation],
                            CellRect(iColumn + 1, key.Row, 0, nextKeyRow - key.Row)
                            .Pan(left: 2)
                            .Expand(top: -2, bottom: -1));
                    }
                }
            }

            // Draw selection
            if (m_Selection != Rectangle.Empty)
            {
                var selectionRect = CellRect(m_Selection.X, m_Selection.Y, m_Selection.Width, m_Selection.Height);
                g.FillRectangle(new SolidBrush(Color.FromArgb(70, 148, 198, 255)), selectionRect);
            }
        }

        private void pnlAudioView_Paint(object sender, PaintEventArgs e)
        {
            if (m_AudioTrack != null)
            {
                // Compute dest rect
                var destRect = pnlAudioView.ClientRectangle.Expand(top: -Row0Height);

                // Draw image
                e.Graphics.DrawImage(m_AudioTrack,
                    destRect,
                    new Rectangle(0, vScrollBar1.Value, destRect.Width, destRect.Height),
                    GraphicsUnit.Pixel);
            }

            // Do we have a valid current column?
            if (m_Cursor.X < m_ColumnToTrack.Count)
            {
                // Find track for graph rendering
                var track = m_ColumnToTrack[m_Cursor.X];

                // Get values
                var clientHeight = pnlDraw.ClientSize.Height;
                var values = new float[clientHeight];
                for (var y = 0; y < clientHeight; y++)
                    values[y] = track.GetValue(ViewYToRow((float)y));

                // Find data range (so we can scale it to view)
                if (track.Keys.Count > 0)
                {
                    var minValue = track.Keys.Min(t => t.Value);
                    var maxValue = track.Keys.Max(t => t.Value);
                    var delta = maxValue - minValue;

                    // Make sure we have a value range
                    if (delta > float.Epsilon)
                    {
                        // Find Scale-to-view transformation
                        var clientWidth = pnlAudioView.ClientRectangle.Width;
                        var distFromEdge = clientWidth * 0.2f;
                        var scale = (pnlAudioView.ClientRectangle.Width - distFromEdge * 2) / delta;

                        // Draw Graph
                        for (var y = 1; y < clientHeight; y++)
                            e.Graphics.DrawLine(Pens.Red,
                                distFromEdge + (values[y - 1] - minValue) * scale, y - 1,
                                distFromEdge + (values[y] - minValue) * scale, y);
                    }
                }
            }


            // Draw cursor
            var cursorViewY = RowToViewY(m_Cursor.Y) + RowHeight / 2;
            e.Graphics.DrawLine(Pens.Yellow, 0, cursorViewY, pnlAudioView.ClientSize.Width, cursorViewY);
        }

        #endregion

        #region Coordinate Conversion

        private int RowToViewY(int row)
        {
            return row * RowHeight + Row0Height - m_ViewTopLeftOffset.Y;
        }

        private int ViewYToRow(int viewY)
        {
            return (viewY - Row0Height + m_ViewTopLeftOffset.Y) / RowHeight;
        }

        private float ViewYToRow(float viewY)
        {
            return (viewY - Row0Height + m_ViewTopLeftOffset.Y) / RowHeight;
        }

        private int ColumnToViewX(int column)
        {
            return column * ColumnWidth - m_ViewTopLeftOffset.X + Column0Width;
        }

        private int ViewXToColumn(int viewX)
        {
            return (viewX - Column0Width + m_ViewTopLeftOffset.X) / ColumnWidth;
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
            if (column >= m_ColumnToTrack.Count)
                return null;

            // Get key
            var track = m_ColumnToTrack[column];
            var index = track.FindKeyByRow(row, includePrevKey);
            return index == -1 ? null : track.Keys[index];
        }

        private Point GetCellFromKey(KeyInfo key)
        {
            return new Point(m_ColumnToTrack.IndexOf(m_KeyToTrack[key]), key.Row);
        }

        private List<KeyInfo> KeysInRect(int column, int row, int columnSpan, int rowSpan, bool includePrevKey = false)
        {
            // Scan a columns
            var keys = new List<KeyInfo>();
            for (; columnSpan > 0; column++, columnSpan--)
            {
                // Ignore out-of-range columns
                if (column < 0) continue;
                if (column >= m_ColumnToTrack.Count) break;

                // Add keys in range
                keys.AddRange(
                    m_ColumnToTrack[column].Keys
                        .Where(t => (t.Row >= row) && (t.Row < row + rowSpan)));

                if (includePrevKey)
                {
                    // Search for key above rect
                    var topKey = m_ColumnToTrack[column].Keys.Where(t => t.Row < row).LastOrDefault();
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
            var key = GetKeyFromCell(m_Cursor.X, m_Cursor.Y, includePrevKey);
            if (key != null)
                keys.Add(key);

            // Get region keys
            if (m_Selection != Rectangle.Empty)
                keys.AddRange(KeysInRect(m_Selection.X, m_Selection.Y, m_Selection.Width, m_Selection.Height, includePrevKey).Where(k => k != key));

            return keys;
        }

        private void JumpToNextKey(Direction direction)
        {
            // Is cursor is valid position?
            if (m_Cursor.X >= m_ColumnToTrack.Count)
                return;

            switch (direction)
            {
                case Direction.Up:
                {
                    // find track
                    var track = m_ColumnToTrack[m_Cursor.X];

                    // Select get
                    var key = track.Keys.Where(k => k.Row < m_Cursor.Y).LastOrDefault();

                    // Move cursor
                    if (key != null)
                        MoveCursor(new Point(m_Cursor.X, key.Row));
                    break;
                }

                case Direction.Down:
                {
                    // find track
                    var track = m_ColumnToTrack[m_Cursor.X];

                    // Select get
                    var key = track.Keys.Where(k => k.Row > m_Cursor.Y).FirstOrDefault();

                    // Move cursor
                    if (key != null)
                        MoveCursor(new Point(m_Cursor.X, key.Row));
                    break;
                }

                case Direction.Left:
                {
                    for (var iColumn = m_Cursor.X - 1; iColumn >= 0; iColumn--)
                    {
                        var key = GetKeyFromCell(iColumn, m_Cursor.Y, true);
                        if ((key != null) && ((key.Row == m_Cursor.Y) || (key.Interpolation != 0)))
                        {
                            MoveCursor(new Point(iColumn, m_Cursor.Y));
                            break;
                        }
                    }

                    break;
                }

                case Direction.Right:
                {
                    for (var iColumn = m_Cursor.X + 1; iColumn < m_ColumnToTrack.Count; iColumn++)
                    {
                        var key = GetKeyFromCell(iColumn, m_Cursor.Y, true);
                        if ((key != null) && ((key.Row == m_Cursor.Y) || (key.Interpolation != 0)))
                        {
                            MoveCursor(new Point(iColumn, m_Cursor.Y));
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
            m_KeyToTrack.Add(key, track);

            // Add to row map
            m_KeysInRow[key.Row].Add(key);

            // Update client
            m_Server.SetKey(m_KeyToTrack[key].Name, key.Row, key.Value, key.Interpolation);
        }

        private void DeleteKeys(List<KeyInfo> keys)
        {
            // Remove keys
            foreach (var key in keys)
            {
                // Report key remove to client
                m_Server.DeleteKey(m_KeyToTrack[key].Name, key.Row);

                // Remove from track
                m_KeyToTrack[key].Keys.Remove(key);
                m_KeyToTrack.Remove(key);

                // Remove from row map
                m_KeysInRow[key.Row].Remove(key);
            }
        }

        private void RebuildVisibleColumnList()
        {
            m_ColumnToTrack = m_Tracks.Where(t => t.Visible).ToList();
        }

        private void RebuildKeyMaps()
        {
            // Apply Row count
            m_RowsCount = m_Project.Rows;

            // Remove keys outside of RowCount
            m_Tracks.ForEach(t => t.Keys = t.Keys.Where(k => k.Row < m_RowsCount).ToList());

            // Sort all tracks
            foreach (var track in m_Tracks)
                track.Keys.Sort((a, b) => a.Row.CompareTo(b.Row));

            // Build the key to track
            m_KeyToTrack = m_Tracks
                .SelectMany(t =>
                    t.Keys.Select(k => new Tuple<KeyInfo, TrackInfo>(k, t)))
                .ToDictionary(t => t.Item1, t => t.Item2);

            // Update KeysInRow
            m_KeysInRow = Enumerable.Range(0, m_RowsCount).Select(t => new List<KeyInfo>()).ToArray();
            m_Tracks.ForEach(t => t.Keys.ForEach(k => m_KeysInRow[k.Row].Add(k)));

            // Update Track Editor
            if (m_TrackEditor != null)
                m_TrackEditor.SetTracks(m_Tracks);

            // Rebuild visible column list
            RebuildVisibleColumnList();
        }

        #endregion

        #region Copy/Paste/Move

        private void CopyToClipboard()
        {
            // Find range
            var region = m_Selection;
            if (region == Rectangle.Empty)
                region = new Rectangle(m_Cursor, new Size(1, 1));

            // Build table
            var sb = new StringBuilder();
            for (var iRow = region.Top; iRow < region.Bottom; iRow++)
            {
                for (var iColumn = region.Left; iColumn < region.Right; iColumn++)
                {
                    var key = GetKeyFromCell(iColumn, iRow);
                    if (key != null)
                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1}", key.Interpolation, key.Value);

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
                var table =
                    clipboardData.TrimEnd('\r', '\n')
                        .Split(new[] {"\r\n", "\n"}, StringSplitOptions.None)
                        .Select(t => t.Split('\t').ToArray())
                        .ToArray();

                // Make sure table is not empty
                if (table.Length == 0) return;
                if (table[0].Length == 0) return;

                // Make sure all rows have the same values count
                if (!table.All(t => t.Length == table[0].Length))
                    return;

                // Convert data
                var newKeys = Enumerable.Range(0, table[0].Length).Select(t => new List<KeyInfo>()).ToArray();
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
                            key.Row           = m_Cursor.Y + iRow;
                            key.Value         = float.Parse(parts.Last(), CultureInfo.InvariantCulture);
                            key.Interpolation = parts.Length > 1 ? int.Parse(parts[0], CultureInfo.InvariantCulture) : 0;
                        }
                    }
                }

                // Save before change
                SaveUndoSnapshot();

                // Get keys to remove from paste region
                var oldKeys = KeysInRect(m_Cursor.X, m_Cursor.Y, table[0].Length, table.Length);
                DeleteKeys(oldKeys);

                // Add new keys
                for (int iColumn = 0; iColumn < newKeys.Length; iColumn++)
                {
                    // Make sure we're in a valid column
                    if (m_Cursor.X + iColumn >= m_ColumnToTrack.Count)
                        continue;

                    // Get track
                    var track = m_ColumnToTrack[m_Cursor.X + iColumn];

                    // Add all keys
                    foreach (var key in newKeys[iColumn])
                        AddKeyToTrack(key, track);
                }
            }
            catch (Exception)
            {
                // ignored
            }

            // Refresh screen
            pnlDraw.Invalidate();
        }

        private void MoveKeys(Direction direction, int steps)
        {
            // Find region
            var region = m_Selection;
            if (region == Rectangle.Empty)
                region = new Rectangle(m_Cursor, new Size(1, 1));

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
                if ((trackIndex < 0) || (trackIndex >= m_ColumnToTrack.Count))
                    continue;

                // update row number
                key.Row += yshift;
                if ((key.Row < 0) || (key.Row >= m_RowsCount))
                    continue;

                // Add key to track
                AddKeyToTrack(key, m_ColumnToTrack[trackIndex]);
            }

            // Store selection state
            var storedSelection      = m_Selection;
            var storedSelectionStart = m_SelectionStart;

            // Move cursor (this cancels selection)
            MoveCursor(new Point(m_Cursor.X + xshift, m_Cursor.Y + yshift));

            // Restore selection
            if (storedSelection != Rectangle.Empty)
            {
                m_SelectionStart = storedSelectionStart;
                m_SelectionStart.Offset(xshift, yshift);

                // Update selection
                m_Selection = storedSelection.Pan(right: xshift, bottom: yshift);
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
                    m_Project = ser.Deserialize(reader) as RocketProject;

                    // Cache variables
                    m_Tracks = m_Project.Tracks;
                    m_RowsCount = m_Project.Rows;

                    // Update project file status
                    m_ProjectFilename = filename;
                    m_Modified = false;
                    UpdateApplicationTitle();

                    // Fix bookmarks
                    foreach (var bookmark in m_Project.Bookmarks)
                        if (bookmark.Number == -1)
                            bookmark.Number =
                                Enumerable.Range(1, 9)
                                    .FirstOrDefault(index => !m_Project.Bookmarks.Any(b => b.Number == index));

                    // Rebuild all key maps
                    RebuildKeyMaps();

                    // Update MRU
                    m_MruMenu.AddFile(filename);
                    m_MruMenu.SaveToRegistry();

                    // Clear undo buffer
                    m_UndoSnapIndex = -1;
                    m_UndoSnaps = new List<Stream>();

                    // Reload audio
                    LoadAudio();

                    // Refresh screen
                    pnlDraw.Refresh();
                    pnlAudioView.Refresh();
                    pnlVScroll.Refresh();
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
                    ser.Serialize(writer, m_Project);
                }

                // Update project file status
                m_ProjectFilename = filename;
                m_Modified = false;
                UpdateApplicationTitle();

                // Update MRU
                m_MruMenu.AddFile(filename);
                m_MruMenu.SaveToRegistry();
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
            if (string.IsNullOrEmpty(m_ProjectFilename))
                title += " [Unsaved Project]";
            else
                title += " [" + Path.GetFileNameWithoutExtension(m_ProjectFilename) + (m_Modified ? "*]" : "]");

            // Update form property    
            Text = title;
        }

        #endregion

        #region Bookmarks related

        private void SetBookmark(int number)
        {
            // Remove bookmark from current row if already exists
            var eraseBookmark = false;
            var bookmark = m_Project.Bookmarks.FirstOrDefault(b => b.Row == m_Cursor.Y);
            if (bookmark != null)
            {
                // Check if this is a bookmark erase (toggling of bookmark)
                eraseBookmark = (bookmark.Number == number) || (number == -1);

                // Anyway, remove bookmark
                m_Project.Bookmarks.Remove(bookmark);
            }

            // Remove bookmark if number already exists somewhere else
            bookmark = m_Project.Bookmarks.FirstOrDefault(b => b.Number == number);
            if (bookmark != null)
                m_Project.Bookmarks.Remove(bookmark);

            // Create new bookmark (if we need to)
            if (!eraseBookmark)
            {
                // create a new bookmark
                bookmark = new Bookmark() { Number = number, Row = m_Cursor.Y };
                m_Project.Bookmarks.Add(bookmark);

                // If this is an automatic index finding bookmark, find index
                if (bookmark.Number == -1)
                    bookmark.Number = Enumerable.Range(1, 9).FirstOrDefault(index => !m_Project.Bookmarks.Any(b => b.Number == index));
            }

            // Refresh screen
            pnlDraw.Invalidate();
        }

        private void GotoBookmark(int number)
        {
            Bookmark target;

            // is it "Next bookmark"?
            if (number == -1)
            {
                // get next bookmark
                target = m_Project.Bookmarks.OrderBy(t => t.Row).Where(t => t.Row > m_Cursor.Y).FirstOrDefault();

                // if next could not be found, go to first
                if (target == null)
                    target = m_Project.Bookmarks.FirstOrDefault();
            }
            else
            {
                // Find relevent bookmark
                target = m_Project.Bookmarks.FirstOrDefault(b => b.Number == number);
            }

            // Did we actually manage to find a bookmark?
            if (target != null)
            {
                // Move cursor
                MoveCursor(new Point(m_Cursor.X, target.Row));

                // Make sure we're centered in view
                vScrollBar1.Value = Math.Max(0, (m_Cursor.Y + 1) * RowHeight + Row0Height - (pnlDraw.ClientSize.Height / 2));
            }
        }

        #endregion

        #region Undo/Redo related

        private void SaveUndoSnapshot()
        {
            // Everytime a snapshot is saved, it means that we're not doing undo right now
            m_UndoInProcess = false;

            // Remember that there was a change
            m_Modified = true;
            UpdateApplicationTitle();

            // Remove all snaps after UndoSnapIndex
            while (m_UndoSnaps.Count - 1 > m_UndoSnapIndex)
                m_UndoSnaps.RemoveAt(m_UndoSnaps.Count - 1);

            // Create snapshot
            var snapStream = new MemoryStream();
            var ser = new XmlSerializer(typeof(RocketProject));
            ser.Serialize(snapStream, m_Project);

            // Add stream
            m_UndoSnaps.Add(snapStream);
            m_UndoSnapIndex = m_UndoSnaps.Count - 1;
        }

        private void RestoreUndoSnapshot()
        {
            // Can we undo?
            if (m_UndoSnapIndex == -1)
                return;

            // Have we started an undo sequance?
            if (!m_UndoInProcess)
            {
                // if yes, save a snapshot so we can redo into current position
                SaveUndoSnapshot();
                m_UndoSnapIndex--;

                // Remember that we're in an undo sequance
                m_UndoInProcess = true;
            }

            // Restore snapshot
            var snapStream = m_UndoSnaps[m_UndoSnapIndex];
            snapStream.Seek(0, SeekOrigin.Begin);
            var ser = new XmlSerializer(typeof(RocketProject));
            m_Project = ser.Deserialize(snapStream) as RocketProject;

            // Rebuild things...
            m_Tracks = m_Project.Tracks;
            m_RowsCount = m_Project.Rows;
            RebuildKeyMaps();
            RebuildVisibleColumnList();

            // Move restore point
            m_UndoSnapIndex--;
        }

        #endregion

        #region Audio track related

        private void LoadAudio()
        {
            // Do we have anything to load
            if (string.IsNullOrEmpty(m_Project.AudioFile))
                return;

            try
            {
                // Open file
                var reader = new AudioFileReader(m_Project.AudioFile);

                // read data
                m_AudioWaveFormat = reader.WaveFormat;
                m_AudioBuffer = new float[reader.Length / 4];
                reader.Read(m_AudioBuffer, 0, m_AudioBuffer.Length);

                // Start building the audio track image
                BuildAudioTrack();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error while loading audio file.\n" + ex.Message, "Loading Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildAudioTrack()
        {
            // Do we have a loaded audio?
            if (m_AudioWaveFormat == null)
                return;

            // Make sure all scroll values are updated
            var g = Graphics.FromHwnd(pnlDraw.Handle);
            pnlDraw_Paint(this, new PaintEventArgs(g, pnlDraw.ClientRectangle));
            g.Dispose();

            // Set bitmap size
            m_AudioTrack = new Bitmap(pnlAudioView.ClientSize.Width, vScrollBar1.Maximum);

            // Compute samples per pixel
            var rowsPerSecond = m_Project.BPM * m_Project.RowsPerBeat / 60.0;

            // Get bits
            BitmapData bmpData = m_AudioTrack.LockBits(
                new Rectangle(0, 0, m_AudioTrack.Width, m_AudioTrack.Height),
                ImageLockMode.ReadWrite,
                m_AudioTrack.PixelFormat);

            for (long y = 0; y < m_AudioTrack.Height; y++) // pixel-time = pixel-y / pixels-per-second
            {
                // Compute first and last samples to include
                var firstSample = (long)(m_AudioWaveFormat.SampleRate * (y - 0) / (RowHeight * rowsPerSecond));
                var lastSample  = (long)(m_AudioWaveFormat.SampleRate * (y + 1) / (RowHeight * rowsPerSecond));
                if (firstSample < 0) firstSample = 0;
                if (lastSample < 0)  lastSample = 0;
                if (firstSample * m_AudioWaveFormat.Channels >= m_AudioBuffer.Length) firstSample = m_AudioBuffer.Length / m_AudioWaveFormat.Channels - 1;
                if (lastSample  * m_AudioWaveFormat.Channels >= m_AudioBuffer.Length) lastSample  = m_AudioBuffer.Length / m_AudioWaveFormat.Channels - 1;

                // Create pixel counter projection
                var midPoint = bmpData.Width / 2;
                var pixelCounter = new int[bmpData.Width];
                for (long iSample = firstSample; iSample < lastSample; iSample++)
                {
                    // Find sample position
                    var sample = midPoint + (int)(m_AudioBuffer[iSample * m_AudioWaveFormat.Channels] * bmpData.Width);
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
            m_AudioTrack.UnlockBits(bmpData);

            // Refresh screen
            pnlAudioView.Invalidate();
        }

        #endregion

        #region Menu Events

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Ask user what to do if application closes with an unsaved project
            if (m_Modified)
                if (MessageBox.Show(this, "Are you sure you want to close aplication without saving?", "Unsaved Project", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
                    e.Cancel = true;
        }

        private void tmrUpdateUI_Tick(object sender, EventArgs e)
        {
            // Refresh trackManager option
            trackManagerToolStripMenuItem.Checked = m_TrackEditor.Visible;

            toolStripConnectionStatus.Text      = m_Server.IsConnected() ? "Connected" : "Disconnected";
            toolStripConnectionStatus.ForeColor = m_Server.IsConnected() ? Color.DarkGreen : Color.DarkRed;
        }

        private void OnMruFile(int number, String filename)
        {
            OpenFile(filename);
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Ask "are you sure"
            if (m_Modified)
                if (MessageBox.Show(this, "Unsaving current project detected.\nAre you sure you want to start a new project?", "Unsaved Project", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
                    return;

            m_ProjectFilename = "";
            m_Project = new RocketProject();
            m_Tracks = m_Project.Tracks;
            m_UndoSnaps = new List<Stream>();
            m_UndoSnapIndex = -1;
            m_Modified = false;

            // Update app title
            UpdateApplicationTitle();

            // Clean key maps
            RebuildKeyMaps();

            BuildAudioTrack();

            pnlDraw.Invalidate();
            pnlAudioView.Invalidate();
            pnlVScroll.Invalidate();
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
            if (string.IsNullOrEmpty(m_ProjectFilename))
                saveAsToolStripMenuItem_Click(null, null);
            else
                SaveProject(m_ProjectFilename);
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

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyToClipboard();

            // Simulate delete key
            pnlDraw_KeyDown(this, new KeyEventArgs(Keys.Delete));
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
            if (m_UndoSnapIndex >= m_UndoSnaps.Count - 2)
                return;

            // Jump two-steps forward (and restore undosnap goes one step back)
            m_UndoSnapIndex += 2;

            // Restore point
            RestoreUndoSnapshot();

            // Redraw screen
            pnlDraw.Invalidate();
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dlg = new FormSettings(m_Project);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                // Rebuild all keys maps
                RebuildKeyMaps();

                // Reload audio
                LoadAudio();
            }
        }

        private void trackManagerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_TrackEditor.Visible = !m_TrackEditor.Visible;
        }

        private void remoteExportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Make sure we have a connection
            if (!m_Server.IsConnected())
            {
                MessageBox.Show(this, "Remote export requires a connection to the player.\nPlease make sure demo is running and connected and try again.", "Remote Export", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Request remote export
            m_Server.RemoteExport();

            // Notify user
            MessageBox.Show(this, "Player was requested to export tracks", "Remote Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion

        private void panelVScroll_Paint(object sender, PaintEventArgs e)
        {
            if (m_ColumnToTrack.Count == 0)
                return;

            var g = e.Graphics;

            var interColors = new[]
            {
                new SolidBrush(Color.White),
                new SolidBrush(Color.Red),
                new SolidBrush(Utils.RGB(0x69FF24)),
                new SolidBrush(Utils.RGB(0x2491FF))
            };

            // compute view scales
            var clientWidth = pnlVScroll.ClientSize.Width;
            var clientHeight = pnlVScroll.ClientSize.Height;
            m_VScroll_YScale = (float)     (clientHeight - m_VScroll_YMargin * 2) / m_RowsCount;
            m_VSCroll_XScale = Math.Max(1, (clientWidth  - m_VScroll_XMargin * 2) / m_ColumnToTrack.Count);

            // Draw column keys
            for (var iCol = 0; iCol < m_ColumnToTrack.Count; iCol++)
            {
                // Draw background
                var color = iCol%2 == 0 ? Utils.Gray(20) : Utils.Gray(0);
                g.FillRectangle(new SolidBrush(color), m_VScroll_YMargin + m_VSCroll_XScale*iCol, 0, m_VScroll_XMargin + m_VSCroll_XScale*iCol, clientHeight);

                var track = m_ColumnToTrack[iCol];
                for (var iKey = 0; iKey < track.Keys.Count; iKey++)
                {
                    // Compute key length
                    var key = track.Keys[iKey];
                    var length = iKey == track.Keys.Count - 1
                        ? m_RowsCount - key.Row
                        : track.Keys[iKey + 1].Row - key.Row;
                    if (key.Interpolation == 0) length = 1;

                    // Select color
                    var brush = interColors[key.Interpolation];
                    g.FillRectangle(brush, m_VScroll_XMargin + m_VSCroll_XScale*iCol, m_VScroll_YMargin + m_VScroll_YScale*key.Row, m_VSCroll_XScale,
                        (float) Math.Ceiling(length*m_VScroll_YScale));
                }
            }

            // Draw frame
            var leftCol  = ViewXToColumn(Column0Width);
            var rightCol = ViewXToColumn(pnlDraw.ClientSize.Width);
            var topRow = ViewYToRow(Row0Height);
            var botRow = ViewYToRow(pnlDraw.ClientSize.Height);
            var viewRect = Rectangle.FromLTRB(
                m_VScroll_XMargin + m_VSCroll_XScale * leftCol,  (int)(m_VScroll_YMargin + m_VScroll_YScale * topRow),
                m_VScroll_XMargin + m_VSCroll_XScale * rightCol, (int)(m_VScroll_YMargin + m_VScroll_YScale * botRow));
            g.DrawRectangle(Pens.Yellow, viewRect);
        }

        private void panelVScroll_SizeChanged(object sender, EventArgs e)
        {
            pnlVScroll.Invalidate();
        }

        private void pnlVScroll_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Find center row/column
                var centerColumn = (e.X - m_VScroll_XMargin) / m_VSCroll_XScale;
                var centerRow    = (e.Y - m_VScroll_YMargin) / m_VScroll_YScale;

                // Enforce view limits
                centerColumn = Utils.EnsureRange(centerColumn, 0, m_ColumnToTrack.Count - 1);
                centerRow    = Utils.EnsureRange(centerRow, 0, m_RowsCount - 1);

                // Compute center offset
                var xOffset = pnlDraw.ClientSize.Width / 2;
                var yOffset = pnlDraw.ClientSize.Height / 2;

                // Update Scroll position
                vScrollBar1.Value = Math.Max(0, (int)(centerRow    * RowHeight   - yOffset));
                hScrollBar1.Value = Math.Max(0, (int)(centerColumn * ColumnWidth - xOffset));
            }
        }
    }
}



