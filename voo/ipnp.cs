using System;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;

namespace Voo.IPNP
{
    public interface IItem {
        UInt16 ItemType { get; }
	void Write(BinaryWriter w);
    }

    public struct Device : IComparable<Device> {
        Guid _id;
        public Device(string id) { if (id == "" || id == null)
            {
                _id = Guid.Empty;
                return;
            }
            if (id.Length != 36) {
                _id = Utils.SerialToGuid(id);
                return;
            }
            _id = new Guid(id);
        }
        public Device(Guid id) { _id = id; }
        public Guid ToGuid() { return _id; }
        public byte[] ToByteArray() { return _id.ToByteArray(); }
        public override string ToString() { return _id.ToString(); }
        public static readonly Device Any = new Device(Guid.Empty);
        public bool Equals(Device o) { return _id.Equals(o._id); }
        public override bool Equals(Object o) { if (o == null || !(o is Device)) return false; return this.Equals((Device)o); }
        public override int GetHashCode() { return _id.GetHashCode(); }
        public static bool operator ==(Device a, Device b) { return a.Equals(b); }
        public static bool operator !=(Device a, Device b) { return !(a == b); }
        public int CompareTo(Device o) {
            return this._id.CompareTo(o._id); }
    }

    public struct Serial : IComparable<Serial> {
        Guid _id;
        public Serial(string id) { if (id == "" || id == null) _id = Guid.Empty; else _id = Utils.SerialToGuid(id); }
        public Serial(Guid id) { _id = id; }
        public Guid ToGuid() { return _id; }
        public byte[] ToByteArray() { return _id.ToByteArray(); }
        public override string ToString() { return Utils.GuidToSerial(_id); }
        public static readonly Serial Any = new Serial(Guid.Empty);
        public bool Equals(Serial o) { return _id.Equals(o._id); }
        public override bool Equals(Object o) { if (o == null || !(o is Serial)) return false; return this.Equals((Serial)o); }
        public override int GetHashCode() { return _id.GetHashCode(); }
        public static bool operator ==(Serial a, Serial b) { return a.Equals(b); }
        public static bool operator !=(Serial a, Serial b) { return !(a == b); }
        public int CompareTo(Serial o) { return this._id.CompareTo(o._id); }
    }

    public class Items : IEnumerable<IItem>, IEnumerable {
        List<IItem> _list = new List<IItem>();

        public Items() { }
        public Items(IList<IItem> list) {
            foreach (IItem i in list)
                _list.Add(i);
        }
        public Items(params IItem[] list) {
            foreach (IItem i in list)
                _list.Add(i);
        }

        IEnumerator<IItem> IEnumerable<IItem>.GetEnumerator() {
            foreach (IItem i in _list)
                yield return i;
        }
        IEnumerator IEnumerable.GetEnumerator() {
            foreach (IItem i in _list)
                yield return i;
        }

        public int Count { get { return _list.Count; } }
        public void Add(IItem item) { _list.Add(item); }

        public DiscoveryItem GetDiscoveryItem() {
            foreach (IItem i in _list)
                if (i is DiscoveryItem)
                    return (DiscoveryItem)i;
            return null;
        }
        public QueryResponseItem GetQueryResponseItem() {
            foreach (IItem i in _list)
                if (i is QueryResponseItem)
                    return (QueryResponseItem)i;
            return null;
        }
        public CommandResponseItem GetCommandResponseItem() {
            foreach (IItem i in _list)
                if (i is CommandResponseItem)
                    return (CommandResponseItem)i;
            return null;
        }
    }

    // -------------------------------------------------------------

    public class DiscoveryItem : IItem {
        public DiscoveryItem(Device device_id, Device device_type, UInt16 version) {
            _device_id = device_id;
            _device_type = device_type;
            _version = version;
        }
        public Device DeviceID { get { return _device_id; } }
        public Device DeviceType { get { return _device_type; } }
        public UInt16 Version { get { return _version; } }

        // -------------------------------------------------------
        public UInt16 ItemType { get { return 1; } }
        public void Write(BinaryWriter w) {
            w.Write(_version);
            w.Write(_device_id.ToByteArray());
            w.Write(_device_type.ToByteArray());
        }
        internal DiscoveryItem(BinaryReader r) {
            _version = r.ReadUInt16();
            _device_id = new Device(new Guid(r.ReadBytes(16)));
            _device_type = new Device(new Guid(r.ReadBytes(16)));
        }
        Device _device_id;
        Device _device_type;
        UInt16 _version;
    }

    public class QueryItem : IItem {
        public QueryItem(IList<string> names) {
            _names = names;
        }
        public IEnumerable<string> Names { get { return _names; } }

