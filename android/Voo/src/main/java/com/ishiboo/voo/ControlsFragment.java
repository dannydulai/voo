
package com.ishiboo.voo;

import android.app.AlertDialog;
import android.app.Fragment;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.app.Activity;
import android.util.AttributeSet;
import android.util.Log;
import android.util.SparseBooleanArray;
import android.view.ActionMode;
import android.view.LayoutInflater;
import android.view.Menu;
import android.view.MenuInflater;
import android.view.MenuItem;
import android.view.View;
import android.view.ViewGroup;
import android.widget.AbsListView;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.Checkable;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;
import android.widget.ViewSwitcher;

import com.ishiboo.EventListener;

import java.util.ArrayList;
import java.util.List;

public class ControlsFragment extends Fragment {
    private static final String TAG = "Voo-VooControlsFragment";
    private VooApplication    _app;
    private Menu              _menu;
    private FrameLayout       _listroot;

    TextView _v_seekpos;
    Button _v_subtitles;
    Button _v_audiotrack;
    Button _v_pause;

    ArrayList<String> _cut = new ArrayList<String>();
    ArrayList<ControlsActivity.VooDir> _dirs = new ArrayList<ControlsActivity.VooDir>();

    @Override
    public void onCreate(Bundle savedInstanceState) {
        Log.d(TAG, "onCreate");
        super.onCreate(savedInstanceState);
    }

