package com.ishiboo.voo;

import android.app.Activity;
import android.app.ProgressDialog;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.AsyncTask;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.util.Log;
import android.view.KeyEvent;
import android.view.View;
import android.view.inputmethod.EditorInfo;
import android.widget.EditText;
import android.widget.TextView;

public class ConnectActivity extends Activity {
    private static final String TAG = "Voo-ConnectActivity";

    private VooApplication    _app;
    private SharedPreferences _prefs;

    private EditText          _ipview;
    private TextView          _errortext;
    private ConnectTask       _connecttask = null;
    private ProgressDialog    _progress;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        _app = (VooApplication)getApplicationContext();
        _prefs = getPreferences(MODE_PRIVATE);

        if (_app.Connection != null && _app.Connection.GetState() != VooConnection.State.Disconnected)
        {
            startActivity(new Intent(this, ControlsActivity.class));
            finish();
            return;
        }

        setContentView(R.layout.activity_connect);

        _ipview = (EditText)findViewById(R.id.ipaddress);
        _ipview.setText(_prefs.getString("lastip", null));

        _ipview.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence charSequence, int i, int i2, int i3) {}
            @Override public void onTextChanged(CharSequence charSequence, int i, int i2, int i3) {}
            @Override public void afterTextChanged(Editable editable) { _errortext.setVisibility(View.INVISIBLE); }
        });

        _ipview.setOnEditorActionListener(new TextView.OnEditorActionListener() {
            @Override
            public boolean onEditorAction(TextView textView, int id, KeyEvent keyEvent) {
                if (id == R.id.connect_button || id == EditorInfo.IME_ACTION_DONE) {
                    _connect();
                    return true;
                }
                return false;
            }
        });

        _errortext = (TextView)findViewById((R.id.errortext));
        _errortext.setVisibility(View.INVISIBLE);

        findViewById(R.id.connect_button).setOnClickListener(new View.OnClickListener() { @Override public void onClick(View view) { _connect(); } });
    }

    private void _connect() {
        if (_connecttask != null) return;

        _ipview.setError(null);

        String ip = _ipview.getText().toString();

        final SharedPreferences.Editor editor = _prefs.edit();
        editor.putString("lastip", ip);
        editor.commit();

        if (TextUtils.isEmpty(ip)) {
            _ipview.setError(getString(R.string.error_field_required));
            _ipview.requestFocus();
        } else {
            _app.Connection.SetHost(ip);
            _progress = ProgressDialog.show(this, "Connecting", "Please wait while connecting...");
            _connecttask = new ConnectTask();
            _connecttask.execute((Void) null);
        }
    }

    private class ConnectTask extends AsyncTask<Void, Void, Boolean> {
        @Override
        protected Boolean doInBackground(Void... params) {
            try {
                _app.Connection.Connect();
            } catch (Exception e) {
                return false;
            }

            return true;
        }

        @Override
        protected void onPreExecute() {
            _errortext.setVisibility(View.INVISIBLE);
        }


        @Override
        protected void onPostExecute(final Boolean success) {
            _progress.dismiss();
            if (success) {
                _errortext.setVisibility(View.INVISIBLE);
                startActivity(new Intent(ConnectActivity.this, ControlsActivity.class));
                finish();
            } else {
                _errortext.setText("Failed to connect");
                _errortext.setVisibility(View.VISIBLE);
                _ipview.selectAll();
                _ipview.requestFocus();
            }
            _connecttask = null;
            _progress = null;
        }

        @Override
        protected void onCancelled() {
            _errortext.setVisibility(View.INVISIBLE);
            _connecttask = null;
            _progress = null;
        }
    }
}
