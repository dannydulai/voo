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
        NetLineParser _nlp = new NetLineParser();
        IPAddress _ip = IPAddress.Any;
        Socket _sock;

        public Connection() {
            IPNP.IO.Init();
            IPNP.IO.IncomingBroadcast += ev_broadcast;
        }

        public void Send(string s)
        {
            if (_sock == null)
                return;
            Console.WriteLine("sent {0}", s);
            SocketManager.Instance.AddWriter(_sock, Encoding.ASCII.GetBytes(s + "\n"));
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

        public void Stop() { Send("stop"); }
        public void Backfast() { Send("backfast"); }
        public void Backslow() { Send("backslow"); }
        public void Fwdfast() { Send("fwdfast"); }
        public void Fwdslow() { Send("fwdslow"); }
        public void Poweroff() { Send("comms off"); }
        public void Louder() { Send("comms volume up"); }
        public void Vol(int vol) { Send("comms volume " + vol); }
        public void Softer() { Send("comms volume down"); }
        public void Subtitles() { Send("subtitle"); }
        public void List(string parent, string name, Action<string, string[]> cb) {
            string path;
            if (parent != null)
                path = parent + "\\" + name;
            else
                path = name;

            Send("list " + path,
                delegate (string line) {
                    cb(path, line.Split(new char[] { ' ' }, 2)[1].Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries));
                });
        }

        public void DeleteFile(string parent, string name) { Send("delfile " + parent + "\\" + name); }
        public void DeleteDir(string parent, string name) { Send("deldir " + parent + "\\" + name); }
        public void Play(string parent, string name) {
            Send("comms source 3"); 
            Send("load " + parent + "\\" + name);
        }
        public void PlayPause() {
            Send("comms source 3");
            Send("playpause");
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

            _sock = null;
            _ip = src_addr;
            SocketManager.Instance.AddConnecter(new IPEndPoint(_ip, 4356), ev_connected);
            if (this.Connecting != null) this.Connecting();
        }

        void ev_connected(Socket sock, bool success)
        {
            if (!success) {
                _sock = null;
                _ip = IPAddress.Any;
                if (this.FailedConnecting != null) this.FailedConnecting();
            } else {
                _sock = sock;
                SocketManager.Instance.AddReader(_sock, ev_read);
                if (this.SuccessConnecting != null) this.SuccessConnecting();
            }
        }

        void ev_read(Socket s, byte[] bytes, int cnt)
        {
//            Console.WriteLine("got {0} bytes", cnt);
            if (s != _sock) return;
            if (cnt == 0) {
                SocketManager.Instance.Shutdown(_sock);
                _sock = null;
                _ip = IPAddress.Any;

                if (this.Disconnected != null) this.Disconnected();
                return;
            }
            _nlp.Process(bytes, 0, bytes.Length, ev_line);
        }

        void ev_line(string s) {
//            Console.WriteLine("got line {0}", s);
            if (_recvq.Count != 0) { _recvq[0](s); _recvq.RemoveAt(0); }
        }
    }
}