    @Override
    public void onAttach(Activity activity) {
        Log.d(TAG, "onAttach");
        super.onAttach(activity);
        _app = (VooApplication)activity.getApplicationContext();
    }

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState) {
        Log.d(TAG, "onCREATEVIEW");
        View view = inflater.inflate(R.layout.fragment_controls, container, false);

        _v_seekpos = (TextView)view.findViewById(R.id.seekpos);
        _v_subtitles = (Button)view.findViewById(R.id.buttonsubtitles);
        _v_audiotrack = (Button)view.findViewById(R.id.buttonaudiotrack);
        _v_pause = (Button)view.findViewById(R.id.buttonpause);

        view.findViewById(R.id.buttonstop).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) { cmd_stop(); } });
        view.findViewById(R.id.buttonpause).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) { _app.Connection.Send(":comms source 3\n:togglepause\n"); }});
        view.findViewById(R.id.buttonvolup).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) { _app.Connection.Send(":comms volume up\n"); } });
        view.findViewById(R.id.buttonvoldown).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) { _app.Connection.Send(":comms volume down\n"); } });
        view.findViewById(R.id.buttonvol1).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) { _app.Connection.Send(":comms volume 1\n"); } });
        view.findViewById(R.id.buttonvol65).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) { _app.Connection.Send(":comms volume 65\n"); } });
        view.findViewById(R.id.buttonvol80).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) { _app.Connection.Send(":comms volume 80\n"); } });
        view.findViewById(R.id.buttonspeakersoff).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) { cmd_poweroff(); } });

        view.findViewById(R.id.buttonfastforward).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) {
            if (_app.Connection.GetTime() < (_app.Connection.GetLength() - 60000))
            _app.Connection.Send(":seek " + (_app.Connection.GetTime() + 30000) + "\n");
        } });
        view.findViewById(R.id.buttonfastrewind).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) {
            _app.Connection.Send(":seek " + Math.max(0, _app.Connection.GetTime() - 30000) + "\n");
        } });
        view.findViewById(R.id.buttonslowforward).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) {
            if (_app.Connection.GetTime() < (_app.Connection.GetLength() - 10000))
            _app.Connection.Send(":seek " + (_app.Connection.GetTime() + 5000) + "\n");
        } });
        view.findViewById(R.id.buttonslowrewind).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) {
            _app.Connection.Send(":seek " + Math.max(0, _app.Connection.GetTime() - 5000) + "\n");
        } });

        view.findViewById(R.id.buttonaudiotrack).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) {
            int i = _app.Connection.GetAudioTrack() + 1;
            if (i == _app.Connection.GetAudioTrackCount()) i = 0;
            _app.Connection.Send(":audiotrack " + i + "\n");
        } });
        view.findViewById(R.id.buttonsubtitles).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) {
            int i = _app.Connection.GetSubtitle() + 1;
            if (i == _app.Connection.GetSubtitleCount()) i = 0;
            _app.Connection.Send(":subtitle " + i + "\n");
        } });

        _app.Connection.SetStateChangedHandler(new EventListener<Void>() {
            @Override public void onNotify(Object source, Void message) {
                _updatestatechange();
            }
        });
        _app.Connection.SetSeekableChangedHandler(new EventListener<Void>() {
            @Override public void onNotify(Object source, Void message) {
                _updateseekpostext();
            }
        });
        _app.Connection.SetTimeChangedHandler(new EventListener<Void>() {
            @Override public void onNotify(Object source, Void message) {
                _updateseekpostext();
            }
        });
        _app.Connection.SetLengthChangedHandler(new EventListener<Void>() {
            @Override public void onNotify(Object source, Void message) {
                _updateseekpostext();
            }
        });
        _app.Connection.SetSubtitleChangedHandler(new EventListener<Void>() {
            @Override public void onNotify(Object source, Void message) {
                _updatesubtitlecounts();
            }
        });
        _app.Connection.SetSubtitleCountChangedHandler(new EventListener<Void>() {
            @Override public void onNotify(Object source, Void message) {
                _updatesubtitlecounts();
            }
        });
        _app.Connection.SetAudioTrackChangedHandler(new EventListener<Void>() {
            @Override public void onNotify(Object source, Void message) {
                _updateaudiotracks();
            }
        });
        _app.Connection.SetAudioTrackCountChangedHandler(new EventListener<Void>() {
            @Override public void onNotify(Object source, Void message) {
                _updateaudiotracks();
            }
        });

        _updatestatechange();
        _updateseekpostext();
        _updatesubtitlecounts();
        _updateaudiotracks();

        return view;
    }

    private void _updatestatechange() {
        switch(_app.Connection.GetState()) {
            case Stopped:
                _v_pause.setText("Speakers On");
                _updateseekpostext();
                _updatesubtitlecounts();
                _updateaudiotracks();
                break;
            case Playing:
                _v_pause.setText("Pause");
                break;
            case Paused:
                _v_pause.setText("Resume");
                break;
        }
    }

    private void _updateaudiotracks() {
        if (_app.Connection.GetState() != VooConnection.State.Stopped && _app.Connection.GetAudioTrackCount() >= 2)
            _v_audiotrack.setVisibility(View.VISIBLE);
        else
            _v_audiotrack.setVisibility(View.GONE);

        int i = _app.Connection.GetAudioTrack();
        int n = _app.Connection.GetAudioTrackCount();
        _v_audiotrack.setText("Audio Track: " +  i + " of " + n);
    }

    private void _updatesubtitlecounts() {
        if (_app.Connection.GetState() != VooConnection.State.Stopped && _app.Connection.GetSubtitleCount() >= 2)
            _v_subtitles.setVisibility(View.VISIBLE);
        else
            _v_subtitles.setVisibility(View.GONE);

        int i = _app.Connection.GetSubtitle();
        int n = _app.Connection.GetSubtitleCount();
        if (i == 0)
            _v_subtitles.setText("Subtitles: Off");
        else
            _v_subtitles.setText("Subtitles: " +  i + " of " + (n-1));
    }

    public String to_time(long time) {
        long d, h, m, s;

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

        if (d != 0)
            return String.format("%d:%02d:%02d:%02d", d, h, m, s);
        else if (h != 0)
            return String.format("%d:%02d:%02d", h, m, s);
        else
            return String.format("%d:%02d", m, s);
    }

    private void _updateseekpostext() {
        switch (_app.Connection.GetState()) {
            case Playing:
            case Paused:
                _v_seekpos.setText(to_time(_app.Connection.GetTime()) + " / " + to_time(_app.Connection.GetLength()));
                break;
            default:
                _v_seekpos.setText("");
                break;
        }
    }

    private void cmd_stop() {
        new AlertDialog.Builder(getActivity())
                .setIcon(android.R.drawable.ic_dialog_alert)
                .setTitle(getString(R.string.stop))
                .setMessage(getString(R.string.areyousureyouwanttostop))
                .setPositiveButton(getString(R.string.yes), new DialogInterface.OnClickListener() {
                    @Override public void onClick(DialogInterface dialog, int which) {
                        _app.Connection.Send(":stop\n");
                    }
                })
                .setNegativeButton(getString(R.string.no), null)
                .show();
    }
    private void cmd_poweroff() {
        new AlertDialog.Builder(getActivity())
            .setIcon(android.R.drawable.ic_dialog_alert)
            .setTitle(getString(R.string.poweroff))
            .setMessage(getString(R.string.areyousurepoweroff))
            .setPositiveButton(getString(R.string.yes), new DialogInterface.OnClickListener() {
                @Override public void onClick(DialogInterface dialog, int which) {
                    _app.Connection.Send(":comms off\n");
                }
            })
            .setNegativeButton(getString(R.string.no), null)
            .show();
    }
}
