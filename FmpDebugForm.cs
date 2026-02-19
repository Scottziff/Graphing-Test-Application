using System;
using System.Drawing;
using System.Windows.Forms;

namespace Graphing_Test_Application
{
    // ============================================================
    // FmpDebugForm - floating log window for Get Data operations
    // Shows every FMP request and SQL operation in real time
    // ============================================================
    public class FmpDebugForm : Form
    {
        private RichTextBox _rtb;
        private Button _btnClear;
        private Button _btnClose;
        private Label _lblSummary;

        public FmpDebugForm()
        {
            this.Text = "Get Data — Debug Log";
            this.Size = new Size(820, 560);
            this.StartPosition = FormStartPosition.Manual;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(20, 20, 20);

            var pnlTop = new Panel();
            pnlTop.Dock = DockStyle.Top;
            pnlTop.Height = 36;
            pnlTop.BackColor = Color.FromArgb(30, 30, 30);

            _lblSummary = new Label();
            _lblSummary.AutoSize = false;
            _lblSummary.Dock = DockStyle.Fill;
            _lblSummary.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _lblSummary.ForeColor = Color.LightGray;
            _lblSummary.TextAlign = ContentAlignment.MiddleLeft;
            _lblSummary.Padding = new Padding(8, 0, 0, 0);
            _lblSummary.Text = "Waiting...";

            _btnClear = new Button();
            _btnClear.Text = "Clear";
            _btnClear.Size = new Size(60, 24);
            _btnClear.Dock = DockStyle.Right;
            _btnClear.FlatStyle = FlatStyle.Flat;
            _btnClear.ForeColor = Color.White;
            _btnClear.BackColor = Color.FromArgb(60, 60, 60);
            _btnClear.FlatAppearance.BorderSize = 0;
            _btnClear.Click += (s, e) => _rtb.Clear();

            _btnClose = new Button();
            _btnClose.Text = "Close";
            _btnClose.Size = new Size(60, 24);
            _btnClose.Dock = DockStyle.Right;
            _btnClose.FlatStyle = FlatStyle.Flat;
            _btnClose.ForeColor = Color.White;
            _btnClose.BackColor = Color.FromArgb(60, 60, 60);
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Click += (s, e) => this.Hide();

            pnlTop.Controls.Add(_lblSummary);
            pnlTop.Controls.Add(_btnClear);
            pnlTop.Controls.Add(_btnClose);

            _rtb = new RichTextBox();
            _rtb.Dock = DockStyle.Fill;
            _rtb.BackColor = Color.FromArgb(15, 15, 15);
            _rtb.ForeColor = Color.LightGray;
            _rtb.Font = new Font("Consolas", 8.5F);
            _rtb.ReadOnly = true;
            _rtb.WordWrap = false;
            _rtb.ScrollBars = RichTextBoxScrollBars.Both;
            _rtb.BorderStyle = BorderStyle.None;

            this.Controls.Add(_rtb);
            this.Controls.Add(pnlTop);
        }

        // ── Logging helpers ────────────────────────────────────────

        public void SetSummary(string text)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetSummary(text))); return; }
            _lblSummary.Text = text;
        }

        public void LogSection(string title)
        {
            AppendLine($"\n{'═',0}══════════════════════════════════════════", Color.DimGray);
            AppendLine($"  {title}", Color.White);
            AppendLine($"{'═',0}══════════════════════════════════════════", Color.DimGray);
        }

        public void LogInfo(string msg) => AppendLine($"  {Ts()} {msg}", Color.LightGray);

        public void LogFmpRequest(string safeUrl)
        {
            AppendLine($"  {Ts()} → FMP  ", Color.Cyan, newline: false);
            AppendLine(safeUrl, Color.DodgerBlue);
        }

        public void LogFmpResponse(int bars, string earliest, string latest, bool empty = false)
        {
            if (empty)
                AppendLine($"  {Ts()} ← FMP  0 bars (empty response)", Color.Orange);
            else
                AppendLine($"  {Ts()} ← FMP  {bars} bars  [{earliest}  →  {latest}]", Color.LimeGreen);
        }

        public void LogFmpError(string msg)
            => AppendLine($"  {Ts()} ✗ FMP  {msg}", Color.Tomato);

        public void LogSql(string msg) => AppendLine($"  {Ts()} ▶ SQL  {msg}", Color.Goldenrod);

        public void LogSqlOk(string msg) => AppendLine($"  {Ts()} ✓ SQL  {msg}", Color.PaleGreen);

        public void LogSqlError(string msg) => AppendLine($"  {Ts()} ✗ SQL  {msg}", Color.Tomato);

        public void LogWarning(string msg) => AppendLine($"  {Ts()} ⚠ {msg}", Color.Orange);

        public void LogSuccess(string msg) => AppendLine($"  {Ts()} ✓ {msg}", Color.LimeGreen);

        // ── Private helpers ────────────────────────────────────────

        private static string Ts() => DateTime.Now.ToString("HH:mm:ss.fff");

        private void AppendLine(string text, Color color, bool newline = true)
        {
            if (InvokeRequired) { Invoke(new Action(() => AppendLine(text, color, newline))); return; }
            int start = _rtb.TextLength;
            _rtb.AppendText(newline ? text + "\n" : text);
            _rtb.Select(start, text.Length);
            _rtb.SelectionColor = color;
            _rtb.SelectionLength = 0;
            _rtb.ScrollToCaret();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Never destroy — just hide, so logs survive
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
            else base.OnFormClosing(e);
        }
    }
}
