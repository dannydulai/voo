using System;
using System.IO;
using System.Threading;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace Voo
{
    public interface IVooApp
    {
        void InvokeOnMainThread(ThreadStart cb);
        void Connecting();
        void FailConnecting();
        void Connected();
        void Disconnected();
        void Browse(string path, string[] parts);

        void TimeChanged(ulong time);
        void StateChanged(Connection.PlayState state );
        void LengthChanged(ulong length);
        void SeekableChanged(bool seekable);
        void SubtitleChanged(int subtitle);
        void SubtitleCountChanged(int subtitlecount);
    }

    public class Application
    {
        static void Main(string[] args)
        {
            Instance = new Application();
            UIApplication.Main(args);
        }

        public IVooApp _app;
        public Connection _conn;

        public void Init(IVooApp app)
        {
            _app = app;
            _conn = new Connection();

            _conn.Connecting += delegate {
                _app.InvokeOnMainThread(delegate { _app.Connecting(); });
            };
            _conn.FailedConnecting += delegate {
                _app.InvokeOnMainThread(delegate { _app.FailConnecting(); });
            };
            _conn.SuccessConnecting += delegate {
                _app.InvokeOnMainThread(delegate {
                                            _app.Connected();
                                            _conn.List(null, ".", (parent,lines) => _app.InvokeOnMainThread(delegate { _app.Browse(parent, lines); }));
                                        });
            };
            _conn.Disconnected += delegate {
                _app.InvokeOnMainThread(delegate { _app.Disconnected(); });
            };

            _conn.TimeChanged += delegate {
                _app.InvokeOnMainThread(delegate { _app.TimeChanged(_conn.Time); });
            };
            _conn.StateChanged += delegate {
                _app.InvokeOnMainThread(delegate { _app.StateChanged(_conn.State); });
            };
            _conn.LengthChanged += delegate {
                _app.InvokeOnMainThread(delegate { _app.LengthChanged(_conn.Length); });
            };
            _conn.SeekableChanged += delegate {
                _app.InvokeOnMainThread(delegate { _app.SeekableChanged(_conn.Seekable); });
            };
            _conn.SubtitleChanged += delegate {
                _app.InvokeOnMainThread(delegate { _app.SubtitleChanged(_conn.Subtitle); });
            };
            _conn.SubtitleCountChanged += delegate {
                _app.InvokeOnMainThread(delegate { _app.SubtitleCountChanged(_conn.SubtitleCount); });
            };
        }

        UIAlertView _poweralert;
        public void PowerOff() {
            _poweralert = new UIAlertView();
            _poweralert.Title = "Are you sure?";
            _poweralert.AddButton("Yes");
            _poweralert.AddButton("No");
            _poweralert.Message = "Power Off Speakers?";
            _poweralert.Clicked += delegate(object sender, UIButtonEventArgs e) {
                if (e.ButtonIndex == 0) {
                    _conn.Poweroff();
                }
                _poweralert.Dispose();
            };
            _poweralert.Show();
        }

        public static Application Instance;
    }

    public partial class AppDelegateIPad : UIApplicationDelegate, IVooApp
    {
        public override void OnActivated(UIApplication application)
        {
        }

        public void InvokeOnMainThread(ThreadStart cb)
        {
            this.window.InvokeOnMainThread(delegate { cb(); });
        }
        public void Connecting()
        {
            this.status.Text = "Connecting to Voo Server...";
        }
        public void FailConnecting()
        {
            this.status.Text = "Connection failed, Locating Voo Server again...";
        }
        public void Connected()
        {
            this.status.Text = "Ready.";

            this.mainnav.SelectedIndex = 0;
            this.window.AddSubview(this.mainnav.View);
        }
        public void Disconnected()
        {
            this.mainnav.View.RemoveFromSuperview();
            this.browsernav.PopToRootViewController(false);

            this.status.Text = "Disconnected. Locating Voo Server...";
        }

        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            UIApplication.SharedApplication.IdleTimerDisabled = true;

            Application.Instance.Init(this);

            this.play.TouchDown += delegate { Application.Instance._conn.TogglePause(); };
            this.stop.TouchDown += delegate { Application.Instance._conn.Stop(); };
            this.backfast.TouchDown += delegate { Application.Instance._conn.Backfast(); };
            this.backslow.TouchDown += delegate { Application.Instance._conn.Backslow(); };
            this.fwdfast.TouchDown += delegate { Application.Instance._conn.Fwdfast(); };
            this.fwdslow.TouchDown += delegate { Application.Instance._conn.Fwdslow(); };
            this.poweroff.TouchDown += delegate { Application.Instance.PowerOff(); };
            this.louder.TouchDown += delegate { Application.Instance._conn.Louder(); };
            this.vol1.TouchDown += delegate { Application.Instance._conn.Vol(1); };
            this.vol2.TouchDown += delegate { Application.Instance._conn.Vol(65); };
            this.vol3.TouchDown += delegate { Application.Instance._conn.Vol(80); };
            this.softer.TouchDown += delegate { Application.Instance._conn.Softer(); };
            this.subtitles.TouchDown += delegate { Application.Instance._conn.Subtitles(); };
            this.status.Text = "Locating Voo Server...";

            window.MakeKeyAndVisible();
            return true;
        }

        public void Browse(string path, string[] parts)
        {
            BrowserTableViewController b = new BrowserTableViewController(path, parts);
            if (path == ".") {
                b.Title = "Library";
                this.browsernav.ViewControllers = new UIViewController[] { b };
            } else {
                b.Title = Path.GetFileName(path.Replace("\\", "/"));
                this.browsernav.PushViewController(b, true);
            }
        }

        ulong _time, _length;
        Connection.PlayState _state;
        int _subtitle, _subtitlecount;
        bool _seekable;

        public void TimeChanged(ulong time) {
            _time = time;
            this.seeklabel.Text = Utils.to_time(_time) + " / " + Utils.to_time(_length);
        }
        public void StateChanged(Connection.PlayState state) {
            _state = state;
            this.play.SetTitle(_state == Connection.PlayState.Playing ? "Pause" : "Resume", UIControlState.Normal);
            this.play.Hidden = _state == Connection.PlayState.Stopped;
            this.seeklabel.Hidden = _state == Connection.PlayState.Stopped;
            this.subtitles.Hidden = _state == Connection.PlayState.Stopped || _subtitlecount == 0;
            this.backfast.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.backslow.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.fwdslow.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.fwdfast.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.seektitle.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
        }
        public void LengthChanged(ulong length) {
            _length = length;
            this.seeklabel.Text = Utils.to_time(_time) + " / " + Utils.to_time(_length);
        }
        public void SeekableChanged(bool seekable) {
	        _seekable = seekable;
            this.backfast.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.backslow.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.fwdslow.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.fwdfast.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.seektitle.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
        }
        public void SubtitleChanged(int subtitle) {
            _subtitle = subtitle;
            this.subtitles.Hidden = _state == Connection.PlayState.Stopped || _subtitlecount == 0;
            this.subtitles.SetTitle(String.Format ("Subtitles: {0}/{1}", _subtitle, _subtitlecount), UIControlState.Normal);
        }
        public void SubtitleCountChanged(int subtitlecount) {
            _subtitlecount = subtitlecount;
            this.subtitles.Hidden = _state == Connection.PlayState.Stopped || _subtitlecount == 0;
            this.subtitles.SetTitle(String.Format ("Subtitles: {0}/{1}", _subtitle, _subtitlecount), UIControlState.Normal);
        }
    }

    public partial class AppDelegateIPhone : UIApplicationDelegate, IVooApp
    {
        public override void OnActivated(UIApplication application)
        {
        }

        public void InvokeOnMainThread(ThreadStart cb)
        {
            this.window.InvokeOnMainThread(delegate { cb(); });
        }
        public void Connecting()
        {
            this.status.Text = "Connecting to Voo Server...";
        }
        public void FailConnecting()
        {
            this.status.Text = "Connection failed, Locating Voo Server again...";
        }
        public void Connected()
        {
            this.status.Text = "Ready.";

            this.mainnav.SelectedIndex = 0;
            this.window.AddSubview(this.mainnav.View);
        }
        public void Disconnected()
        {
            this.mainnav.View.RemoveFromSuperview();
            this.browsernav.PopToRootViewController(false);

            this.status.Text = "Disconnected. Locating Voo Server...";
        }

        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            UIApplication.SharedApplication.IdleTimerDisabled = true;

            Application.Instance.Init(this);

            this.play.TouchDown += delegate { Application.Instance._conn.TogglePause(); };
            this.stop.TouchDown += delegate { Application.Instance._conn.Stop(); };
            this.backfast.TouchDown += delegate { Application.Instance._conn.Backfast(); };
            this.backslow.TouchDown += delegate { Application.Instance._conn.Backslow(); };
            this.fwdfast.TouchDown += delegate { Application.Instance._conn.Fwdfast(); };
            this.fwdslow.TouchDown += delegate { Application.Instance._conn.Fwdslow(); };
            this.poweroff.TouchDown += delegate { Application.Instance.PowerOff(); };
            this.louder.TouchDown += delegate { Application.Instance._conn.Louder(); };
            this.vol1.TouchDown += delegate { Application.Instance._conn.Vol(1); };
            this.vol2.TouchDown += delegate { Application.Instance._conn.Vol(65); };
            this.vol3.TouchDown += delegate { Application.Instance._conn.Vol(80); };
            this.softer.TouchDown += delegate { Application.Instance._conn.Softer(); };
            this.subtitles.TouchDown += delegate { Application.Instance._conn.Subtitles(); };
            this.status.Text = "Locating Voo Server...";

            window.MakeKeyAndVisible();
            return true;
        }

        public void Browse(string path, string[] parts)
        {
            BrowserTableViewController b = new BrowserTableViewController(path, parts);
            if (path == ".") {
                b.Title = "Library";
                this.browsernav.ViewControllers = new UIViewController[] { b };
            } else {
                b.Title = Path.GetFileName(path.Replace("\\", "/"));
                this.browsernav.PushViewController(b, true);
            }
        }

        ulong _time, _length;
        Connection.PlayState _state;
        int _subtitle, _subtitlecount;
        bool _seekable;

        public void TimeChanged(ulong time) {
            _time = time;
            this.seeklabel.Text = Utils.to_time(_time) + " / " + Utils.to_time(_length);
        }
        public void StateChanged(Connection.PlayState state) {
            _state = state;
            this.play.SetTitle(_state == Connection.PlayState.Playing ? "Pause" : "Resume", UIControlState.Normal);
            this.play.Hidden = _state == Connection.PlayState.Stopped;
            this.seeklabel.Hidden = _state == Connection.PlayState.Stopped;
            this.subtitles.Hidden = _state == Connection.PlayState.Stopped || _subtitlecount == 0;
            this.backfast.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.backslow.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.fwdslow.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.fwdfast.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.seektitle.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
        }
        public void LengthChanged(ulong length) {
            _length = length;
            this.seeklabel.Text = Utils.to_time(_time) + " / " + Utils.to_time(_length);
        }
        public void SeekableChanged(bool seekable) {
	        _seekable = seekable;
            this.backfast.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.backslow.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.fwdslow.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.fwdfast.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
            this.seektitle.Hidden = _state == Connection.PlayState.Stopped || !_seekable;
        }
        public void SubtitleChanged(int subtitle) {
            _subtitle = subtitle;
            this.subtitles.Hidden = _state == Connection.PlayState.Stopped || _subtitlecount == 0;
            this.subtitles.SetTitle(String.Format ("Subtitles: {0}/{1}", _subtitle, _subtitlecount), UIControlState.Normal);
        }
        public void SubtitleCountChanged(int subtitlecount) {
            _subtitlecount = subtitlecount;
            this.subtitles.Hidden = _state == Connection.PlayState.Stopped || _subtitlecount == 0;
            this.subtitles.SetTitle(String.Format ("Subtitles: {0}/{1}", _subtitle, _subtitlecount), UIControlState.Normal);
        }
    }

    public partial class BrowserTableViewController : UITableViewController
    {
        static NSString CellID = new NSString("BrowserIdentifier");

        // Constructor invoked from the NIB loader
        public BrowserTableViewController(IntPtr p) : base(p)
        {
        }

        List<string> _dirs = new List<string>();
        List<string> _files = new List<string>();
        string _dir;
        UIBarButtonItem _buttonEdit;
        UIBarButtonItem _buttonDone;

        public BrowserTableViewController(string dir, IList<string> items)
        {
            _dir = dir;
            _dirs.Clear();
            _files.Clear();
            foreach (string i in items) {
                if (i[0] == 'D')
                    _dirs.Add(i.Substring(1));
                if (i[0] == 'F')
                    _files.Add(i.Substring(1));
            }

            _buttonEdit = new UIBarButtonItem(UIBarButtonSystemItem.Edit);
            _buttonDone = new UIBarButtonItem(UIBarButtonSystemItem.Done);
            _buttonEdit.Clicked += Handle_buttonEditClicked;
            _buttonDone.Clicked += Handle_buttonDoneClicked;

            NavigationItem.RightBarButtonItem = _buttonEdit;
        }

        void Handle_buttonDoneClicked(object sender, EventArgs e)
        {
            Editing = false;
            NavigationItem.RightBarButtonItem = _buttonEdit;
        }

        void Handle_buttonEditClicked(object sender, EventArgs e)
        {
            Editing = true;
            NavigationItem.RightBarButtonItem = _buttonDone;
        }

        // The data source for our TableView
        class DataSource : UITableViewDataSource
        {
            BrowserTableViewController tvc;

            public DataSource(BrowserTableViewController tableViewController)
            {
                this.tvc = tableViewController;
            }

            public override int RowsInSection(UITableView tableView, int section)
            {
                return this.tvc._dirs.Count + this.tvc._files.Count;
            }

            public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
            {
                var cell = tableView.DequeueReusableCell(CellID);
                if (cell == null) {
                    cell = new UITableViewCell(UITableViewCellStyle.Default, CellID);
                }

                if (indexPath.Row < this.tvc._dirs.Count) {
                    cell.TextLabel.Text = this.tvc._dirs[indexPath.Row];
                    cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                } else {
                    cell.TextLabel.Text = this.tvc._files[indexPath.Row - this.tvc._dirs.Count];
                    cell.Accessory = UITableViewCellAccessory.None;
                }
                return cell;
            }

            public override void CommitEditingStyle(UITableView tableView, UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
            {
                if (editingStyle == UITableViewCellEditingStyle.Delete) {
                    if (indexPath.Row < this.tvc._dirs.Count) {
                        string v = this.tvc._dirs[indexPath.Row];
                        Application.Instance._conn.DeleteDir(this.tvc._dir, v);
                        this.tvc._dirs.RemoveAt(indexPath.Row);
                    } else {
                        string v = this.tvc._files[indexPath.Row - this.tvc._dirs.Count];
                        Application.Instance._conn.DeleteFile(this.tvc._dir, v);
                        this.tvc._files.RemoveAt(indexPath.Row - this.tvc._dirs.Count);
                    }
                    tableView.DeleteRows(new[] { indexPath }, UITableViewRowAnimation.Fade);
                }
            }
        }

        // This class receives notifications that happen on the UITableView
        class TableDelegate : UITableViewDelegate
        {
            BrowserTableViewController tvc;

            public TableDelegate(BrowserTableViewController tableViewController)
            {
                this.tvc = tableViewController;
            }

            public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
            {
                if (indexPath.Row < this.tvc._dirs.Count) {
                    Application.Instance._conn.List(this.tvc._dir, this.tvc._dirs[indexPath.Row],
                                                    (parent,lines) => this.InvokeOnMainThread(delegate { Application.Instance._app.Browse(parent, lines); }));
                } else {
                    Application.Instance._conn.Play(this.tvc._dir, this.tvc._files[indexPath.Row - this.tvc._dirs.Count]);
                }
            }
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            TableView.Delegate = new TableDelegate(this);
            TableView.DataSource = new DataSource(this);
        }
    }


}
