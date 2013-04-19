using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TimeTracker
{
    partial class _TimeTrackingService : ServiceBase
    {
        delegate string ReadLineDelegate();
        IntPtr _hWnd;
        NotifyIcon notifyIcon;

        [DllImport("User32.Dll", EntryPoint = "PostMessageA")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        const int VK_RETURN = 0x0D;
        const int WM_KEYDOWN = 0x100;
        const int WM_CHAR = 0x0102;

        public _TimeTrackingService()
        {
            InitializeComponent();
            notifyIcon = new NotifyIcon();
            //notifyIcon.Icon = System.Windows.Forms.ToolTipIcon.Warning;
        }

        protected override void OnStart(string[] args)
        {
            // TODO: Add code here to start your service.
            ThreadStart ts = new ThreadStart(Run);
            Thread thread = new Thread(ts);
            thread.Start();

            base.OnStart(args);
        }

        protected override void OnStop()
        {
            // TODO: Add code here to perform any tear-down necessary to stop your service.
            base.OnStop();
        }

        private void Run()
        {
            _hWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

            var effort = "goofing off";
            while (effort.ToUpper() != "END")
            {
                // get effort
                effort = PromptForEffort(effort);

                // log effort
                System.IO.File.AppendAllText("c:\\temp\\effortlog.txt", string.Format("{0}, {1}{2}", DateTime.Now.ToString("G"), effort, Environment.NewLine));

                // sleep
                //Console.WriteLine("Sleeping...");
                Thread.Sleep((5 * 60) * 1000);
            }
            //Console.WriteLine("Press any key to exit...");
            //Console.ReadKey();
        }

        private string PromptForEffort(string defaultEffort)
        {
            //Console.WriteLine(string.Format("What are you currently working on?({0})", defaultEffort));
            notifyIcon.BalloonTipTitle = "What are you currently working on?";
            notifyIcon.BalloonTipText = defaultEffort;
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(5000);

            //return ReadLine(_hWnd, 20000, defaultEffort);
            return defaultEffort;
        }

        private string ReadLine(IntPtr hWnd, int timeoutms, string defaultMsg)
        {
            string resultstr;
            ReadLineDelegate d = Console.ReadLine;
            IAsyncResult result = d.BeginInvoke(null, null);
            result.AsyncWaitHandle.WaitOne(timeoutms);
            if (result.IsCompleted)
            {
                resultstr = d.EndInvoke(result);
                Console.WriteLine("Read: " + resultstr);
            }
            else
            {
                foreach (char chr in defaultMsg.ToCharArray())
                {
                    PostMessage(hWnd, WM_CHAR, (int)chr, 0);
                }
                PostMessage(hWnd, WM_KEYDOWN, VK_RETURN, 0);
                d.EndInvoke(result);
                resultstr = defaultMsg;
            }

            return resultstr;
        }

    }
}
