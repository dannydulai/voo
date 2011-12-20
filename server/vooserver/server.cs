using System;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Drawing;
using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;
using System.IO.Ports;

using IPNP = Voo.IPNP;

namespace vooserver
{
    public class Client {
        static List<Client> __all = new List<Client>();

        Server _s;
        string _share;
        TcpClient _client;
        StreamReader _rdr;

        public Client(Server s, TcpClient client, string share) {
            _s = s;
            _share = share;
            _client = client;
            _client.NoDelay = true;

            _rdr = new StreamReader(client.GetStream(), Encoding.UTF8);

            (new Thread(ev_read) { IsBackground = true }).Start();

            lock (__all) {
                __all.Add(this);
            }
        }

        void ev_read()
        {
            try {
                string s;
                while ((s = _rdr.ReadLine()) != null)
                    ev_parsecmd(s);
            } catch {
            } finally {
                Abort();
            }
        }

        void ev_parsecmd(string fromwire)
        {
            try {
                string[] parts = fromwire.Split(new char[]{' '}, 2);
                if (parts[0] == ":nop") return;

                Console.WriteLine("GOT [" + fromwire + "]");

                switch (parts[0]) {
                    case ":exit": Console.WriteLine("client issued exit"); this.Abort(); break;
                    case ":togglepause": { _s.TogglePause(); break; }
                    case ":stop": { _s.Stop(); break; }
                    case ":subtitle": { _s.Subtitle(Convert.ToInt32(parts[1])); break; }
                    case ":seek": { _s.Seek(Convert.ToUInt64(parts[1])); break; }
                    case ":nextframe": { _s.NextFrame(); break; }
                    case ":comms": { _s.Comms(parts[1]); break; }
                    case ":delfile": { 
                        string file = makesafe(parts[1]);
                        Console.WriteLine("deleting file {0}", file);
                        try {
                            File.Delete(Path.Combine(_share, file));
                        } catch (Exception e) {
                            Console.WriteLine("failed to delete file {0}:\n{1}", file, e.ToString());
                        }
                        break;
                    }
                    case ":deldir": { 
                        string dir = makesafe(parts[1]);
                        Console.WriteLine("deleting dir {0}", dir);
                        try {
                            Directory.Delete(Path.Combine(_share, dir), true);
                        } catch (Exception e) {
                            Console.WriteLine("failed to delete dir {0}:\n{1}", dir, e.ToString());
                        }
                        break;
                    }
                    case ":load": {
                        string file = makesafe(parts[1]);
                        _s.Play(Path.Combine(_share, file));
                        break;
                    }
                    case ":list": {
                        string dir;
                        if (parts.Length == 1 || parts[1].Trim() == "")
                            dir = ".";
                        else
                            dir = parts[1].Replace("\\", "/").Trim();

                        if (dir == "..") { Abort(); break; }
                        if (dir.IndexOf("../") != -1) { Abort(); break; }
                        if (dir.IndexOf("/..") != -1) { Abort(); break; }
                        if (dir[0] == '/') { Abort(); break; } 

                        dir = dir.Replace("/", Path.DirectorySeparatorChar.ToString());

                        string r = "";
                        foreach (string subdir in Directory.GetDirectories(Path.Combine(_share, dir))) {
                            string n = Path.GetFileName(subdir);
                            if (r != "") r += ":";
                            r += "D" + n;
                        }
                        foreach (string subfile in Directory.GetFiles(Path.Combine(_share, dir))) {
                            string n = Path.GetFileName(subfile);
                            switch (Path.GetExtension(n).ToLower()) {
                                case ".avi": break;
                                case ".mpg": break;
                                case ".mp4": break;
                                case ".divx": break;
                                case ".xvid": break;
                                case ".mkv": break;
                                case ".wmv": break;
                                case ".mov": break;
                                case ".flv": break;
                                case ".mpeg": break;
                                default : continue;
                            }
                            if (r != "") r += ":";
                            r += "F" + n;
                        }
                        Send("!" + r);
                        break;
                    }
                }

            } catch (Exception e) {
                Console.WriteLine("client blew up: " + e.ToString());
                Abort();
            }
        }

        string makesafe(string file) {
            file = file.Replace("\\", "/");
            if (file == "..") { Abort(); return null; }
            if (file.IndexOf("../") != -1) { Abort(); return null; }
            if (file.IndexOf("/..") != -1) { Abort(); return null; }
            if (file[0] == '/') { Abort(); return null; } 
            file = file.Replace("/", Path.DirectorySeparatorChar.ToString());
            return file;
        }

        static public void Broadcast(string s) {
            lock (__all) {
                foreach (Client c in __all) c.Send(s);
            }
        }
        public void Send(string s)
        {
            try {
                Socket sock = _client.Client;
                byte[] buf = Encoding.UTF8.GetBytes(s + "\n");
                sock.BeginSend(buf, 0, buf.Length, SocketFlags.None, ar => { try { sock.EndSend(ar); } catch { } }, null);
                Console.WriteLine("sent [" + s + "]");
            } catch { }
        }

