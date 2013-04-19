using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Reflection;
using System.IO;

namespace TimeTracker
{
    public class _TimeTracking
    {
        NotifyIcon notifyIcon;

        public _TimeTracking()
        {
            notifyIcon = new NotifyIcon();
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            Stream IconResourceStream = currentAssembly.GetManifestResourceStream("Icon1.ico");
            notifyIcon.Icon = new Icon(IconResourceStream);
            //notifyIcon.Container = 
        }

        public string PromptForEffort(string defaultEffort)
        {
            //Console.WriteLine(string.Format("What are you currently working on?({0})", defaultEffort));
            notifyIcon.BalloonTipTitle = "What are you currently working on?";
            notifyIcon.BalloonTipText = defaultEffort;
            notifyIcon.Text = "Text Attribute";
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(20000);

            //return ReadLine(_hWnd, 20000, defaultEffort);
            return defaultEffort;
        }

    }
}