        // -------------------------------------------------------
        public UInt16 ItemType { get { return 2; } }
        public void Write(BinaryWriter w) {
            w.Write((UInt16)_names.Count);
            foreach (string s in _names) {
                byte[] b = System.Text.Encoding.UTF8.GetBytes(s);
                w.Write((byte)b.Length);
                w.Write(b);
            }
        }
        internal QueryItem(BinaryReader r) {
            _names = new List<string>();
            int count = r.ReadUInt16();
            while (count-- != 0) {
                int slen = r.ReadByte();
                _names.Add(System.Text.Encoding.UTF8.GetString(r.ReadBytes(slen), 0, slen));
            }
        }
        IList<string> _names;
    }

    public class QueryResponseItem : IItem {
        public QueryResponseItem() { }
        public void SetResponse(string n, string v) { _responses[n] = v; }
        public void UnsetResponse(string n) { if (_responses.ContainsKey(n)) _responses.Remove(n); }
        public void ClearResponses() { _responses.Clear(); }
        public string GetResponse(string n) { string val = null; _responses.TryGetValue(n, out val); return val; }

        // -------------------------------------------------------
        public UInt16 ItemType { get { return 3; } }
        public void Write(BinaryWriter w) {
            w.Write((UInt16)_responses.Count);
            foreach (KeyValuePair<string, string> response in _responses) {
                byte[] b = System.Text.Encoding.UTF8.GetBytes(response.Key);
                w.Write((byte)b.Length);
                w.Write(b);

                b = System.Text.Encoding.UTF8.GetBytes(response.Value);
                w.Write((UInt16)b.Length);
                w.Write(b);
            }
        }
        internal QueryResponseItem(BinaryReader r) {
            int count = r.ReadUInt16();
            while (count-- != 0) {
                int slen = r.ReadByte();
                string key = System.Text.Encoding.UTF8.GetString(r.ReadBytes(slen), 0, slen);
                slen = r.ReadUInt16();
                string val = System.Text.Encoding.UTF8.GetString(r.ReadBytes(slen), 0, slen);
                _responses[key] = val;
            }
        }
        Dictionary<string, string> _responses = new Dictionary<string, string>();
        public IEnumerable<KeyValuePair<string, string>> Responses
        {
            get
            {
                foreach (KeyValuePair<string, string> param in _responses)
                    yield return param;
            }
        }
    }

    public class CommandItem : IItem {
        public CommandItem(string command) {
            _command = command;
        }
        public string Command { get { return _command; } }

        public void SetParam(string n, byte[] v) { _params[n] = v; }
        public void SetParam(string n, byte[] v, int sz) { byte[] b = new byte[sz]; Array.Copy(v, 0, b, 0, sz); SetParam(n, b); }
        public void SetParam(string n, string v) { _params[n] = System.Text.Encoding.UTF8.GetBytes(v); }
        public void UnsetParam(string n) { if (_params.ContainsKey(n)) _params.Remove(n); }
        public void ClearParams() { _params.Clear(); }
        public string GetParam(string n) { byte[] val = null; _params.TryGetValue(n, out val); return System.Text.Encoding.UTF8.GetString(val, 0, val.Length); }
        public byte[] GetParamByteArray(string n) { byte[] val = null; _params.TryGetValue(n, out val); return val; }

        public IEnumerable<KeyValuePair<string, byte[]>> Params
        {
            get
            {
                foreach (KeyValuePair<string, byte[]> param in _params)
                    yield return param;
            }
        }

        // -------------------------------------------------------
        public UInt16 ItemType { get { return 4; } }
        public void Write(BinaryWriter w) {
            byte[] b = System.Text.Encoding.UTF8.GetBytes(_command);
            w.Write((byte)b.Length);
            w.Write(b);
            w.Write((UInt16)_params.Count);
            foreach (KeyValuePair<string, byte[]> param in _params) {
                b = System.Text.Encoding.UTF8.GetBytes(param.Key);
                w.Write((byte)b.Length);
                w.Write(b);
                b = param.Value;
                w.Write((UInt16)b.Length);
                w.Write(b);
            }
        }
        internal CommandItem(BinaryReader r) {
            int slen = r.ReadByte();
            _command = System.Text.Encoding.UTF8.GetString(r.ReadBytes(slen), 0, slen);
            int count = r.ReadUInt16();
            while (count-- != 0) {
                slen = r.ReadByte();
                string key = System.Text.Encoding.UTF8.GetString(r.ReadBytes(slen), 0, slen);
                slen = r.ReadUInt16();
                byte[] val = r.ReadBytes(slen);
                _params[key] = val;
            }
        }
        string _command;
        Dictionary<string, byte[]> _params = new Dictionary<string, byte[]>();
    }

