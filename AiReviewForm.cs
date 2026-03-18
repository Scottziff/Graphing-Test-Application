using System;
using System.Drawing;
using System.Windows.Forms;

namespace Graphing_Test_Application
{
    // ============================================================
    // AiReviewForm — floating window for month-end AI analysis
    // Shows news titles, executive summary, and Buy/Hold/Sell
    // signal for each month, plus a running tally.
    // ============================================================
    public class AiReviewForm : Form
    {
        private RichTextBox _rtb;
        private Button _btnClear;
        private Button _btnClose;
        private Label _lblSummary;
        private Panel _pnlTally;
        private Label _lblBuy;
        private Label _lblSell;
        private Label _lblHold;

        private int _buyCount;
        private int _sellCount;
        private int _holdCount;

        public AiReviewForm()
        {
            this.Text = "AI Review — Month-End Analysis";
            this.Size = new Size(900, 660);
            this.StartPosition = FormStartPosition.Manual;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.TopMost = false;
            this.ShowInTaskbar = true;
            this.BackColor = Color.FromArgb(18, 18, 24);

            // ── Top bar (summary + buttons) ──────────────────────────
            var pnlTop = new Panel();
            pnlTop.Dock = DockStyle.Top;
            pnlTop.Height = 36;
            pnlTop.BackColor = Color.FromArgb(28, 28, 38);

            _lblSummary = new Label();
            _lblSummary.AutoSize = false;
            _lblSummary.Dock = DockStyle.Fill;
            _lblSummary.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _lblSummary.ForeColor = Color.LightGray;
            _lblSummary.TextAlign = ContentAlignment.MiddleLeft;
            _lblSummary.Padding = new Padding(8, 0, 0, 0);
            _lblSummary.Text = "Waiting for analysis...";

            _btnClear = new Button();
            _btnClear.Text = "Clear";
            _btnClear.Size = new Size(60, 24);
            _btnClear.Dock = DockStyle.Right;
            _btnClear.FlatStyle = FlatStyle.Flat;
            _btnClear.ForeColor = Color.White;
            _btnClear.BackColor = Color.FromArgb(55, 55, 70);
            _btnClear.FlatAppearance.BorderSize = 0;
            _btnClear.Click += (s, e) => { _rtb.Clear(); ResetTally(); };

            _btnClose = new Button();
            _btnClose.Text = "Close";
            _btnClose.Size = new Size(60, 24);
            _btnClose.Dock = DockStyle.Right;
            _btnClose.FlatStyle = FlatStyle.Flat;
            _btnClose.ForeColor = Color.White;
            _btnClose.BackColor = Color.FromArgb(55, 55, 70);
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Click += (s, e) => this.Hide();

            pnlTop.Controls.Add(_lblSummary);
            pnlTop.Controls.Add(_btnClear);
            pnlTop.Controls.Add(_btnClose);

            // ── Tally bar (BUY / SELL / HOLD counts) ─────────────────
            _pnlTally = new Panel();
            _pnlTally.Dock = DockStyle.Top;
            _pnlTally.Height = 32;
            _pnlTally.BackColor = Color.FromArgb(24, 24, 34);

            _lblBuy = MakeTallyLabel("BUY: 0", Color.LimeGreen);
            _lblSell = MakeTallyLabel("SELL: 0", Color.Tomato);
            _lblHold = MakeTallyLabel("HOLD: 0", Color.White);

            _lblBuy.Dock = DockStyle.Left;
            _lblBuy.Width = 120;
            _lblSell.Dock = DockStyle.Left;
            _lblSell.Width = 120;
            _lblHold.Dock = DockStyle.Left;
            _lblHold.Width = 120;

            _pnlTally.Controls.Add(_lblHold);
            _pnlTally.Controls.Add(_lblSell);
            _pnlTally.Controls.Add(_lblBuy);

            // ── Main log area ─────────────────────────────────────────
            _rtb = new RichTextBox();
            _rtb.Dock = DockStyle.Fill;
            _rtb.BackColor = Color.FromArgb(14, 14, 20);
            _rtb.ForeColor = Color.LightGray;
            _rtb.Font = new Font("Consolas", 9F);
            _rtb.ReadOnly = true;
            _rtb.WordWrap = true;
            _rtb.ScrollBars = RichTextBoxScrollBars.Vertical;
            _rtb.BorderStyle = BorderStyle.None;

            this.Controls.Add(_rtb);
            this.Controls.Add(_pnlTally);
            this.Controls.Add(pnlTop);
        }

        // ── Public API ─────────────────────────────────────────────

        public void ResetTally()
        {
            if (InvokeRequired) { Invoke(new Action(ResetTally)); return; }
            _buyCount = 0; _sellCount = 0; _holdCount = 0;
            UpdateTallyLabels();
        }

