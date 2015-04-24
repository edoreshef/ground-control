using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GroundControl
{
    public class RocketServer
    {
        public delegate void GetTrackDelegate(object sender, string trackName);
        public delegate void RowSetDelegate(object sender, int rowNr);
        public event GetTrackDelegate GetTrack;
        public event RowSetDelegate RowSet;

        public Dictionary<string, int> TrackMap = new Dictionary<string, int>();
        public bool PlayMode;

        private TcpListener server = null;
        private Thread listenThread;
        private Control syncControl;
        private TcpClient connectedClient;
        private Stream stream;
        private System.Windows.Forms.Timer updateTimer;

        private const string ClientGreet = "hello, synctracker!";
        private const string ServerGreet = "hello, demo!";

        private enum Command
        {
            SetKey = 0,
            DeleteKey = 1,
            GetTrack = 2,
            SetRow = 3,
            Pause = 4,
            SaveTracks = 5
        };

        public RocketServer()
        {
            // Create sync control
            syncControl = new Control();
            syncControl.CreateControl();

            // Setup housekeeping timer
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 1;
            updateTimer.Tick += updateTimer_Tick;
            updateTimer.Start();

            // Setup server
            server = new TcpListener(IPAddress.Any, 1338);
            listenThread = new Thread(() => 
            {
                try
                {
                    server.Start();
                    while (true)
                    {
                        // blocks until a client has connected to the server
                        TcpClient client = server.AcceptTcpClient();

                        // Notify main thread
                        syncControl.BeginInvoke(new MethodInvoker(delegate { NewConnection(client); }));
                    }
                }
                catch(Exception ex)
                {
                    MessageBox.Show("Rocket Server Error.\nDetails: " + ex.Message, "Rocket Server Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
              
            });
            listenThread.IsBackground = true;
            listenThread.Start();
        }

        private void updateTimer_Tick(object sender, EventArgs e)
        {
            // Exit if there is no connection
            if (connectedClient == null)
                return;

            // Is there a command to process?
            while (connectedClient.Available > 0)
            {
                // Get command
                var cmd = stream.ReadByte();

                // Execute command
                switch((Command)cmd)
                {
                    case 0: 
                        // DO NOTHING - This is an invalid command that often is received when send command as 32bit
                        break;

                    case Command.SetRow:
                        var row = BitConverter.ToInt32(read(4).Reverse().ToArray(), 0);
                        if (RowSet != null)
                            RowSet(this, row);
                        break;

                    case Command.GetTrack:
                        // Read track name
                        var strLen =  BitConverter.ToInt32(read(4).Reverse().ToArray(), 0);
                        var trackName = Encoding.ASCII.GetString(read(strLen));

                        // Add to track list
                        TrackMap.Add(trackName, TrackMap.Count);

                        // Notify on gettrack request
                        if (GetTrack != null)
                            GetTrack(this, trackName);
                        break;

                    default:
                        0.ToString();
                        //var data = read(connectedClient.Available);
                        //data.ToString();
                        break;
                }
            }
        }

        public void Pause()
        {
            PlayMode = false;

            // Build and send command
            write(new[] { (byte)Command.Pause, (byte)1 });
        }

        public void Play()
        {
            PlayMode = true;

            // Build and send command
            write(new[] { (byte)Command.Pause, (byte)0 });
        }

        public void SetRow(int rowNr)
        {
            // Build and send command
            write(new[] { (byte)Command.SetRow });

            // Send position
            write(BitConverter.GetBytes(rowNr).Reverse().ToArray());
        }

        public void SetKey(string track, int row, float value, int interType)
        {
            // Look for track index
            int trackIndex;
            if (!TrackMap.TryGetValue(track, out trackIndex))
                return;

            // Send command
            write(new[] { (byte)Command.SetKey });

            // Send track number
            write(BitConverter.GetBytes(trackIndex).Reverse().ToArray());

            // Send row number
            write(BitConverter.GetBytes(row).Reverse().ToArray());

            // Send row number
            write(BitConverter.GetBytes(value).Reverse().ToArray());

            // Send interType number
            write(new[] { (byte)interType });
        }

        public void DeleteKey(string track, int row)
        {
            // Look for track index
            int trackIndex;
            if (!TrackMap.TryGetValue(track, out trackIndex))
                return;

            // Send command
            write(new[] { (byte)Command.DeleteKey });

            // Send track number
            write(BitConverter.GetBytes(trackIndex).Reverse().ToArray());

            // Send row number
            write(BitConverter.GetBytes(row).Reverse().ToArray());
        }

        private void NewConnection(TcpClient client)
        {
            // Dump old client
            if (connectedClient != null)
                CloseConnection();

            // Store new client
            connectedClient = client;
            stream = connectedClient.GetStream();

            // send welcome message
            var sGreet = Encoding.ASCII.GetBytes(ServerGreet);
            stream.Write(sGreet, 0, sGreet.Length);

            // Wait for client greet
            var cGreet = Encoding.ASCII.GetString(read(ClientGreet.Length));
            if (cGreet != ClientGreet)
                CloseConnection();

            // Assume start play mode is stop
            PlayMode = false;

            // Clean track list
            TrackMap = new Dictionary<string, int>();
        }

        private void CloseConnection()
        {
            connectedClient.Close();
            connectedClient = null;
            stream = null;
        }

        private byte[] read(int bytes)
        {
            var buf = new byte[bytes];
            var bufIndex = 0;

            while (buf.Length - bufIndex > 0)
            {
                var retLen = stream.Read(buf, bufIndex, buf.Length - bufIndex);
                if (retLen == 0)
                    0.ToString();

                bufIndex += retLen;
            }

            return buf;
        }

        private void write(byte[] data)
        {
            try 
            {
                // Make sure stream is valid
                if (stream == null)
                    return;

                // Write data
                stream.Write(data, 0, data.Length);
            }
            catch(Exception)
            {
                CloseConnection();
            }
        }
    }
}