    public class CommandResponseItem : IItem {
        public CommandResponseItem() { }
        public void SetResponse(string n, string v) { _responses[n] = v; }
        public void UnsetResponse(string n) { if (_responses.ContainsKey(n)) _responses.Remove(n); }
        public void ClearResponses() { _responses.Clear(); }
        public string GetResponse(string n) { string val = null; _responses.TryGetValue(n, out val); return val; }

        // -------------------------------------------------------
        public UInt16 ItemType { get { return 5; } }
        public void Write(BinaryWriter w) {
            w.Write((UInt16)_responses.Count);
            foreach (KeyValuePair<string, string> response in _responses) {
                byte[] b = System.Text.Encoding.UTF8.GetBytes(response.Key);
                w.Write((byte)b.Length);
                w.Write(b);
                b = System.Text.Encoding.UTF8.GetBytes(response.Value);
                w.Write((UInt16)b.Length);
                w.Write(b);
            }
        }
        public IEnumerable<KeyValuePair<string, string>> Responses
        {
            get
            {
                foreach (KeyValuePair<string, string> param in _responses)
                    yield return param;
            }
        }
        internal CommandResponseItem(BinaryReader r) {
            int count = r.ReadUInt16();
            while (count-- != 0) {
                int slen = r.ReadByte();
                string key = System.Text.Encoding.UTF8.GetString(r.ReadBytes(slen), 0, slen);
                slen = r.ReadUInt16();
                string val = System.Text.Encoding.UTF8.GetString(r.ReadBytes(slen), 0, slen);
                _responses[key] = val;
            }
        }
        Dictionary<string, string> _responses = new Dictionary<string, string>();
    }

    // -------------------------------------------------------------

    public static class IO {
        static bool _inited_once = false;

        static string IPNP_IP = "239.255.255.249";
//        static string IPNP_IP2 = "239.0.0.248"; // not used anymore.. was for stupid WINCE bug

        // Manipulate this processes device IDs here.
        //
        // These are the device ids that if pkts are not broadcast, they are
        // picked up here. You may use instance specific device ids here or
        // generic type device ids.
        public static void AddDeviceID(Device id)
        {
            lock (_devids) {
                int refcnt;
                _devids[id] = _devids.TryGetValue(id, out refcnt) ? refcnt + 1 : 1;
            }
        }
        public static void RemoveDeviceID(Device id)
        {
            lock (_devids) {
                int refcnt;
                if (_devids.TryGetValue(id, out refcnt)) {
                    if (refcnt == 1)
                        _devids.Remove(id);
                    else
                        _devids[id] = refcnt - 1;
                } else {
                    throw new InvalidOperationException();
                }
            }
        }

        // handlers for incoming messages. Broadcasts don't require responses,
        // and messages may require a response. You can send a response using Respond()
        public delegate void IncomingMessageHandler(Guid transaction_id,
                                                    IPAddress src_addr, Serial src_serial, Device src_devid,
                                                    Device dest_devid,
                                                    Items items);
        public delegate void IncomingBroadcastHandler(IPAddress src_addr, Serial src_serial, Device src_devid,
                                                      Items items);

        static public event IncomingMessageHandler IncomingMessage;
        static public event IncomingBroadcastHandler IncomingBroadcast;

        static public void Respond(Guid transaction_id, Device src_devid,
                                   Serial dest_serial, Device dest_devid,
                                   Items items)
        {
            if (transaction_id == Guid.Empty)
                return;
            byte[] buf = IO.Build(transaction_id,
                                  new IPNP.Serial("vooclient"), dest_serial,
                                  src_devid, dest_devid,
                                  items, IO.HeaderFlags.ACK);
            response r = new response();
            r.buf = buf;
            r.transaction_id = transaction_id;
            AddResponse(r);
            SendNoAck(buf);
        }

        static public void Init() {
            InitSocket();
        }

#if MONOTOUCH
#elif MONO
#else
        static void ev_AddressChangedCallback(object sender, EventArgs ea) {
//            Console.WriteLine("address changed!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            SocketManager.Instance.Post(delegate {
                FixInConn();
                Shutdown();
                re_init();
            }, null);
        }
#endif

        // =======================================================================

        class response {
            internal Guid transaction_id;
            internal byte[] buf;
        }
        class outgoing {
            internal Guid transaction_id;
            internal ResponseHandler cb;
            internal byte[] buf;
            internal SocketManager.TimerHandle timer;
            internal int retries;
        }

        class InConn {
            public Socket socket { set; get; }
            public IPAddress ipaddress { set; get; }
        }

        static Dictionary<IPAddress, InConn> _in_conns = new Dictionary<IPAddress, InConn>();
        static Socket _out_sock = null;