        public void Abort()
        {
            try { _rdr.Close(); } catch { }
            try { _rdr.Dispose(); } catch { }
            _rdr = null;
            lock (__all) {
                __all.Remove(this);
            }
        }
        static public void AbortAll() {
            lock (__all) {
                while (__all.Count > 0) 
                    __all[0].Abort();
            }
        }
    }

    public class Server {
        public readonly static IPNP.Device VooServerDeviceType = new IPNP.Device("DB8DD6FA-398A-4548-8CD1-9231D6DC4EE2");
        public readonly static ushort VERSION = 1;
        public readonly static IPNP.Device MyDeviceID = new IPNP.Device(Guid.NewGuid());
        IPNP.Broadcaster _ipnpdev;

        string _share;
        Player _player;
        CommsProcessor _comms;

        string _state = "stopped";
        string _seekable = "notseekable";
        string _subtitle = "subtitle -1";
        string _subtitlecount = "subtitlecount 0";
        string _time = "time 0";
        string _length = "length 0";

        object _lock = new object();


        public Server() {
            IPNP.IO.AddDeviceID(MyDeviceID);
            IPNP.IO.AddDeviceID(VooServerDeviceType);
            IPNP.DiscoveryItem di = new IPNP.DiscoveryItem(MyDeviceID, VooServerDeviceType, VERSION);
            IPNP.QueryResponseItem qri = new IPNP.QueryResponseItem();
            qri.SetResponse("tcp_port", "4356");
            _ipnpdev = new IPNP.Broadcaster(MyDeviceID, new IPNP.IItem[] { di, qri });
            _ipnpdev.Start();

            try {
                SerialPort sp = new SerialPort("/dev/tty.usbserial-000013FDB", 115200, Parity.None, 8, StopBits.One);
                sp.Open();
                _comms = new CommsProcessor(new CommsInterface(sp));

                _comms.SourceSelectionReceived += delegate(int src) { Client.Broadcast("*comms source " + src); };
                _comms.VolumeReceived += delegate(int vol) { Client.Broadcast("*comms volume " + vol); };
                _comms.OffReceived += delegate() { Client.Broadcast("*comms off"); };
            } catch { }

            _share = NSUserDefaults.StandardUserDefaults.StringForKey("sharepath");
            if (_share == null) {
                _share = "/";
                NSUserDefaults.StandardUserDefaults.SetString(_share, "sharepath");
                NSUserDefaults.StandardUserDefaults.Synchronize();
            }

            TcpListener tcpListener = new TcpListener(IPAddress.Any, 4356);
            (new Thread(delegate() {
                        tcpListener.Start();
                        while (true) {
                            try {
                                Client c = new Client(this, tcpListener.AcceptTcpClient(), _share);
                                lock (_lock) {
                                    c.Send("*" + _state);
                                    c.Send("*" + _seekable);
                                    c.Send("*" + _subtitle);
                                    c.Send("*" + _subtitlecount);
                                    c.Send("*" + _time);
                                    if (_comms != null) {
                                        c.Send("*comms volume " + _comms.SystemVolume);
                                        if (_comms.IsOff)
                                            c.Send("*comms off");
                                        else
                                            c.Send("*comms source " + _comms.SystemSourceNum);
                                    }
                                }
                            } catch (Exception e) {
                                Console.WriteLine("error creating client: " + e.ToString());
                            }
                        }
                        }) { IsBackground = true }).Start();
        }

