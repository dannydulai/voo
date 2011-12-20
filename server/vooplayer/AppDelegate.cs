using System;
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

namespace vooplayer
{
    public partial class AppDelegate : NSApplicationDelegate
    {
        public AppDelegate () {
        }

        Server _server;

        public override void FinishedLaunching (NSObject notification)
        {
            _server = new Server(this);
        }
    }

    public class Client {
        static List<Client> __all = new List<Client>();

        Server _s;
        TcpClient _client;
        StreamReader _rdr;

        public Client(Server s, TcpClient client) {
            _s = s;
            _client = client;

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
                    case ":load": {
                        string file = makesafe(parts[1]);
                        _s.Play(file);
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
            if (file[0] != '/') { Abort(); return null; } 
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
                sock.BeginSend(buf, 0, buf.Length, SocketFlags.None, ar => { try { sock.EndSend(ar); } catch { }}, null);
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
        IntPtr vlc;
        IntPtr mp;
        NSTimer _timer;

        bool _playing;
        bool _seekable;
        int _subtitle;
        int _subtitlecount;
        ulong _time;
        ulong _length;
        object _lock = new object();
        NSObject _app;

        public Server(NSObject app) {
            _app = app;
            string[] args = new string[] {
//                "-vvvvv",
                "--no-xlib",
            };
            vlc = VLC.libvlc_new(args.Length, args);

            _timer = NSTimer.CreateRepeatingTimer(0.1, ev_timer);
            NSRunLoop.Current.AddTimer(_timer, NSRunLoop.NSRunLoopCommonModes);

            TcpListener tcpListener = new TcpListener(IPAddress.Any, 4357);
            (new Thread(delegate() {
                        tcpListener.Start();
                        while (true) {
                            try {
                                Client c = new Client(this, tcpListener.AcceptTcpClient());
                                lock (_lock) {
                                    if (mp != IntPtr.Zero) {
                                        c.Send(_playing ? "*playing" : "*paused");
                                        c.Send(_seekable ? "*seekable" : "*notseekable");
                                        c.Send("*subtitle " + _subtitle);
                                        c.Send("*subtitlecount " + _subtitlecount);
                                        c.Send("*time " + _time);
                                    }
                                }
                            } catch (Exception e) {
                                Console.WriteLine("error creating client: " + e.ToString());
                            }
                        }
                        }) { IsBackground = true }).Start();
        }

        bool firsttimer = true;
        void ev_timer() {
            lock (_lock) {
                if (mp == IntPtr.Zero)
                    return;

                bool playing = false;

                switch (VLC.libvlc_media_player_get_state(mp)) {
                    case VLC.State.NothingSpecial:
                        return;
                    case VLC.State.Opening:
                        return;
                    case VLC.State.Buffering:
                        return;
                    case VLC.State.Playing:
                        playing = true;
                        break;
                    case VLC.State.Paused:
                        playing = false;
                        break;
                    case VLC.State.Stopped:
                        Console.WriteLine("STOPPED");
                        NSApplication.SharedApplication.Terminate(_app);
                        return;
                    case VLC.State.Ended:
                        Client.Broadcast("*ended");
                        Thread.Sleep(500);
                        NSApplication.SharedApplication.Terminate(_app);
                        return;
                    case VLC.State.Error:
                        NSApplication.SharedApplication.Terminate(_app);
                        return;
                    default:
                        NSApplication.SharedApplication.Terminate(_app);
                        return;
                }

//                Console.WriteLine("timer");

                NSCursor.SetHiddenUntilMouseMoves(true);

                bool force = firsttimer;
                firsttimer = false;

                if (_playing != playing || force) {
                    _playing = playing;
                    Client.Broadcast(_playing ? "*playing" : "*paused");
                }
                bool seekable = VLC.libvlc_media_player_is_seekable(mp);
                if (_seekable != seekable || force) {
                    _seekable = seekable;
                    Client.Broadcast(_seekable ? "*seekable" : "*notseekable");
                }
                int subtitle = VLC.libvlc_video_get_spu(mp);
                if (_subtitle != subtitle || force) {
                    _subtitle = subtitle;
                    Client.Broadcast("*subtitle " + _subtitle);
                }
                int subtitlecount = VLC.libvlc_video_get_spu_count(mp);
                if (_subtitlecount != subtitlecount || force) {
                    _subtitlecount = subtitlecount;
                    Client.Broadcast("*subtitlecount " + _subtitlecount);
                }
                ulong time = VLC.libvlc_media_player_get_time(mp);
                if (_time != time || force) {
                    _time = time;
                    Client.Broadcast("*time " + _time);
                }
                ulong length = VLC.libvlc_media_player_get_length(mp);
                if (_length != length || force) {
                    _length = length;
                    Client.Broadcast("*length " + _length);
                }

//                Console.WriteLine("donetimer");
            }
        }

        public void Play(string path) {
            if (File.Exists(path)) {
                lock (_lock) {
                    IntPtr m = VLC.libvlc_media_new_path(vlc, path);
                    mp = VLC.libvlc_media_player_new_from_media(m);
                    VLC.libvlc_set_fullscreen(mp, true);
                    VLC.libvlc_media_player_play(mp);
                }
            } else {
                Stop();
            }
        }
        public void Stop() {
            mp = IntPtr.Zero;
            NSApplication.SharedApplication.Terminate(_app);
        }
        public void TogglePause() {
            lock(_lock) {
                if (mp == IntPtr.Zero) return;
                VLC.libvlc_media_player_pause(mp);
            }
        }
        public void Seek(ulong ms) {
            lock(_lock) {
                if (mp == IntPtr.Zero) return;
                VLC.libvlc_media_player_set_time(mp, ms);
            }
        }
        public void Subtitle(int which) {
            lock(_lock) {
                if (mp == IntPtr.Zero) return;
                VLC.libvlc_video_set_spu(mp, which);
            }
        }
        public void NextFrame() {
            lock(_lock) {
                if (mp == IntPtr.Zero) return;
                VLC.libvlc_media_player_next_frame(mp);
            }
        }
    }
}

