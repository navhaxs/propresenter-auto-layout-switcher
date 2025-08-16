using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ProPresenter_StageDisplayLayout_AutoSwitcher
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private LogForm? _logForm;

        // Static reference to marshal external UI requests (like single-instance show logs)
        private static SynchronizationContext? s_sync;
        private static TrayApplicationContext? s_instance;

        public static void RequestShowLogs()
        {
            var inst = s_instance;
            if (inst == null) return;

            void Do() { inst.ShowLogs(); }

            if (s_sync != null)
                s_sync.Post(_ => Do(), null);
            else
                Do();
        }

        public TrayApplicationContext()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = TrayIconHelper.RedDot,
                Text = "ProPresenter Auto Layout Switcher",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();
            var showItem = new ToolStripMenuItem("Show Logs", null, (_, _) => ShowLogs());
            var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());
            contextMenu.Items.Add(showItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (_, _) => ShowLogs();

            // Subscribe to connection changes to update tray icon
            try
            {
                var sync = System.Threading.SynchronizationContext.Current;
                AppConnectionIndicator.ConnectionChanged += connected =>
                {
                    void Update()
                    {
                        _notifyIcon.Icon = connected ? TrayIconHelper.GreenDot : TrayIconHelper.RedDot;
                        _notifyIcon.Text = connected
                            ? "ProPresenter Auto Layout Switcher - Connected"
                            : "ProPresenter Auto Layout Switcher - Disconnected";
                    }

                    if (sync != null)
                        sync.Post(_ => Update(), null);
                    else
                        Update();
                };
            }
            catch { /* ignore wiring issues */ }

            // Small balloon tip on start
            try
            {
                _notifyIcon.BalloonTipTitle = "Auto Layout Switcher";
                _notifyIcon.BalloonTipText = "Running in tray. Right-click to open logs.";
                _notifyIcon.ShowBalloonTip(3000);
            }
            catch { /* ignore if balloon not supported */ }

            // Expose this instance for cross-process UI requests
            try
            {
                s_sync = SynchronizationContext.Current;
                s_instance = this;
            }
            catch { /* ignore */ }
        }

        private void ShowLogs()
        {
            if (_logForm == null || _logForm.IsDisposed)
                _logForm = new LogForm();

            if (_logForm.WindowState == FormWindowState.Minimized)
                _logForm.WindowState = FormWindowState.Normal;

            _logForm.Show();
            _logForm.BringToFront();
            _logForm.Activate();
        }

        private void ExitApplication()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _logForm?.Close();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _notifyIcon.Dispose();
                _logForm?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
