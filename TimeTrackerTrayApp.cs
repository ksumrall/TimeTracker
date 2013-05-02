using System;
using System.Collections.Generic;
using System.Configuration;
using System.ServiceProcess;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace TimeTracker
{
    class TimeTrackerTrayApp : Form
    {
        #region member variables

        TaskbarNotifier taskbarNotifier1;
        static string _effort = "goofing off";
        static IntPtr _foregroundWindowHandle = IntPtr.Zero;
        static string _cacheTitle = "";
        protected Timer timer = new Timer();

        private string _logFile = "c:\\temp\\effortlog.txt";

        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;

        IntPtr _hhook1;
        IntPtr _hhook2;

        const int MINUTES_BETWEEN_PROMPTS = 15;

        #region capture foreground window title definitions

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        #endregion

        #region active window change notification definitions

        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr
           hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess,
           uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        //  DWORD GetWindowThreadProcessId(
        //      __in   HWND hWnd,
        //      __out  LPDWORD lpdwProcessId
        //  );
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        //HANDLE WINAPI OpenProcess(
        //  __in  DWORD dwDesiredAccess,
        //  __in  BOOL bInheritHandle,
        //  __in  DWORD dwProcessId
        //);
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        //  DWORD WINAPI GetModuleFileNameEx(
        //      __in      HANDLE hProcess,
        //      __in_opt  HMODULE hModule,
        //      __out     LPTSTR lpFilename,
        //      __in      DWORD nSize
        //  );
        [DllImport("psapi.dll")]
        private static extern uint GetModuleFileNameEx(IntPtr hWnd, IntPtr hModule, StringBuilder lpFileName, int nSize);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint GetWindowModuleFileName(IntPtr hwnd,
           StringBuilder lpszFileName, uint cchFileNameMax);

        // Constants from winuser.h
        const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
        const uint EVENT_SYSTEM_FOREGROUND = 3;
        const uint WINEVENT_OUTOFCONTEXT = 0;

        #endregion

        #region computer lock notification definitions

        //[DllImport("wtsapi32.dll")]
        //private static extern bool WTSRegisterSessionNotification(IntPtr hWnd,
        //int dwFlags);

        //[DllImport("wtsapi32.dll")]
        //private static extern bool WTSUnRegisterSessionNotification(IntPtr
        //hWnd);

        //private const int NotifyForThisSession = 0; // This session only

        //private const int SessionChangeMessage = 0x02B1;
        //private const int SessionLockParam = 0x7;
        //private const int SessionUnlockParam = 0x8;

        #endregion

        // Need to ensure delegate is not collected while we're using it,
        // storing it in a class field is simplest way to do this.
        static WinEventDelegate procDelegate = new WinEventDelegate(WinEventProc);

        #endregion

        static void Main(string[] args)
        {
            Application.Run(new TimeTrackerTrayApp());
        }

        #region constructor

        public TimeTrackerTrayApp()
        {
            _logFile = ConfigurationManager.AppSettings["LogFile"];

            Microsoft.Win32.SystemEvents.SessionSwitch += new Microsoft.Win32.SessionSwitchEventHandler(SystemEvents_SessionSwitch);
            trayMenu = new System.Windows.Forms.ContextMenu();
            trayMenu.MenuItems.Add("Update Effort", OnPromptForCurrentEffort);
            trayMenu.MenuItems.Add("Exit", OnExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Time Tracker";
            trayIcon.Icon = new Icon(typeof(TimeTrackerTrayApp), "stopwatch.ico");
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;

            taskbarNotifier1 = new TaskbarNotifier();
            taskbarNotifier1.SetBackgroundBitmap(new Bitmap(GetType(), "skin.bmp"), Color.FromArgb(255, 0, 255));
            taskbarNotifier1.SetCloseBitmap(new Bitmap(GetType(), "close.bmp"), Color.FromArgb(255, 0, 255), new Point(177, 8));
            taskbarNotifier1.TitleRectangle = new Rectangle(50, 9, 120, 25);
            taskbarNotifier1.ContentRectangle = new Rectangle(8, 41, 180, 68);
            taskbarNotifier1.TitleClick += new EventHandler(taskbarNotifier1_TitleClick);
            taskbarNotifier1.ContentClick += new EventHandler(taskbarNotifier1_ContentClick);
            taskbarNotifier1.CloseClick += new EventHandler(taskbarNotifier1_CloseClick);
            taskbarNotifier1.OnHide += new EventHandler(taskbarNotifier1_OnHide);

            #region listen for computer lock

            //WTSRegisterSessionNotification(this.Handle, NotifyForThisSession);

            #endregion

            #region listen for active window change

            // Listen for foreground changes across all processes/threads on current desktop...
            _hhook1 = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
                    procDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            _hhook2 = SetWinEventHook(EVENT_OBJECT_NAMECHANGE, EVENT_OBJECT_NAMECHANGE, IntPtr.Zero,
                    procDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            #endregion

            timer.Enabled = true;
            timer.Interval = ((60 * MINUTES_BETWEEN_PROMPTS) * 1000); // 60 seconds * min * millisecond
            timer.Tick += new EventHandler(OnTimer);

            ShowPopup();
        }

        //protected override void WndProc(ref Message m)
        //{
        //    // check for session change notifications
        //    //if (m.Msg == SessionChangeMessage)
        //    //{
        //    //    if (m.WParam.ToInt32() == SessionLockParam)
        //    //        OnSessionLock(); // Do something when locked
        //    //    else if (m.WParam.ToInt32() == SessionUnlockParam)
        //    //        OnSessionUnlock(); // Do something when unlocked
        //    //}

        //    base.WndProc(ref m);
        //    return;
        //}

        protected void SystemEvents_SessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock)
            {
                OnSessionLock();
            }
            if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock)
            {
                OnSessionUnlock();
            }
        }

        #endregion

        #region TimeTrackerTrayApp events

        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;
            timer.Start();

            base.OnLoad(e);
        }

        protected void OnExit(object obj, EventArgs ea)
        {
            UnhookWinEvent(_hhook1);
            UnhookWinEvent(_hhook2);
            //WTSUnRegisterSessionNotification(this.Handle);
            trayIcon.Visible = false;
            Application.Exit();
        }

        protected void OnPromptForCurrentEffort(object obj, EventArgs ea)
        {
            PromptForCurrentEffort();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                trayIcon.Dispose();
            }

            base.Dispose(disposing);
        }

        protected void OnTimer(object obj, EventArgs ea)
        {
            ShowPopup();
        }

        #endregion

        #region TaskbarNotifier events

        void taskbarNotifier1_CloseClick(object obj, EventArgs ea)
        {
        }

        void taskbarNotifier1_TitleClick(object obj, EventArgs ea)
        {
            //MessageBox.Show("Title was Clicked");
        }

        void taskbarNotifier1_ContentClick(object obj, EventArgs ea)
        {
            PromptForCurrentEffort();
        }

        void taskbarNotifier1_OnHide(object obj, EventArgs ea)
        {
            LogEffort(_logFile);
        }

        #endregion

        #region private implementation

        private void ShowPopup()
        {
            taskbarNotifier1.Show("What are you working on?", _effort, Int32.Parse("500"), Int32.Parse("10000"), Int32.Parse("5000"));
        }

        private static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            IntPtr handle = IntPtr.Zero;
            StringBuilder Buff = new StringBuilder(nChars);
            handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

        static void WinEventProc(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            switch (eventType)
            {
                case EVENT_OBJECT_NAMECHANGE:
                    if (hwnd == _foregroundWindowHandle && IsWindowVisible(hwnd))
                    {
                        LogWinEvent(hwnd);
                    }
                    break;

                case EVENT_SYSTEM_FOREGROUND:
                    if (IsWindowVisible(hwnd))
                    {
                        _foregroundWindowHandle = GetForegroundWindow();
                        LogWinEvent(hwnd);
                    }
                    break;
            }

        }

        private static void LogWinEvent(IntPtr hwnd)
        {
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            string curTitle;
            string moduleName;
            string logFile = ConfigurationManager.AppSettings["LogFile"];

            GetWindowText(hwnd, buff, nChars);
            curTitle = buff.ToString();

            if (curTitle != _cacheTitle)
            {
                _cacheTitle = curTitle;
                moduleName = GetTopWindowName(hwnd);
                System.IO.File.AppendAllText(logFile
                    , string.Format("{0}, {1}, {2}, {3}{4}"
                        , DateTime.Now.ToString("G")
                        , hwnd.ToString()
                        , moduleName
                        , curTitle
                        , Environment.NewLine
                    )
                );
            }
        }

        private static void LogEffort(string logFile)
        {
            System.IO.File.AppendAllText(logFile, string.Format("=========================={0}", Environment.NewLine));
            System.IO.File.AppendAllText(logFile, string.Format("{0}, {1}{2}", DateTime.Now.ToString("G"), _effort, Environment.NewLine));
        }

        private void PromptForCurrentEffort()
        {
            taskbarNotifier1.Pause();
            string value = _effort;
            if (TimeTrackerTrayApp.InputBox("Current Errort", "What are you currently working on.", ref value) == DialogResult.OK)
            {
                _effort = value;
                taskbarNotifier1.Hide();
                LogEffort(_logFile);
                timer.Stop();
                timer.Start();
            }
            else
            {
                taskbarNotifier1.Resume();
            }
        }

        public static string GetTopWindowName(IntPtr hWnd)
        {
            StringBuilder lpszFileName = new StringBuilder(1000);
            uint lpdwProcessId;

            if (GetWindowThreadProcessId(hWnd, out lpdwProcessId) > 0)
            {
                IntPtr hProcess = OpenProcess(0x0410, false, lpdwProcessId);

                GetModuleFileNameEx(hProcess, IntPtr.Zero, lpszFileName, lpszFileName.Capacity);

                CloseHandle(hProcess);
            }

            return lpszFileName.ToString();
        }

        void OnSessionLock()
        {
            timer.Enabled = false;
            System.IO.File.AppendAllText(_logFile, string.Format("#######################################################################{0}", Environment.NewLine));
            System.IO.File.AppendAllText(_logFile, string.Format("{0}, {1}{2}", DateTime.Now.ToString("G"), "Work Station Locked", Environment.NewLine));
        }

        void OnSessionUnlock()
        {
            System.IO.File.AppendAllText(_logFile, string.Format("{0}, {1}{2}", DateTime.Now.ToString("G"), "Work Station UnLocked", Environment.NewLine));
            System.IO.File.AppendAllText(_logFile, string.Format("#######################################################################{0}", Environment.NewLine));
            timer.Enabled = true;
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TimeTrackerTrayApp));
            this.SuspendLayout();
            // 
            // TimeTrackerTrayApp
            // 
            this.ClientSize = new System.Drawing.Size(284, 262);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "TimeTrackerTrayApp";
            this.ResumeLayout(false);

        }

        private static DialogResult InputBox(string title, string promptText, ref string value)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }
        
        #endregion
    }
}
