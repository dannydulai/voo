package com.ishiboo.voo;

import android.app.AlertDialog;
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

public class ControlsActivity extends Activity {
    private static final String TAG = "Voo-VooControls";
    private VooApplication    _app;
    private Menu              _menu;
    private FrameLayout       _listroot;

    ArrayList<String> _cut = new ArrayList<String>();
    ArrayList<VooDir> _dirs = new ArrayList<VooDir>();

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        _app = (VooApplication)getApplicationContext();

        _app.Connection.SetStateChangedHandler(new EventListener<Void>() {
            @Override public void onNotify(Object source, Void message) {
                _updatestatechange();
            }
        });

        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_controls);

        _listroot = (FrameLayout)findViewById(R.id.listroot);

        // initial gui state
        _updatestatechange();
        if (_app.Connection.IsConnected()) {
            findViewById(R.id.listloading).setVisibility(View.VISIBLE);
            _app.Connection.Send(":list .\n", new EventListener<String>() {
                @Override public void onNotify(Object source, String message) {
                    findViewById(R.id.listloading).setVisibility(View.GONE);
                    _dirs.clear();
                    VooDir dir = new VooDir(ControlsActivity.this, ".", message, _dirs);
                    _dirs.add(dir);
                    _listroot.addView(dir.View, FrameLayout.LayoutParams.MATCH_PARENT);
                }
            });
        }
    }

    @Override
    public void onBackPressed() {
        if (((ViewSwitcher)(findViewById(R.id.viewswitcher))).getNextView().getId() == R.id.listroot) {
            Intent setIntent = new Intent(Intent.ACTION_MAIN);
            setIntent.addCategory(Intent.CATEGORY_HOME);
            setIntent.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            startActivity(setIntent);
        } else if (_dirs.size() <= 1) {
            show_controls();
        } else {
            VooDir dir = _dirs.get(_dirs.size()-1);
            _dirs.remove(_dirs.size()-1);
            _listroot.removeView(dir.View);
            dir = _dirs.get(_dirs.size()-1);
            _listroot.addView(dir.View, FrameLayout.LayoutParams.MATCH_PARENT);
        }
    }

    public class VooDir {
        public String Path;
        public ListView View;
        public VooFile[] Files;

        private ArrayList<VooDir> _dirs;
        private Context _ctx;

        int _checkedcount = 0;

        public class RowView extends LinearLayout implements Checkable {
            public RowView(Context context, AttributeSet attrs) {
                super(context, attrs);
                View.inflate(context, R.layout.filerow, this);
            }

            @Override
            public void setChecked(boolean b) {
                this.setSelected(b);
                if (b)
                    this.setBackgroundColor(Color.argb(0x40,0,0,0));
                else
                    this.setBackgroundColor(Color.argb(0,0,0,0));
            }

            @Override
            public boolean isChecked() {
                return this.isSelected();
            }

            @Override
            public void toggle() {
                this.setSelected(!this.isSelected());
            }
        }

        public VooDir(Context ctx, String path, String message, ArrayList<VooDir> dirs) {
            _ctx = ctx;
            _dirs = dirs;
            this.Path = path;
            if (message.length() == 0) {
                this.Files = new VooFile[0];
            } else {
                String[] strs = message.split(":");
                this.Files = new VooFile[strs.length];
                int i = 0;
                while (i < strs.length) {
                    VooFile vf = this.Files[i] = new VooFile();
                    vf.IsDir = strs[i].charAt(0) == 'D';
                    vf.Name = strs[i].substring(1);
                    i++;
                }
            }
            this.View = new ListView(ctx);
            this.View.setLongClickable(true);
            this.View.setAdapter(new DirListAdapter(ctx, this.Files));
            this.View.setChoiceMode(ListView.CHOICE_MODE_MULTIPLE_MODAL);
            this.View.setMultiChoiceModeListener(new AbsListView.MultiChoiceModeListener() {
                @Override public boolean onPrepareActionMode(ActionMode mode, Menu menu) {
                    return false;
                }
                @Override public void onDestroyActionMode(ActionMode mode) { }
                @Override public boolean onCreateActionMode(ActionMode mode, Menu menu) {
                    MenuInflater inflater = mode.getMenuInflater();
                    inflater.inflate(R.menu.actionselected, menu);
                    return true;
                }
                @Override public boolean onActionItemClicked(final ActionMode mode, MenuItem item) {
                    if (item.getItemId() == R.id.action_cut) {
                        _cut.clear();

                        SparseBooleanArray selecteditems = VooDir.this.View.getCheckedItemPositions();
                        int i = 0;
                        while (i < VooDir.this.View.getCount()) {
                            if (selecteditems.get(i) == true) {
                                VooFile file = Files[i];
                                _cut.add(VooDir.this.Path + "\\" + Files[i].Name);
                            }
                            i++;
                        }

                        _menu.findItem(R.id.action_paste).setVisible(!_cut.isEmpty());
                        mode.finish();
                        return true;

                    } else if (item.getItemId() == R.id.action_delete) {
                        final ArrayList<VooFile> deletes = new ArrayList<VooFile>();

                        SparseBooleanArray selecteditems = VooDir.this.View.getCheckedItemPositions();
                        int i = 0;
                        while (i < VooDir.this.View.getCount()) {
                            if (selecteditems.get(i) == true)
                                deletes.add(Files[i]);
                            i++;
                        }

                        new AlertDialog.Builder(_ctx)
                                .setIcon(android.R.drawable.ic_dialog_alert)
                                .setTitle(getString(R.string.stop))
                                .setMessage(getString(R.string.areyousureyouwanttodelete))
                                .setPositiveButton(getString(R.string.yes), new DialogInterface.OnClickListener() {
                                    @Override public void onClick(DialogInterface dialog, int which) {
                                        for (VooFile file : deletes)
                                            _app.Connection.Send(":del " + VooDir.this.Path + "\\" + file.Name + "\n");
                                        mode.finish();
                                        refresh();
                                    }
                                })
                                .setNegativeButton(getString(R.string.no), null)
                                .show();

                        return true;
                    }
                    return false;
                }

                @Override public void onItemCheckedStateChanged(ActionMode mode, int position, long id, boolean checked) {
                    if (checked)
                        _checkedcount++;
                    else
                        _checkedcount--;
                    mode.setTitle(_checkedcount + " selected");
                }
            });

        this.View.setOnItemClickListener(new AdapterView.OnItemClickListener() {
                @Override public void onItemClick(AdapterView<?> parent, final View view, int position, long id) {
                    final VooFile item = (VooFile)parent.getItemAtPosition(position);

                    final String path = Path + "/" + item.Name;
                    if (item.IsDir) {
                        findViewById(R.id.listloading).setVisibility(View.VISIBLE);
                        _app.Connection.Send(":list " +  path + "\n", new EventListener<String>() {
                            @Override public void onNotify(Object source, String message) {
                                VooDir dir = new VooDir(view.getContext(), path, message, _dirs);
                                _dirs.add(dir);
                                findViewById(R.id.listloading).setVisibility(View.GONE);
                                _listroot.addView(dir.View, FrameLayout.LayoutParams.MATCH_PARENT);
                                _listroot.removeView(_dirs.get(_dirs.size()-2).View);
                            }
                        });
                    } else {
                        Toast.makeText(getApplicationContext(), "Playing " + item.Name, Toast.LENGTH_LONG).show();
                        _app.Connection.Send(":comms source 3\n:load " + path + "\n");
                        show_controls();
                    }
                }
            });
        }

        public class DirListAdapter extends ArrayAdapter<VooFile> {
            private final Context _context;
            private final VooFile[] _values;

            public DirListAdapter(Context context, VooFile[] values) {
                super(context, 0, values);
                _context = context;
                _values = values;
            }

            @Override
            public View getView(int position, View convertView, ViewGroup parent) {
                LayoutInflater inflater = (LayoutInflater) _context.getSystemService(Context.LAYOUT_INFLATER_SERVICE);
                View rowView = new RowView(_context, null);
                TextView textView = (TextView) rowView.findViewById(R.id.text);
                ImageView imageView = (ImageView) rowView.findViewById(R.id.icon);

                VooFile item = _values[position];
                textView.setText(item.Name);
                if (item.IsDir) {
                    imageView.setImageResource(R.drawable.folder);
                    imageView.setVisibility(View.VISIBLE);
                } else {
                    imageView.setVisibility(View.INVISIBLE);
                }
                return rowView;
            }
        }

        public class VooFile {
            public Boolean IsDir;
            public String Name;
        }
    }

    private void _updatestatechange() {
        switch(_app.Connection.GetState()) {
            case Disconnected:
                startActivity(new Intent(ControlsActivity.this, ConnectActivity.class));
                finish();
                break;
            case Connecting:
                break;
            case Stopped:
                break;
            case Playing:
                break;
            case Paused:
                break;
        }
    }

    private void show_library() {
        if (((ViewSwitcher)(findViewById(R.id.viewswitcher))).getNextView().getId() == R.id.listroot)
            ((ViewSwitcher)(findViewById(R.id.viewswitcher))).showNext();
        _menu.findItem(R.id.action_controls).setVisible(true);
        _menu.findItem(R.id.action_refresh).setVisible(true);

        _menu.findItem(R.id.action_paste).setVisible(!_cut.isEmpty());

        _menu.findItem(R.id.action_library).setVisible(false);
    }
    private void show_controls() {
        if (((ViewSwitcher)(findViewById(R.id.viewswitcher))).getNextView().getId() != R.id.listroot)
            ((ViewSwitcher)(findViewById(R.id.viewswitcher))).showPrevious();
        _menu.findItem(R.id.action_controls).setVisible(false);
        _menu.findItem(R.id.action_refresh).setVisible(false);

        _menu.findItem(R.id.action_paste).setVisible(false);

        _menu.findItem(R.id.action_library).setVisible(true);
    }
    private void refresh() {
        if (_dirs.size() == 0) return;
        findViewById(R.id.listloading).setVisibility(View.VISIBLE);
        final VooDir olddir = _dirs.get(_dirs.size()-1);
        _app.Connection.Send(":list " + olddir.Path + "\n", new EventListener<String>() {
            @Override public void onNotify(Object source, String message) {
                findViewById(R.id.listloading).setVisibility(View.GONE);
                _listroot.removeView(olddir.View);
                _dirs.remove(_dirs.size()-1);
                VooDir dir = new VooDir(ControlsActivity.this, olddir.Path, message, _dirs);
                _dirs.add(dir);
                _listroot.addView(dir.View, FrameLayout.LayoutParams.MATCH_PARENT);
            }
        });
    }
    private void paste() {
        final VooDir dir = _dirs.get(_dirs.size()-1);
        for (String path : _cut)
            _app.Connection.Send(":move " + path + "|" + dir.Path + "\n");
        _cut.clear();
        _menu.findItem(R.id.action_paste).setVisible(!_cut.isEmpty());
        refresh();
    }

    // create Android Menu, set default hide/show/enable/disables
    @Override
    public boolean onCreateOptionsMenu(Menu menu) {
        getMenuInflater().inflate(R.menu.controls, menu);
        _menu = menu;
        show_controls();
        return true;
    }

    // Android Menu click handler
    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        // Handle item selection
        switch (item.getItemId()) {
            case R.id.action_disconnect: _app.Connection.Disconnect(); return true;
            case R.id.action_library:    show_library(); return true;
            case R.id.action_controls:   show_controls(); return true;
            case R.id.action_refresh:    refresh(); return true;
            case R.id.action_paste:      paste(); return true;
            default: return super.onOptionsItemSelected(item);
        }
    }
}
