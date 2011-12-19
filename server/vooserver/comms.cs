using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO.Ports;

namespace vooserver
{
    public delegate void Callback<T>(T x);
    public enum CommsSource : int
    {
        SystemWide = 1,
        CdPlayer = 16,
        Radio = 17,
        DVDPlayer = 18,
        SooloosZone = 19
    }

    public class CommsInterface : IDisposable
    {
        object _cmdlock = new object();
        object _lock = new object();
        bool _isdisposed;
        SerialPort _port;
        Thread _readthread;
        Callback<string> _responsecb;
        int _productaddr;
        int _systemaddr;
        CommsSource _category = CommsSource.SooloosZone;
        string _version;
        string _id;

        public CommsInterface(SerialPort control)
        {
            _port = control;

            _readthread = new Thread(_ReadThread, 131072) { IsBackground = true };
            _readthread.Start();

            // per documentation, send two ID commands to flush tx buffers at startup
            SendCommand("ID", null);
            SendCommand("ID", null);

            if (!SendCommand("ID", s => _id = s))
                throw new Exception("Failed to get ID from usb audio card");
            if (_id == null) throw new Exception("No ID response received from usb audio card");
            if (!SendCommand("VE", s => _version = s))
                throw new Exception("Failed to get VE from usb audio card");
            if (_version == null) throw new Exception("No VE response received from usb audio card");
            Console.WriteLine("Initialized USBAudioInterface(ID=" + _id + " VE=" + _version + ")");
        }

        void _ReadThread()
        {
            while (true)
            {
                string line = _port.ReadLine();
                if (line == null)
                {
                    Console.WriteLine("Serial Port Read Failure");
                    break;
                }
                line = line.Trim();
                if (line == string.Empty) continue;
                Console.WriteLine("SERIAL READ [" + line + "]");
                if (line.StartsWith("MC") && !line.StartsWith("MCER"))
                {
                    byte[] buf = ConvertFromBase16(line.Substring(2).ToLower());
                    _DecodeComms(buf);
                }
                else if (line.StartsWith("U"))
                {
                }
                else if (line.StartsWith("IE") || line.StartsWith("ie") || line.StartsWith("EI"))
                {
                }
                else
                {
                    Callback<string> responsecb = _responsecb;
                    if (responsecb != null)
                        responsecb(line);
                }
            }
        }

        public static byte[] ConvertFromBase16(string s)
        {
            if (s.Length % 2 != 0) throw new FormatException();
            byte[] ret = new byte[s.Length / 2];
            for (int x = 0; x < s.Length; x += 2)
            {
                byte b;
                switch (s[x])
                {
                    case '0': b = 0x00; break;
                    case '1': b = 0x10; break;
                    case '2': b = 0x20; break;
                    case '3': b = 0x30; break;
                    case '4': b = 0x40; break;
                    case '5': b = 0x50; break;
                    case '6': b = 0x60; break;
                    case '7': b = 0x70; break;
                    case '8': b = 0x80; break;
                    case '9': b = 0x90; break;
                    case 'a': b = 0xa0; break;
                    case 'b': b = 0xb0; break;
                    case 'c': b = 0xc0; break;
                    case 'd': b = 0xd0; break;
                    case 'e': b = 0xe0; break;
                    case 'f': b = 0xf0; break;
                    default: throw new FormatException();
                }
                switch (s[x + 1])
                {
                    case '0': b |= 0; break;
                    case '1': b |= 1; break;
                    case '2': b |= 2; break;
                    case '3': b |= 3; break;
                    case '4': b |= 4; break;
                    case '5': b |= 5; break;
                    case '6': b |= 6; break;
                    case '7': b |= 7; break;
                    case '8': b |= 8; break;
                    case '9': b |= 9; break;
                    case 'a': b |= 0xa; break;
                    case 'b': b |= 0xb; break;
                    case 'c': b |= 0xc; break;
                    case 'd': b |= 0xd; break;
                    case 'e': b |= 0xe; break;
                    case 'f': b |= 0xf; break;
                    default: throw new FormatException();
                }
                ret[x / 2] = b;
            }
            return ret;
        }

        public static string ConvertToBase16(byte[] p)
        {
            StringBuilder sb = new StringBuilder();
            const string xxx = "0123456789abcdef";
            foreach (byte b in p)
            {
                int highorder = (b >> 4) & 0x0f;
                int loworder = b & 0x0f;
                sb.Append(xxx[highorder]);
                sb.Append(xxx[loworder]);
            }
            return sb.ToString();
        }