        public void Play(string path) {
            lock (_lock) {
                if (_player != null) {
                    _player.Dispose();
                    _player = null;
                }
                _player = new Player(path);
                _player.TimeChanged += s => {
                    if (_time != s) {
                        _time = s;
                        Client.Broadcast("*" + _time);
                    }
                };
                _player.StateChanged += s => {
                    if (_state != s) {
                        _state = s;
                        Client.Broadcast("*" + _state);
                    }
                };
                _player.LengthChanged += s => {
                    if (_length != s) {
                        _length = s;
                        Client.Broadcast("*" + _length);
                    }
                };
                _player.SeekableChanged += s => {
                    if (_seekable != s) {
                        _seekable = s;
                        Client.Broadcast("*" + _seekable);
                    }
                };
                _player.SubtitleChanged += s => {
                    if (_subtitle != s) {
                        _subtitle = s;
                        Client.Broadcast("*" + _subtitle);
                    }
                };
                _player.SubtitleCountChanged += s => {
                    if (_subtitlecount != s) {
                        _subtitlecount = s;
                        Client.Broadcast("*" + _subtitlecount);
                    }
                };
            }
        }
        public void Stop() {
            lock (_lock) {
                if (_player != null) {
                    _player.Dispose();
                    _player = null;
                }
            }
        }
        public void TogglePause() {
            lock(_lock) {
                if (_player == null) return;
                _player.TogglePause();
            }
        }
        public void Seek(ulong ms) {
            lock(_lock) {
                if (_player == null) return;
                _player.Seek(ms);
            }
        }
        public void Subtitle(int which) {
            lock(_lock) {
                if (_player == null) return;
                _player.Subtitle(which);
            }
        }
        public void NextFrame() {
            lock(_lock) {
                if (_player == null) return;
                _player.NextFrame();
            }
        }
        public void Comms(string s) {
            lock(_lock) {
                if (_comms != null) {
                    string[] parts = s.Split(new char[]{' '}, 2);
                    switch (parts[0]) {
                        case "off":
                            _comms.SendSystemOff();
                            break;
                        case "source":
                            _comms.SystemSourceNum = Convert.ToInt32(parts[1]);
                            break;
                        case "volume":
                            if (parts[1] == "up")
                                _comms.SystemVolume = _comms.SystemVolume+1;
                            else if (parts[1] == "down")
                                _comms.SystemVolume = _comms.SystemVolume-1;
                            else
                                _comms.SystemVolume = Convert.ToInt32(parts[1]);
                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }

    public class Player {
        TcpClient _client;
        StreamReader _rdr;

        public Player(string path)
        {
            Kill();


            using (NSAutoreleasePool pool = new NSAutoreleasePool())
            {
                string fn = NSBundle.MainBundle.BundlePath + "/Contents/Resources/vooplayer.app";
                NSWorkspace.SharedWorkspace.LaunchApplication(fn);
            }


            int i = 10;
            while (--i != 0) {
                try {
                    _client = new TcpClient("localhost", 4357);
                    break;
                } catch {
                    Thread.Sleep(1000);
                }
            }
            if (i == 0) {
                Console.WriteLine("failed to connect to child process");
                Thread.Sleep(10000);
                Dispose();
                return;
            }

            _rdr = new StreamReader(_client.GetStream(), Encoding.UTF8);
            (new Thread(ev_read) { IsBackground = true }).Start();

            Send(":load " + path);
            Console.WriteLine("foo19");
        }

        public void Dispose() {
            OnState("stopped");

            Kill();

            try { _rdr.Close(); } catch { }
            try { _rdr.Dispose(); } catch { }
            _rdr = null;
            try { _client.Close(); } catch { }
            try { ((IDisposable)_client).Dispose(); } catch { }
            _client = null;
        }
        public void TogglePause() {
            Send(":togglepause");
        }
        public void Seek(ulong ms) {
            Send(":seek " + ms);
        }
        public void Subtitle(int which) {
            Send(":subtitle " + which);
        }
        public void NextFrame() {
            Send(":nextframe");
        }

        void Kill() {
            using (Process p = new Process()) {
                p.StartInfo.FileName = "killall";
                p.StartInfo.Arguments = "-9 vooplayer";
                p.Start();
                p.WaitForExit();
            }
        }

        public void Send(string s) {
            try {
                Socket sock = _client.Client;
                byte[] buf = Encoding.UTF8.GetBytes(s + "\n");
                sock.BeginSend(buf, 0, buf.Length, SocketFlags.None, ar => { try { sock.EndSend(ar); } catch { }}, null);
                Console.WriteLine("playerSENT [" + s + "]");
            } catch { }
        }

        void ev_read()
        {
            try {
                string s;
                while ((s = _rdr.ReadLine()) != null)
                    ev_parsecmd(s);
            } catch {
            } finally {
                Dispose();
            }
        }

        void ev_parsecmd(string fromwire)
        {
            try {
                string[] parts = fromwire.Split(new char[]{' '}, 2);
                if (parts[0] == ":nop") return;

                Console.WriteLine("playerGOT [" + fromwire + "]");

                switch (parts[0]) {
                    case "*time":
                        OnTime("time " + parts[1]);
                        break;
                    case "*playing":
                        OnState("playing");
                        break;
                    case "*paused":
                        OnState("paused");
                        break;
                    case "*length":
                        OnLength("length " + parts[1]);
                        break;
                    case "*seekable":
                        OnSeekable("seekable");
                        break;
                    case "*notseekable":
                        OnSeekable("notseekable");
                        break;
                    case "*subtitle":
                        OnSubtitle("subtitle " + parts[1]);
                        break;
                    case "*subtitlecount":
                        OnSubtitleCount("subtitlecount " + parts[1]);
                        break;
                }

            } catch (Exception e) {
                Console.WriteLine("player connection blew up: " + e.ToString());
                Dispose();
            }
        }

        public event Action<string> TimeChanged;
        void OnTime(string s) { if (TimeChanged != null) TimeChanged(s); }
        public event Action<string> StateChanged;
        void OnState(string s) { if (StateChanged != null) StateChanged(s); }
        public event Action<string> LengthChanged;
        void OnLength(string s) { if (LengthChanged != null) LengthChanged(s); }
        public event Action<string> SeekableChanged;
        void OnSeekable(string s) { if (SeekableChanged != null) SeekableChanged(s); }
        public event Action<string> SubtitleChanged;
        void OnSubtitle(string s) { if (SubtitleChanged != null) SubtitleChanged(s); }
        public event Action<string> SubtitleCountChanged;
        void OnSubtitleCount(string s) { if (SubtitleCountChanged != null) SubtitleCountChanged(s); }
    }


}
