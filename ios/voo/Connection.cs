using System;
using System.IO;
using System.Threading;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace Voo
{
    public class Connection {
        List<RecvHandler> _recvq = new List<RecvHandler>();
        IPAddress _ip = IPAddress.Any;
        TcpClient _client;
        object _lock = new object();

        public Connection() {
            IPNP.IO.Init();
            IPNP.IO.IncomingBroadcast += ev_broadcast;
        }

        public void Send(string s)
        {
            lock (_lock) {
                if (_client == null)
                    return;
                try {
                    Socket sock = _client.Client;
                    byte[] buf = Encoding.UTF8.GetBytes(s + "\n");
                    sock.BeginSend(buf, 0, buf.Length, SocketFlags.None, ar => { sock.EndSend(ar); }, null);
                    Console.WriteLine("sent [" + s + "]");
                } catch { }
            }
        }

        public delegate void RecvHandler(string s);
        public void Send(string s, RecvHandler cb) {
            _recvq.Add(cb);
            Send(s);
        }

        public event Action Connecting;
        public event Action FailedConnecting;
        public event Action SuccessConnecting;
        public event Action Disconnected;

        public void Stop() { Send(":stop"); }
        public void Backfast()   { Send(":seek " + (Math.Max(0, this.Time - 30000)).ToString()); }
        public void Backslow()   { Send(":seek " + (Math.Max(0, this.Time - 5000)).ToString()); }
        public void Fwdfast()    { if (this.Time < this.Length - 60000) Send(":seek " + (this.Time + 30000).ToString()); }
        public void Fwdslow()    { if (this.Time < this.Length - 10000) Send(":seek " + (this.Time + 5000).ToString()); }
        public void Poweroff()   { Send(":comms off"); }
        public void Louder()     { Send(":comms volume up"); }
        public void Vol(int vol) { Send(":comms volume " + vol); }
        public void Softer()     { Send(":comms volume down"); }
        public void Subtitles() {
            if (this.SubtitleCount == 0) {
                return;
            } else if (Subtitle == -1) {
                Send(":subtitle 0");
            } else {
                if (this.Subtitle == this.SubtitleCount-1)
                    Send(":subtitle 0");
                else
                    Send(":subtitle " + (this.Subtitle + 1));
            }
        }
        public void List(string parent, string name, Action<string, string[]> cb) {
            string path;
            if (parent != null)
                path = parent + "\\" + name;
            else
                path = name;

            Send(":list " + path,
                delegate (string line) {
                Console.WriteLine("LINE:" + line);
                    cb(path, line.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries));
                });
        }

        public void DeleteFile(string parent, string name) { Send(":delfile " + parent + "\\" + name); }
        public void DeleteDir(string parent, string name) { Send(":deldir " + parent + "\\" + name); }
        public void Play(string parent, string name) {
            Send(":comms source 3");
            Send(":load " + parent + "\\" + name);
        }
        public void TogglePause() {
            Send(":comms source 3");
            Send(":togglepause");
        }

        //--------------------------------------

        void ev_broadcast(IPAddress src_addr, IPNP.Serial src_serial, IPNP.Device src_devid, IPNP.Items items)
        {
            if (_ip != IPAddress.Any)
                return;
            IPNP.DiscoveryItem di = items.GetDiscoveryItem();
            if (di == null)
                return;
            if (di.DeviceType != new IPNP.Device("DB8DD6FA-398A-4548-8CD1-9231D6DC4EE2"))
                return;

            Connect(src_addr);
        }

        void Connect(IPAddress ip) {
            lock (_lock) {
                if (_client != null) {
                    try { _client.Close(); } catch { }
                    try { ((IDisposable)_client).Dispose(); } catch { }
                }
                _client = null;
                _ip = ip;
                _client = new TcpClient();
                _client.BeginConnect(_ip, 4356, ev_connected, null);
            }
            if (this.Connecting != null) this.Connecting();
        }

        void ev_connected(IAsyncResult ar)
        {
            if (ar.IsCompleted) {
                try {
                    lock (_lock) {
                        _client.EndConnect(ar);
                    }
                    if (this.SuccessConnecting != null) this.SuccessConnecting();
                    (new Thread(ev_read) { IsBackground = true }).Start();
                } catch {
                    lock (_lock) {
                        try { _client.Close(); } catch { }
                        try { ((IDisposable)_client).Dispose(); } catch { }
                        _client = null;
                        _ip = IPAddress.Any;
                    }
                    if (this.FailedConnecting != null) this.FailedConnecting();
                }
            }
        }

        void ev_read()
        {
            StreamReader rdr;
            lock (_lock) {
                rdr = new StreamReader(_client.GetStream(), Encoding.UTF8);
            }
            try {
                string s;
                while ((s = rdr.ReadLine()) != null)
                    ev_line(s);
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            } finally {
                lock (_lock) {
                    try { _client.Close(); } catch { }
                    try { ((IDisposable)_client).Dispose(); } catch { }
                    _client = null;
                    _ip = IPAddress.Any;
                }
                if (this.Disconnected != null) this.Disconnected();
            }
        }

        void ev_line(string s) {
            Console.WriteLine("got line {0}:{1}", s.Length, s.Replace("\r", "").Replace("\n", ""));
            if (s.Length == 0) return;
            if (s[0] == '*') {
                if (s.StartsWith("*stopped")) {
                    this.State = PlayState.Stopped;
                    OnStateChanged();
                }
                if (s.StartsWith("*playing")) {
                    this.State = PlayState.Playing;
                    OnStateChanged();
                }
                if (s.StartsWith("*paused")) {
                    this.State = PlayState.Paused;
                    OnStateChanged();
                }
                if (s.StartsWith("*seekable")) {
                    this.Seekable = true;
                    OnSeekableChanged();
                }
                if (s.StartsWith("*notseekable")) {
                    this.Seekable = false;
                    OnSeekableChanged();
                }
                if (s.StartsWith("*subtitle ")) {
                    this.Subtitle = Convert.ToInt32(s.Substring(10));
                    OnSubtitleChanged();
                }
                if (s.StartsWith("*subtitlecount ")) {
                    this.SubtitleCount = Convert.ToInt32(s.Substring(15));
                    OnSubtitleCountChanged();
                }
                if (s.StartsWith("*time ")) {
                    this.Time = Convert.ToUInt64(s.Substring(6));
                    OnTimeChanged();
                }
                if (s.StartsWith("*length ")) {
                    this.Length = Convert.ToUInt64(s.Substring(8));
                    OnLengthChanged();
                }

            } else if (s[0] == '!') {
                if (_recvq.Count != 0) { _recvq[0](s.Substring(1)); _recvq.RemoveAt(0); }
            }
        }

        public enum PlayState {
            Stopped,
            Playing,
            Paused
        }

        public PlayState State { private set; get; }
        public bool Seekable { private set; get; }
        public int Subtitle { private set; get; }
        public int SubtitleCount { private set; get; }
        public ulong Time { private set; get; }
        public ulong Length { private set; get; }

        public event Action TimeChanged;
        void OnTimeChanged() { if (TimeChanged != null) TimeChanged(); }
        public event Action StateChanged;
        void OnStateChanged() { if (StateChanged != null) StateChanged(); }
        public event Action LengthChanged;
        void OnLengthChanged() { if (LengthChanged != null) LengthChanged(); }
        public event Action SeekableChanged;
        void OnSeekableChanged() { if (SeekableChanged != null) SeekableChanged(); }
        public event Action SubtitleChanged;
        void OnSubtitleChanged() { if (SubtitleChanged != null) SubtitleChanged(); }
        public event Action SubtitleCountChanged;
        void OnSubtitleCountChanged() { if (SubtitleCountChanged != null) SubtitleCountChanged(); }
    }
}