        public bool SendCommand(string command, Callback<string> responsecb)
        {
            command = command.Trim();
            bool success = false;
            lock (_cmdlock)
            {
                if (_isdisposed) throw new InvalidOperationException();
                try
                {
                    using (ManualResetEvent ev = new ManualResetEvent(false))
                    {
                        _responsecb = s =>
                        {
                            s = s.Trim();
                            Console.WriteLine("GOT RESPONSE TO [" + command + "] === [" + s + "]");
                            while ((int)s[0] == 0)
                            {
                                Console.WriteLine("Warning: Leading null char in response: " + s);
                                s = s.Substring(1);
                            }
                            if (s == "AK")
                            {
                                Console.WriteLine("CHECK");
                                success = true;
                                _responsecb = null;
                                ev.Set();
                                Console.WriteLine("SET EVENT");
                            }
                            else if (s == "ER")
                            {
                                Console.WriteLine("CHECK");
                                success = true;
                                _responsecb = null;
                                ev.Set();
                                Console.WriteLine("SET EVENT");
                            }
                            else
                            {
                                if (responsecb != null)
                                    responsecb(s);
                            }
                        };
                        Console.WriteLine("SERIAL WRITE [" + command + "]");
                        _port.WriteLine(command);
                        if (!ev.WaitOne(1000, false))
                            Console.WriteLine("SMI took more than 1s to respond to command: [" + command + "]");
                    }
                }
                finally
                {
                    _responsecb = null;
                }
            }
            return success;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_isdisposed) return;
                _isdisposed = true;
                _port.Dispose();
            }
        }

        ~CommsInterface() { Dispose(); }

        #region CommsInterface Members

        public string VersionString { get { return _version; } }
        public string IdString { get { return _id; } }

        public event CommsReceivedDelegate CommsReceived;

        public CommsSource Category { get { return _category; } }

        private void _DecodeComms(byte[] buf)
        {
            if (buf.Length == 0)
                return;
            int len = buf[0];
            if (len < 1 || len > 3)
                return;
            if (buf.Length != 3 + len)
                return;

            int srccat = (buf[1] >> 3) & 0x1f;
            int srcaddr = buf[1] & 0x7;
            int destcat = (buf[2] >> 3) & 0x1f;
            int destaddr = buf[2] & 0x7;

            byte cmd = len >= 1 ? buf[3] : (byte)0;
            byte data1 = len >= 2 ? buf[4] : (byte)0;
            byte data2 = len >= 3 ? buf[5] : (byte)0;

            if (CommsReceived != null)
                CommsReceived((CommsSource)srccat, srcaddr, (CommsSource)destcat, destaddr, cmd, data1, data2);
        }

        public int ProductAddr
        {
            get { return _productaddr; }
            set { _ThrowOnInvalidAddr(value); _productaddr = value; }
        }

        public int SystemAddr
        {
            get { return _systemaddr; }
            set { _ThrowOnInvalidAddr(value); _systemaddr = value; }
        }

        private void _ThrowOnInvalidAddr(int value)
        {
            if (value < 0 || value > 7)
                throw new ArgumentException("Invalid address: " + value);
        }

        public void SendComms(CommsSource dest, byte cmd)
        {
            byte[] buf = new byte[4];
            buf[0] = 1;         // length of command and data bytes
            buf[1] = (byte)((int)_category << 3 | _productaddr);
            buf[2] = (byte)((int)dest << 3 | _systemaddr);
            buf[3] = cmd;
            _SendComms(buf);
        }

        public void SendComms(CommsSource dest, byte cmd, byte data1)
        {
            byte[] buf = new byte[5];
            buf[0] = 2;         // length of command and data bytes
            buf[1] = (byte)((int)_category << 3 | _productaddr);
            buf[2] = (byte)((int)dest << 3 | _systemaddr);
            buf[3] = cmd;
            buf[4] = data1;
            _SendComms(buf);
        }

        public void SendComms(CommsSource dest, byte cmd, byte data1, byte data2)
        {
            byte[] buf = new byte[6];
            buf[0] = 3;         // length of command and data bytes
            buf[1] = (byte)((int)_category << 3 | _productaddr);
            buf[2] = (byte)((int)dest << 3 | _systemaddr);
            buf[3] = cmd;
            buf[4] = data1;
            buf[5] = data2;
            _SendComms(buf);
        }

        private void _SendComms(byte[] buf)
        {
            if (_port == null)
                return;
            //Console.WriteLine("SENDING COMMS BUFFER: \n" + Environment.StackTrace + "\n-----------------------------------");
            string cmdstring = "MC" + ConvertToBase16(buf).ToUpper() + "\r\n";
            if (!SendCommand(cmdstring, null))
                Console.WriteLine("Error Sending Comms command: " + cmdstring);
        }

        #endregion

        public delegate void CommsReceivedDelegate(CommsSource src, int sourceaddr, CommsSource dest, int destaddr, byte cmd, byte data1, byte data2);
    }

    class CommsProcessor : IDisposable 
    {
        CommsInterface _iface;
        int _systemsourcenum;
        int _systemvolume;

        public delegate void SystemSourceSelectionReceived(int sourcenum);
        public delegate void SystemVolumeReceived(int volume);
        public delegate void SystemOffRecieved();
        public delegate void DeviceKeyReceived(Key key);

        public event SystemSourceSelectionReceived SourceSelectionReceived;
        public event SystemVolumeReceived VolumeReceived;
        public event SystemOffRecieved OffReceived;
        public event DeviceKeyReceived KeyReceived;

        public int SystemVolume {
            get { return _systemvolume; }
            set {
                if (_systemvolume == value) return;
                SendSystemVolume(value);
            }
        }
        public bool IsOff {
            get { return _systemsourcenum == 16; }
        }
        public int SystemSourceNum {
            get { return _systemsourcenum; }
            set {
                if (_systemsourcenum == value) {
Console.WriteLine("ignoring src selection to {0}", value);
return;
}
                SendSystemSourceSelection(value);
            }
        }

        public CommsProcessor(CommsInterface iface)
        {
            _iface = iface;
            _iface.CommsReceived += _iface_Received;
        }
        
        void _iface_Received(CommsSource src, int sourceaddr, CommsSource dest, int destaddr, byte cmd, byte data1, byte data2)
        {
            if (dest != CommsSource.SystemWide && dest != _iface.Category)
                return;         // ignore comms messages destined for other types of devices
            if (destaddr != _iface.SystemAddr)
                return;         // ignore comms messages destined for other meridian systems

            if (dest == CommsSource.SystemWide)
            {
                if ((cmd >= 1 && cmd <= 11) || cmd == 17)
                {
                    if (SourceSelectionReceived != null)
                        SourceSelectionReceived(cmd);
                    _systemsourcenum = cmd;
                }
                else if (cmd == 16)
                {
                    if (OffReceived != null)
                        OffReceived();
                    _systemsourcenum = 16;
                }
                else if (cmd == 20)
                {
                    if (VolumeReceived != null && data1 >= 1 && data1 <= 99) {
                        _systemvolume = data1;
                        VolumeReceived(data1);
                    }
                }
            }
            else if (dest == _iface.Category)
            {
                if (destaddr == _iface.ProductAddr)
                {
                    switch (cmd)
                    {
                        case 10: _Key(Key.Next); break;
                        case 11: _Key(Key.Previous); break;
                        case 12: _Key(Key.Play); break;
                        case 13: _Key(Key.Stop); break;
                        case 14: _Key(Key.Pause); break;
                        case 18: _Key(Key.FastForward); break;
                        case 19: _Key(Key.Rewind); break;
                    }
                }
            }
        }

        public enum Key
        {
            Next,
            Previous,
            Play,
            Stop,
            Pause,
            FastForward,
            Rewind
        }

        private void _Key(Key key)
        {
            if (KeyReceived != null)
                KeyReceived(key);
        }

        void SendSystemVolume(int num)
        {
            if (num < 1 || num > 99)
                throw new ArgumentException("Invalid Volume: " + num);
            _systemvolume = num;
            _iface.SendComms(CommsSource.SystemWide, (byte)20, (byte)num);
        }
        
        public void SendSystemOff()
        {
            _iface.SendComms(CommsSource.SystemWide, (byte)16);
            _systemsourcenum = 16;
        }

        void SendSystemSourceSelection(int src)
        {
            _systemsourcenum = src;
            _iface.SendComms(CommsSource.SystemWide, (byte)src);
        }

        public void Dispose()
        {
            _iface.Dispose();
        }
    }
}
