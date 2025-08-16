using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ProPresenter_StageDisplayLayout_AutoSwitcher
{
    public class LogForm : Form
    {
        private readonly TextBox _textBox;

        public LogForm()
        {
            Text = "ProPresenter Auto Layout Switcher - Logs";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 900;
            Height = 600;

            _textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 9f)
            };


            Controls.Add(_textBox);

            // Preload existing logs from the bridge buffer so the UI shows logs from the start.
            try
            {
                var snapshot = SerilogUiBridge.GetAllLinesSnapshot();
                if (snapshot.Count > 0)
                {
                    _textBox.SuspendLayout();
                    foreach (var line in snapshot)
                    {
                        AppendLine(line);
                    }
                    _textBox.ResumeLayout();
                }
            }
            catch { /* ignore preload issues */ }

            // Subscribe to log events coming directly from Serilog
            SerilogUiBridge.LineEmitted += OnLogLineEmitted;
            FormClosed += (_, _) => SerilogUiBridge.LineEmitted -= OnLogLineEmitted;
        }

        private void OnLogLineEmitted(string line)
        {
            if (IsHandleCreated && !IsDisposed)
            {
                try
                {
                    if (InvokeRequired)
                    {
                        BeginInvoke(new Action<string>(AppendLine), line);
                    }
                    else
                    {
                        AppendLine(line);
                    }
                }
                catch
                {
                    // ignore UI errors on shutdown
                }
            }
        }

        public void AppendLine(string text)
        {
            _textBox.AppendText(text);
            if (!text.EndsWith(Environment.NewLine))
                _textBox.AppendText(Environment.NewLine);
        }
    }
}
