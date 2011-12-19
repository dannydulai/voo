using System;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Drawing;
using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;

namespace vooserver
{
    public partial class AppDelegate : NSApplicationDelegate
    {
        public AppDelegate () {
        }

        Server _server;

        public override void FinishedLaunching (NSObject notification)
        {
            _server = new Server();
        }
    }
}