        public void SetSummary(string text)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetSummary(text))); return; }
            _lblSummary.Text = text;
        }

        public void LogSection(string title)
        {
            AppendLine("", Color.DimGray);
            AppendLine("══════════════════════════════════════════════════════", Color.FromArgb(60, 70, 100));
            AppendLine($"  {title}", Color.White, bold: true);
            AppendLine("══════════════════════════════════════════════════════", Color.FromArgb(60, 70, 100));
        }

        public void LogMonthHeader(DateTime date, int titleCount)
        {
            AppendLine("", Color.DimGray);
            AppendLine($"▶  {date:MMMM yyyy}  ({date:MMM d, yyyy})  —  {titleCount} headline{(titleCount == 1 ? "" : "s")}",
                Color.CornflowerBlue, bold: true);
        }

        public void LogTitle(int index, string title)
        {
            AppendLine($"   {index,2}. {title}", Color.Silver);
        }

        public void LogNoTitles()
        {
            AppendLine("       (no headlines found — skipped)", Color.DimGray);
        }

        public void LogSql(string msg)
        {
            AppendLine($"   ▶ SQL  {msg}", Color.Goldenrod);
        }

        public void LogSqlOk(string msg)
        {
            AppendLine($"   ✓ SQL  {msg}", Color.PaleGreen);
        }

        public void LogSqlError(string msg)
        {
            AppendLine($"   ✗ SQL  {msg}", Color.Tomato);
        }

        public void LogApiCall()
        {
            AppendLine($"   → Sending to Claude...", Color.DodgerBlue);
        }

        public void LogApiError(string msg)
        {
            AppendLine($"   ✗ API error: {msg}", Color.Tomato);
        }

        public void LogApiRaw(string msg)
        {
            AppendLine($"   ← API raw: {msg}", Color.Orange);
        }

        public void LogSummaryText(string text)
        {
            // Strip the trailing signal word line before displaying
            string trimmed = text.Trim();
            var lines = trimmed.Split('\n');
            foreach (string line in lines)
            {
                string l = line.Trim();
                if (string.IsNullOrEmpty(l)) continue;
                string lu = l.ToUpper();
                // Skip bare signal word lines
                if (lu == "BUY" || lu == "SELL" || lu == "HOLD") continue;
                AppendLine($"   {l}", Color.LightGray);
            }
        }

        public void LogDiag(string msg)
        {
            AppendLine($"   ℹ {msg}", Color.DarkGray);
        }

        public void LogSignal(string signal)
        {
            Color color;
            string label;
            switch (signal)
            {
                case "BUY":
                    color = Color.LimeGreen;
                    label = "▲  BUY";
                    _buyCount++;
                    break;
                case "SELL":
                    color = Color.Tomato;
                    label = "▼  SELL";
                    _sellCount++;
                    break;
                default:
                    color = Color.White;
                    label = "◆  HOLD";
                    _holdCount++;
                    break;
            }
            AppendLine($"   Signal:  {label}", color, bold: true);
            UpdateTallyLabels();
        }

        public void LogComplete(int totalMonths, int analyzed)
        {
            AppendLine("", Color.DimGray);
            AppendLine("══════════════════════════════════════════════════════", Color.FromArgb(60, 70, 100));
            AppendLine($"  COMPLETE — {totalMonths} months in range, {analyzed} analyzed",
                Color.LimeGreen, bold: true);
            AppendLine($"  BUY: {_buyCount}   SELL: {_sellCount}   HOLD: {_holdCount}", Color.Gold, bold: true);
            AppendLine("══════════════════════════════════════════════════════", Color.FromArgb(60, 70, 100));
        }

        // ── Private helpers ────────────────────────────────────────

        private void UpdateTallyLabels()
        {
            if (InvokeRequired) { Invoke(new Action(UpdateTallyLabels)); return; }
            _lblBuy.Text  = $"▲ BUY: {_buyCount}";
            _lblSell.Text = $"▼ SELL: {_sellCount}";
            _lblHold.Text = $"◆ HOLD: {_holdCount}";
        }

        private static Label MakeTallyLabel(string text, Color color)
        {
            var lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lbl.ForeColor = color;
            lbl.TextAlign = ContentAlignment.MiddleCenter;
            lbl.AutoSize = false;
            return lbl;
        }

        private void AppendLine(string text, Color color, bool bold = false)
        {
            if (InvokeRequired) { Invoke(new Action(() => AppendLine(text, color, bold))); return; }
            int start = _rtb.TextLength;
            _rtb.AppendText(text + "\n");
            _rtb.Select(start, text.Length);
            _rtb.SelectionColor = color;
            if (bold)
                _rtb.SelectionFont = new Font(_rtb.Font, FontStyle.Bold);
            _rtb.SelectionLength = 0;
            _rtb.ScrollToCaret();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
            else base.OnFormClosing(e);
        }
    }
}
