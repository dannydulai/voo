using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Util;

namespace Voo
{
    [Activity (Label = "voo", MainLauncher = true)]
    public class VooMainActivity1 : Activity
    {
        Connection _conn;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.Main);

            _conn = new Connection();

            FindViewById<Button>(Resource.Id.buttonstop).Click        += delegate { _conn.Stop(); };
            FindViewById<Button>(Resource.Id.buttonpause).Click       += delegate { _conn.TogglePause(); };
            FindViewById<Button>(Resource.Id.buttonfastforward).Click += delegate { _conn.Fwdfast(); };
            FindViewById<Button>(Resource.Id.buttonfastrewind).Click  += delegate { _conn.Backfast(); };
            FindViewById<Button>(Resource.Id.buttonslowforward).Click += delegate { _conn.Fwdslow(); };
            FindViewById<Button>(Resource.Id.buttonslowrewind).Click  += delegate { _conn.Backslow(); };
            FindViewById<Button>(Resource.Id.buttonspeakersoff).Click += delegate { PowerOff(); };
            FindViewById<Button>(Resource.Id.buttonsubtitles).Click   += delegate { _conn.Subtitles(); };
            FindViewById<Button>(Resource.Id.buttonvolup).Click       += delegate { _conn.Louder(); };
            FindViewById<Button>(Resource.Id.buttonvoldown).Click     += delegate { _conn.Softer(); };
            FindViewById<Button>(Resource.Id.buttonvol1).Click        += delegate { _conn.Vol(1); };
            FindViewById<Button>(Resource.Id.buttonvol65).Click       += delegate { _conn.Vol(65); };
            FindViewById<Button>(Resource.Id.buttonvol80).Click       += delegate { _conn.Vol(80); };

            ShowSplash("Locating Voo Server...");

            _conn.Connecting += delegate {
                Log.Info("voo", "Connecting");
//                RunOnUiThread(delegate { _app.Connecting(); });
            };
            _conn.FailedConnecting += delegate {;
                Log.Info("voo", "FailedConnecting");
//                RunOnUiThread(delegate { _app.FailConnecting(); });
            };
            _conn.SuccessConnecting += delegate {
                Log.Info("voo", "SuccessConnecting");
//                RunOnUiThread(delegate {
//                                        _app.Connected();
//                                        _conn.List(null, ".", (parent,lines) => RunOnUiThread(delegate { _app.Browse(parent, lines); }));
//                                        });
            };
            _conn.Disconnected += delegate {
                Log.Info("voo", "Disconnected");
//                RunOnUiThread(delegate { _app.Disconnected(); });
            };

            _conn.TimeChanged += delegate {
                Log.Info("voo", String.Format("TimeChanged: {0}", _conn.Time));
//                RunOnUiThread(delegate { _app.TimeChanged(_conn.Time); });
            };
            _conn.StateChanged += delegate {
                Log.Info("voo", String.Format("StateChanged: {0}", _conn.State));
//                RunOnUiThread(delegate { _app.StateChanged(_conn.State); });
            };
            _conn.LengthChanged += delegate {
                Log.Info("voo", String.Format("LengthChanged: {0}", _conn.Length));
//                RunOnUiThread(delegate { _app.LengthChanged(_conn.Length); });
            };
            _conn.SeekableChanged += delegate {
                Log.Info("voo", String.Format("SeekableChanged: {0}", _conn.Seekable));
//                RunOnUiThread(delegate { _app.SeekableChanged(_conn.Seekable); });
            };
            _conn.SubtitleChanged += delegate {
                Log.Info("voo", String.Format("SubtitleChanged: {0}", _conn.Subtitle));
//                RunOnUiThread(delegate { _app.SubtitleChanged(_conn.Subtitle); });
            };
            _conn.SubtitleCountChanged += delegate {
                Log.Info("voo", String.Format("SubtitleCountChanged: {0}", _conn.SubtitleCount));
//                RunOnUiThread(delegate { _app.SubtitleCountChanged(_conn.SubtitleCount); });
            };
        }

        void PowerOff() {

        }

        void ShowSplash(string s) {
        }


    }
}
