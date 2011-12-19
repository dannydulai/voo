//#define BAD_USER_TRACE

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Voo {
    public class SocketManager
    {
	public class TimerHandle { }
	static public SocketManager Instance { get { return Singleton.i; } }

	// in the following callbacks, return true to remove from the
	// socketmgr, false to leave the action in the socket mgr
	//
	// AddAccepter will leave listener IN on error. taking listener out
	// will also close it
        //
	// AddConnecter will take connector OUT always
        //
	// AddReader will take reader OUT on error
        //
	// AddWriter will take writer OUT on error or completion, but leave
	// IN on partial write
        //
        // AddTimer will take the timer OUT every time, so if you want
        // recurring timer, you must AddTimer every time
	//
	public delegate void AcceptCB(Socket s);
	public delegate void ConnectCB(Socket s, bool success);
	public delegate void ReadCB(Socket s, byte[] bytes, int cnt);
	public delegate void Read2CB(Socket s, IPEndPoint endpoint, byte[] bytes, int cnt);
	public delegate void WriteCB(Socket s);
	public delegate void TimerCB();

        List<a_state> _accepters = new List<a_state>();

	public void AddAccepter(IPEndPoint endpoint, AcceptCB cb)
	{
	    _accepters.Add(new a_state(endpoint, cb));
	}
	
        public void AddAccepter(IPEndPoint endpoint, AcceptCB cb, out Socket acceptsock)
	{
        a_state a = new a_state(endpoint, cb);
        _accepters.Add(a);
        acceptsock = a.Socket;
	}

        public void RemoveAccepter(IPEndPoint endpoint)
        {
            lock (this)
            {
                foreach (a_state a in _accepters)
                {
                    if (a.Endpoint.Port == endpoint.Port)
                    {
                        a.Cancel();
                        _accepters.Remove(a);
                        break;
                    }
                }
            }
        }

	public void AddConnecter(IPEndPoint endpoint, ConnectCB cb)
	{
	    lock (this) {
		_list_c.Add(new c_state(endpoint, cb));
	    }
	}

	public void AddReader(Socket s, ReadCB cb)
	{
	    if (s == null)
		throw new Exception("SocketManager.AddReader: can't read from null socket");
	    lock (this) {
		if (_readers.ContainsKey(s))
		    throw new Exception("SocketManager.AddReader: already reading  from this socket");
		_readers[s] = new r_state(s, cb);
	    }
	}

	public void AddReader(Socket s, Read2CB cb)
	{
	    if (s == null)
		throw new Exception("SocketManager.AddReader2: can't read from null socket");
	    lock (this) {
		if (_readers.ContainsKey(s))
		    throw new Exception("SocketManager.AddReader: already reading  from this socket");
		_readers[s] = new r_state(s, cb);
	    }
	}

	public void AddWriter(Socket s, byte[] bytes, WriteCB cb)
	{
	    AddWriter(s, bytes, 0, bytes.Length, cb);
	}

	public void AddWriter(Socket s, byte[] bytes)
	{
	    AddWriter(s, bytes, 0, bytes.Length, null);
	}

	public void AddWriter(Socket s, byte[] bytes, int off, int sz)
	{
	    AddWriter(s, bytes, off, sz, null);
	}

	public void AddWriter(Socket s, byte[] bytes, int off, int sz, WriteCB cb)
	{
	    if (s == null)
		throw new Exception("SocketManager.AddWriter: can't write to null socket");
	    lock (this) {
		if (_writers.ContainsKey(s)) {
		    _writers[s].AddData(bytes, off, sz, cb);
		} else {
		    _writers[s] = new w_state(s, bytes, off, sz, cb);
		}
	    }
	}

	public TimerHandle AddTimer(int ms, TimerCB cb)
	{
	    lock (this) {
		t_state t = new t_state(ms, cb);
		int i = 0;
		int max = _list_t.Count;
		while (i < max && _list_t[i].Time.CompareTo(t.Time) < 0)
		    i++;
		_list_t.Insert(i, t);
		_moredata.Set();
		return t;
	    }
	}

	public void Remove(TimerHandle timerobj)
	{
	    lock (this) {
		if (timerobj == null) return;
		if (_list_t.Count == 0) return;
		int idx = _list_t.BinarySearch((t_state)timerobj, _tsorter);
		if (idx >= 0)
		    _list_t.RemoveAt(idx);
		_moredata.Set();
	    }
	}

	public void Remove(Socket sock)
	{
	    if (sock == null) return;
	    lock (this) {
		if (_writers.ContainsKey(sock)) {
		    w_state w = _writers[sock];
		    w.Cancel();
		    _writers.Remove(sock);
		}
		if (_readers.ContainsKey(sock)) {
		    r_state r = _readers[sock];
		    r.Cancel();
		    _readers.Remove(sock);
		}

		int i, max;
		i = 0;
		max = _list_c.Count;
		while(i < max) {
		    c_state c = _list_c[i];
		    if (c.Socket == sock) {
			c.Cancel();
			_list_c.RemoveAt(i);
			break;
		    }
		    i++;
		}

		foreach (workpair d in _workqueue) {
		    // remove if for this socket!
		    if (d.sock == sock) {
			d.sock = null;
			d.d = null;
		    }
		}
	    }
	}

	// shuts down socketmanager and shuts down io thread
	public void Close()
	{
            _shutdown = true;
	    _moredata.Set();
	    _ended.WaitOne(500, false);
	}

	// nice utility function that closes a socket cleanly
	public void Shutdown(Socket sock)
	{
	    try { sock.Shutdown(SocketShutdown.Both); } catch { }
	    try { sock.Close(); } catch { }
	}

	// --------------------------------------------------------------------

	List<c_state> _list_c = new List<c_state>();
	List<t_state> _list_t = new List<t_state>();
	Dictionary<Socket, r_state> _readers = new Dictionary<Socket, r_state>();
	Dictionary<Socket, w_state> _writers = new Dictionary<Socket, w_state>();

	internal delegate void WorkDelegate();
	Queue<workpair> _workqueue = new Queue<workpair>();
	Queue<workpair> _workqueue2 = new Queue<workpair>();
	Thread _thread;
	AutoResetEvent _ended;
	bool _shutdown = false;
	AutoResetEvent _moredata;
	TimerSorter _tsorter = new TimerSorter();

	class t_state : TimerHandle {
	    static long __id = 1;
	    SocketManager.TimerCB _cb;
	    DateTime _time;
	    long _id;

	    public t_state(int ms, SocketManager.TimerCB cb) {
		_id = __id++;
		_cb = cb;
		_time = DateTime.Now.AddMilliseconds(ms);
	    }

	    public long ID { get { return _id; } }
	    public DateTime Time { get { return _time; } }

	    public void Go() {
		try { _cb(); } catch (Exception e) { Debug.WriteLine(e.ToString()); }
	    }
	};

	class a_state {
	    Socket _sock;
	    SocketManager.AcceptCB _cb;
#if BAD_USER_TRACE
            string stack;
#endif

	    public a_state(IPEndPoint endpoint, SocketManager.AcceptCB cb) {
		_cb = cb;
		_sock = new Socket(AddressFamily.InterNetwork,
				       SocketType.Stream, ProtocolType.Tcp);
#if MONO
#else
#if COMPACT_FRAMEWORK
#else
		_sock.NoDelay = true;
#endif
#endif
		_sock.Blocking = false;
                _sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, (int)1);
		_sock.Bind(endpoint);
		_sock.Listen(1000);
		Start();
#if BAD_USER_TRACE
                stack = Environment.StackTrace;
#endif
	    }

	    void Start()
            {
                _sock.BeginAccept(this.AsyncCallback, this);
	    }

	    private void AsyncCallback(IAsyncResult ar)
	    {
                if (_sock == null) return;
		try {
		    Socket newsock = _sock.EndAccept(ar);
		    WorkDelegate d = delegate {
			lock (this) {
			    if (_cb != null)
				_cb(newsock);
			}
		    };
#if BAD_USER_TRACE
                SocketManager.Instance.DoWork(_sock, d, stack);
#else
    	        SocketManager.Instance.DoWork(_sock, d);
#endif
                } catch (ObjectDisposedException) {
                    // this is nonfatal. This means that we had an acceptor open and we closed the socket.
                    // this is a normal way for an acceptor to die.
                    return;
                } catch (Exception e) {
                    Debug.WriteLine(e.ToString());
                }
                lock (this)
                {
                    if (_sock != null)
                        Start();
                }
	    }

        public void Cancel()
        {
            lock (this)
            {
                _cb = null;
                Socket sock = _sock;
                _sock = null;
                try { sock.Close(); }
                catch (Exception e) { Debug.WriteLine(e.ToString()); }
            }
        }

	    public Socket Socket { get { return _sock; } }

            public IPEndPoint Endpoint { get { return (IPEndPoint)_sock.LocalEndPoint; } }
	};

	class c_state {
	    SocketManager.ConnectCB _cb;
	    Socket _sock;
#if BAD_USER_TRACE
            string stack;
#endif

	    public c_state(IPEndPoint endpoint, SocketManager.ConnectCB cb) {
		_cb = cb;
		_sock = new Socket(AddressFamily.InterNetwork,
				   SocketType.Stream, ProtocolType.Tcp);
#if MONO
#else
#if COMPACT_FRAMEWORK
#else
		_sock.NoDelay = true;
#endif
#endif
		_sock.Blocking = false;
		_sock.BeginConnect(endpoint, this.AsyncCallback, this);
#if BAD_USER_TRACE
                stack = Environment.StackTrace;
#endif
	    }

	    private void AsyncCallback(IAsyncResult ar)
	    {
		bool success;
		try {
		    success = true;
		    _sock.EndConnect(ar);
		} catch {
		    success = false;
		    try { _sock.Close(); } catch { }
		    _sock = null;
		}
		WorkDelegate d = delegate {
		    lock (this) {
			if (_cb != null)
			    _cb(_sock, success);
		    }
		};
#if BAD_USER_TRACE
                SocketManager.Instance.DoWork(_sock, d, stack);
#else
    		SocketManager.Instance.DoWork(_sock, d);
#endif
	    }

	    public void Cancel() {
		lock (this) {
		    _cb = null;
		    try { _sock.Close(); } catch { }
		    _sock = null;
		}
	    }

	    public Socket Socket { get { return _sock; } }
	};

	class r_state {
	    Socket _sock;
	    SocketManager.ReadCB _cb;
	    SocketManager.Read2CB _cb2;
	    byte[] _buffer;
#if BAD_USER_TRACE
            string stack;
#endif

	    public r_state(Socket sock, SocketManager.ReadCB cb) {
		_sock = sock;
		_cb = cb;
#if BAD_USER_TRACE
                stack = Environment.StackTrace;
#endif
		Start();
	    }
	    public r_state(Socket sock, SocketManager.Read2CB cb) {
		_sock = sock;
		_cb2 = cb;
#if BAD_USER_TRACE
                stack = Environment.StackTrace;
#endif
		Start();
	    }

	    private void Start() {
		_buffer = new byte[32768];
		try {
		    if (_cb2 != null) {
			EndPoint ep = new IPEndPoint(0, 0);
			_sock.BeginReceiveFrom(_buffer, 0,
                                               _buffer.Length,
                                               0, ref ep,
                                               this.AsyncCallback,
                                               this);
                    } else {
                        _sock.BeginReceive(_buffer, 0,
                                           _buffer.Length,
#if MONO
                                           SocketFlags.None,
#else
                                           SocketFlags.Partial,
#endif
                                           this.AsyncCallback,
                                           this);
		    }
		} catch {
		    WorkDelegate d;
		    if (_cb2 != null)
			d = delegate {
			    lock (this) {
				if (_cb2 != null)
				    _cb2(_sock, null, null, 0);
			    }
			};
		    else
			d = delegate {
			    lock (this) {
				if (_cb != null)
				    _cb(_sock, null, 0);
			    }
			};
#if BAD_USER_TRACE
		    SocketManager.Instance.DoWork(_sock, d, stack);
#else
		    SocketManager.Instance.DoWork(_sock, d);
#endif
		}
	    }

	    private void AsyncCallback(IAsyncResult ar)
	    {
		bool restart = false;
		try {
		    int r = 0;
		    if (_cb2 != null) {
			EndPoint ep = new IPEndPoint(0, 0);
			r = _sock.EndReceiveFrom(ar, ref ep);
			byte[] b = _buffer;

			WorkDelegate d;
			if (r != 0) {
			    restart = true;
			    d = delegate {
				lock (this) {
				    if (_cb2 != null)
					_cb2(_sock, (IPEndPoint)ep, b, r);
				}
			    };
			} else {
			    d = delegate {
				lock (this) {
				    if (_cb2 != null)
					_cb2(_sock, (IPEndPoint)ep, b, r);
				}
				SocketManager.Instance.Remove(_sock);
			    };
			}
#if BAD_USER_TRACE
                        SocketManager.Instance.DoWork(_sock, d, stack);
#else
        		SocketManager.Instance.DoWork(_sock, d);
#endif
		    } else {
			r = _sock.EndReceive(ar);

			byte[] b = _buffer;

			WorkDelegate d;
			if (r != 0) {
			    restart = true;
			    d = delegate {
				lock (this) {
				    if (_cb != null)
					_cb(_sock, b, r);
				}
			    };
			} else {
			    d = delegate {
				lock (this) {
				    if (_cb != null)
					_cb(_sock, b, r);
				}
				SocketManager.Instance.Remove(_sock);
			    };
			}
#if BAD_USER_TRACE
                        SocketManager.Instance.DoWork(_sock, d, stack);
#else
        		SocketManager.Instance.DoWork(_sock, d);
#endif
		    }

		} catch {
		    try {
			WorkDelegate d;
			if (_cb2 != null)
			    d = delegate {
				lock (this) {
				    if (_cb2 != null)
					_cb2(_sock, null, null, 0);
				}
				SocketManager.Instance.Remove(_sock);
			    };
			else
			    d = delegate {
				lock (this) {
				    if (_cb != null)
					_cb(_sock, null, 0);
				}
				SocketManager.Instance.Remove(_sock);
			    };
#if BAD_USER_TRACE
                        SocketManager.Instance.DoWork(_sock, d, stack);
#else
        		SocketManager.Instance.DoWork(_sock, d);
#endif
		    } catch { }
		}

		if (restart) Start();
	    }

	    public void Cancel() {
		lock (this) {
		    _cb = null;
		    _cb2 = null;
		}
	    }

	    public Socket Socket { get { return _sock; } }
	};

	class w_state {
	    Socket _sock;
	    class singlewrite {
		public byte[] data;
		public int offset;
		public int size;
		public SocketManager.WriteCB cb;
	    }
	    Queue<singlewrite> _queue;
	    byte[] _bytes;
	    int _pos;
	    int _sz;
	    SocketManager.WriteCB _cb;

	    public w_state(Socket sock, byte[] bytes, int offset, int sz,
			   SocketManager.WriteCB cb)
	    {
		lock(this) {
		    _sock = sock;
		    _cb = cb;
		    _queue = new Queue<singlewrite>();
		    _bytes = bytes;
		    _pos = offset;
		    _sz = sz;
		    Start();
		}
	    }

	    void Start() {
		int sz = _sz-_pos;
		try {
                    _sock.BeginSend(_bytes, _pos, sz, 0,
                                    this.AsyncCallback, this);
		} catch {
                    try {
                        if (_cb != null) {
                            WorkDelegate d = delegate {
                                lock (this) {
                                    if (_cb != null)
                                        _cb(_sock);
                                }
                            };
                            SocketManager.Instance.DoWork(_sock, d);
                        }
                    } catch (Exception e) { Debug.WriteLine(e.ToString()); }
		}
	    }

	    void AsyncCallback(IAsyncResult ar) {
		bool restart = false;
		try {
		    int r = _sock.EndSend(ar);
		    lock(this) {
			_pos += r;
			if (_pos < _sz) {
			    restart = true;
			} else {
			    _bytes = null;
			    if (_cb != null) {
				WorkDelegate d = delegate {
				    lock (this) {
					if (_cb != null)
					    _cb(_sock);
				    }
				};
				SocketManager.Instance.DoWork(_sock, d);
			    }
			    if (_queue.Count != 0) {
				singlewrite sw = _queue.Dequeue();
				_bytes = sw.data;
				_cb = sw.cb;
				_pos = sw.offset;
				_sz = sw.size;
				restart = true;
			    }
			}
		    }
		} catch {
		    try {
			if (_cb != null) {
			    WorkDelegate d = delegate {
				lock (this) {
				    if (_cb != null)
					_cb(_sock);
				}
			    };
			    SocketManager.Instance.DoWork(_sock, d);
			}
		    } catch (Exception e) { Debug.WriteLine(e.ToString()); }
		}

		if (restart) Start();
	    }

	    public void Cancel() {
		lock(this) {
		    _cb = null;
		    _queue.Clear();
		    _sz = 0;
		}
	    }

	    public void AddData(byte[] data, int offset, int sz,
				SocketManager.WriteCB cb)
	    {
		lock(this) {
		    if (_bytes == null) {
			_bytes = data;
			_cb = cb;
			_pos = offset;
			_sz = sz;
			Start();
		    } else {
			singlewrite sw = new singlewrite();
			sw.data = data;
			sw.offset = offset;
			sw.size = sz;
			sw.cb = cb;
			_queue.Enqueue(sw);
		    }
		}
	    }

	    public Socket Socket { get { return _sock; } }
	};

	class TimerSorter : IComparer<t_state> {
	    public int Compare(t_state a, t_state b) {
		if (a.Time.Ticks != b.Time.Ticks)
		    return a.Time.CompareTo(b.Time);
		else
		    return (int)(a.ID - b.ID);
	    }
	}

        public static bool DontStartInThread = false;

	private SocketManager() {
	    _ended = new AutoResetEvent(false);
	    _moredata = new AutoResetEvent(false);
            if (!DontStartInThread) {
                _thread = new Thread(this.Go);
                _thread.IsBackground = true;
                _thread.Start();
            }
	}

	class workpair {
	    public WorkDelegate d;
	    public Socket sock;
#if BAD_USER_TRACE
            public string stack;
#endif
	}
#if BAD_USER_TRACE
	void DoWork(Socket sock, WorkDelegate d, string stack) {
	    lock(this) {
		workpair p = new workpair();
		p.sock = sock;
		p.d = d;
                p.stack = stack;
		_workqueue.Enqueue(p);
		_moredata.Set();
	    }
        }
#endif
	void DoWork(Socket sock, WorkDelegate d) {
	    lock(this) {
		workpair p = new workpair();
		p.sock = sock;
		p.d = d;
		_workqueue.Enqueue(p);
		_moredata.Set();
	    }
	}

	void Go() {
	    Thread.CurrentThread.Name = "SocketManagerThread";
	    while (true) {
		int? timeout;
		lock(this) {
		    if (_list_t.Count == 0) {
			timeout = null;
		    } else {
			timeout = (int)(_list_t[0].Time -
					DateTime.Now).TotalMilliseconds;
			if (timeout < 0) timeout = 0;
		    }
		}

		if (timeout == null) {
                    _moredata.WaitOne();
                } else {
                    _moredata.WaitOne((int)timeout, false);
		}

		if (_shutdown) {
		    // shutdown fired
		    break;
		}

		List<t_state> tt = new List<t_state>();
		lock (this) {
                    DateTime now = DateTime.Now;
		    while (_list_t.Count != 0) {
			t_state t = _list_t[0];
			if (t.Time.CompareTo(now) <= 0) {
			    tt.Add(t);
			    _list_t.RemoveAt(0);
			} else {
			    break;
			}
		    }

		    Queue<workpair> tmp = _workqueue;
		    _workqueue = _workqueue2;
		    _workqueue2 = tmp;
		}
		while (_workqueue2.Count > 0)
		{
#if BAD_USER_TRACE
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
#endif
		    workpair d = _workqueue2.Dequeue();
		    if (d.d != null)
                        d.d();
#if BAD_USER_TRACE
                    long ms = sw.ElapsedMilliseconds;
                    if (ms > 100)
                        Console.WriteLine("d.d() took " + ms + "\n" + d.stack);
#endif
		}
                foreach (t_state t in tt)
                {
#if BAD_USER_TRACE
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
#endif
                    t.Go();
#if BAD_USER_TRACE
                    long ms = sw.ElapsedMilliseconds;
                    if (ms > 100)
                        Console.WriteLine("t.Go() took " + ms);
#endif
                }
	    }
	    _ended.Set();
	}

	class Singleton {
	    static Singleton() { } // static constructor tells compiler not to mark type as beforefieldinit
	    internal static readonly SocketManager i = new SocketManager();
	}

        #region ISendPost Members

        public void Post(SendOrPostCallback d, object state)
        {
            if (d == null) throw new ArgumentNullException("d");
            DoWork(null, delegate { d(state); });
        }

        public void Send(SendOrPostCallback d, object state)
        {
            if (d == null) throw new ArgumentNullException("d");
            AutoResetEvent are = new AutoResetEvent(false);
            DoWork(null, delegate
            {
                try
                {
                    d(state);
                }
                finally
                {
                    are.Set();
                }
            });
            are.WaitOne();
        }

        #endregion

        public void AddAccepter(object ep, IPEndPoint iPEndPoint, object _Accept)
        {
            throw new NotImplementedException();
        }
    }
}
