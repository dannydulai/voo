using System;
using System.IO;
using System.Text;

namespace Voo
{
    public class Utils {
        public static string to_time(ulong time) {
            ulong d, h, m, s;

            time /= 1000;

            s = time % 60;
            time -= s;
            time /= 60;

            m = time % 60;
            time -= m;
            time /= 60;

            h = time % 24;
            time -= h;
            time /= 24;

            d = time;

            string str = ":" + s.ToString("00");
            if (d != 0)
                str = d.ToString() + ":" + h.ToString("00") + ":" + m.ToString("00") + str;
            else if (h != 0)
                str = h.ToString() + ":" + m.ToString("00") + str;
            else
                str = m.ToString() + str;

            return str;
        }
    }
    public class NetLineParser {
	public delegate void ProcessCB(string s);

	StringBuilder _sb = new StringBuilder();
	bool _newline;
	bool _usechar;
	char _char;
	int _i;
	Encoding _e;

	public NetLineParser() : this(Encoding.UTF8) {
	    _i = 0;
	    _newline = true;
            _usechar = false;
	}

	public NetLineParser(Encoding e) {
	    _e = e;
	    _i = 0;
	    _newline = true;
            _usechar = false;
	}

	public NetLineParser(char c) : this(c, Encoding.UTF8, false) {
	}

	public NetLineParser(char c, Encoding e, bool parsenewline) {
	    _e = e;
	    _i = 0;
	    _newline = parsenewline;
            _usechar = true;
	    _char = c;
	}

	public void Process(byte[] inbuf, int inbufpos, int inbufsz,
			    ProcessCB callout)
	{
	    if (callout == null)
		throw new Exception("callout paramater can not be null with " +
				    "this overload");
	    _Process(inbuf, inbufpos, inbufsz, callout, true);
	}

	public void Process(byte[] inbuf, int inbufpos, int inbufsz,
			    ProcessCB callout, bool allowempty)
	{
	    if (callout == null)
		throw new Exception("callout paramater can not be null with " +
				    "this overload");
	    _Process(inbuf, inbufpos, inbufsz, callout, allowempty);
	}

	public string Process(byte[] inbuf, int inbufpos, int inbufsz,
			      bool allowempty)
	{
	    return _Process(inbuf, inbufpos, inbufsz, null, allowempty);
	}

        bool _last_was_cr = false;

	string _Process(byte[] inbuf, int inbufpos, int inbufsz,
		       ProcessCB callout, bool allowempty)
	{
	    if (inbufsz != 0)
		_sb.Append(_e.GetString(inbuf, inbufpos, inbufsz));
	    int i = _i;
	    int max = _sb.Length;
	    while (i < max) {
		char c = _sb[i];
		bool found = false;
		if (_newline) {
                    if ((c == '\r')) {
                        found = true;
                        _last_was_cr = true;
                    } else if ((c == '\n')) {
                        if (_last_was_cr) {
			    _sb.Remove(i, 1);
                            max--;
                            i--;
                        } else {
                            found = true;
                        }
                        _last_was_cr = false;
                    } else {
                        _last_was_cr = false;
                    }
                }
                if (!found && _usechar) {
		    if (c == _char)
			found = true;
		}
		if (found) {
		    if (i != 0 || allowempty)
		    {
			if (callout != null) {
			    callout(_sb.ToString(0, i));
			} else {
			    string ret = _sb.ToString(0, i);
			    _i = 0;
			    _sb.Remove(0, i+1);
			    return ret;
			}
		    }
		    _sb.Remove(0, i+1);
		    i = 0;
		    max = _sb.Length;
		    continue;
		}
		i++;
	    }
	    _i = i;
	    return null;
	}

	public void Process(Stream stream, ProcessCB callout)
	{
            byte[] buf = new byte[1000000];
            while (true) {
                int r = stream.Read(buf, 0, buf.Length);
                if (r == 0) {
                    break;
                }
                this.Process(buf, 0, r, callout);
            }
	}
    }
}