//        static Socket _in_sock = null;
        static IPEndPoint _out_ipep;

        static Dictionary<Device, int> _devids = new Dictionary<Device, int>();
        static Queue<outgoing> _pkts = new Queue<outgoing>();
        static Queue<response> _responses = new Queue<response>();

        internal delegate void ResponseHandler(Serial src_serial, Device src_devid, Items items);


        static internal byte[] Build(Guid transaction_id,
                                     Serial src_serial, Serial dest_serial,
                                     Device src_devid, Device dest_devid,
                                     Items items, HeaderFlags flags)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter b = new BinaryWriter(ms, Encoding.UTF8);

            b.Write(new char[]{ 'I', 'P', 'N', 'P' });

            b.Write((byte)1);
            b.Write((byte)flags);
            if (items != null)
                b.Write((UInt16)items.Count);
            else
                b.Write((UInt16)0);

            b.Write(transaction_id.ToByteArray());
            b.Write(src_serial.ToByteArray());
            b.Write(dest_serial.ToByteArray());
            b.Write(src_devid.ToByteArray());
            b.Write(dest_devid.ToByteArray());

            if (items != null) {
                foreach (IItem item in items) {
                    using (MemoryStream tmpms = new MemoryStream()) {
                        using (BinaryWriter tmpw = new BinaryWriter(tmpms)) {
                            item.Write(tmpw);
                            tmpw.Flush();

                            b.Write((UInt16)(tmpms.Length + 4));
                            b.Write((UInt16)item.ItemType);
                            b.Write(tmpms.ToArray());
                        }
                    }
                }
            }

            b.Flush();
            byte[] bytes = ms.ToArray();
            b.Close();
            return bytes;
        }

        static internal void SendNoAck(byte[] buf) {
            try {
                InitSocket();
                _WriteAsync(buf);
            } catch { }
        }
        static internal void SendWithAck(Guid transaction_id, byte[] buf, ResponseHandler cb) {
            InitSocket();

            outgoing o = new outgoing();
            o.retries = 50;
            o.buf = buf;
            o.cb = cb;
            o.transaction_id = transaction_id;
            o.timer = SocketManager.Instance.AddTimer(200, delegate { ev_retry(o); } );
            AddOutgoing(o);
            _WriteAsync(buf);
        }

        static response GetResponse(Guid transaction_id)
        {
            foreach (response o in _responses)
                if (o.transaction_id == transaction_id)
                    return o;
            return null;
        }
        static outgoing GetOutgoing(Guid transaction_id)
        {
            foreach (outgoing o in _pkts)
                if (o.transaction_id == transaction_id)
                    return o;
            return null;
        }

        static void AddResponse(response r)
        {
            _responses.Enqueue(r);
            if (_responses.Count > 100)
                _responses.Dequeue();
        }
        static void AddOutgoing(outgoing o)
        {
            _pkts.Enqueue(o);
            if (_pkts.Count > 100)
                _pkts.Dequeue();
        }

        static void ev_retry(outgoing o)
        {
            if (o.retries-- != 0) {
                try { InitSocket(); } catch { }
                if (_out_sock != null)
                    _WriteAsync(o.buf);
                o.timer = SocketManager.Instance.AddTimer(200, delegate { ev_retry(o); } );
                return;
            }
            // xxx timed out with no ack or response
        }

        static void _WriteAsync(byte[] buf) {
            try {
                _out_sock.BeginSendTo(buf, 0, buf.Length, SocketFlags.None, _out_ipep,
                                      ar => {
                                          try {
                                              _out_sock.EndSendTo(ar);
                                          } catch {
                                                SocketManager.Instance.Post(delegate {
                                                                                try { _out_sock.Close(); } catch { }
                                                                                try { _out_sock = null; } catch { }
                                                                                try { InitSocket(); } catch { }
                                                                            }, null);
                                          }
                                      }, null);
            } catch (Exception e) {
                Debug.WriteLine("[ipnp] BeginSendTo() failed: " + e);
            }
        }

        static private void InitSocket()
        {
            if (!_inited_once) {
                _inited_once = true;
#if MONO
                _in_conns[IPAddress.Any] = new InConn() { ipaddress = IPAddress.Any };
#elif MONOTOUCH
                _in_conns[IPAddress.Any] = new InConn() { ipaddress = IPAddress.Any };
#else
                ThreadUtil.QueueUserWorkItem(delegate {
                                                NetworkChange.NetworkAddressChanged += ev_AddressChangedCallback;
                                                SocketManager.Instance.Post(delegate {
                                                                                FixInConn();
                                                                                InitSocket();
                                                                            }, null);
                                             });
#endif
            }

            foreach (InConn inconn in _in_conns.Values) {
                if (inconn.socket == null) {
                    try {
                        inconn.socket = new Socket(AddressFamily.InterNetwork,
                                                   SocketType.Dgram, ProtocolType.Udp);
                        IPEndPoint ipep = new IPEndPoint(IPAddress.Any, 9001);
                        inconn.socket.SetSocketOption(SocketOptionLevel.Socket,
                                                      SocketOptionName.ReuseAddress, 1);
                        inconn.socket.Bind(ipep);
                        inconn.socket.SetSocketOption(SocketOptionLevel.IP,
                                                      SocketOptionName.AddMembership,
                                                      new MulticastOption(IPAddress.Parse(IPNP_IP),
                                                                          inconn.ipaddress));
                        SocketManager.Instance.AddReader(inconn.socket,
                                                         delegate(Socket sock, IPEndPoint endpoint, byte[] bytes, int bsz) { ev_read(sock, endpoint, bytes, bsz, inconn); } );
                    } catch {
                        try { inconn.socket.Close(); } catch {}
                        inconn.socket = null;
                        throw;
                    }
                }
            }

            if (_out_sock != null && !_out_sock.Connected) {
                try { SocketManager.Instance.Shutdown(_out_sock); } catch { }
                _out_sock = null;
            }

            if (_out_sock == null) {
                _out_sock = new Socket(AddressFamily.InterNetwork,
                                       SocketType.Dgram, ProtocolType.Udp);
                IPAddress ip = IPAddress.Parse(IPNP_IP);
                _out_ipep = new IPEndPoint(ip, 9001);
                _out_sock.SetSocketOption(SocketOptionLevel.IP,
                                          SocketOptionName.MulticastTimeToLive, 2);
            }
        }

