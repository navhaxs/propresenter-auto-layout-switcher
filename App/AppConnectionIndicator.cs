using System;

namespace ProPresenter_StageDisplayLayout_AutoSwitcher
{
    // Simple app-wide connection indicator bridge between core logic and UI
    public static class AppConnectionIndicator
    {
        private static readonly object Sync = new();
        private static bool _isConnected;

        public static bool IsConnected
        {
            get { lock (Sync) return _isConnected; }
        }

        public static event Action<bool>? ConnectionChanged;

        internal static void SetConnected(bool connected)
        {
            bool changed;
            lock (Sync)
            {
                if (_isConnected == connected) return;
                _isConnected = connected;
                changed = true;
            }
            if (changed)
            {
                try { ConnectionChanged?.Invoke(connected); } catch { /* ignore */ }
            }
        }
    }
}
