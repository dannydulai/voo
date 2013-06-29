package com.ishiboo.voo;

import android.os.AsyncTask;
import android.util.Log;
import com.ishiboo.Event;
import com.ishiboo.EventListener;

import java.io.*;
import java.net.InetAddress;
import java.net.Socket;
import java.nio.charset.Charset;
import java.util.ArrayList;

public class VooConnection {
    private static final String TAG = "Voo-VooConnection";
    Socket         _socket;
    PrintWriter    _out;
    BufferedReader _in;
    VooConnectionTask _task;
    ArrayList<EventListener<String>> _recvq = new ArrayList<EventListener<String>>();

    String _host;
    int    _port = 4356;

    State _state = State.Disconnected;
    boolean   _seekable;
    int       _audiotrack;
    int       _subtitle;
    int       _audiotrackcount;
    int       _subtitlecount;
    long      _length;
    long      _time;

    public enum State {
        Disconnected,
        Connecting,
        Stopped,
        Playing,
        Paused
    }

    public void SetHost(String host) { _host = host; }
    public void SetPort(int port)    { _port = port; }

    public boolean   IsConnected()        { return !(_state == State.Disconnected || _state == State.Connecting); }
    public State     GetState()           { return _state; }
    public boolean   GetSeekable()        { return _seekable; }
    public int       GetAudioTrack()      { return _audiotrack; }
    public int       GetSubtitle()        { return _subtitle; }
    public int       GetAudioTrackCount() { return _audiotrackcount; }
    public int       GetSubtitleCount()   { return _subtitlecount; }
    public long      GetLength()          { return _length; }
    public long      GetTime()            { return _time; }

    public void Connect() throws Exception {
        if (_socket != null) Disconnect();

        try {
            _socket = new Socket(_host, _port);
            _out = new PrintWriter(new BufferedWriter(new OutputStreamWriter(_socket.getOutputStream(), Charset.forName("UTF-8"))), true);
            _in = new BufferedReader(new InputStreamReader(_socket.getInputStream()));
            _task = new VooConnectionTask();
            _task.execute((Void) null);
        } catch (Exception e) {
            throw e;
        }
    }

    private class VooConnectionTask extends AsyncTask<Object, String, Object> {
        protected Object doInBackground(Object... params) {
            try {
                while (true) {
                    if (this.isCancelled())
                        return null;
                    String s = _in.readLine();
                    if (s != null) {
                        publishProgress(s);
                        s = null;
                    } else {
                        break;
                    }
                }
            } catch (Exception e) {
                Log.e(TAG, "Error in read loop", e);
            } finally {
                Disconnect();
            }
            return null;
        }

        @Override protected void onProgressUpdate(String... s) {
            ev_readline(s[0]);
        }
    }

    public void Disconnect() {
        try {    _out.close();  } catch (Exception e) { }
        try {     _in.close();  } catch (Exception e) { }
        try { _socket.close(); } catch (Exception e) { }
        try { _task.cancel(true); } catch (Exception e) { }
        _out    = null;
        _in     = null;
        _socket = null;
        _task   = null;
        _recvq.clear();

        if (_state == State.Disconnected) return;
        _state  = State.Disconnected;
        OnStateChanged.dispatch(this, null);
    }

    private void ev_readline(String s)
    {
        Log.d(TAG, "Got " + s);
        if (s.length() == 0) return;
        if (s.charAt(0) == '*') {
            if (s.startsWith("*stopped")) {
                _state = State.Stopped;
                OnStateChanged.dispatch(this, null);
            } else if (s.startsWith("*playing")) {
                _state = State.Playing;
                OnStateChanged.dispatch(this, null);
            } else if (s.startsWith("*paused")) {
                _state = State.Paused;
                OnStateChanged.dispatch(this, null);
            } else if (s.startsWith("*seekable")) {
                _seekable = true;
                OnSeekableChanged.dispatch(this, null);
            } else if (s.startsWith("*notseekable")) {
                _seekable = false;
                OnSeekableChanged.dispatch(this, null);
            } else if (s.startsWith("*audiotrack ")) {
                _audiotrack = Integer.parseInt(s.substring(12));
                OnAudioTrackChanged.dispatch(this, null);
            } else if (s.startsWith("*audiotrackcount ")) {
                _audiotrackcount = Integer.parseInt(s.substring(17));
                OnAudioTrackCountChanged.dispatch(this, null);
            } else if (s.startsWith("*subtitle ")) {
                _subtitle = Integer.parseInt(s.substring(10));
                OnSubtitleChanged.dispatch(this, null);
            } else if (s.startsWith("*subtitlecount ")) {
                _subtitlecount = Integer.parseInt(s.substring(15));
                OnSubtitleCountChanged.dispatch(this, null);
            } else if (s.startsWith("*time ")) {
                _time = Long.parseLong(s.substring(6));
                OnTimeChanged.dispatch(this, null);
            } else if (s.startsWith("*length ")) {
                _length = Long.parseLong(s.substring(8));
                OnLengthChanged.dispatch(this, null);
            }

        } else if (s.charAt(0) == '!') {
            if (!_recvq.isEmpty()) { _recvq.get(0).onNotify(this, s.substring(1)); _recvq.remove(0); }
        }
    }

    private Event<Void> OnStateChanged = new Event<Void>();
    private Event<Void> OnSeekableChanged = new Event<Void>();
    private Event<Void> OnSubtitleChanged = new Event<Void>();
    private Event<Void> OnSubtitleCountChanged = new Event<Void>();
    private Event<Void> OnAudioTrackChanged = new Event<Void>();
    private Event<Void> OnAudioTrackCountChanged = new Event<Void>();
    private Event<Void> OnTimeChanged = new Event<Void>();
    private Event<Void> OnLengthChanged = new Event<Void>();

    public void SetStateChangedHandler(EventListener<Void> h) { OnStateChanged.add(h); }
    public void SetSeekableChangedHandler(EventListener<Void> h) { OnSeekableChanged.add(h); }
    public void SetSubtitleChangedHandler(EventListener<Void> h) { OnSubtitleChanged.add(h); }
    public void SetSubtitleCountChangedHandler(EventListener<Void> h) { OnSubtitleCountChanged.add(h); }
    public void SetAudioTrackChangedHandler(EventListener<Void> h) { OnAudioTrackChanged.add(h); }
    public void SetAudioTrackCountChangedHandler(EventListener<Void> h) { OnAudioTrackCountChanged.add(h); }
    public void SetTimeChangedHandler(EventListener<Void> h) { OnTimeChanged.add(h); }
    public void SetLengthChangedHandler(EventListener<Void> h) { OnLengthChanged.add(h); }

    public void Send(String cmd, EventListener<String> cb) {
        _recvq.add(cb);
        Send(cmd);
    }
    public void Send(String cmd) {
        Log.d(TAG, "Sending " + cmd);
        try { _out.write(cmd); _out.flush(); } catch (Exception e) { Disconnect(); }
    }
}