#if MONOTOUCH
#elif MONO
#else
        static void FixInConn() {
            List<IPAddress> ips = new List<IPAddress>();
            foreach (string sip in IPAddresses.GetAll())
                ips.Add(IPAddress.Parse(sip));
            foreach (IPAddress ip in ips)
                if (!_in_conns.ContainsKey(ip))
                    _in_conns[ip] = new InConn() { ipaddress = ip };
            List<IPAddress> ips2 = new List<IPAddress>(_in_conns.Keys);
            foreach (IPAddress ip in ips2)
                if (!ips.Contains(ip)) {
                    try { _in_conns[ip].socket.Close(); } catch {}
                    try { _in_conns[ip].socket = null; } catch {}
                    _in_conns.Remove(ip);
                }
        }
#endif

        public static void Shutdown() {
            try { if (_out_sock != null) SocketManager.Instance.Shutdown(_out_sock); } catch { }
            _out_sock = null;
            foreach (InConn inconn in _in_conns.Values) {
                try { if (inconn.socket != null) SocketManager.Instance.Shutdown(inconn.socket); } catch { }
                inconn.socket = null;
            }
        }

        static void re_init() {
            SocketManager.Instance.AddTimer(200,
                                            delegate {
                                                try { InitSocket(); } catch { }
                                                re_init();
                                            });
        }

        static void ev_read(Socket sock, IPEndPoint endpoint, byte[] bytes, int bsz, InConn inconn)
        {
//            Console.WriteLine("GOT READ {0}", bsz);
            if (bsz == 0) {
                // close the socket and restart it up
                try { inconn.socket.Close(); } catch {}
                inconn.socket = null;
                re_init();
                return;
            }

            try {
                BinaryReader r = new BinaryReader(new MemoryStream(bytes, 0, bsz), Encoding.UTF8);

                char[] hdr = r.ReadChars(4);
                if (hdr[0] != 'I' || hdr[1] != 'P' || hdr[2] != 'N' || hdr[3] != 'P') return;
                byte ver = r.ReadByte(); if (ver != 1) return;
                HeaderFlags flags = (HeaderFlags)r.ReadByte();
                UInt16 itemcount = r.ReadUInt16();

                Guid transaction_id = new Guid(r.ReadBytes(16));
                Serial src_serial = new Serial(new Guid(r.ReadBytes(16)));
                Serial dest_serial = new Serial(new Guid(r.ReadBytes(16)));
                Device src_devid = new Device(new Guid(r.ReadBytes(16)));
                Device dest_devid = new Device(new Guid(r.ReadBytes(16)));

                // check if serial destination matches us
                if (dest_serial != Serial.Any && dest_serial != new IPNP.Serial("vooclient"))
                    return;

                // check if device ID destination matches us
                if (dest_devid != Device.Any) {
                    lock (_devids) {
                        if (!_devids.ContainsKey(dest_devid))
                            return;
                    }
                }

                if (transaction_id != Guid.Empty && ((flags & HeaderFlags.ACK) == 0))
                {
                    // if we already have sent response to this transaction, send it again here
                    response res = GetResponse(transaction_id);
                    if (res != null) {
                        SendNoAck(res.buf);
                        return;
                    }
                }

                Items items = new Items();
                while (itemcount-- != 0) {
                    int itemlen = r.ReadUInt16() - 4;
                    UInt16 itemtype = r.ReadUInt16();

                    using (MemoryStream svc_ms = new MemoryStream(r.ReadBytes(itemlen))) {
                        using (BinaryReader svc_r = new BinaryReader(svc_ms, Encoding.UTF8)) {

                            IItem item = null;
                            switch (itemtype)
                            {
                                case 1: item = new DiscoveryItem(svc_r); break;
                                case 2: item = new QueryItem(svc_r); break;
                                case 3: item = new QueryResponseItem(svc_r); break;
                                case 4: item = new CommandItem(svc_r); break;
                                case 5: item = new CommandResponseItem(svc_r); break;
                            }
                            if (item != null)
                                items.Add(item);
                        }
                    }
                }

                if ((flags & HeaderFlags.ACK) != 0) {
                    // we are getting an ack here, so see what the outgoing pkt
                    // that requested this ack, and if it exists, call its callback
                    // it may not exist because we got suprious ack or double ack

                    outgoing o = GetOutgoing(transaction_id);
                    if (o != null) {
                        SocketManager.Instance.Remove(o.timer);
                        o.cb(src_serial, src_devid, items);
                    }

                } else {
                    // no ack, so just let listeners know we got a msg

                    try {
                        if ((flags & HeaderFlags.BROADCAST) != 0) {
                            if (IncomingBroadcast != null)
                                IncomingBroadcast(endpoint.Address, src_serial, src_devid, items);
                        } else {
                            if (IncomingMessage != null)
                                IncomingMessage(transaction_id, endpoint.Address, src_serial, src_devid, dest_devid, items);
                        }
                    } catch (Exception e) { Debug.WriteLine(String.Format("error app-handling IPNP pkt: {0}", e.ToString())); }
                }
            } catch (Exception e) { Debug.WriteLine(String.Format("error handling IPNP pkt: {0}", e.ToString())); }
        }

        [Flags]
        internal enum HeaderFlags {
            NONE         = 0x0,
            ACK          = 0x1,
            NEEDS_ACK    = 0x2,
            BROADCAST    = 0x4
        };

        public static IItem ParseItemForDebug(ushort itemtype, BinaryReader svc_r)
        {
            switch (itemtype)
            {
                case 1: return new DiscoveryItem(svc_r);
                case 2: return new QueryItem(svc_r);
                case 3: return new QueryResponseItem(svc_r);
                case 4: return new CommandItem(svc_r);
                case 5: return new CommandResponseItem(svc_r);
                default: return null;
            }
        }
    }

    public class Broadcaster {
        Items _items;
        byte[] _buf;
        SocketManager.TimerHandle _timer = null;
        Device _src_devid;
        bool _stopped = false;

        public Broadcaster(Device src_devid, Items items)
        {
            _src_devid = src_devid;
            _items = items;
            Update();
        }
        public Broadcaster(Device src_devid, IList<IItem> items)
        {
            _src_devid = src_devid;
            _items = new Items();
            foreach (IItem i in items)
                _items.Add(i);
            Update();
        }
        public void Update() {
            _buf = IO.Build(Guid.Empty,
                            new IPNP.Serial("vooclient"), Serial.Any,
                            _src_devid, Device.Any,
                            _items, IO.HeaderFlags.BROADCAST);
        }

        public void ForceBroadcast()
        {
            IO.SendNoAck(_buf);
        }

	void ev_timer()
	{
            if (_stopped == true) return;
	    _timer = null;
            IO.SendNoAck(_buf);
	    _timer = SocketManager.Instance.AddTimer(2000, ev_timer);
	}

        public void Start() {
            _stopped = false;
            if (_timer == null)
                ev_timer();
        }
        public void Stop() {
            _stopped = true;
            if (_timer == null) return;
            SocketManager.Instance.Remove(_timer);
            _timer = null;
        }
    }

    public class Transaction {
        Items _items;
        byte[] _buf;
        ResponseHandler _cb;

        Serial _dest_serial;
        Device _dest_devid;
        Guid _transaction_id;
        Device _src_devid;

        public delegate void ResponseHandler(Serial src_serial, Device src_devid, Items items);

        public Transaction(Device srcdevid, Serial destserial, Device destdevid, Items items, ResponseHandler cb)
        {
            _src_devid = srcdevid;
            _cb = cb;
            _dest_serial = destserial;
            _dest_devid = destdevid;
            _items = items;
            if (_cb != null)
                _transaction_id = Guid.NewGuid();
        }

	public void Send()
	{
            _buf = IO.Build(_transaction_id,
                            new IPNP.Serial("vooclient"), _dest_serial,
                            _src_devid, _dest_devid,
                            _items,
                            (_cb != null ? IO.HeaderFlags.NEEDS_ACK : IO.HeaderFlags.NONE));
            if (_cb != null)
                IO.SendWithAck(_transaction_id, _buf, ev_ack);
            else
                IO.SendNoAck(_buf);
	}

        void ev_ack(Serial src_serial, Device src_devid, Items items) {
            if (_transaction_id == Guid.Empty)
                return;
            _transaction_id = Guid.Empty;
            if (_cb != null)
                _cb(src_serial, src_devid, items);
        }
    }

    public static class ConfigurationType
    {
        public readonly static Guid Empty                 = new Guid("9F2A0095-4F3F-4A07-9848-A64CA0D366CB");

        public readonly static Guid RMEAudioEndpoint      = new Guid("29654DA4-0387-447D-BBF4-4B0DE71126F9");
        public readonly static Guid SourceAudioEndpoint   = new Guid("4AAEB991-6C09-4FE7-B782-7E93DC78CCB9");
        public readonly static Guid MeridianAudioEndpoint = new Guid("76A5F984-A320-4838-8FD8-98550CDB8F1C");

        public readonly static Guid ControlTenGoer        = new Guid("3FEDA681-3E7E-4FAE-B1D9-D984A5015AE2");
        public readonly static Guid EnsembleGoer          = new Guid("1E46AFC7-8D5B-4DA8-9AED-66588C1B6A46");
        public readonly static Guid GenericGoer           = new Guid("C232CB4D-EBD8-42EB-B1F9-81E18F95580F");
        public readonly static Guid SourceGoer            = new Guid("57B634BA-B0FA-4CC6-8C9B-5D954FB7A33F");
        public readonly static Guid StoreGoer             = new Guid("C6C6159A-CD56-4A6F-8768-4A42F652D104");

        public readonly static Guid ID40                  = new Guid("D0A5A067-D5A2-427D-8C40-B0A54C39BCA9");
        public readonly static Guid Solo                  = new Guid("05CFDDAC-420D-4AD0-BA07-5D52FE74D35F");

        public readonly static Guid ControlPCGoer         = new Guid("6F5A2288-0640-4D6C-BDE4-B116171F6B04");
        public readonly static Guid ControlMacGoer        = new Guid("C184012E-43FD-4DC8-880C-CC38D46BCE21");
    }

    public static class Devices {
        public readonly static Device Broker               = new Device("98F10667-64D4-424F-9EA8-E0B654673C3C");
        public readonly static Device SlideShowDevice      = new Device("7B80C6D3-0FC0-40D8-B303-00F93B90551A");
        public readonly static Device AudioDevice          = new Device("C0B772E4-0FC5-4F00-831D-8555E62E29D3");
        public readonly static Device AudioEndpoint        = new Device("CF785921-8A74-49D7-AC23-9AC9CF60041E");
        public readonly static Device VideoDevice          = new Device("4F7AD513-E96F-4D2B-9969-64E78A48EB9C");
        public readonly static Device PhotoDevice          = new Device("3175E333-E2BF-6D2B-9339-5757BC48EA32");
        public readonly static Device ControlClient        = new Device("85492B65-932C-4E42-BE67-527561FB9C0F");

        public readonly static Device Control              = new Device("7E5F105D-7F6A-42E8-8166-B53729C5D027");
        public readonly static Device Store                = new Device("FAF1D263-099F-48EB-A3E9-6A5BE46010C1");
        public readonly static Device Source               = new Device("9AB4DE53-4DDD-4539-B872-73A408CDE96D");

        public readonly static Device ID40                 = new Device("23AAA786-8E37-4C98-8099-EB086A54AF76");
        public readonly static Device ID40Bootloader       = new Device("F70C4D92-F589-41BC-9253-6594D34D7371");

        public readonly static Device Solo                 = new Device("8119461B-8E84-47A2-A8CF-A0CA600F237E");
        public readonly static Device SoloBootloader       = new Device("CE17F4A5-1DEA-4EF1-BC64-33B273D48A6B");

        public readonly static Device Application          = new Device("ED8F36FD-C5DE-4C42-884C-B00389211D43");
        public readonly static Device Goer                 = new Device("10102016-F277-4D84-9D20-1C03909EB8FC");

        public readonly static Device EnsembleStore        = new Device("56207A65-7097-451A-9AC3-EBFBB9337C63");
        public readonly static Device EnsembleAudioDevice  = new Device("68FE8386-1E63-4E8F-98A0-5751923FA005");

        public readonly static Device QAServer             = new Device("9370D4F6-72F7-4197-9A8C-AF72ACB4329A");

        public readonly static Device Worker               = new Device("CB0722A6-DD80-42AF-B7F6-40F5B97868AE");

        public readonly static Device ControlPCGoer        = new Device("7D6521A1-9BBA-4750-872A-2FF5F76BB658");
        public readonly static Device ControlMacGoer       = new Device("D11BF409-FAAD-43A9-99F0-93978706B32C");

        public readonly static Device MobileSyncServer     = new Device("C5953BC7-6535-4E59-9CED-95870AC8C33B");
        public readonly static Device StorageBackend       = new Device("01C86C2C-A07A-4397-AE2B-4015816CD4B2");
    }

    public static class Utils {
        public static void SendCommandNoAck(IPNP.Device src_devid,
                                            IPNP.Serial dest_serial, IPNP.Device dest_device,
                                            string command, string[] param_pairs)
        {
            CommandItem ci = new CommandItem(command);

            if (param_pairs != null) {
                if ((param_pairs.Length % 2) != 0)
                    throw new Exception("you must use name, value pairs of strings for the parameter param_pairs");

                int i = 0;
                while (i < param_pairs.Length) {
                    ci.SetParam(param_pairs[i], param_pairs[i+1]);
                    i += 2;
                }
            }
            Transaction t = new Transaction(src_devid, dest_serial, dest_device, new Items(new IItem[]{ci}), null);
            t.Send();
        }
        public static void SendCommandAck(IPNP.Device src_devid,
                                          IPNP.Serial dest_serial, IPNP.Device dest_device,
                                          string command, string[] param_pairs, Transaction.ResponseHandler cb)
        {
            CommandItem ci = new CommandItem(command);

            if (param_pairs != null) {
                if ((param_pairs.Length % 2) != 0)
                    throw new Exception("you must use name, value pairs of strings for the parameter param_pairs");

                int i = 0;
                while (i < param_pairs.Length) {
                    ci.SetParam(param_pairs[i], param_pairs[i+1]);
                    i += 2;
                }
            }
            Transaction t = new Transaction(src_devid, dest_serial, dest_device, new Items(new IItem[]{ci}), cb);
            t.Send();
        }

        public static void SendQuery(IPNP.Device src_devid,
                                     IPNP.Serial dest_serial, IPNP.Device dest_device,
                                     string[] names, Transaction.ResponseHandler cb)
        {
            QueryItem ci = new QueryItem(names);
            Transaction t = new Transaction(src_devid, dest_serial, dest_device, new Items(new IItem[]{ci}), cb);
            t.Send();
        }

        static internal string GuidToSerial(Guid guid)
        {
            byte[] b = guid.ToByteArray();
            if (b [6] == 0 && b [7] == 0 && b [8] == 0 && b [9] == 0 && b [10] == 0 && 
                b [11] == 0 && b [12] == 0 && b [13] == 0 && b [14] == 0 && b [15] == 0) {
                return b [0].ToString("x2") + b [1].ToString("x2") + b [2].ToString("x2") + 
                    b [3].ToString("x2") + b [4].ToString("x2") + b [5].ToString("x2");
            }
            else
            if (b [0] != 0 && b [1] != 0 && b [2] != 0 && b [3] != 0 && b [4] != 0 && b [5] != 0 && b [6] != 0 && b [7] != 0 && 
                       b [8] != 0 && b [9] != 0 && b [10] != 0 && b [11] != 0 && b [12] != 0 && b [13] != 0 && b [14] != 0 && b [15] != 0) {
                return guid.ToString().ToUpper();
            }
            else {
                string s = "";
                int i = 0;
                while (i < 16) {
                    if (b [i] == 0xff)
                        break;
                    s += (char)b [i];
                    i++;
                }
                return s;
            }
        }
        static internal Guid SerialToGuid(string serial)
        {
            if (serial.Length == 12) {
                return new Guid(new byte[]{
                                (byte)Int32.Parse(serial.Substring(0, 2), System.Globalization.NumberStyles.HexNumber), 
                                (byte)Int32.Parse(serial.Substring(2, 2), System.Globalization.NumberStyles.HexNumber), 
                                (byte)Int32.Parse(serial.Substring(4, 2), System.Globalization.NumberStyles.HexNumber), 
                                (byte)Int32.Parse(serial.Substring(6, 2), System.Globalization.NumberStyles.HexNumber), 
                                (byte)Int32.Parse(serial.Substring(8, 2), System.Globalization.NumberStyles.HexNumber), 
                                (byte)Int32.Parse(serial.Substring(10, 2), System.Globalization.NumberStyles.HexNumber), 
                                0, 0, 0, 0, 0, 
                                0, 0, 0, 0, 0
                                });
            }
            else
            if (serial.Length == 36) {
                return new Guid(serial);
            }
            else {
                byte[] b = new byte[16];

                int i = 0;
                foreach (char c in serial) {
                    if (i >= 16)
                        break;
                    b [i] = (byte)c;
                    i++;
                }
                while (i < 16)
                    b [i++] = 0xff;
                return new Guid(b);
            }
        }
    }
}
