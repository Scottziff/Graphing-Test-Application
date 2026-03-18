using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Microsoft.Data.SqlClient;

namespace Graphing_Test_Application
{
    // ============================================================
    // Form1 - Stock Price Candlestick Chart with Technical Indicators
    // ============================================================
    public partial class Form1 : Form
    {
        private readonly DatabaseConnection _db;
        private readonly DatabaseConfig _config;

        // --- Top bar controls ---
        private Panel pnlTop;
        private Label lblSymbol;
        private ComboBox cboSymbols;
        private Label lblDateRange;
        private Button btnGetData;
        private Label lblStatus;

        // --- Chart ---
        private Chart chartStock;

        // --- Indicator panel ---
        private Panel pnlIndicators;
        private Label lblIndicatorHeader;
        private CheckedListBox chkIndicators;
        private Panel pnlSignalColumn;
        private readonly Dictionary<string, TextBox> _signalTextBoxes = new Dictionary<string, TextBox>();
        private readonly Dictionary<string, int> _indicatorYPositions = new Dictionary<string, int>();

        // --- Pivot threshold controls ---
        private Label lblPivotThreshold;
        private TextBox txtPivotThreshold;
        private decimal _pivotThreshold = 8.0m;

        // --- Chart type radio buttons ---
        private RadioButton rbCandlestick;
        private RadioButton rbLine;

        // --- OHLCV data cache ---
        private List<DateTime> _cachedDates = new List<DateTime>();
        private List<decimal> _cachedOpens = new List<decimal>();
        private List<decimal> _cachedHighs = new List<decimal>();
        private List<decimal> _cachedLows = new List<decimal>();
        private List<decimal> _cachedCloses = new List<decimal>();
        private List<decimal> _cachedVolumes = new List<decimal>();

        // --- Indicator tracking ---
        private readonly List<IndicatorDefinition> _allIndicators;
        private readonly Dictionary<int, IndicatorDefinition> _indexToIndicator = new Dictionary<int, IndicatorDefinition>();
        private readonly HashSet<string> _activeIndicatorKeys = new HashSet<string>();

        // --- Signal cache ---
        private Dictionary<string, SignalResult> _signalCache = new Dictionary<string, SignalResult>();

        // --- Crosshair navigation ---
        private int _crosshairIndex = -1;
        private Label _lblCrosshairPrice;
        private Label _lblCrosshairPriceLeft;

        // --- Click hover info panel ---
        private Panel _pnlHover;
        private Label _lblHoverDate;
        private Label _lblHoverTime;
        private Label _lblHoverOHLC;

        // --- FMP debug log window ---
        private FmpDebugForm _debugLog;

        // --- Indicator tooltip ---
        private ToolTip _indicatorToolTip;
        private int _lastTooltipIndex = -1;

        // --- Pivot points ---
        private List<PivotPoint> _pivotPoints = new List<PivotPoint>();
        private Series _pivotPeakSeries;
        private Series _pivotTroughSeries;
        private readonly List<CalloutAnnotation> _pivotAnnotations = new List<CalloutAnnotation>();

        // --- Trading Simulation ---
        private Panel pnlSimulation;
        private Label lblSimHeader;
        private Label lblStartPosition;
        private RadioButton rbStartBuy;
        private RadioButton rbStartSell;
        private Label lblStrategy;
        private ComboBox cboStrategy;
        private Button btnRunSim;
        private Button btnClearSim;
        private DataGridView dgvTransactions;
        private Panel pnlSimSummary;
        private Label lblSimPnL;
        private Label lblSimCash;
        private Label lblSimShares;
        private Label lblSimStockValue;
        private SimulationResult _simulationResult;
        private Series _simBuySeries;
        private Series _simSellSeries;
        private readonly List<TextAnnotation> _simNumberAnnotations = new List<TextAnnotation>();

        // --- Events / Gap Analysis ---
        private List<StockEvent> _stockEvents = new List<StockEvent>();
        private List<GapBar> _detectedGaps = new List<GapBar>();
        private Dictionary<int, List<StockEvent>> _eventsByBarIndex = new Dictionary<int, List<StockEvent>>();
        private readonly List<TextAnnotation> _eventAnnotations = new List<TextAnnotation>();
        private Button btnEvents;
        private TextBox txtGapThreshold;
        private Label lblGapThreshold;
        private decimal _gapThreshold = 0.5m;
        private Label _lblHoverEvent;

        // --- Month-End AI Analysis ---
        private Button btnAiReview;
        private Series _aiSignalBuySeries;
        private Series _aiSignalSellSeries;
        private Series _aiSignalHoldSeries;
        private AiReviewForm _aiReviewForm;

        // --- Simulation-active state (locks indicators during sim) ---
        private bool _simulationActive = false;
        private readonly Dictionary<string, Color> _savedBuyCircleColors = new Dictionary<string, Color>();
        private readonly Dictionary<string, Color> _savedSellCircleColors = new Dictionary<string, Color>();
        private readonly Dictionary<string, string> _savedBuyCountTexts = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _savedSellCountTexts = new Dictionary<string, string>();

        // --- Pivot Point Transaction ---
        private Button btnPivotTransaction;

        // --- Pivot Analysis ---
        private Button btnAnalyze;
        private Panel pnlBuyAnalysis;
        private Panel pnlSellAnalysis;
        private Panel pnlBuyCountColumn;
        private Panel pnlSellCountColumn;
        private readonly Dictionary<string, Panel> _buyAnalysisCircles = new Dictionary<string, Panel>();
        private readonly Dictionary<string, Panel> _sellAnalysisCircles = new Dictionary<string, Panel>();
        private readonly Dictionary<string, Label> _buyCountLabels = new Dictionary<string, Label>();
        private readonly Dictionary<string, Label> _sellCountLabels = new Dictionary<string, Label>();

        // ============================================================
        // Constructor
        // ============================================================
        public Form1()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            InitializeComponent();

            _allIndicators = IndicatorRegistry.GetAll();

            BuildUI();

            _db = new DatabaseConnection();
            _config = _db.LoadConfig();

            _debugLog = new FmpDebugForm();

            // Hide until fully loaded so the user never sees a blank/partially-built form
            this.Opacity = 0;

            this.Load += async (s, e) =>
            {
                // await TestConnectionAsync();  // Debug dialog - uncomment when needed
                await LoadSymbolsAsync();
                this.Opacity = 1;
            };
        }

        // ============================================================
        // BuildUI - All controls created in code (not Designer)
        // ============================================================
        private void BuildUI()
        {
            this.SuspendLayout();

            // --- Top Panel ---
            pnlTop = new Panel();
            pnlTop.Dock = DockStyle.Top;
            pnlTop.Height = 45;
            pnlTop.BackColor = Color.WhiteSmoke;

            lblSymbol = new Label();
            lblSymbol.Text = "Symbol:";
            lblSymbol.AutoSize = true;
            lblSymbol.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblSymbol.Location = new Point(10, 12);

            cboSymbols = new ComboBox();
            cboSymbols.DropDownStyle = ComboBoxStyle.DropDown;
            cboSymbols.Font = new Font("Segoe UI", 10F);
            cboSymbols.Location = new Point(85, 8);
            cboSymbols.Size = new Size(160, 25);
            cboSymbols.Sorted = true;
            cboSymbols.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cboSymbols.AutoCompleteSource = AutoCompleteSource.ListItems;
            cboSymbols.MaxLength = 4;
            cboSymbols.SelectedIndexChanged += cboSymbols_SelectedIndexChanged;
            cboSymbols.KeyPress += cboSymbols_KeyPress;

            lblDateRange = new Label();
            lblDateRange.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            lblDateRange.ForeColor = Color.DimGray;
            lblDateRange.AutoSize = true;
            lblDateRange.Location = new Point(335, 14);
            lblDateRange.Text = "";

            btnGetData = new Button();
            btnGetData.Text = "Get Data";
            btnGetData.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnGetData.Location = new Point(248, 8);
            btnGetData.Size = new Size(80, 30);
            btnGetData.BackColor = Color.FromArgb(0, 150, 136);
            btnGetData.ForeColor = Color.White;
            btnGetData.FlatStyle = FlatStyle.Flat;
            btnGetData.FlatAppearance.BorderSize = 0;
            btnGetData.Click += btnGetData_Click;

            lblStatus = new Label();
            lblStatus.Font = new Font("Segoe UI", 9F);
            lblStatus.ForeColor = Color.DimGray;
            lblStatus.Size = new Size(300, 20);
            lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblStatus.Location = new Point(this.ClientSize.Width - 310, 14);
            lblStatus.TextAlign = ContentAlignment.MiddleRight;
            lblStatus.Text = "Ready";

            // --- Pivot Threshold controls ---
            lblPivotThreshold = new Label();
            lblPivotThreshold.Text = "Pivot %:";
            lblPivotThreshold.AutoSize = true;
            lblPivotThreshold.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblPivotThreshold.Location = new Point(540, 12);

            txtPivotThreshold = new TextBox();
            txtPivotThreshold.Font = new Font("Segoe UI", 10F);
            txtPivotThreshold.Location = new Point(615, 8);
            txtPivotThreshold.Size = new Size(55, 25);
            txtPivotThreshold.Text = _pivotThreshold.ToString("F1");
            txtPivotThreshold.TextAlign = HorizontalAlignment.Center;
            txtPivotThreshold.KeyDown += txtPivotThreshold_KeyDown;
            txtPivotThreshold.Leave += txtPivotThreshold_Leave;

            // --- Chart Type radio buttons ---
            rbCandlestick = new RadioButton();
            rbCandlestick.Text = "Candlestick";
            rbCandlestick.Font = new Font("Segoe UI", 9F);
            rbCandlestick.Location = new Point(685, 12);
            rbCandlestick.AutoSize = true;
            rbCandlestick.Checked = true;
            rbCandlestick.CheckedChanged += rbChartType_CheckedChanged;

            rbLine = new RadioButton();
            rbLine.Text = "Line";
            rbLine.Font = new Font("Segoe UI", 9F);
            rbLine.Location = new Point(795, 12);
            rbLine.AutoSize = true;
            rbLine.CheckedChanged += rbChartType_CheckedChanged;

            btnAnalyze = new Button();
            btnAnalyze.Text = "Analyze";
            btnAnalyze.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAnalyze.Location = new Point(855, 7);
            btnAnalyze.Size = new Size(80, 30);
            btnAnalyze.BackColor = Color.FromArgb(63, 81, 181);
            btnAnalyze.ForeColor = Color.White;
            btnAnalyze.FlatStyle = FlatStyle.Flat;
            btnAnalyze.FlatAppearance.BorderSize = 0;
            btnAnalyze.Click += btnAnalyze_Click;

            // Pivot navigation arrows
            var btnPivotLeft = new Button();
            btnPivotLeft.Text = "\u25C4";
            btnPivotLeft.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnPivotLeft.Location = new Point(940, 7);
            btnPivotLeft.Size = new Size(30, 30);
            btnPivotLeft.BackColor = Color.FromArgb(55, 71, 161);
            btnPivotLeft.ForeColor = Color.White;
            btnPivotLeft.FlatStyle = FlatStyle.Flat;
            btnPivotLeft.FlatAppearance.BorderSize = 0;
            btnPivotLeft.Click += btnPivotLeft_Click;

            var btnPivotRight = new Button();
            btnPivotRight.Text = "\u25BA";
            btnPivotRight.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnPivotRight.Location = new Point(973, 7);
            btnPivotRight.Size = new Size(30, 30);
            btnPivotRight.BackColor = Color.FromArgb(55, 71, 161);
            btnPivotRight.ForeColor = Color.White;
            btnPivotRight.FlatStyle = FlatStyle.Flat;
            btnPivotRight.FlatAppearance.BorderSize = 0;
            btnPivotRight.Click += btnPivotRight_Click;

            // Pivot Point Transaction button
            btnPivotTransaction = new Button();
            btnPivotTransaction.Text = "Pivot Point Transaction";
            btnPivotTransaction.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnPivotTransaction.Location = new Point(1010, 7);
            btnPivotTransaction.Size = new Size(175, 30);
            btnPivotTransaction.BackColor = Color.FromArgb(156, 39, 176);
            btnPivotTransaction.ForeColor = Color.White;
            btnPivotTransaction.FlatStyle = FlatStyle.Flat;
            btnPivotTransaction.FlatAppearance.BorderSize = 0;
            btnPivotTransaction.Click += btnPivotTransaction_Click;

            // Events button
            btnEvents = new Button();
            btnEvents.Text = "Events";
            btnEvents.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnEvents.Location = new Point(1195, 7);
            btnEvents.Size = new Size(75, 30);
            btnEvents.BackColor = Color.FromArgb(230, 119, 0);
            btnEvents.ForeColor = Color.White;
            btnEvents.FlatStyle = FlatStyle.Flat;
            btnEvents.FlatAppearance.BorderSize = 0;
            btnEvents.Click += btnEvents_Click;

            // AI Review button
            btnAiReview = new Button();
            btnAiReview.Text = "AI Review";
            btnAiReview.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnAiReview.Location = new Point(1278, 7);
            btnAiReview.Size = new Size(85, 30);
            btnAiReview.BackColor = Color.FromArgb(50, 120, 200);
            btnAiReview.ForeColor = Color.White;
            btnAiReview.FlatStyle = FlatStyle.Flat;
            btnAiReview.FlatAppearance.BorderSize = 0;
            btnAiReview.Click += btnAiReview_Click;

            // Gap % threshold controls
            lblGapThreshold = new Label();
            lblGapThreshold.Text = "Gap %:";
            lblGapThreshold.AutoSize = true;
            lblGapThreshold.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblGapThreshold.Location = new Point(1373, 13);

            txtGapThreshold = new TextBox();
            txtGapThreshold.Font = new Font("Segoe UI", 9F);
            txtGapThreshold.Location = new Point(1425, 9);
            txtGapThreshold.Size = new Size(48, 24);
            txtGapThreshold.Text = "0.5";
            txtGapThreshold.TextAlign = HorizontalAlignment.Center;
            txtGapThreshold.KeyDown += txtGapThreshold_KeyDown;
            txtGapThreshold.Leave    += txtGapThreshold_Leave;

            pnlTop.Controls.Add(lblSymbol);
            pnlTop.Controls.Add(cboSymbols);
            pnlTop.Controls.Add(btnGetData);
            pnlTop.Controls.Add(lblDateRange);
            pnlTop.Controls.Add(lblPivotThreshold);
            pnlTop.Controls.Add(txtPivotThreshold);
            pnlTop.Controls.Add(rbCandlestick);
            pnlTop.Controls.Add(rbLine);
            pnlTop.Controls.Add(btnAnalyze);
            pnlTop.Controls.Add(btnPivotLeft);
            pnlTop.Controls.Add(btnPivotRight);
            pnlTop.Controls.Add(btnPivotTransaction);
            pnlTop.Controls.Add(btnEvents);
            pnlTop.Controls.Add(btnAiReview);
            pnlTop.Controls.Add(lblGapThreshold);
            pnlTop.Controls.Add(txtGapThreshold);
            pnlTop.Controls.Add(lblStatus);

            // --- Indicator Panel (Left) ---
            pnlIndicators = new Panel();
            pnlIndicators.Dock = DockStyle.Left;
            pnlIndicators.Width = 375;
            pnlIndicators.BackColor = Color.FromArgb(245, 245, 250);
            pnlIndicators.BorderStyle = BorderStyle.FixedSingle;

            lblIndicatorHeader = new Label();
            lblIndicatorHeader.Text = "Technical Indicators";
            lblIndicatorHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblIndicatorHeader.ForeColor = Color.FromArgb(50, 50, 80);
            lblIndicatorHeader.Dock = DockStyle.Top;
            lblIndicatorHeader.Height = 30;
            lblIndicatorHeader.TextAlign = ContentAlignment.MiddleCenter;
            lblIndicatorHeader.BackColor = Color.FromArgb(230, 230, 240);

            // Signal column panel (right side, fixed width)
            pnlSignalColumn = new Panel();
            pnlSignalColumn.Dock = DockStyle.Right;
            pnlSignalColumn.Width = 75;
            pnlSignalColumn.BackColor = Color.FromArgb(245, 245, 250);

            chkIndicators = new CheckedListBox();
            chkIndicators.Dock = DockStyle.Fill;
            chkIndicators.Font = new Font("Segoe UI", 9F);
            chkIndicators.CheckOnClick = true;
            chkIndicators.BorderStyle = BorderStyle.None;
            chkIndicators.BackColor = Color.FromArgb(245, 245, 250);
            chkIndicators.DrawMode = DrawMode.OwnerDrawFixed;
            chkIndicators.ItemHeight = 22;
            chkIndicators.DrawItem += chkIndicators_DrawItem;
            chkIndicators.ItemCheck += chkIndicators_ItemCheck;
            chkIndicators.MouseMove += chkIndicators_MouseMove;
            chkIndicators.MouseLeave += (s, ev) =>
            {
                _indicatorToolTip.Hide(chkIndicators);
                _lastTooltipIndex = -1;
            };

            _indicatorToolTip = new ToolTip();
            _indicatorToolTip.AutoPopDelay = 15000;
            _indicatorToolTip.InitialDelay = 400;
            _indicatorToolTip.ReshowDelay = 200;
            _indicatorToolTip.BackColor = Color.FromArgb(40, 40, 60);
            _indicatorToolTip.ForeColor = Color.White;
            _indicatorToolTip.OwnerDraw = true;
            _indicatorToolTip.Draw += IndicatorToolTip_Draw;
            _indicatorToolTip.Popup += IndicatorToolTip_Popup;

            PopulateIndicatorList();
            BuildSignalTextBoxes();
            BuildAnalysisColumns();

            // Dock order: last added docks first (rightmost first)
            pnlIndicators.Controls.Add(chkIndicators);       // Fill
            pnlIndicators.Controls.Add(pnlBuyCountColumn);   // Right (#B counts)
            pnlIndicators.Controls.Add(pnlBuyAnalysis);      // Right (buy circles)
            pnlIndicators.Controls.Add(pnlSignalColumn);     // Right (signal boxes)
            pnlIndicators.Controls.Add(pnlSellAnalysis);     // Right (sell circles)
            pnlIndicators.Controls.Add(pnlSellCountColumn);  // Right (#S counts, rightmost)
            pnlIndicators.Controls.Add(lblIndicatorHeader);   // Top

            // --- Chart ---
            chartStock = new Chart();
            chartStock.Dock = DockStyle.Fill;
            chartStock.BackColor = Color.White;

            var chartArea = new ChartArea("MainArea");
            chartArea.BackColor = Color.White;
            chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisX.LabelStyle.Angle = -45;
            chartArea.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            chartArea.AxisX.ScaleView.Zoomable = true;
            chartArea.AxisX.ScrollBar.IsPositionedInside = true;
            chartArea.CursorX.IsUserEnabled = true;
            chartArea.CursorX.IsUserSelectionEnabled = false;
            chartArea.CursorX.LineColor = Color.DimGray;
            chartArea.CursorX.LineDashStyle = ChartDashStyle.Dash;
            chartArea.CursorY.IsUserEnabled = true;
            chartArea.CursorY.LineColor = Color.DimGray;
            chartArea.CursorY.LineDashStyle = ChartDashStyle.Dash;
            chartArea.AxisY.ScaleView.Zoomable = true;
            chartArea.AxisY.ScrollBar.IsPositionedInside = true;
            chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            chartArea.AxisY.LabelStyle.Format = "C2";
            chartArea.AxisY.IsStartedFromZero = false;
            chartStock.ChartAreas.Add(chartArea);

            var series = new Series("Price");
            series.ChartType = SeriesChartType.Candlestick;
            series.ChartArea = "MainArea";
            series.YValuesPerPoint = 4;
            series["PriceUpColor"] = "Green";
            series["PriceDownColor"] = "Red";
            series["ShowOpenClose"] = "Both";
            chartStock.Series.Add(series);
            chartStock.TabStop = true;
            chartStock.PostPaint += DrawClosePriceTicks;
            chartStock.MouseWheel += chartStock_MouseWheel;
            chartStock.AxisViewChanged += chartStock_AxisViewChanged;
            chartStock.KeyDown += chartStock_KeyDown;
            chartStock.PreviewKeyDown += (s, ev) =>
            {
                if (ev.KeyCode == Keys.Left || ev.KeyCode == Keys.Right ||
                    ev.KeyCode == Keys.Home || ev.KeyCode == Keys.End)
                    ev.IsInputKey = true;
            };
            chartStock.MouseClick += chartStock_MouseClick;
            chartStock.MouseDoubleClick += chartStock_MouseDoubleClick;

            // Crosshair price label (floats on right Y-axis edge)
            _lblCrosshairPrice = new Label();
            _lblCrosshairPrice.AutoSize = false;
            _lblCrosshairPrice.Size = new Size(72, 18);
            _lblCrosshairPrice.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _lblCrosshairPrice.BackColor = Color.FromArgb(50, 50, 50);
            _lblCrosshairPrice.ForeColor = Color.White;
            _lblCrosshairPrice.TextAlign = ContentAlignment.MiddleCenter;
            _lblCrosshairPrice.Visible = false;
            _lblCrosshairPrice.BorderStyle = BorderStyle.None;
            chartStock.Controls.Add(_lblCrosshairPrice);

            // Crosshair price label (floats on left Y-axis edge)
            _lblCrosshairPriceLeft = new Label();
            _lblCrosshairPriceLeft.AutoSize = false;
            _lblCrosshairPriceLeft.Size = new Size(72, 18);
            _lblCrosshairPriceLeft.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _lblCrosshairPriceLeft.BackColor = Color.FromArgb(50, 50, 50);
            _lblCrosshairPriceLeft.ForeColor = Color.White;
            _lblCrosshairPriceLeft.TextAlign = ContentAlignment.MiddleCenter;
            _lblCrosshairPriceLeft.Visible = false;
            _lblCrosshairPriceLeft.BorderStyle = BorderStyle.None;
            chartStock.Controls.Add(_lblCrosshairPriceLeft);

            // --- Click hover info panel (floats near click point) ---
            _pnlHover = new Panel();
            _pnlHover.Size = new Size(160, 72);
            _pnlHover.BackColor = Color.FromArgb(240, 30, 30, 30);
            _pnlHover.Visible = false;

            _lblHoverDate = new Label();
            _lblHoverDate.AutoSize = false;
            _lblHoverDate.Size = new Size(156, 18);
            _lblHoverDate.Location = new Point(2, 4);
            _lblHoverDate.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lblHoverDate.ForeColor = Color.LightSkyBlue;
            _lblHoverDate.BackColor = Color.Transparent;
            _lblHoverDate.TextAlign = ContentAlignment.MiddleCenter;

            _lblHoverTime = new Label();
            _lblHoverTime.AutoSize = false;
            _lblHoverTime.Size = new Size(156, 16);
            _lblHoverTime.Location = new Point(2, 22);
            _lblHoverTime.Font = new Font("Segoe UI", 8F);
            _lblHoverTime.ForeColor = Color.Silver;
            _lblHoverTime.BackColor = Color.Transparent;
            _lblHoverTime.TextAlign = ContentAlignment.MiddleCenter;

            _lblHoverOHLC = new Label();
            _lblHoverOHLC.AutoSize = false;
            _lblHoverOHLC.Size = new Size(156, 44);
            _lblHoverOHLC.Location = new Point(2, 38);
            _lblHoverOHLC.Font = new Font("Segoe UI", 8.5F);
            _lblHoverOHLC.ForeColor = Color.White;
            _lblHoverOHLC.BackColor = Color.Transparent;
            _lblHoverOHLC.TextAlign = ContentAlignment.MiddleCenter;

            _lblHoverEvent = new Label();
            _lblHoverEvent.AutoSize = false;
            _lblHoverEvent.Size = new Size(196, 50);
            _lblHoverEvent.Location = new Point(2, 74);
            _lblHoverEvent.Font = new Font("Segoe UI", 7.5F);
            _lblHoverEvent.ForeColor = Color.Gold;
            _lblHoverEvent.BackColor = Color.Transparent;
            _lblHoverEvent.TextAlign = ContentAlignment.MiddleCenter;
            _lblHoverEvent.Visible = false;

            _pnlHover.Controls.Add(_lblHoverDate);
            _pnlHover.Controls.Add(_lblHoverTime);
            _pnlHover.Controls.Add(_lblHoverOHLC);
            _pnlHover.Controls.Add(_lblHoverEvent);
            chartStock.Controls.Add(_pnlHover);
            _pnlHover.BringToFront();

            chartStock.MouseLeave += (s, ev) => _pnlHover.Visible = false;

            // --- Simulation Panel (Right) ---
            BuildSimulationPanel();

            // Dock order matters: last added docks first
            this.Controls.Add(chartStock);      // Fill
            this.Controls.Add(pnlSimulation);   // Right
            this.Controls.Add(pnlIndicators);   // Left
            this.Controls.Add(pnlTop);          // Top

            this.ClientSize = new Size(1780, 1094);
            this.Text = "Stock Price Chart - v01.01.071";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.DoubleBuffered = true;

            this.ResumeLayout(false);

            // Grey out all unchecked indicators on startup
            ApplyInitialVisualStates();
        }

        // ============================================================
        // Populate indicator CheckedListBox with category headers
        // ============================================================
        private void PopulateIndicatorList()
        {
            _indexToIndicator.Clear();
            chkIndicators.Items.Clear();

            IndicatorCategory? lastCategory = null;
            var categoryLabels = new Dictionary<IndicatorCategory, string>
            {
                { IndicatorCategory.Trend, "\u2500\u2500\u2500 TREND (35%) \u2500\u2500\u2500" },
                { IndicatorCategory.Momentum, "\u2500\u2500\u2500 MOMENTUM (25%) \u2500\u2500\u2500" },
                { IndicatorCategory.Volatility, "\u2500\u2500\u2500 VOLATILITY (15%) \u2500\u2500\u2500" },
                { IndicatorCategory.Volume, "\u2500\u2500\u2500 VOLUME (25%) \u2500\u2500\u2500" }
            };

            foreach (var def in _allIndicators)
            {
                if (def.Category != lastCategory)
                {
                    int headerIdx = chkIndicators.Items.Add(categoryLabels[def.Category]);
                    // Headers map to null (not an indicator)
                    lastCategory = def.Category;
                }

                int idx = chkIndicators.Items.Add("  " + def.DisplayName);
                _indexToIndicator[idx] = def;
            }
        }

        // ============================================================
        // Build signal TextBox column aligned with indicator rows
        // ============================================================
        private void BuildSignalTextBoxes()
        {
            _signalTextBoxes.Clear();
            _indicatorYPositions.Clear();
            pnlSignalColumn.Controls.Clear();

            int itemHeight = chkIndicators.ItemHeight; // 22
            int y = 0;

            for (int i = 0; i < chkIndicators.Items.Count; i++)
            {
                if (_indexToIndicator.ContainsKey(i))
                {
                    var def = _indexToIndicator[i];

                    var txt = new TextBox();
                    txt.ReadOnly = true;
                    txt.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
                    txt.TextAlign = HorizontalAlignment.Center;
                    txt.BorderStyle = BorderStyle.FixedSingle;
                    txt.Size = new Size(71, itemHeight - 2);
                    txt.Location = new Point(2, y + 1);
                    txt.BackColor = Color.Gray;
                    txt.ForeColor = Color.White;
                    txt.Text = "";
                    txt.TabStop = false;
                    txt.Cursor = Cursors.Arrow;

                    pnlSignalColumn.Controls.Add(txt);
                    _signalTextBoxes[def.Key] = txt;
                    _indicatorYPositions[def.Key] = y;
                }

                y += itemHeight;
            }

            // Sync signal panel scrolling with the CheckedListBox
            chkIndicators.SelectedIndexChanged += (s, ev) => SyncSignalScroll();
            chkIndicators.MouseWheel += (s, ev) =>
            {
                this.BeginInvoke((Action)(() => SyncSignalScroll()));
            };
            chkIndicators.MouseMove += (s, ev) => SyncSignalScroll();
        }

        private void SyncSignalScroll()
        {
            int topIndex = chkIndicators.TopIndex;
            int scrollOffset = topIndex * chkIndicators.ItemHeight;
            foreach (var kvp in _signalTextBoxes)
            {
                int baseY = _indicatorYPositions[kvp.Key];
                kvp.Value.Top = baseY - scrollOffset + 1;
            }
        }

        // ============================================================
        // Build Buy/Sell analysis circle columns (left of signal column)
        // ============================================================
        private void BuildAnalysisColumns()
        {
            // Buy count column (leftmost - shows # of buy pivots correctly predicted)
            pnlBuyCountColumn = new Panel();
            pnlBuyCountColumn.Dock = DockStyle.Right;
            pnlBuyCountColumn.Width = 22;
            pnlBuyCountColumn.BackColor = Color.FromArgb(245, 245, 250);

            var lblBuyCountHdr = new Label();
            lblBuyCountHdr.Text = "#B";
            lblBuyCountHdr.Font = new Font("Segoe UI", 6F, FontStyle.Bold);
            lblBuyCountHdr.ForeColor = Color.FromArgb(50, 50, 80);
            lblBuyCountHdr.Dock = DockStyle.Top;
            lblBuyCountHdr.Height = 16;
            lblBuyCountHdr.TextAlign = ContentAlignment.MiddleCenter;
            pnlBuyCountColumn.Controls.Add(lblBuyCountHdr);

            // Buy analysis column (green/red circles showing best buy predictors)
            pnlBuyAnalysis = new Panel();
            pnlBuyAnalysis.Dock = DockStyle.Right;
            pnlBuyAnalysis.Width = 20;
            pnlBuyAnalysis.BackColor = Color.FromArgb(245, 245, 250);

            var lblBuyCol = new Label();
            lblBuyCol.Text = "Buy";
            lblBuyCol.Font = new Font("Segoe UI", 6.5F, FontStyle.Bold);
            lblBuyCol.ForeColor = Color.FromArgb(50, 50, 80);
            lblBuyCol.Dock = DockStyle.Top;
            lblBuyCol.Height = 16;
            lblBuyCol.TextAlign = ContentAlignment.MiddleCenter;
            pnlBuyAnalysis.Controls.Add(lblBuyCol);

            // Sell analysis column (green/red circles showing best sell predictors)
            pnlSellAnalysis = new Panel();
            pnlSellAnalysis.Dock = DockStyle.Right;
            pnlSellAnalysis.Width = 20;
            pnlSellAnalysis.BackColor = Color.FromArgb(245, 245, 250);

            var lblSellCol = new Label();
            lblSellCol.Text = "Sell";
            lblSellCol.Font = new Font("Segoe UI", 6.5F, FontStyle.Bold);
            lblSellCol.ForeColor = Color.FromArgb(50, 50, 80);
            lblSellCol.Dock = DockStyle.Top;
            lblSellCol.Height = 16;
            lblSellCol.TextAlign = ContentAlignment.MiddleCenter;
            pnlSellAnalysis.Controls.Add(lblSellCol);

            // Sell count column (rightmost - shows # of sell pivots correctly predicted)
            pnlSellCountColumn = new Panel();
            pnlSellCountColumn.Dock = DockStyle.Right;
            pnlSellCountColumn.Width = 22;
            pnlSellCountColumn.BackColor = Color.FromArgb(245, 245, 250);

            var lblSellCountHdr = new Label();
            lblSellCountHdr.Text = "#S";
            lblSellCountHdr.Font = new Font("Segoe UI", 6F, FontStyle.Bold);
            lblSellCountHdr.ForeColor = Color.FromArgb(50, 50, 80);
            lblSellCountHdr.Dock = DockStyle.Top;
            lblSellCountHdr.Height = 16;
            lblSellCountHdr.TextAlign = ContentAlignment.MiddleCenter;
            pnlSellCountColumn.Controls.Add(lblSellCountHdr);

            int itemHeight = chkIndicators.ItemHeight; // 22
            int y = 0;

            for (int i = 0; i < chkIndicators.Items.Count; i++)
            {
                if (_indexToIndicator.ContainsKey(i))
                {
                    var def = _indexToIndicator[i];

                    // Buy analysis circle
                    var buyCircle = new Panel();
                    buyCircle.Size = new Size(14, 14);
                    buyCircle.Location = new Point(3, y + 4);
                    buyCircle.BackColor = Color.FromArgb(220, 220, 220);
                    buyCircle.Tag = def.Key;
                    buyCircle.Cursor = Cursors.Hand;
                    buyCircle.Click += AnalysisCircle_Click;
                    MakeCircular(buyCircle);
                    pnlBuyAnalysis.Controls.Add(buyCircle);
                    _buyAnalysisCircles[def.Key] = buyCircle;

                    // Sell analysis circle
                    var sellCircle = new Panel();
                    sellCircle.Size = new Size(14, 14);
                    sellCircle.Location = new Point(3, y + 4);
                    sellCircle.BackColor = Color.FromArgb(220, 220, 220);
                    sellCircle.Tag = def.Key;
                    sellCircle.Cursor = Cursors.Hand;
                    sellCircle.Click += AnalysisCircle_Click;
                    MakeCircular(sellCircle);
                    pnlSellAnalysis.Controls.Add(sellCircle);
                    _sellAnalysisCircles[def.Key] = sellCircle;

                    // Buy count label
                    var buyCountLbl = new Label();
                    buyCountLbl.Text = "";
                    buyCountLbl.Font = new Font("Segoe UI", 6.5F);
                    buyCountLbl.ForeColor = Color.FromArgb(0, 150, 80);
                    buyCountLbl.Size = new Size(22, itemHeight);
                    buyCountLbl.Location = new Point(0, y);
                    buyCountLbl.TextAlign = ContentAlignment.MiddleCenter;
                    pnlBuyCountColumn.Controls.Add(buyCountLbl);
                    _buyCountLabels[def.Key] = buyCountLbl;

                    // Sell count label
                    var sellCountLbl = new Label();
                    sellCountLbl.Text = "";
                    sellCountLbl.Font = new Font("Segoe UI", 6.5F);
                    sellCountLbl.ForeColor = Color.FromArgb(200, 40, 40);
                    sellCountLbl.Size = new Size(22, itemHeight);
                    sellCountLbl.Location = new Point(0, y);
                    sellCountLbl.TextAlign = ContentAlignment.MiddleCenter;
                    pnlSellCountColumn.Controls.Add(sellCountLbl);
                    _sellCountLabels[def.Key] = sellCountLbl;
                }

                y += itemHeight;
            }

            // Sync scrolling with indicator list
            chkIndicators.SelectedIndexChanged += (s, ev) => SyncAnalysisScroll();
            chkIndicators.MouseWheel += (s, ev) =>
            {
                this.BeginInvoke((Action)(() => SyncAnalysisScroll()));
            };
            chkIndicators.MouseMove += (s, ev) => SyncAnalysisScroll();
        }

        private void MakeCircular(Panel panel)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, panel.Width, panel.Height);
            panel.Region = new Region(path);
        }

        private void SyncAnalysisScroll()
        {
            int topIndex = chkIndicators.TopIndex;
            int scrollOffset = topIndex * chkIndicators.ItemHeight;
            foreach (var kvp in _buyAnalysisCircles)
            {
                int baseY = _indicatorYPositions[kvp.Key];
                kvp.Value.Top = baseY - scrollOffset + 4;
            }
            foreach (var kvp in _sellAnalysisCircles)
            {
                int baseY = _indicatorYPositions[kvp.Key];
                kvp.Value.Top = baseY - scrollOffset + 4;
            }
            foreach (var kvp in _buyCountLabels)
            {
                int baseY = _indicatorYPositions[kvp.Key];
                kvp.Value.Top = baseY - scrollOffset;
            }
            foreach (var kvp in _sellCountLabels)
            {
                int baseY = _indicatorYPositions[kvp.Key];
                kvp.Value.Top = baseY - scrollOffset;
            }
        }

        // ============================================================
        // Circle click: cycle Green → Red → Blank (only if indicator checked)
        // ============================================================
        private void AnalysisCircle_Click(object sender, EventArgs e)
        {
            var circle = sender as Panel;
            if (circle == null) return;

            string key = circle.Tag as string;
            if (string.IsNullOrEmpty(key)) return;

            // Only allow cycling if the indicator is checked
            if (!GetCheckedIndicatorKeys().Contains(key)) return;

            // Cycle: Blank → Green → Red → Blank
            Color blank = Color.FromArgb(220, 220, 220);
            if (circle.BackColor == blank)
                circle.BackColor = SignalResult.BuyColor;        // Green
            else if (circle.BackColor == SignalResult.BuyColor)
                circle.BackColor = SignalResult.SellColor;       // Red
            else
                circle.BackColor = blank;                        // Blank

            // Indicator Collection: user clicks Run Simulation manually
            // Contrarian: no circles, so no action needed here
        }

        // ============================================================
        // Analyze button - evaluate which indicators best predict pivots
        // ============================================================
        private void btnAnalyze_Click(object sender, EventArgs e)
        {
            if (_cachedDates.Count == 0 || _pivotPoints.Count == 0)
            {
                MessageBox.Show("Please load a symbol with pivot points first.", "No Data",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            lblStatus.Text = "Analyzing pivot predictions...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                var allKeys = new List<string>();
                foreach (var def in _allIndicators)
                    allKeys.Add(def.Key);

                // Per indicator: count how many times it showed Green vs Red
                // at trough (buy) pivots and peak (sell) pivots
                var buyShowedGreen = new Dictionary<string, int>();
                var buyShowedRed = new Dictionary<string, int>();
                var sellShowedGreen = new Dictionary<string, int>();
                var sellShowedRed = new Dictionary<string, int>();

                foreach (var key in allKeys)
                {
                    buyShowedGreen[key] = 0;
                    buyShowedRed[key] = 0;
                    sellShowedGreen[key] = 0;
                    sellShowedRed[key] = 0;
                }

                // Evaluate signals at each pivot and up to 3 days before it.
                // For each indicator at each pivot, if ANY bar in the window
                // (pivot day and 3 prior bars) shows Buy or Sell, count the
                // dominant signal across that window for the indicator.
                foreach (var pivot in _pivotPoints)
                {
                    if (pivot.BarIndex < 1 || pivot.BarIndex >= _cachedDates.Count)
                        continue;

                    // Window: up to 3 bars before the pivot + the pivot bar itself
                    int windowStart = Math.Max(1, pivot.BarIndex - 3);
                    int windowEnd = pivot.BarIndex;

                    // Per indicator: track best signal seen in the window
                    var windowBuyCount = new Dictionary<string, int>();
                    var windowSellCount = new Dictionary<string, int>();
                    foreach (var key in allKeys)
                    {
                        windowBuyCount[key] = 0;
                        windowSellCount[key] = 0;
                    }

                    for (int bar = windowStart; bar <= windowEnd; bar++)
                    {
                        var signals = IndicatorSignalEvaluator.EvaluateAllAtIndex(
                            _cachedOpens, _cachedHighs, _cachedLows, _cachedCloses,
                            _cachedVolumes, bar);

                        foreach (var key in allKeys)
                        {
                            if (!signals.ContainsKey(key)) continue;
                            var sig = signals[key];
                            if (sig.Signal == SignalType.Hold || sig.DisplayValue == "N/A")
                                continue;

                            if (sig.Signal == SignalType.Buy) windowBuyCount[key]++;
                            else if (sig.Signal == SignalType.Sell) windowSellCount[key]++;
                        }
                    }

                    // For each indicator, use the dominant signal from the window
                    foreach (var key in allKeys)
                    {
                        int greenHits = windowBuyCount[key];
                        int redHits = windowSellCount[key];
                        if (greenHits == 0 && redHits == 0) continue;

                        // Dominant signal in the window
                        SignalType dominant = greenHits >= redHits ? SignalType.Buy : SignalType.Sell;

                        if (pivot.Type == PivotType.Trough)
                        {
                            if (dominant == SignalType.Buy) buyShowedGreen[key]++;
                            else buyShowedRed[key]++;
                        }
                        else // Peak
                        {
                            if (dominant == SignalType.Buy) sellShowedGreen[key]++;
                            else sellShowedRed[key]++;
                        }
                    }
                }

                // Update circles: only light up if indicator correctly picks
                // the pivot direction 80% or more of the time
                const double accuracyThreshold = 0.80;

                foreach (var key in allKeys)
                {
                    // Buy column: show whichever color most accurately predicted troughs at 80%+
                    if (_buyAnalysisCircles.ContainsKey(key))
                    {
                        int total = buyShowedGreen[key] + buyShowedRed[key];
                        int buyWinCount = 0;
                        if (total > 0)
                        {
                            double greenPct = (double)buyShowedGreen[key] / total;
                            double redPct = (double)buyShowedRed[key] / total;

                            if (greenPct >= redPct && greenPct >= accuracyThreshold)
                            {
                                _buyAnalysisCircles[key].BackColor = SignalResult.BuyColor;
                                buyWinCount = buyShowedGreen[key];
                            }
                            else if (redPct > greenPct && redPct >= accuracyThreshold)
                            {
                                _buyAnalysisCircles[key].BackColor = SignalResult.SellColor;
                                buyWinCount = buyShowedRed[key];
                            }
                            else
                            {
                                _buyAnalysisCircles[key].BackColor = Color.FromArgb(220, 220, 220);
                            }
                        }
                        else
                        {
                            _buyAnalysisCircles[key].BackColor = Color.FromArgb(220, 220, 220);
                        }

                        if (_buyCountLabels.ContainsKey(key))
                            _buyCountLabels[key].Text = buyWinCount > 0
                                ? buyWinCount.ToString() : "";
                    }

                    // Sell column: show whichever color most accurately predicted peaks at 80%+
                    if (_sellAnalysisCircles.ContainsKey(key))
                    {
                        int total = sellShowedGreen[key] + sellShowedRed[key];
                        int sellWinCount = 0;
                        if (total > 0)
                        {
                            double redPct = (double)sellShowedRed[key] / total;
                            double greenPct = (double)sellShowedGreen[key] / total;

                            if (redPct >= greenPct && redPct >= accuracyThreshold)
                            {
                                _sellAnalysisCircles[key].BackColor = SignalResult.SellColor;
                                sellWinCount = sellShowedRed[key];
                            }
                            else if (greenPct > redPct && greenPct >= accuracyThreshold)
                            {
                                _sellAnalysisCircles[key].BackColor = SignalResult.BuyColor;
                                sellWinCount = sellShowedGreen[key];
                            }
                            else
                            {
                                _sellAnalysisCircles[key].BackColor = Color.FromArgb(220, 220, 220);
                            }
                        }
                        else
                        {
                            _sellAnalysisCircles[key].BackColor = Color.FromArgb(220, 220, 220);
                        }

                        if (_sellCountLabels.ContainsKey(key))
                            _sellCountLabels[key].Text = sellWinCount > 0
                                ? sellWinCount.ToString() : "";
                    }
                }

                int peakCount = _pivotPoints.Count(p => p.Type == PivotType.Peak);
                int troughCount = _pivotPoints.Count(p => p.Type == PivotType.Trough);
                lblStatus.Text = $"Analysis complete: {troughCount} buy pivots, {peakCount} sell pivots evaluated";
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        // ============================================================
        // Clear analysis circles (on symbol change)
        // ============================================================
        private void ClearAnalysisCircles()
        {
            foreach (var kvp in _buyAnalysisCircles)
                kvp.Value.BackColor = Color.FromArgb(220, 220, 220);
            foreach (var kvp in _sellAnalysisCircles)
                kvp.Value.BackColor = Color.FromArgb(220, 220, 220);
            foreach (var kvp in _buyCountLabels)
                kvp.Value.Text = "";
            foreach (var kvp in _sellCountLabels)
                kvp.Value.Text = "";
        }

        // ============================================================
        // Pivot navigation: move crosshair to previous pivot point
        // ============================================================
        private void btnPivotLeft_Click(object sender, EventArgs e)
        {
            if (_pivotPoints.Count == 0 || _cachedDates.Count == 0) return;

            // Find the nearest pivot to the LEFT of current crosshair
            int bestBar = -1;
            foreach (var pivot in _pivotPoints)
            {
                if (pivot.BarIndex < _crosshairIndex)
                {
                    bestBar = pivot.BarIndex; // keep overwriting; last one is closest
                }
            }

            if (bestBar >= 0)
            {
                _crosshairIndex = bestBar;
                UpdateCrosshairPosition();
            }
        }

        // ============================================================
        // Pivot navigation: move crosshair to next pivot point
        // ============================================================
        private void btnPivotRight_Click(object sender, EventArgs e)
        {
            if (_pivotPoints.Count == 0 || _cachedDates.Count == 0) return;

            // Find the nearest pivot to the RIGHT of current crosshair
            foreach (var pivot in _pivotPoints)
            {
                if (pivot.BarIndex > _crosshairIndex)
                {
                    _crosshairIndex = pivot.BarIndex;
                    UpdateCrosshairPosition();
                    return;
                }
            }
        }

        // ============================================================
        // Get set of indicator keys that are currently checked
        // ============================================================
        private HashSet<string> GetCheckedIndicatorKeys()
        {
            var checkedKeys = new HashSet<string>();
            foreach (var kvp in _indexToIndicator)
            {
                if (chkIndicators.GetItemChecked(kvp.Key))
                    checkedKeys.Add(kvp.Value.Key);
            }
            return checkedKeys;
        }

        // ============================================================
        // Build the Trading Simulation panel (right side)
        // ============================================================
        private void BuildSimulationPanel()
        {
            pnlSimulation = new Panel();
            pnlSimulation.Dock = DockStyle.Right;
            pnlSimulation.Width = 570;
            pnlSimulation.BackColor = Color.FromArgb(245, 245, 250);
            pnlSimulation.BorderStyle = BorderStyle.FixedSingle;

            // --- Header ---
            lblSimHeader = new Label();
            lblSimHeader.Text = "Trading Simulation";
            lblSimHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblSimHeader.ForeColor = Color.FromArgb(50, 50, 80);
            lblSimHeader.Dock = DockStyle.Top;
            lblSimHeader.Height = 30;
            lblSimHeader.TextAlign = ContentAlignment.MiddleCenter;
            lblSimHeader.BackColor = Color.FromArgb(230, 230, 240);

            // --- Controls area ---
            var pnlSimControls = new Panel();
            pnlSimControls.Dock = DockStyle.Top;
            pnlSimControls.Height = 120;
            pnlSimControls.BackColor = Color.FromArgb(245, 245, 250);

            lblStartPosition = new Label();
            lblStartPosition.Text = "Start:";
            lblStartPosition.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblStartPosition.Location = new Point(8, 8);
            lblStartPosition.AutoSize = true;

            rbStartBuy = new RadioButton();
            rbStartBuy.Text = "Buy";
            rbStartBuy.Font = new Font("Segoe UI", 9F);
            rbStartBuy.Location = new Point(55, 6);
            rbStartBuy.AutoSize = true;
            rbStartBuy.Checked = true;

            rbStartSell = new RadioButton();
            rbStartSell.Text = "Sell";
            rbStartSell.Font = new Font("Segoe UI", 9F);
            rbStartSell.Location = new Point(115, 6);
            rbStartSell.AutoSize = true;

            lblStrategy = new Label();
            lblStrategy.Text = "Strategy:";
            lblStrategy.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblStrategy.Location = new Point(8, 35);
            lblStrategy.AutoSize = true;

            cboStrategy = new ComboBox();
            cboStrategy.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStrategy.Font = new Font("Segoe UI", 9F);
            cboStrategy.Location = new Point(80, 32);
            cboStrategy.Size = new Size(140, 25);
            cboStrategy.Items.Add("Contrarian");
            cboStrategy.Items.Add("Indicator Collection");
            cboStrategy.SelectedIndex = 0;
            cboStrategy.SelectedIndexChanged += (s, ev) =>
            {
                ClearSimulation();   // erase old triangles & results
                // Contrarian auto-runs; Indicator Collection waits for Run Simulation
                if (cboStrategy.SelectedIndex == 0)
                    RunSimulationAuto();
            };

            btnRunSim = new Button();
            btnRunSim.Text = "Run Simulation";
            btnRunSim.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnRunSim.Location = new Point(8, 65);
            btnRunSim.Size = new Size(150, 30);
            btnRunSim.BackColor = Color.FromArgb(76, 175, 80);
            btnRunSim.ForeColor = Color.White;
            btnRunSim.FlatStyle = FlatStyle.Flat;
            btnRunSim.FlatAppearance.BorderSize = 0;
            btnRunSim.Click += btnRunSim_Click;

            btnClearSim = new Button();
            btnClearSim.Text = "Clear";
            btnClearSim.Font = new Font("Segoe UI", 9F);
            btnClearSim.Location = new Point(165, 65);
            btnClearSim.Size = new Size(70, 30);
            btnClearSim.BackColor = Color.FromArgb(158, 158, 158);
            btnClearSim.ForeColor = Color.White;
            btnClearSim.FlatStyle = FlatStyle.Flat;
            btnClearSim.FlatAppearance.BorderSize = 0;
            btnClearSim.Click += btnClearSim_Click;

            pnlSimControls.Controls.Add(lblStartPosition);
            pnlSimControls.Controls.Add(rbStartBuy);
            pnlSimControls.Controls.Add(rbStartSell);
            pnlSimControls.Controls.Add(lblStrategy);
            pnlSimControls.Controls.Add(cboStrategy);
            pnlSimControls.Controls.Add(btnRunSim);
            pnlSimControls.Controls.Add(btnClearSim);

            // --- Summary panel (bottom) ---
            pnlSimSummary = new Panel();
            pnlSimSummary.Dock = DockStyle.Bottom;
            pnlSimSummary.Height = 85;
            pnlSimSummary.BackColor = Color.FromArgb(235, 235, 245);

            lblSimCash = new Label();
            lblSimCash.Text = "Cash: --";
            lblSimCash.Font = new Font("Segoe UI", 8.5F);
            lblSimCash.Location = new Point(8, 5);
            lblSimCash.AutoSize = true;

            lblSimShares = new Label();
            lblSimShares.Text = "Shares: --";
            lblSimShares.Font = new Font("Segoe UI", 8.5F);
            lblSimShares.Location = new Point(170, 5);
            lblSimShares.AutoSize = true;

            lblSimStockValue = new Label();
            lblSimStockValue.Text = "Stock Value: --";
            lblSimStockValue.Font = new Font("Segoe UI", 8.5F);
            lblSimStockValue.Location = new Point(8, 27);
            lblSimStockValue.AutoSize = true;

            lblSimPnL = new Label();
            lblSimPnL.Text = "Total P&L: --";
            lblSimPnL.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblSimPnL.ForeColor = Color.DimGray;
            lblSimPnL.Location = new Point(8, 52);
            lblSimPnL.AutoSize = true;

            pnlSimSummary.Controls.Add(lblSimCash);
            pnlSimSummary.Controls.Add(lblSimShares);
            pnlSimSummary.Controls.Add(lblSimStockValue);
            pnlSimSummary.Controls.Add(lblSimPnL);

            // --- Transaction DataGridView ---
            dgvTransactions = new DataGridView();
            dgvTransactions.Dock = DockStyle.Fill;
            dgvTransactions.ReadOnly = true;
            dgvTransactions.AllowUserToAddRows = false;
            dgvTransactions.AllowUserToDeleteRows = false;
            dgvTransactions.AllowUserToResizeRows = false;
            dgvTransactions.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvTransactions.MultiSelect = false;
            dgvTransactions.RowHeadersVisible = false;
            dgvTransactions.BackgroundColor = Color.White;
            dgvTransactions.BorderStyle = BorderStyle.None;
            dgvTransactions.Font = new Font("Segoe UI", 8F);
            dgvTransactions.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            dgvTransactions.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 230, 240);
            dgvTransactions.EnableHeadersVisualStyles = false;
            dgvTransactions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvTransactions.CellClick += dgvTransactions_CellClick;

            dgvTransactions.Columns.Add("colNum", "#");
            dgvTransactions.Columns.Add("colDate", "Date");
            dgvTransactions.Columns.Add("colAction", "Action");
            dgvTransactions.Columns.Add("colPrice", "Price");
            dgvTransactions.Columns.Add("colShares", "Shares");
            dgvTransactions.Columns.Add("colHeld", "Held");
            dgvTransactions.Columns.Add("colCash", "Cash");
            dgvTransactions.Columns.Add("colLiquidated", "Liquidated");

            dgvTransactions.Columns["colNum"].Width = 36;
            dgvTransactions.Columns["colDate"].Width = 90;
            dgvTransactions.Columns["colAction"].Width = 52;
            dgvTransactions.Columns["colPrice"].Width = 75;
            dgvTransactions.Columns["colShares"].Width = 55;
            dgvTransactions.Columns["colHeld"].Width = 48;
            dgvTransactions.Columns["colCash"].Width = 85;
            dgvTransactions.Columns["colLiquidated"].Width = 90;

            // Dock order: last added docks first
            pnlSimulation.Controls.Add(dgvTransactions);     // Fill
            pnlSimulation.Controls.Add(pnlSimSummary);       // Bottom
            pnlSimulation.Controls.Add(pnlSimControls);      // Top (below header)
            pnlSimulation.Controls.Add(lblSimHeader);         // Top
        }

        // ============================================================
        // Pivot Point Transaction button handler
        // ============================================================
        private void btnPivotTransaction_Click(object sender, EventArgs e)
        {
            if (_cachedDates.Count == 0)
            {
                MessageBox.Show("Please load a symbol first.", "No Data",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_pivotPoints.Count == 0)
            {
                MessageBox.Show("No pivot points detected. Adjust the Pivot % threshold and try again.",
                    "No Pivots", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Clear any existing simulation first
            ClearSimulation();

            lblStatus.Text = "Running pivot point transaction...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                _simulationResult = TradingSimulationEngine.RunSimulation(
                    _cachedOpens, _cachedHighs, _cachedLows, _cachedCloses,
                    _cachedVolumes, _cachedDates,
                    SimulationStrategy.PivotPointTransaction,
                    true, _pivotThreshold,
                    pivotPoints: _pivotPoints);

                PopulateTransactionGrid();
                UpdateSimulationSummary();
                DrawSimulationMarkers();

                lblStatus.Text = $"Pivot transaction complete: {_simulationResult.Transactions.Count} transactions";
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        // ============================================================
        // Run Simulation button handler
        // ============================================================
        private void btnRunSim_Click(object sender, EventArgs e)
        {
            if (_cachedDates.Count == 0)
            {
                MessageBox.Show("Please load a symbol first.", "No Data",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RunSimulationAuto();
        }

        // ============================================================
        // Auto-run simulation (called from button, combo change, analyze)
        // ============================================================
        private void RunSimulationAuto()
        {
            if (_cachedDates.Count == 0) return;

            bool startWithBuy = true; // Always start with a BUY at bar 0
            var strategy = cboStrategy.SelectedIndex == 1
                ? SimulationStrategy.IndicatorCollection
                : SimulationStrategy.Contrarian;

            // For Indicator Collection, silently skip if no analysis data
            if (strategy == SimulationStrategy.IndicatorCollection)
            {
                bool hasAnalysis = false;
                foreach (var kvp in _buyAnalysisCircles)
                {
                    if (kvp.Value.BackColor != Color.FromArgb(220, 220, 220))
                    {
                        hasAnalysis = true;
                        break;
                    }
                }
                if (!hasAnalysis) return;
            }

            lblStatus.Text = "Running simulation...";
            this.Cursor = Cursors.WaitCursor;

            try
            {
                // Build analysis dictionaries from circle colors
                Dictionary<string, SignalType> buyAnalysis = null;
                Dictionary<string, SignalType> sellAnalysis = null;

                if (strategy == SimulationStrategy.IndicatorCollection)
                {
                    buyAnalysis = new Dictionary<string, SignalType>();
                    sellAnalysis = new Dictionary<string, SignalType>();

                    // Only checked indicators act as active constraints
                    var checkedKeys = GetCheckedIndicatorKeys();

                    foreach (var kvp in _buyAnalysisCircles)
                    {
                        if (!checkedKeys.Contains(kvp.Key)) continue;
                        if (kvp.Value.BackColor == SignalResult.BuyColor)
                            buyAnalysis[kvp.Key] = SignalType.Buy;
                        else if (kvp.Value.BackColor == SignalResult.SellColor)
                            buyAnalysis[kvp.Key] = SignalType.Sell;
                    }

                    foreach (var kvp in _sellAnalysisCircles)
                    {
                        if (!checkedKeys.Contains(kvp.Key)) continue;
                        if (kvp.Value.BackColor == SignalResult.SellColor)
                            sellAnalysis[kvp.Key] = SignalType.Sell;
                        else if (kvp.Value.BackColor == SignalResult.BuyColor)
                            sellAnalysis[kvp.Key] = SignalType.Buy;
                    }
                }

                _simulationResult = TradingSimulationEngine.RunSimulation(
                    _cachedOpens, _cachedHighs, _cachedLows, _cachedCloses,
                    _cachedVolumes, _cachedDates, strategy, startWithBuy,
                    _pivotThreshold, buyAnalysis, sellAnalysis);

                PopulateTransactionGrid();
                UpdateSimulationSummary();
                DrawSimulationMarkers();

                // Lock indicators: grey out unchecked, show only checked lines/values
                ApplySimulationIndicatorLock();

                lblStatus.Text = $"Simulation complete: {_simulationResult.Transactions.Count} transactions";
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        // ============================================================
        // Clear Simulation button handler
        // ============================================================
        private void btnClearSim_Click(object sender, EventArgs e)
        {
            ClearSimulation();
        }

        private void ClearSimulation()
        {
            _simulationResult = null;
            dgvTransactions.Rows.Clear();
            lblSimCash.Text = "Cash: --";
            lblSimShares.Text = "Shares: --";
            lblSimStockValue.Text = "Stock Value: --";
            lblSimPnL.Text = "Total P&L: --";
            lblSimPnL.ForeColor = Color.DimGray;

            // Remove chart markers
            if (_simBuySeries != null && chartStock.Series.FindByName("SimBuy") != null)
                chartStock.Series.Remove(_simBuySeries);
            if (_simSellSeries != null && chartStock.Series.FindByName("SimSell") != null)
                chartStock.Series.Remove(_simSellSeries);
            _simBuySeries = null;
            _simSellSeries = null;

            // Remove number annotations
            foreach (var ann in _simNumberAnnotations)
            {
                if (chartStock.Annotations.Contains(ann))
                    chartStock.Annotations.Remove(ann);
            }
            _simNumberAnnotations.Clear();

            // Unlock indicators: restore all visuals and chart lines
            if (_simulationActive)
                ReleaseSimulationIndicatorLock();
        }

        // ============================================================
        // Populate the transaction DataGridView with simulation results
        // ============================================================
        private void PopulateTransactionGrid()
        {
            dgvTransactions.Rows.Clear();
            if (_simulationResult == null) return;

            int txnNum = 1;
            foreach (var txn in _simulationResult.Transactions)
            {
                // Liquidated = cash + (shares held × price) as if all stock sold now
                decimal liquidatedValue = txn.CashBalance + (txn.SharesHeld * txn.Price);

                int rowIdx = dgvTransactions.Rows.Add(
                    txnNum,
                    txn.Date.ToString("yyyy-MM-dd"),
                    txn.Action,
                    txn.Price.ToString("C2"),
                    txn.SharesTraded,
                    txn.SharesHeld,
                    txn.CashBalance.ToString("C2"),
                    liquidatedValue.ToString("C2")
                );

                var row = dgvTransactions.Rows[rowIdx];
                if (txn.Action == "BUY")
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(232, 245, 233);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(27, 94, 32);
                }
                else
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 238);
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(183, 28, 28);
                }

                txnNum++;
            }

            if (dgvTransactions.Rows.Count > 0)
                dgvTransactions.FirstDisplayedScrollingRowIndex = dgvTransactions.Rows.Count - 1;
        }

        // ============================================================
        // Update the simulation summary labels
        // ============================================================
        private void UpdateSimulationSummary()
        {
            if (_simulationResult == null) return;

            lblSimCash.Text = $"Cash: {_simulationResult.FinalCashBalance:C2}";
            lblSimShares.Text = $"Shares: {_simulationResult.FinalSharesHeld}";
            lblSimStockValue.Text = $"Stock Value: {_simulationResult.FinalStockValue:C2}";

            decimal pnl = _simulationResult.FinalPnL;
            lblSimPnL.Text = $"Total P&L: {pnl:C2}";
            lblSimPnL.ForeColor = pnl >= 0
                ? Color.FromArgb(0, 230, 118)
                : Color.FromArgb(255, 82, 82);
        }

        // ============================================================
        // Draw green/red triangle markers on chart at transaction points
        // ============================================================
        private void DrawSimulationMarkers()
        {
            // Remove old markers
            if (_simBuySeries != null && chartStock.Series.FindByName("SimBuy") != null)
                chartStock.Series.Remove(_simBuySeries);
            if (_simSellSeries != null && chartStock.Series.FindByName("SimSell") != null)
                chartStock.Series.Remove(_simSellSeries);

            // Remove old number annotations
            foreach (var ann in _simNumberAnnotations)
            {
                if (chartStock.Annotations.Contains(ann))
                    chartStock.Annotations.Remove(ann);
            }
            _simNumberAnnotations.Clear();

            if (_simulationResult == null) return;

            // Green up-triangles for BUY
            _simBuySeries = new Series("SimBuy");
            _simBuySeries.ChartType = SeriesChartType.Point;
            _simBuySeries.ChartArea = "MainArea";
            _simBuySeries.MarkerStyle = MarkerStyle.Triangle;
            _simBuySeries.MarkerSize = 12;
            _simBuySeries.MarkerColor = Color.FromArgb(0, 230, 118);
            _simBuySeries.MarkerBorderColor = Color.FromArgb(0, 180, 90);
            _simBuySeries.MarkerBorderWidth = 1;
            _simBuySeries.IsVisibleInLegend = false;

            // Red triangles for SELL
            _simSellSeries = new Series("SimSell");
            _simSellSeries.ChartType = SeriesChartType.Point;
            _simSellSeries.ChartArea = "MainArea";
            _simSellSeries.MarkerStyle = MarkerStyle.Triangle;
            _simSellSeries.MarkerSize = 12;
            _simSellSeries.MarkerColor = Color.FromArgb(255, 82, 82);
            _simSellSeries.MarkerBorderColor = Color.FromArgb(200, 40, 40);
            _simSellSeries.MarkerBorderWidth = 1;
            _simSellSeries.IsVisibleInLegend = false;

            int txnNum = 1;
            foreach (var txn in _simulationResult.Transactions)
            {
                double xVal = txn.BarIndex;
                if (txn.Action == "BUY")
                    _simBuySeries.Points.AddXY(xVal, (double)txn.Price);
                else
                    _simSellSeries.Points.AddXY(xVal, (double)txn.Price);

                // Add number label annotation above/below the triangle
                var ann = new TextAnnotation();
                ann.Text = txnNum.ToString();
                ann.Font = new Font("Segoe UI", 7F, FontStyle.Bold);
                ann.ForeColor = txn.Action == "BUY"
                    ? Color.FromArgb(0, 140, 60)
                    : Color.FromArgb(200, 40, 40);
                ann.AnchorX = xVal;
                // BUY labels below the price, SELL labels above
                ann.AnchorY = txn.Action == "BUY"
                    ? (double)txn.Price * 0.985
                    : (double)txn.Price * 1.015;
                ann.ClipToChartArea = "MainArea";
                ann.Alignment = ContentAlignment.MiddleCenter;

                chartStock.Annotations.Add(ann);
                _simNumberAnnotations.Add(ann);

                txnNum++;
            }

            chartStock.Series.Add(_simBuySeries);
            chartStock.Series.Add(_simSellSeries);
        }

        // ============================================================
        // Highlight matching simulation row when crosshair moves
        // ============================================================
        private void HighlightSimulationRow(int barIndex)
        {
            if (_simulationResult == null || dgvTransactions.Rows.Count == 0) return;

            dgvTransactions.ClearSelection();
            for (int i = 0; i < _simulationResult.Transactions.Count; i++)
            {
                if (_simulationResult.Transactions[i].BarIndex == barIndex)
                {
                    dgvTransactions.Rows[i].Selected = true;
                    dgvTransactions.FirstDisplayedScrollingRowIndex = i;
                    break;
                }
            }
        }

        // ============================================================
        // Transaction grid click - navigate chart to that bar
        // ============================================================
        private void dgvTransactions_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _simulationResult == null) return;
            if (e.RowIndex >= _simulationResult.Transactions.Count) return;

            int barIndex = _simulationResult.Transactions[e.RowIndex].BarIndex;
            _crosshairIndex = barIndex;
            UpdateCrosshairPosition();
        }

        // ============================================================
        // Indicator hover tooltip: show description on mouse-over
        // ============================================================
        private void chkIndicators_MouseMove(object sender, MouseEventArgs e)
        {
            int index = chkIndicators.IndexFromPoint(e.Location);

            if (index == _lastTooltipIndex) return;
            _lastTooltipIndex = index;

            if (index >= 0 && _indexToIndicator.ContainsKey(index))
            {
                var def = _indexToIndicator[index];
                if (!string.IsNullOrEmpty(def.Description))
                {
                    _indicatorToolTip.Hide(chkIndicators);
                    _indicatorToolTip.Show(def.Description, chkIndicators,
                        e.X + 15, e.Y + 15, 15000);
                }
                else
                {
                    _indicatorToolTip.Hide(chkIndicators);
                }
            }
            else
            {
                _indicatorToolTip.Hide(chkIndicators);
            }
        }

        private void IndicatorToolTip_Popup(object sender, PopupEventArgs e)
        {
            // Measure string and size the tooltip
            string text = _indicatorToolTip.GetToolTip(e.AssociatedControl);
            if (string.IsNullOrEmpty(text)) return;
            using (var font = new Font("Segoe UI", 9F))
            {
                var size = TextRenderer.MeasureText(text, font, new Size(320, 0),
                    TextFormatFlags.WordBreak);
                e.ToolTipSize = new Size(size.Width + 16, size.Height + 10);
            }
        }

        private void IndicatorToolTip_Draw(object sender, DrawToolTipEventArgs e)
        {
            using (var bgBrush = new SolidBrush(Color.FromArgb(40, 40, 60)))
                e.Graphics.FillRectangle(bgBrush, e.Bounds);

            using (var borderPen = new Pen(Color.FromArgb(100, 100, 140)))
                e.Graphics.DrawRectangle(borderPen,
                    e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);

            using (var font = new Font("Segoe UI", 9F))
            using (var textBrush = new SolidBrush(Color.White))
            {
                var rect = new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 5,
                    e.Bounds.Width - 16, e.Bounds.Height - 10);
                e.Graphics.DrawString(e.ToolTipText, font, textBrush, rect);
            }
        }

        // ============================================================
        // OwnerDraw: bold + gray background for category headers
        // ============================================================
        private void chkIndicators_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            string text = chkIndicators.Items[e.Index].ToString();
            bool isHeader = !_indexToIndicator.ContainsKey(e.Index);

            e.DrawBackground();

            if (isHeader)
            {
                using (var brush = new SolidBrush(Color.FromArgb(220, 220, 230)))
                    e.Graphics.FillRectangle(brush, e.Bounds);

                using (var font = new Font("Segoe UI", 8.5F, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(60, 60, 100)))
                {
                    var rect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height);
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(text, font, brush, rect, sf);
                }
            }
            else
            {
                bool isChecked = chkIndicators.GetItemChecked(e.Index);
                var bgColor = (e.State & DrawItemState.Selected) != 0
                    ? Color.FromArgb(210, 220, 240)
                    : Color.FromArgb(245, 245, 250);

                using (var brush = new SolidBrush(bgColor))
                    e.Graphics.FillRectangle(brush, e.Bounds);

                // Checkbox
                var checkRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 3, 14, 14);
                ControlPaint.DrawCheckBox(e.Graphics, checkRect,
                    isChecked ? ButtonState.Checked | ButtonState.Flat : ButtonState.Normal | ButtonState.Flat);

                // Text - use the indicator's primary line color, grey if sim active & unchecked
                var def = _indexToIndicator[e.Index];
                Color textColor;
                if (_simulationActive && !isChecked)
                    textColor = Color.FromArgb(190, 190, 190);
                else
                    textColor = (def.SeriesList != null && def.SeriesList.Count > 0)
                        ? def.SeriesList[0].Color
                        : Color.Black;

                using (var font = new Font("Segoe UI", 9F, FontStyle.Bold))
                using (var brush = new SolidBrush(textColor))
                {
                    var textRect = new Rectangle(e.Bounds.X + 22, e.Bounds.Y, e.Bounds.Width - 22, e.Bounds.Height);
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(text, font, brush, textRect, sf);
                }
            }
        }

        // ============================================================
        // ItemCheck: block headers, add/remove indicators
        // ============================================================
        private void chkIndicators_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // Block category header toggling
            if (!_indexToIndicator.ContainsKey(e.Index))
            {
                e.NewValue = e.CurrentValue;
                return;
            }

            var def = _indexToIndicator[e.Index];

            // Use BeginInvoke so check state is committed before we act
            this.BeginInvoke((Action)(() =>
            {
                // If simulation is active, restore saved analysis state before re-running
                bool wasSimActive = _simulationActive;
                if (wasSimActive)
                {
                    _simulationActive = false;

                    // Restore analysis circle colors from saved state so simulation reads real values
                    foreach (var kvp in _savedBuyCircleColors)
                        if (_buyAnalysisCircles.ContainsKey(kvp.Key))
                            _buyAnalysisCircles[kvp.Key].BackColor = kvp.Value;
                    foreach (var kvp in _savedSellCircleColors)
                        if (_sellAnalysisCircles.ContainsKey(kvp.Key))
                            _sellAnalysisCircles[kvp.Key].BackColor = kvp.Value;
                    foreach (var kvp in _savedBuyCountTexts)
                        if (_buyCountLabels.ContainsKey(kvp.Key))
                            _buyCountLabels[kvp.Key].Text = kvp.Value;
                    foreach (var kvp in _savedSellCountTexts)
                        if (_sellCountLabels.ContainsKey(kvp.Key))
                            _sellCountLabels[kvp.Key].Text = kvp.Value;
                }

                if (e.NewValue == CheckState.Checked)
                    AddIndicator(def);
                else
                    RemoveIndicator(def);

                // Update visual state: grey out unchecked, restore checked
                UpdateIndicatorVisualState(def.Key, e.NewValue == CheckState.Checked);

                // If simulation was running, re-run with new indicator selection
                if (wasSimActive)
                {
                    RunSimulationAuto();
                }
            }));
        }

        // ============================================================
        // Grey out / restore indicator visual elements based on check state
        // ============================================================
        private void UpdateIndicatorVisualState(string key, bool isChecked)
        {
            Color dimGrey = Color.FromArgb(200, 200, 200);

            // Signal text box
            if (_signalTextBoxes.ContainsKey(key))
            {
                var txt = _signalTextBoxes[key];
                if (!isChecked)
                {
                    txt.BackColor = dimGrey;
                    txt.ForeColor = Color.FromArgb(170, 170, 170);
                }
                else
                {
                    // Restore from signal cache if available
                    if (_signalCache != null && _signalCache.ContainsKey(key))
                    {
                        txt.BackColor = _signalCache[key].SignalColor;
                        txt.ForeColor = Color.White;
                    }
                }
            }

            // Buy circle
            if (_buyAnalysisCircles.ContainsKey(key))
            {
                if (!isChecked)
                    _buyAnalysisCircles[key].BackColor = dimGrey;
            }

            // Sell circle
            if (_sellAnalysisCircles.ContainsKey(key))
            {
                if (!isChecked)
                    _sellAnalysisCircles[key].BackColor = dimGrey;
            }

            // Buy count label
            if (_buyCountLabels.ContainsKey(key))
                _buyCountLabels[key].ForeColor = isChecked
                    ? Color.FromArgb(0, 150, 80)
                    : Color.FromArgb(190, 190, 190);

            // Sell count label
            if (_sellCountLabels.ContainsKey(key))
                _sellCountLabels[key].ForeColor = isChecked
                    ? Color.FromArgb(200, 40, 40)
                    : Color.FromArgb(190, 190, 190);
        }

        // ============================================================
        // Apply greyed-out state to all unchecked indicators on load
        // ============================================================
        private void ApplyInitialVisualStates()
        {
            var checkedKeys = GetCheckedIndicatorKeys();
            foreach (var def in _allIndicators)
            {
                bool isChecked = checkedKeys.Contains(def.Key);
                UpdateIndicatorVisualState(def.Key, isChecked);
            }
        }

        // ============================================================
        // Lock indicators during simulation: grey out unchecked, hide their chart lines
        // ============================================================
        private void ApplySimulationIndicatorLock()
        {
            _simulationActive = true;
            var checkedKeys = GetCheckedIndicatorKeys();
            Color lockGrey = Color.FromArgb(200, 200, 200);
            Color lockTextGrey = Color.FromArgb(170, 170, 170);

            // Save analysis circle colors & count texts before greying out
            _savedBuyCircleColors.Clear();
            _savedSellCircleColors.Clear();
            _savedBuyCountTexts.Clear();
            _savedSellCountTexts.Clear();
            foreach (var kvp in _buyAnalysisCircles)
                _savedBuyCircleColors[kvp.Key] = kvp.Value.BackColor;
            foreach (var kvp in _sellAnalysisCircles)
                _savedSellCircleColors[kvp.Key] = kvp.Value.BackColor;
            foreach (var kvp in _buyCountLabels)
                _savedBuyCountTexts[kvp.Key] = kvp.Value.Text;
            foreach (var kvp in _sellCountLabels)
                _savedSellCountTexts[kvp.Key] = kvp.Value.Text;

            foreach (var def in _allIndicators)
            {
                bool isChecked = checkedKeys.Contains(def.Key);

                if (!isChecked)
                {
                    // Grey out signal text box
                    if (_signalTextBoxes.ContainsKey(def.Key))
                    {
                        _signalTextBoxes[def.Key].BackColor = lockGrey;
                        _signalTextBoxes[def.Key].ForeColor = lockTextGrey;
                        _signalTextBoxes[def.Key].Text = "";
                    }

                    // Grey out buy/sell circles
                    if (_buyAnalysisCircles.ContainsKey(def.Key))
                        _buyAnalysisCircles[def.Key].BackColor = lockGrey;
                    if (_sellAnalysisCircles.ContainsKey(def.Key))
                        _sellAnalysisCircles[def.Key].BackColor = lockGrey;

                    // Grey out count labels
                    if (_buyCountLabels.ContainsKey(def.Key))
                    {
                        _buyCountLabels[def.Key].ForeColor = Color.FromArgb(190, 190, 190);
                        _buyCountLabels[def.Key].Text = "";
                    }
                    if (_sellCountLabels.ContainsKey(def.Key))
                    {
                        _sellCountLabels[def.Key].ForeColor = Color.FromArgb(190, 190, 190);
                        _sellCountLabels[def.Key].Text = "";
                    }

                    // Remove chart lines for unchecked indicators
                    foreach (var si in def.SeriesList)
                    {
                        if (chartStock.Series.FindByName(si.SeriesName) != null)
                            chartStock.Series.Remove(chartStock.Series[si.SeriesName]);
                    }
                    if (def.AreaType == ChartAreaType.SeparatePane)
                    {
                        if (chartStock.ChartAreas.FindByName(def.ChartAreaName) != null)
                            chartStock.ChartAreas.Remove(chartStock.ChartAreas[def.ChartAreaName]);
                    }
                }
            }

            // Reflow chart areas after removing panes
            ReflowChartAreas();

            // Force redraw of the CheckedListBox so unchecked text appears greyed
            chkIndicators.Invalidate();
        }

        // ============================================================
        // Unlock indicators after simulation is cleared: restore all visuals
        // ============================================================
        private void ReleaseSimulationIndicatorLock()
        {
            _simulationActive = false;
            var checkedKeys = GetCheckedIndicatorKeys();

            // Restore saved analysis circle colors
            foreach (var kvp in _savedBuyCircleColors)
                if (_buyAnalysisCircles.ContainsKey(kvp.Key))
                    _buyAnalysisCircles[kvp.Key].BackColor = kvp.Value;
            foreach (var kvp in _savedSellCircleColors)
                if (_sellAnalysisCircles.ContainsKey(kvp.Key))
                    _sellAnalysisCircles[kvp.Key].BackColor = kvp.Value;
            foreach (var kvp in _savedBuyCountTexts)
                if (_buyCountLabels.ContainsKey(kvp.Key))
                    _buyCountLabels[kvp.Key].Text = kvp.Value;
            foreach (var kvp in _savedSellCountTexts)
                if (_sellCountLabels.ContainsKey(kvp.Key))
                    _sellCountLabels[kvp.Key].Text = kvp.Value;

            // Re-add chart lines for unchecked indicators that were in _activeIndicatorKeys
            // (they were visually removed but still tracked as active)
            foreach (var def in _allIndicators)
            {
                bool isChecked = checkedKeys.Contains(def.Key);

                if (!isChecked && _activeIndicatorKeys.Contains(def.Key))
                {
                    // Re-add chart series
                    if (def.AreaType == ChartAreaType.SeparatePane)
                        CreateSecondaryChartArea(def);
                    PopulateIndicatorSeries(def);
                }
            }

            ReflowChartAreas();

            // Restore visual states for all indicators
            ApplyInitialVisualStates();

            // Re-compute signals at crosshair to restore signal text boxes
            if (_crosshairIndex >= 0 && _crosshairIndex < _cachedDates.Count)
                ComputeSignalsAtIndex(_crosshairIndex);

            chkIndicators.Invalidate();
        }

        // ============================================================
        // Add an indicator overlay to the chart
        // ============================================================
        private void AddIndicator(IndicatorDefinition def)
        {
            if (_cachedDates.Count == 0) return;
            if (_activeIndicatorKeys.Contains(def.Key)) return;

            _activeIndicatorKeys.Add(def.Key);

            // Create secondary chart area if needed
            if (def.AreaType == ChartAreaType.SeparatePane)
            {
                CreateSecondaryChartArea(def);
            }

            // Calculate and populate series
            PopulateIndicatorSeries(def);

            // Reflow chart areas
            ReflowChartAreas();
        }

        // ============================================================
        // Remove an indicator overlay from the chart
        // ============================================================
        private void RemoveIndicator(IndicatorDefinition def)
        {
            if (!_activeIndicatorKeys.Contains(def.Key)) return;

            _activeIndicatorKeys.Remove(def.Key);

            // Remove all series for this indicator
            foreach (var si in def.SeriesList)
            {
                if (chartStock.Series.FindByName(si.SeriesName) != null)
                    chartStock.Series.Remove(chartStock.Series[si.SeriesName]);
            }

            // Remove secondary chart area if applicable
            if (def.AreaType == ChartAreaType.SeparatePane)
            {
                if (chartStock.ChartAreas.FindByName(def.ChartAreaName) != null)
                    chartStock.ChartAreas.Remove(chartStock.ChartAreas[def.ChartAreaName]);
            }

            ReflowChartAreas();
        }

        // ============================================================
        // Re-apply all active indicators (after symbol change)
        // ============================================================
        private void ApplyActiveIndicators()
        {
            // Collect currently active keys
            var activeKeys = new List<string>(_activeIndicatorKeys);

            // Remove all indicator series and secondary chart areas
            foreach (var key in activeKeys)
            {
                var def = _allIndicators.First(d => d.Key == key);
                foreach (var si in def.SeriesList)
                {
                    if (chartStock.Series.FindByName(si.SeriesName) != null)
                        chartStock.Series.Remove(chartStock.Series[si.SeriesName]);
                }
                if (def.AreaType == ChartAreaType.SeparatePane)
                {
                    if (chartStock.ChartAreas.FindByName(def.ChartAreaName) != null)
                        chartStock.ChartAreas.Remove(chartStock.ChartAreas[def.ChartAreaName]);
                }
            }

            _activeIndicatorKeys.Clear();

            // Re-add each active indicator with new data
            foreach (var key in activeKeys)
            {
                var def = _allIndicators.First(d => d.Key == key);
                _activeIndicatorKeys.Add(def.Key);

                if (def.AreaType == ChartAreaType.SeparatePane)
                    CreateSecondaryChartArea(def);

                PopulateIndicatorSeries(def);
            }

            ReflowChartAreas();
        }

        // ============================================================
        // Create a secondary ChartArea for separate-pane indicators
        // ============================================================
        private void CreateSecondaryChartArea(IndicatorDefinition def)
        {
            if (chartStock.ChartAreas.FindByName(def.ChartAreaName) != null)
                return;

            var area = new ChartArea(def.ChartAreaName);
            area.AlignWithChartArea = "MainArea";
            area.AlignmentOrientation = AreaAlignmentOrientations.Vertical;
            area.AlignmentStyle = AreaAlignmentStyles.All;

            area.BackColor = Color.White;
            area.AxisX.MajorGrid.LineColor = Color.LightGray;
            area.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            area.AxisX.LabelStyle.Enabled = false;
            area.AxisX.MajorTickMark.Enabled = false;

            area.AxisY.MajorGrid.LineColor = Color.LightGray;
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            area.AxisY.IsStartedFromZero = false;
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 7F);

            if (def.YAxisMin.HasValue) area.AxisY.Minimum = def.YAxisMin.Value;
            if (def.YAxisMax.HasValue) area.AxisY.Maximum = def.YAxisMax.Value;

            // Add reference lines (StripLines)
            if (def.ReferenceLines != null)
            {
                foreach (var refVal in def.ReferenceLines)
                {
                    var strip = new StripLine();
                    strip.IntervalOffset = refVal;
                    strip.Interval = 0;
                    strip.StripWidth = 0;
                    strip.BorderColor = Color.Gray;
                    strip.BorderDashStyle = ChartDashStyle.Dash;
                    strip.BorderWidth = 1;
                    area.AxisY.StripLines.Add(strip);
                }
            }

            // Pane title on Y2 axis
            area.AxisY2.Title = def.DisplayName;
            area.AxisY2.TitleFont = new Font("Segoe UI", 7F, FontStyle.Bold);
            area.AxisY2.Enabled = AxisEnabled.True;
            area.AxisY2.MajorTickMark.Enabled = false;
            area.AxisY2.LabelStyle.Enabled = false;
            area.AxisY2.MajorGrid.Enabled = false;

            chartStock.ChartAreas.Add(area);
        }

        // ============================================================
        // Reflow chart areas - dynamic vertical sizing
        // ============================================================
        private void ReflowChartAreas()
        {
            int secondaryCount = chartStock.ChartAreas.Count - 1;
            float mainHeight;
            float secondaryHeight = 0f;

            if (secondaryCount == 0)
            {
                mainHeight = 95f;
            }
            else
            {
                // Cap secondary panes: each gets ~12%, main gets the rest
                secondaryHeight = Math.Min(12f, 45f / secondaryCount);
                mainHeight = 95f - (secondaryCount * secondaryHeight);
                if (mainHeight < 30f) mainHeight = 30f;
                secondaryHeight = (95f - mainHeight) / secondaryCount;
            }

            float yPos = 2f;
            var mainArea = chartStock.ChartAreas["MainArea"];
            mainArea.Position = new ElementPosition(8f, yPos, 90f, mainHeight);
            yPos += mainHeight + 1f;

            foreach (var area in chartStock.ChartAreas)
            {
                if (area.Name == "MainArea") continue;
                area.Position = new ElementPosition(8f, yPos, 90f, secondaryHeight);
                yPos += secondaryHeight + 0.5f;
            }
        }

        // ============================================================
        // Calculate and populate indicator series
        // ============================================================
        private void PopulateIndicatorSeries(IndicatorDefinition def)
        {
            switch (def.Key)
            {
                case "SMA":
                    {
                        var sma = TechnicalIndicatorCalcs.CalculateSMA(_cachedCloses, 20);
                        AddLineSeries(def.SeriesList[0], _cachedDates, sma, def.ChartAreaName);
                    }
                    break;

                case "EMA":
                    {
                        var ema = TechnicalIndicatorCalcs.CalculateEMA(_cachedCloses, 12);
                        AddLineSeries(def.SeriesList[0], _cachedDates, ema, def.ChartAreaName);
                    }
                    break;

                case "MACD":
                    {
                        var macd = TechnicalIndicatorCalcs.CalculateMACD(_cachedCloses, 12, 26, 9);
                        AddLineSeries(def.SeriesList[0], _cachedDates, macd.MACDLine, def.ChartAreaName);
                        AddLineSeries(def.SeriesList[1], _cachedDates, macd.SignalLine, def.ChartAreaName);
                        AddColumnSeries(def.SeriesList[2], _cachedDates, macd.Histogram, def.ChartAreaName);
                    }
                    break;

                case "ADX":
                    {
                        var adx = TechnicalIndicatorCalcs.CalculateADXWithDI(_cachedHighs, _cachedLows, _cachedCloses, 14);
                        AddLineSeries(def.SeriesList[0], _cachedDates, adx.ADX, def.ChartAreaName);
                        AddLineSeries(def.SeriesList[1], _cachedDates, adx.PlusDI, def.ChartAreaName);
                        AddLineSeries(def.SeriesList[2], _cachedDates, adx.MinusDI, def.ChartAreaName);
                    }
                    break;

                case "PSAR":
                    {
                        var psar = TechnicalIndicatorCalcs.CalculateParabolicSAR(_cachedHighs, _cachedLows, 0.02m, 0.2m);
                        AddPointSeries(def.SeriesList[0], _cachedDates, psar, def.ChartAreaName);
                    }
                    break;

                case "ICHI":
                    {
                        var ichi = TechnicalIndicatorCalcs.CalculateIchimoku(_cachedHighs, _cachedLows, _cachedCloses);
                        AddLineSeries(def.SeriesList[0], _cachedDates, ichi.TenkanSen, def.ChartAreaName);
                        AddLineSeries(def.SeriesList[1], _cachedDates, ichi.KijunSen, def.ChartAreaName);
                        // Senkou Span A & B displaced forward 26 periods
                        AddLineSeries(def.SeriesList[2], _cachedDates, ichi.SenkouSpanA, def.ChartAreaName, displacement: ichi.Displacement);
                        AddLineSeries(def.SeriesList[3], _cachedDates, ichi.SenkouSpanB, def.ChartAreaName, displacement: ichi.Displacement);
                        // Chikou Span displaced back 26 periods
                        AddLineSeries(def.SeriesList[4], _cachedDates, ichi.ChikouSpan, def.ChartAreaName, displacement: -ichi.Displacement);
                    }
                    break;

                case "RSI":
                    {
                        var rsi = TechnicalIndicatorCalcs.CalculateRSI(_cachedCloses, 14);
                        AddLineSeries(def.SeriesList[0], _cachedDates, rsi, def.ChartAreaName);
                    }
                    break;

                case "STOCH":
                    {
                        var stoch = TechnicalIndicatorCalcs.CalculateStochastic(_cachedHighs, _cachedLows, _cachedCloses, 14, 3);
                        AddLineSeries(def.SeriesList[0], _cachedDates, stoch.PercentK, def.ChartAreaName);
                        AddLineSeries(def.SeriesList[1], _cachedDates, stoch.PercentD, def.ChartAreaName);
                    }
                    break;

                case "MOM":
                    {
                        var mom = TechnicalIndicatorCalcs.CalculateMomentum(_cachedCloses, 10);
                        AddLineSeries(def.SeriesList[0], _cachedDates, mom, def.ChartAreaName);
                    }
                    break;

                case "ROC":
                    {
                        var roc = TechnicalIndicatorCalcs.CalculateROC(_cachedCloses, 12);
                        AddLineSeries(def.SeriesList[0], _cachedDates, roc, def.ChartAreaName);
                    }
                    break;

                case "CCI":
                    {
                        var cci = TechnicalIndicatorCalcs.CalculateCCI(_cachedHighs, _cachedLows, _cachedCloses, 20);
                        AddLineSeries(def.SeriesList[0], _cachedDates, cci, def.ChartAreaName);
                    }
                    break;

                case "WILLR":
                    {
                        var willr = TechnicalIndicatorCalcs.CalculateWilliamsR(_cachedHighs, _cachedLows, _cachedCloses, 14);
                        AddLineSeries(def.SeriesList[0], _cachedDates, willr, def.ChartAreaName);
                    }
                    break;

                case "BB":
                    {
                        var bb = TechnicalIndicatorCalcs.CalculateBollingerBands(_cachedCloses, 20, 2.0m);
                        AddLineSeries(def.SeriesList[0], _cachedDates, bb.UpperBand, def.ChartAreaName);
                        AddLineSeries(def.SeriesList[1], _cachedDates, bb.MiddleBand, def.ChartAreaName);
                        AddLineSeries(def.SeriesList[2], _cachedDates, bb.LowerBand, def.ChartAreaName);
                    }
                    break;

                case "KC":
                    {
                        var kc = TechnicalIndicatorCalcs.CalculateKeltnerChannels(_cachedHighs, _cachedLows, _cachedCloses, 20, 10, 2.0m);
                        AddLineSeries(def.SeriesList[0], _cachedDates, kc.UpperChannel, def.ChartAreaName);
                        AddLineSeries(def.SeriesList[1], _cachedDates, kc.MiddleLine, def.ChartAreaName);
                        AddLineSeries(def.SeriesList[2], _cachedDates, kc.LowerChannel, def.ChartAreaName);
                    }
                    break;

                case "ATR":
                    {
                        var atr = TechnicalIndicatorCalcs.CalculateATR(_cachedHighs, _cachedLows, _cachedCloses, 14);
                        AddLineSeries(def.SeriesList[0], _cachedDates, atr, def.ChartAreaName);
                    }
                    break;

                case "OBV":
                    {
                        var obv = TechnicalIndicatorCalcs.CalculateOBV(_cachedCloses, _cachedVolumes);
                        AddLineSeries(def.SeriesList[0], _cachedDates, obv, def.ChartAreaName);
                    }
                    break;

                case "MFI":
                    {
                        var mfi = TechnicalIndicatorCalcs.CalculateMFI(_cachedHighs, _cachedLows, _cachedCloses, _cachedVolumes, 14);
                        AddLineSeries(def.SeriesList[0], _cachedDates, mfi, def.ChartAreaName);
                    }
                    break;

                case "CMF":
                    {
                        var cmf = TechnicalIndicatorCalcs.CalculateChaikinMoneyFlow(_cachedHighs, _cachedLows, _cachedCloses, _cachedVolumes, 20);
                        AddLineSeries(def.SeriesList[0], _cachedDates, cmf, def.ChartAreaName);
                    }
                    break;

                case "ADL":
                    {
                        var adl = TechnicalIndicatorCalcs.CalculateADL(_cachedHighs, _cachedLows, _cachedCloses, _cachedVolumes);
                        AddLineSeries(def.SeriesList[0], _cachedDates, adl, def.ChartAreaName);
                    }
                    break;

                case "VWAP":
                    {
                        var vwap = TechnicalIndicatorCalcs.CalculateVWAP(_cachedHighs, _cachedLows, _cachedCloses, _cachedVolumes);
                        AddLineSeries(def.SeriesList[0], _cachedDates, vwap, def.ChartAreaName);
                    }
                    break;

                case "AROON":
                    {
                        var aroon = TechnicalIndicatorCalcs.CalculateAroonWithOscillator(_cachedHighs, _cachedLows, 25);
                        AddLineSeries(def.SeriesList[0], _cachedDates, aroon.AroonUp, def.ChartAreaName);
                        AddLineSeries(def.SeriesList[1], _cachedDates, aroon.AroonDown, def.ChartAreaName);
                    }
                    break;
            }
        }

        // ============================================================
        // Helper: Add a Line series
        // ============================================================
        private void AddLineSeries(IndicatorSeriesInfo info, List<DateTime> dates, List<decimal?> values, string chartAreaName, int displacement = 0)
        {
            var s = new Series(info.SeriesName);
            s.ChartType = info.ChartType;
            s.ChartArea = chartAreaName;
            s.Color = info.Color;
            s.BorderWidth = info.BorderWidth;
            s.BorderDashStyle = info.DashStyle;
            s.IsVisibleInLegend = false;

            for (int i = 0; i < values.Count; i++)
            {
                if (!values[i].HasValue) continue;

                int dateIdx = i + displacement;
                if (dateIdx < 0 || dateIdx >= dates.Count) continue;

                s.Points.AddXY(dateIdx, (double)values[i].Value);
            }

            chartStock.Series.Add(s);
        }

        // ============================================================
        // Helper: Add a Point series (e.g. Parabolic SAR dots)
        // ============================================================
        private void AddPointSeries(IndicatorSeriesInfo info, List<DateTime> dates, List<decimal?> values, string chartAreaName)
        {
            var s = new Series(info.SeriesName);
            s.ChartType = SeriesChartType.Point;
            s.ChartArea = chartAreaName;
            s.Color = info.Color;
            s.MarkerSize = info.MarkerSize > 0 ? info.MarkerSize : 4;
            s.MarkerStyle = MarkerStyle.Circle;
            s.IsVisibleInLegend = false;

            for (int i = 0; i < values.Count; i++)
            {
                if (!values[i].HasValue) continue;
                s.Points.AddXY(i, (double)values[i].Value);
            }

            chartStock.Series.Add(s);
        }

        // ============================================================
        // Helper: Add a Column series (e.g. MACD Histogram)
        // ============================================================
        private void AddColumnSeries(IndicatorSeriesInfo info, List<DateTime> dates, List<decimal?> values, string chartAreaName)
        {
            var s = new Series(info.SeriesName);
            s.ChartType = SeriesChartType.Column;
            s.ChartArea = chartAreaName;
            s.Color = info.Color;
            s.BorderWidth = 0;
            s.IsVisibleInLegend = false;
            s["PointWidth"] = "0.8";

            for (int i = 0; i < values.Count; i++)
            {
                if (!values[i].HasValue) continue;

                int idx = s.Points.AddXY(i, (double)values[i].Value);

                // Color bars: green for positive, red for negative
                if (values[i].Value >= 0)
                    s.Points[idx].Color = Color.FromArgb(120, Color.ForestGreen);
                else
                    s.Points[idx].Color = Color.FromArgb(120, Color.IndianRed);
            }

            chartStock.Series.Add(s);
        }

        // ============================================================
        // Debug: Test connection and show detailed log
        // ============================================================
        private async Task TestConnectionAsync()
        {
            var log = new StringBuilder();
            log.AppendLine("===== DATABASE CONNECTION DEBUG LOG =====");
            log.AppendLine();

            string configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AlpacaTrader", "dbconfig.json");

            if (File.Exists(configPath))
            {
                log.AppendLine($"[CONFIG] Loaded from: {configPath}");
                try
                {
                    string json = File.ReadAllText(configPath);
                    log.AppendLine($"[CONFIG] Contents: {json}");
                }
                catch { }
            }
            else
            {
                log.AppendLine($"[CONFIG] File NOT found: {configPath}");
                log.AppendLine("[CONFIG] Using defaults: Tiger2023\\ZProduction / Stock System");
            }

            log.AppendLine();
            log.AppendLine("[CONNECTION DETAILS]");
            log.AppendLine($"  Server (Data Source):    {_config.Server}");
            log.AppendLine($"  Database (Catalog):     {_config.Database}");
            log.AppendLine($"  Windows Auth:           {_config.UseWindowsAuth}");
            log.AppendLine($"  Trust Server Cert:      {_config.TrustServerCertificate}");
            log.AppendLine($"  Current Windows User:   {Environment.UserDomainName}\\{Environment.UserName}");
            log.AppendLine($"  Machine Name:           {Environment.MachineName}");
            log.AppendLine($"  Connection String:      {_config.ConnectionString}");

            log.AppendLine();
            log.AppendLine("[CONNECTION TEST]");
            lblStatus.Text = "Testing database connection...";
            var sw = Stopwatch.StartNew();

            try
            {
                using (var conn = await _db.GetOpenConnectionAsync())
                {
                    sw.Stop();
                    log.AppendLine($"  STATUS: CONNECTED  ({sw.ElapsedMilliseconds} ms)");
                    log.AppendLine($"  Server Version:         {conn.ServerVersion}");
                    log.AppendLine($"  Database:               {conn.Database}");

                    log.AppendLine();
                    log.AppendLine("[TABLE TEST: Stock List]");
                    try
                    {
                        using (var cmd = new SqlCommand("SELECT COUNT(*) FROM [Stock List]", conn))
                        {
                            cmd.CommandTimeout = 15;
                            var count = await cmd.ExecuteScalarAsync();
                            log.AppendLine($"  Row count: {count}");
                        }
                    }
                    catch (Exception ex) { log.AppendLine($"  ERROR: {ex.Message}"); }

                    log.AppendLine();
                    log.AppendLine("[TABLE TEST: 5 Minute Data]");
                    try
                    {
                        using (var cmd = new SqlCommand("SELECT COUNT(*) FROM [5 Minute Data]", conn))
                        {
                            cmd.CommandTimeout = 30;
                            var count = await cmd.ExecuteScalarAsync();
                            log.AppendLine($"  Row count: {count}");
                        }
                    }
                    catch (Exception ex) { log.AppendLine($"  ERROR: {ex.Message}"); }

                    log.AppendLine();
                    log.AppendLine("[COLUMN CHECK: 5 Minute Data]");
                    try
                    {
                        using (var cmd = new SqlCommand("SELECT TOP 0 * FROM [5 Minute Data]", conn))
                        {
                            cmd.CommandTimeout = 15;
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                var schema = reader.GetSchemaTable();
                                if (schema != null)
                                {
                                    foreach (System.Data.DataRow row in schema.Rows)
                                    {
                                        log.AppendLine($"  [{row["ColumnName"]}]  {row["DataTypeName"]}({row["ColumnSize"]})");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { log.AppendLine($"  ERROR: {ex.Message}"); }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                log.AppendLine($"  STATUS: FAILED  ({sw.ElapsedMilliseconds} ms)");
                log.AppendLine($"  Error Type:    {ex.GetType().Name}");
                log.AppendLine($"  Message:       {ex.Message}");
                if (ex.InnerException != null)
                    log.AppendLine($"  Inner:         {ex.InnerException.Message}");
            }

            log.AppendLine();
            log.AppendLine("=========================================");

            string result = log.ToString();
            Debug.WriteLine(result);

            MessageBox.Show(result,
                "Database Connection Debug Log",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ============================================================
        // Load symbols from [Stock List]
        // ============================================================
        private async Task LoadSymbolsAsync()
        {
            lblStatus.Text = "Loading symbols...";
            cboSymbols.Enabled = false;

            try
            {
                using (var conn = await _db.GetOpenConnectionAsync())
                {
                    const string query = "SELECT [Symbol] FROM [Stock List] ORDER BY [Symbol]";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 30;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            cboSymbols.BeginUpdate();
                            cboSymbols.Items.Clear();

                            while (await reader.ReadAsync())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    string symbol = reader.GetString(0).Trim();
                                    if (symbol.Length > 0)
                                        cboSymbols.Items.Add(symbol);
                                }
                            }

                            cboSymbols.EndUpdate();
                        }
                    }
                }

                lblStatus.Text = $"{cboSymbols.Items.Count:N0} symbols loaded";
                cboSymbols.Enabled = true;

                // Default to MSFT and auto-load its chart
                int msftIdx = cboSymbols.Items.IndexOf("MSFT");
                if (msftIdx >= 0)
                {
                    cboSymbols.SelectedIndex = msftIdx;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error loading symbols";
                MessageBox.Show(
                    $"Failed to load symbols:\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async void cboSymbols_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboSymbols.SelectedItem == null) return;
            string symbol = cboSymbols.SelectedItem.ToString();
            await LoadChartDataAsync(symbol);
        }

        // ============================================================
        // Validate symbol input: letters only, max 4 characters
        // ============================================================
        private void cboSymbols_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Allow control keys (backspace, etc.)
            if (char.IsControl(e.KeyChar)) return;

            // Block non-letter characters
            if (!char.IsLetter(e.KeyChar))
            {
                e.Handled = true;
                return;
            }

            // Force uppercase
            e.KeyChar = char.ToUpper(e.KeyChar);

            // Block if already at 4 characters (and no text is selected)
            if (cboSymbols.Text.Length >= 4 && cboSymbols.SelectionLength == 0)
            {
                e.Handled = true;
            }
        }

        // ============================================================
        // Get Data button — fetch all available 5-min data from FMP
        // ============================================================
        private async void btnGetData_Click(object sender, EventArgs e)
        {
            string symbol = cboSymbols.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(symbol)) return;

            btnGetData.Enabled = false;
            btnGetData.Text = "Fetching...";
            lblStatus.Text = $"Fetching FMP 5-min data for {symbol}...";

            try
            {
                await FetchAndStoreFmpDataAsync(symbol);
                await LoadChartDataAsync(symbol);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Get Data failed:\n\n{ex.Message}", "FMP Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Get Data failed.";
            }
            finally
            {
                btnGetData.Enabled = true;
                btnGetData.Text = "Get Data";
            }
        }

        private async Task FetchAndStoreFmpDataAsync(string symbol)
        {
            var dbg = _debugLog;

            // Position debug window to the right of the main form
            dbg.Location = new Point(this.Right + 8, this.Top);
            dbg.Show();
            dbg.BringToFront();
            dbg.LogSection($"GET DATA  —  {symbol}  —  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            // ── Step 1: Read API key ────────────────────────────────
            dbg.SetSummary($"{symbol} — Reading API key...");
            dbg.LogSql("SELECT [Key1] FROM [APIKeys] WHERE [Api Compnay] = 'FMP'");
            string apiKey = "";
            using (var conn = await _db.GetOpenConnectionAsync())
            {
                var keyCmd = new SqlCommand(
                    "SELECT [Key1] FROM [APIKeys] WHERE [Api Compnay] = 'FMP'", conn);
                var result = await keyCmd.ExecuteScalarAsync();
                apiKey = result as string ?? "";
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                dbg.LogSqlError("FMP API key not found in [APIKeys] table.");
                throw new Exception("FMP API key not found in [APIKeys] table.");
            }
            dbg.LogSqlOk($"API key retrieved  ({apiKey.Substring(0, Math.Min(8, apiKey.Length))}***)");

            // ── Step 2: Clear existing data ─────────────────────────
            dbg.LogSection($"SQL — Clearing existing {symbol} data");
            lblStatus.Text = $"Clearing existing {symbol} data...";
            Application.DoEvents();
            dbg.SetSummary($"{symbol} — Clearing old data from [5 Minute Data]...");
            dbg.LogSql($"DELETE FROM [5 Minute Data] WHERE [Symbol] = '{symbol}'");

            int deletedRows = 0;
            using (var conn = await _db.GetOpenConnectionAsync())
            {
                var clearCmd = new SqlCommand(
                    "DELETE FROM [5 Minute Data] WHERE [Symbol] = @Sym", conn);
                clearCmd.Parameters.Add("@Sym", System.Data.SqlDbType.NVarChar, 10).Value = symbol;
                clearCmd.CommandTimeout = 60;
                deletedRows = await clearCmd.ExecuteNonQueryAsync();
            }
            dbg.LogSqlOk($"Deleted {deletedRows:N0} existing rows for {symbol}");

            // Verify the delete took effect
            using (var conn = await _db.GetOpenConnectionAsync())
            {
                var checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM [5 Minute Data] WHERE [Symbol] = @Sym", conn);
                checkCmd.Parameters.Add("@Sym", System.Data.SqlDbType.NVarChar, 10).Value = symbol;
                int remaining = (int)await checkCmd.ExecuteScalarAsync();
                if (remaining > 0)
                    dbg.LogSqlError($"WARNING: {remaining:N0} rows still remain after delete!");
                else
                    dbg.LogSqlOk($"Verified: 0 rows remain for {symbol} ✓");
            }

            // ── Step 3: Fetch from FMP ──────────────────────────────
            dbg.LogSection("FMP API — Fetching 5-min bars (backward weekly chunks)");

            var today = DateTime.Today;
            var fetchStart = today.AddYears(-1);
            dbg.LogInfo($"Date range: {fetchStart:yyyy-MM-dd} → {today:yyyy-MM-dd}");

            var allBars = new List<(DateTime dt, decimal open, decimal high, decimal low, decimal close, long volume)>();
            int totalBarsReceived = 0;
            int emptyChunks = 0;

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(30);

            DateTime chunkTo = today;
            int totalChunks = (int)Math.Ceiling((today - fetchStart).TotalDays / 7.0);
            int chunkNum = 0;

            while (chunkTo > fetchStart)
            {
                chunkNum++;
                DateTime chunkFrom = chunkTo.AddDays(-6);
                if (chunkFrom < fetchStart) chunkFrom = fetchStart;

                string fromStr = chunkFrom.ToString("yyyy-MM-dd");
                string toStr   = chunkTo.ToString("yyyy-MM-dd");

                string safeUrl = $"https://financialmodelingprep.com/stable/historical-chart/5min" +
                                 $"?symbol={symbol}&from={fromStr}&to={toStr}&apikey=***";
                string url     = $"https://financialmodelingprep.com/stable/historical-chart/5min" +
                                 $"?symbol={symbol}&from={fromStr}&to={toStr}&apikey={apiKey}";

                lblStatus.Text = $"Fetching {symbol}  {fromStr} → {toStr}  ({chunkNum}/{totalChunks})";
                dbg.SetSummary($"{symbol} — Chunk {chunkNum}/{totalChunks}  |  {fromStr} → {toStr}  |  {totalBarsReceived:N0} bars so far");
                Application.DoEvents();

                dbg.LogFmpRequest(safeUrl);

                System.Net.Http.HttpResponseMessage response;
                string json;
                try
                {
                    response = await http.GetAsync(url);
                    json = await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    dbg.LogFmpError($"HTTP exception: {ex.Message}");
                    throw;
                }

                if (!response.IsSuccessStatusCode)
                {
                    dbg.LogFmpError($"HTTP {(int)response.StatusCode}  body: {json.Substring(0, Math.Min(200, json.Length))}");
                    throw new Exception($"FMP returned HTTP {(int)response.StatusCode} for {fromStr}→{toStr}");
                }

                if (json.Contains("\"Error Message\"") || json.Contains("\"error\""))
                {
                    dbg.LogFmpError($"API error: {json.Substring(0, Math.Min(300, json.Length))}");
                    throw new Exception($"FMP API error: {json.Substring(0, Math.Min(300, json.Length))}");
                }

                int barsThisChunk = 0;
                if (json.TrimStart().StartsWith("[") && json.Length > 2)
                {
                    using var doc = JsonDocument.Parse(json);
                    string earliest = "", latest = "";
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        string dateStr = el.GetProperty("date").GetString() ?? "";
                        if (!DateTime.TryParse(dateStr, out DateTime dt)) continue;

                        decimal open  = el.GetProperty("open").GetDecimal();
                        decimal high  = el.GetProperty("high").GetDecimal();
                        decimal low   = el.GetProperty("low").GetDecimal();
                        decimal close = el.GetProperty("close").GetDecimal();
                        long volume   = el.TryGetProperty("volume", out var vp) ? vp.GetInt64() : 0;

                        allBars.Add((dt, open, high, low, close, volume));
                        barsThisChunk++;
                        if (earliest == "") earliest = dateStr;
                        latest = dateStr;
                    }
                    if (barsThisChunk > 0)
                        dbg.LogFmpResponse(barsThisChunk, earliest, latest);
                    else
                    {
                        dbg.LogFmpResponse(0, "", "", empty: true);
                        emptyChunks++;
                    }
                }
                else
                {
                    dbg.LogFmpResponse(0, "", "", empty: true);
                    emptyChunks++;
                }

                totalBarsReceived += barsThisChunk;
                chunkTo = chunkFrom.AddDays(-1);
                await Task.Delay(200);
            }

            dbg.LogSection("FMP Fetch Complete");
            dbg.LogInfo($"Total chunks: {chunkNum}   Empty chunks: {emptyChunks}");
            dbg.LogInfo($"Total raw bars received: {totalBarsReceived:N0}");

            if (allBars.Count == 0)
            {
                dbg.LogFmpError($"No data returned at all for {symbol}.");
                throw new Exception($"No 5-minute data returned by FMP for {symbol}.");
            }

            // Sort and deduplicate
            allBars.Sort((a, b) => a.dt.CompareTo(b.dt));
            var seen = new HashSet<DateTime>();
            var dedupedBars = allBars.Where(b => seen.Add(b.dt)).ToList();
            int dupes = totalBarsReceived - dedupedBars.Count;
            dbg.LogInfo($"After dedup: {dedupedBars.Count:N0} bars  ({dupes:N0} duplicates removed)");
            dbg.LogInfo($"Date range in data: {dedupedBars[0].dt:yyyy-MM-dd HH:mm} → {dedupedBars[dedupedBars.Count-1].dt:yyyy-MM-dd HH:mm}");

            // ── Step 4: Insert into SQL ─────────────────────────────
            dbg.LogSection($"SQL — Inserting {dedupedBars.Count:N0} bars into [5 Minute Data]");
            lblStatus.Text = $"Saving {dedupedBars.Count:N0} bars for {symbol} to database...";
            dbg.SetSummary($"{symbol} — Inserting {dedupedBars.Count:N0} bars into SQL...");
            Application.DoEvents();

            DateTime minDt = dedupedBars[0].dt;
            DateTime maxDt = dedupedBars[dedupedBars.Count - 1].dt;

            int insertedRows = 0;
            using (var conn = await _db.GetOpenConnectionAsync())
            {
                const string insertSql = @"
                    INSERT INTO [5 Minute Data]
                        ([Symbol],[Date],[Time],[Open],[High],[Low],[Close],[AdjClose],[Volume],[VWAP],[Change],[ChangePercent],[Price])
                    VALUES
                        (@Sym,@Date,@Time,@Open,@High,@Low,@Close,@AdjClose,@Volume,0,0,0,@Close)";

                using var insCmd = new SqlCommand(insertSql, conn);
                insCmd.CommandTimeout = 120;
                var pSym  = insCmd.Parameters.Add("@Sym",      System.Data.SqlDbType.NVarChar, 10);
                var pDate = insCmd.Parameters.Add("@Date",     System.Data.SqlDbType.Date);
                var pTime = insCmd.Parameters.Add("@Time",     System.Data.SqlDbType.Time);
                var pOpen = insCmd.Parameters.Add("@Open",     System.Data.SqlDbType.Decimal); pOpen.Precision = 18; pOpen.Scale = 4;
                var pHigh = insCmd.Parameters.Add("@High",     System.Data.SqlDbType.Decimal); pHigh.Precision = 18; pHigh.Scale = 4;
                var pLow  = insCmd.Parameters.Add("@Low",      System.Data.SqlDbType.Decimal); pLow.Precision = 18; pLow.Scale = 4;
                var pClose= insCmd.Parameters.Add("@Close",    System.Data.SqlDbType.Decimal); pClose.Precision = 18; pClose.Scale = 4;
                var pAdj  = insCmd.Parameters.Add("@AdjClose", System.Data.SqlDbType.Decimal); pAdj.Precision = 18; pAdj.Scale = 4;
                var pVol  = insCmd.Parameters.Add("@Volume",   System.Data.SqlDbType.Int);

                // Log every 500th insert so the log isn't flooded
                int logEvery = 500;
                foreach (var bar in dedupedBars)
                {
                    pSym.Value  = symbol;
                    pDate.Value = bar.dt.Date;
                    pTime.Value = bar.dt.TimeOfDay;
                    pOpen.Value = bar.open;
                    pHigh.Value = bar.high;
                    pLow.Value  = bar.low;
                    pClose.Value= bar.close;
                    pAdj.Value  = bar.close;
                    pVol.Value  = (int)Math.Min(bar.volume, int.MaxValue);
                    await insCmd.ExecuteNonQueryAsync();
                    insertedRows++;

                    if (insertedRows % logEvery == 0)
                    {
                        dbg.LogSql($"Inserted {insertedRows:N0} / {dedupedBars.Count:N0} rows...  last: {bar.dt:yyyy-MM-dd HH:mm}");
                        dbg.SetSummary($"{symbol} — Inserting {insertedRows:N0}/{dedupedBars.Count:N0} rows...");
                        Application.DoEvents();
                    }
                }
            }

            // ── Step 5: Verify final row count in DB ────────────────
            dbg.LogSection("SQL — Verify final row count");
            int finalCount = 0;
            int finalDays  = 0;
            using (var conn = await _db.GetOpenConnectionAsync())
            {
                var verCmd = new SqlCommand(
                    "SELECT COUNT(*) AS Rows, COUNT(DISTINCT CAST([Date] AS DATE)) AS Days " +
                    "FROM [5 Minute Data] WHERE [Symbol] = @Sym", conn);
                verCmd.Parameters.Add("@Sym", System.Data.SqlDbType.NVarChar, 10).Value = symbol;
                using var rdr = await verCmd.ExecuteReaderAsync();
                if (await rdr.ReadAsync())
                {
                    finalCount = (int)rdr["Rows"];
                    finalDays  = (int)rdr["Days"];
                }
            }

            if (finalCount == insertedRows)
                dbg.LogSqlOk($"Verified: {finalCount:N0} rows  |  {finalDays} trading days  in [5 Minute Data] ✓");
            else
                dbg.LogSqlError($"Row count mismatch! Inserted {insertedRows:N0} but DB has {finalCount:N0}");

            dbg.LogSuccess($"DONE — {symbol}  |  {insertedRows:N0} bars inserted  |  {finalDays} days  |  {minDt:MMM dd yyyy} – {maxDt:MMM dd yyyy}");
            dbg.SetSummary($"✓ {symbol} complete — {insertedRows:N0} bars / {finalDays} days  ({minDt:MMM dd} – {maxDt:MMM dd yyyy})");

            lblStatus.Text = $"{symbol}: {insertedRows:N0} bars saved  ({minDt:MMM dd yyyy} – {maxDt:MMM dd yyyy})";
        }

        // ============================================================
        // Load OHLCV data, render candlestick chart, apply indicators
        // ============================================================
        private async Task LoadChartDataAsync(string symbol)
        {
            lblStatus.Text = $"Loading {symbol}...";
            lblDateRange.Text = "";
            cboSymbols.Enabled = false;

            var priceSeries = chartStock.Series["Price"];
            priceSeries.Points.Clear();

            // Ensure candlestick mode has 4 Y values after clear
            if (rbCandlestick.Checked)
            {
                priceSeries.ChartType = SeriesChartType.Candlestick;
                priceSeries.YValuesPerPoint = 4;
            }

            // Clear OHLCV cache
            _cachedDates.Clear();
            _cachedOpens.Clear();
            _cachedHighs.Clear();
            _cachedLows.Clear();
            _cachedCloses.Clear();
            _cachedVolumes.Clear();

            // Clear events and gap markers
            ClearEventMarkers();
            _stockEvents.Clear();
            _detectedGaps.Clear();
            _eventsByBarIndex.Clear();

            try
            {
                decimal globalHigh = decimal.MinValue;
                decimal globalLow = decimal.MaxValue;
                int pointCount = 0;

                using (var conn = await _db.GetOpenConnectionAsync())
                {
                    const string query = @"
                        SELECT [Date], [Time], [Open], [High], [Low], [Close], [Volume]
                        FROM [5 Minute Data]
                        WHERE [Symbol] = @Symbol
                          AND [Date] IS NOT NULL
                          AND [Open] IS NOT NULL
                          AND [High] IS NOT NULL
                          AND [Low] IS NOT NULL
                          AND [Close] IS NOT NULL
                        ORDER BY [Date] ASC, [Time] ASC";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@Symbol", System.Data.SqlDbType.NVarChar, 10).Value = symbol;
                        cmd.CommandTimeout = 60;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            int colDate = reader.GetOrdinal("Date");
                            int colTime = reader.GetOrdinal("Time");
                            int colOpen = reader.GetOrdinal("Open");
                            int colHigh = reader.GetOrdinal("High");
                            int colLow = reader.GetOrdinal("Low");
                            int colClose = reader.GetOrdinal("Close");
                            int colVolume = reader.GetOrdinal("Volume");

                            priceSeries.Points.SuspendUpdates();

                            while (await reader.ReadAsync())
                            {
                                DateTime date = reader.GetDateTime(colDate);
                                if (!reader.IsDBNull(colTime))
                                {
                                    TimeSpan time = reader.GetTimeSpan(colTime);
                                    date = date.Date + time;
                                }
                                decimal open = reader.GetDecimal(colOpen);
                                decimal high = reader.GetDecimal(colHigh);
                                decimal low = reader.GetDecimal(colLow);
                                decimal close = reader.GetDecimal(colClose);
                                decimal volume = reader.IsDBNull(colVolume) ? 0m : Convert.ToDecimal(reader.GetValue(colVolume));

                                // Cache OHLCV
                                _cachedDates.Add(date);
                                _cachedOpens.Add(open);
                                _cachedHighs.Add(high);
                                _cachedLows.Add(low);
                                _cachedCloses.Add(close);
                                _cachedVolumes.Add(volume);

                                if (high > globalHigh) globalHigh = high;
                                if (low < globalLow) globalLow = low;

                                // Candlestick Y values: High, Low, Open, Close (X = bar index)
                                int idx = priceSeries.Points.AddXY(
                                    pointCount,
                                    (double)high,
                                    (double)low,
                                    (double)open,
                                    (double)close);

                                var pt = priceSeries.Points[idx];
                                if (close >= open)
                                {
                                    pt.Color = Color.Green;
                                    pt.BorderColor = Color.DarkGreen;
                                }
                                else
                                {
                                    pt.Color = Color.Red;
                                    pt.BorderColor = Color.DarkRed;
                                }

                                pointCount++;
                            }

                            priceSeries.Points.ResumeUpdates();
                        }
                    }
                }

                if (pointCount > 0)
                {
                    double yMax = (double)(globalHigh * 1.10m);
                    double yMin = (double)(globalLow * 0.92m);
                    if (yMin < 0) yMin = 0;

                    var area = chartStock.ChartAreas["MainArea"];
                    area.AxisY.Minimum = yMin;
                    area.AxisY.Maximum = yMax;
                    area.AxisX.Minimum = double.NaN;
                    area.AxisX.Maximum = double.NaN;
                    area.AxisX.ScaleView.ZoomReset();
                }

                this.Text = $"{symbol} - {pointCount:N0} bars - Stock Price Chart v01.01.071";
                lblStatus.Text = $"{symbol}: {pointCount:N0} data points loaded";

                if (_cachedDates.Count > 0)
                {
                    string firstDate = _cachedDates[0].ToString("MMM dd, yyyy");
                    string lastDate  = _cachedDates[_cachedDates.Count - 1].ToString("MMM dd, yyyy");
                    lblDateRange.Text = $"{firstDate}  –  {lastDate}";
                }
                else
                {
                    lblDateRange.Text = "";
                }

                // Apply custom X-axis date labels (index-based, no weekend gaps)
                ApplyCustomXAxisLabels();

                // Draw vertical lines at every calendar day boundary
                DrawDayDividers();

                // Re-apply any active indicators with new symbol data
                if (_activeIndicatorKeys.Count > 0)
                    ApplyActiveIndicators();

                // Detect and draw pivot point markers
                DetectAndDrawPivots();

                // Clear any previous simulation and analysis
                ClearSimulation();
                ClearAnalysisCircles();

                // Compute signals for all indicators
                ComputeSignals();

                // Initialize crosshair on last bar
                _crosshairIndex = _cachedDates.Count - 1;
                UpdateCrosshairPosition();
                chartStock.Focus();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error loading {symbol}";
                MessageBox.Show(
                    $"Failed to load chart data for {symbol}:\n\n{ex.Message}",
                    "Data Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                cboSymbols.Enabled = true;
            }
        }
        // ============================================================
        // Compute signals for all 21 indicators and refresh list
        // ============================================================
        private void ComputeSignals()
        {
            _signalCache = IndicatorSignalEvaluator.EvaluateAll(
                _cachedOpens, _cachedHighs, _cachedLows, _cachedCloses, _cachedVolumes);

            // Update signal textboxes with computed values and colors
            foreach (var kvp in _signalTextBoxes)
            {
                if (_signalCache.ContainsKey(kvp.Key))
                {
                    var signal = _signalCache[kvp.Key];
                    kvp.Value.Text = signal.DisplayValue;
                    kvp.Value.BackColor = signal.SignalColor;
                    kvp.Value.ForeColor = Color.White;
                }
                else
                {
                    kvp.Value.Text = "";
                    kvp.Value.BackColor = Color.Gray;
                }
            }
        }

        // ============================================================
        // Compute signals at a specific bar index (for crosshair nav)
        // ============================================================
        private void ComputeSignalsAtIndex(int barIndex)
        {
            _signalCache = IndicatorSignalEvaluator.EvaluateAllAtIndex(
                _cachedOpens, _cachedHighs, _cachedLows, _cachedCloses, _cachedVolumes, barIndex);

            foreach (var kvp in _signalTextBoxes)
            {
                if (_signalCache.ContainsKey(kvp.Key))
                {
                    var signal = _signalCache[kvp.Key];
                    kvp.Value.Text = signal.DisplayValue;
                    kvp.Value.BackColor = signal.SignalColor;
                    kvp.Value.ForeColor = Color.White;
                }
                else
                {
                    kvp.Value.Text = "";
                    kvp.Value.BackColor = Color.Gray;
                }
            }
        }

        // ============================================================
        // ProcessCmdKey - intercept dedicated arrow keys at form level
        // (skip when a text-entry control has focus)
        // ============================================================
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Let text controls handle their own arrow keys
            var focused = this.ActiveControl;
            if (focused is ComboBox || focused is TextBox)
                return base.ProcessCmdKey(ref msg, keyData);

            // Only respond to dedicated arrow keys (not numpad)
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Left || key == Keys.Right ||
                key == Keys.Home || key == Keys.End)
            {
                if (_cachedDates.Count > 0)
                {
                    chartStock_KeyDown(chartStock, new KeyEventArgs(keyData));
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ============================================================
        // Arrow Key Crosshair Navigation
        // ============================================================
        private void chartStock_KeyDown(object sender, KeyEventArgs e)
        {
            if (_cachedDates.Count == 0) return;

            int lastBar = _cachedDates.Count - 1;
            bool moved = false;

            switch (e.KeyCode)
            {
                case Keys.Left:
                    if (_crosshairIndex > 0)
                    {
                        _crosshairIndex--;
                        moved = true;
                    }
                    e.Handled = true;
                    break;

                case Keys.Right:
                    if (_crosshairIndex < lastBar)
                    {
                        _crosshairIndex++;
                        moved = true;
                    }
                    e.Handled = true;
                    break;

                case Keys.Home:
                    _crosshairIndex = 0;
                    moved = true;
                    e.Handled = true;
                    break;

                case Keys.End:
                    _crosshairIndex = lastBar;
                    moved = true;
                    e.Handled = true;
                    break;
            }

            if (moved)
            {
                UpdateCrosshairPosition();
            }
        }

        private void UpdateCrosshairPosition()
        {
            if (_crosshairIndex < 0 || _crosshairIndex >= _cachedDates.Count) return;

            var area = chartStock.ChartAreas["MainArea"];
            double xVal = _crosshairIndex;

            area.CursorX.Position = xVal;

            // Center the zoom view on the crosshair
            CenterViewOnCrosshair(area, xVal);

            // Update signals at crosshair position
            ComputeSignalsAtIndex(_crosshairIndex);

            // Highlight matching simulation transaction row
            HighlightSimulationRow(_crosshairIndex);

            // Update status bar
            var barDateTime = _cachedDates[_crosshairIndex];
            string date = barDateTime.TimeOfDay != TimeSpan.Zero
                ? barDateTime.ToString("yyyy-MM-dd HH:mm")
                : barDateTime.ToString("yyyy-MM-dd");
            decimal close = _cachedCloses[_crosshairIndex];
            decimal open = _cachedOpens[_crosshairIndex];
            decimal high = _cachedHighs[_crosshairIndex];
            decimal low = _cachedLows[_crosshairIndex];
            lblStatus.Text = $"{date}  O:{open:C2}  H:{high:C2}  L:{low:C2}  C:{close:C2}";

            // Position the crosshair price labels on both Y-axis edges
            // and update the hover panel at the crosshair location
            try
            {
                double priceVal = (double)close;
                float pixelY = (float)area.AxisY.ValueToPixelPosition(priceVal);
                float axisRightX = (float)area.AxisX.ValueToPixelPosition(area.AxisX.Maximum);
                float axisLeftX = (float)area.AxisX.ValueToPixelPosition(area.AxisX.Minimum);
                float pixelX = (float)area.AxisX.ValueToPixelPosition(xVal);

                // Right-side label
                _lblCrosshairPrice.Text = close.ToString("C2");
                _lblCrosshairPrice.Location = new Point(
                    (int)axisRightX - _lblCrosshairPrice.Width,
                    (int)pixelY - _lblCrosshairPrice.Height / 2);
                _lblCrosshairPrice.Visible = true;
                _lblCrosshairPrice.BringToFront();

                // Left-side label
                _lblCrosshairPriceLeft.Text = close.ToString("C2");
                _lblCrosshairPriceLeft.Location = new Point(
                    (int)axisLeftX,
                    (int)pixelY - _lblCrosshairPriceLeft.Height / 2);
                _lblCrosshairPriceLeft.Visible = true;
                _lblCrosshairPriceLeft.BringToFront();

                // Update hover panel at crosshair pixel position
                ShowHoverPanel(_crosshairIndex, (int)pixelX, (int)pixelY);
            }
            catch
            {
                _lblCrosshairPrice.Visible = false;
                _lblCrosshairPriceLeft.Visible = false;
            }
        }

        // ============================================================
        // Center the chart zoom view on the crosshair position
        // ============================================================
        private void CenterViewOnCrosshair(ChartArea area, double xVal)
        {
            var xAxis = area.AxisX;
            double viewMin = xAxis.ScaleView.ViewMinimum;
            double viewMax = xAxis.ScaleView.ViewMaximum;
            double viewRange = viewMax - viewMin;

            // Only scroll if chart is zoomed in (view range < full data range)
            double dataMin = xAxis.Minimum;
            double dataMax = xAxis.Maximum;
            if (double.IsNaN(dataMin)) dataMin = xAxis.ScaleView.ViewMinimum;
            if (double.IsNaN(dataMax)) dataMax = xAxis.ScaleView.ViewMaximum;
            double dataRange = dataMax - dataMin;

            if (viewRange >= dataRange * 0.99) return; // Not zoomed in, no scrolling needed

            // Center the view on the crosshair
            double newMin = xVal - viewRange / 2.0;
            double newMax = xVal + viewRange / 2.0;

            // Clamp to data boundaries
            if (newMin < dataMin)
            {
                newMin = dataMin;
                newMax = dataMin + viewRange;
            }
            if (newMax > dataMax)
            {
                newMax = dataMax;
                newMin = dataMax - viewRange;
            }

            xAxis.ScaleView.Zoom(newMin, newMax);
            RecalculateYAxis();
        }

        // ============================================================
        // Mouse Click - snap crosshair to nearest bar, recompute signals
        // ============================================================
        private void chartStock_MouseClick(object sender, MouseEventArgs e)
        {
            chartStock.Focus();

            if (e.Button != MouseButtons.Left) return;
            if (_cachedDates.Count == 0) return;

            var area = chartStock.ChartAreas["MainArea"];

            // Convert pixel X to axis value (bar index)
            double xValue;
            try
            {
                xValue = area.AxisX.PixelPositionToValue(e.X);
            }
            catch
            {
                return; // Click was outside the plot area
            }

            // Snap to nearest bar index
            int nearestIndex = (int)Math.Round(xValue);
            if (nearestIndex < 0) nearestIndex = 0;
            if (nearestIndex >= _cachedDates.Count) nearestIndex = _cachedDates.Count - 1;

            _crosshairIndex = nearestIndex;
            UpdateCrosshairPosition();
            ShowHoverPanel(nearestIndex, e.X, e.Y);
        }

        private void ShowHoverPanel(int barIndex, int mouseX, int mouseY)
        {
            if (barIndex < 0 || barIndex >= _cachedDates.Count) return;

            var dt = _cachedDates[barIndex];
            decimal open  = _cachedOpens[barIndex];
            decimal high  = _cachedHighs[barIndex];
            decimal low   = _cachedLows[barIndex];
            decimal close = _cachedCloses[barIndex];

            _lblHoverDate.Text = dt.ToString("MMM dd, yyyy");
            _lblHoverTime.Text = dt.TimeOfDay != TimeSpan.Zero ? dt.ToString("HH:mm") : "Daily";
            _lblHoverOHLC.Text = $"O: {open:F2}   H: {high:F2}\nL: {low:F2}    C: {close:F2}";

            // Color close label based on direction
            bool up = close >= open;
            _lblHoverOHLC.ForeColor = up ? Color.LimeGreen : Color.Tomato;

            // Show event info if any events at this bar (or same calendar date)
            List<StockEvent> eventsAtBar = null;
            _eventsByBarIndex.TryGetValue(barIndex, out eventsAtBar);

            // Also check same date (events may land on different bar within same day)
            if ((eventsAtBar == null || eventsAtBar.Count == 0) && _stockEvents.Count > 0)
            {
                DateTime barDate = dt.Date;
                var sameDay = new List<StockEvent>();
                foreach (var ev in _stockEvents)
                    if (ev.Date.Date == barDate) sameDay.Add(ev);
                if (sameDay.Count > 0) eventsAtBar = sameDay;
            }

            if (eventsAtBar != null && eventsAtBar.Count > 0)
            {
                var first = eventsAtBar[0];
                string label = GetEventLabel(first.EventType);
                string eventText = $"[{label}] {first.Title}";
                if (eventsAtBar.Count > 1)
                    eventText += $"\n+{eventsAtBar.Count - 1} more";
                if (first.IsGapEvent)
                    eventText += $"\nGap: {first.GapPercent:+0.00;-0.00}%";

                _lblHoverEvent.Text = eventText;
                _lblHoverEvent.ForeColor = GetEventLabelColor(first.EventType);
                _lblHoverEvent.Visible = true;
                _pnlHover.Size = new Size(200, 130);
                _lblHoverDate.Size = new Size(196, 18);
                _lblHoverTime.Size = new Size(196, 16);
                _lblHoverOHLC.Size = new Size(196, 44);
            }
            else
            {
                _lblHoverEvent.Visible = false;
                _pnlHover.Size = new Size(160, 72);
                _lblHoverDate.Size = new Size(156, 18);
                _lblHoverTime.Size = new Size(156, 16);
                _lblHoverOHLC.Size = new Size(156, 44);
            }

            // Position panel near click/crosshair, keeping it inside chart bounds
            int panelX = mouseX + 12;
            int panelY = mouseY + 12;
            if (panelX + _pnlHover.Width > chartStock.Width - 5)
                panelX = mouseX - _pnlHover.Width - 8;
            if (panelY + _pnlHover.Height > chartStock.Height - 5)
                panelY = mouseY - _pnlHover.Height - 8;
            if (panelX < 0) panelX = 0;
            if (panelY < 0) panelY = 0;

            _pnlHover.Location = new Point(panelX, panelY);
            _pnlHover.Visible = true;
            _pnlHover.BringToFront();
        }

        // ============================================================
        // Mouse Double-Click - find nearest buy/sell marker, highlight row
        // ============================================================
        private void chartStock_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (_simulationResult == null || _simulationResult.Transactions.Count == 0) return;

            // Hit-test against SimBuy and SimSell series
            var hit = chartStock.HitTest(e.X, e.Y);
            if (hit.ChartElementType == ChartElementType.DataPoint &&
                hit.Series != null &&
                (hit.Series.Name == "SimBuy" || hit.Series.Name == "SimSell") &&
                hit.PointIndex >= 0)
            {
                // The point was hit directly - find the matching transaction
                var hitPoint = hit.Series.Points[hit.PointIndex];
                double hitX = hitPoint.XValue;
                double hitY = hitPoint.YValues[0];

                // Match by bar index and price to the transaction list
                for (int i = 0; i < _simulationResult.Transactions.Count; i++)
                {
                    var txn = _simulationResult.Transactions[i];
                    if (Math.Abs(txn.BarIndex - hitX) < 0.5 &&
                        Math.Abs((double)txn.Price - hitY) < 0.01)
                    {
                        dgvTransactions.ClearSelection();
                        dgvTransactions.Rows[i].Selected = true;
                        dgvTransactions.FirstDisplayedScrollingRowIndex = i;

                        // Also move crosshair to that bar
                        _crosshairIndex = txn.BarIndex;
                        UpdateCrosshairPosition();
                        return;
                    }
                }
            }

            // If no direct hit on a marker, find nearest transaction by proximity
            try
            {
                var area = chartStock.ChartAreas["MainArea"];
                double xValue = area.AxisX.PixelPositionToValue(e.X);
                double yValue = area.AxisY.PixelPositionToValue(e.Y);

                int bestIdx = -1;
                double bestDist = double.MaxValue;

                for (int i = 0; i < _simulationResult.Transactions.Count; i++)
                {
                    var txn = _simulationResult.Transactions[i];
                    double txnX = txn.BarIndex;
                    double txnY = (double)txn.Price;

                    // Normalize distances: X in pixels, Y in pixels
                    double pixTxnX = area.AxisX.ValueToPixelPosition(txnX);
                    double pixTxnY = area.AxisY.ValueToPixelPosition(txnY);
                    double dist = Math.Sqrt(Math.Pow(e.X - pixTxnX, 2) + Math.Pow(e.Y - pixTxnY, 2));

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = i;
                    }
                }

                // Only snap if within 30 pixels of a marker
                if (bestIdx >= 0 && bestDist <= 30)
                {
                    dgvTransactions.ClearSelection();
                    dgvTransactions.Rows[bestIdx].Selected = true;
                    dgvTransactions.FirstDisplayedScrollingRowIndex = bestIdx;

                    _crosshairIndex = _simulationResult.Transactions[bestIdx].BarIndex;
                    UpdateCrosshairPosition();
                }
            }
            catch
            {
                // Click was outside the plot area
            }
        }

        // ============================================================
        // Mouse Wheel Zoom on Chart (X-axis)
        // ============================================================
        private void chartStock_MouseWheel(object sender, MouseEventArgs e)
        {
            var area = chartStock.ChartAreas["MainArea"];
            var xAxis = area.AxisX;
            var yAxis = area.AxisY;

            try
            {
                double xMin = xAxis.ScaleView.ViewMinimum;
                double xMax = xAxis.ScaleView.ViewMaximum;
                double xRange = xMax - xMin;

                // Determine mouse position as fraction of the chart X range
                double mouseXFraction = 0.5;
                var result = chartStock.HitTest(e.X, e.Y);
                if (result.ChartArea != null && xRange > 0)
                {
                    double pixelPos = xAxis.ValueToPixelPosition(xMin)
                        + (xAxis.ValueToPixelPosition(xMax) - xAxis.ValueToPixelPosition(xMin))
                        * ((double)(e.X - chartStock.Location.X) / chartStock.Width);
                    mouseXFraction = (e.X - xAxis.ValueToPixelPosition(xMin))
                        / (xAxis.ValueToPixelPosition(xMax) - xAxis.ValueToPixelPosition(xMin));
                    if (mouseXFraction < 0) mouseXFraction = 0;
                    if (mouseXFraction > 1) mouseXFraction = 1;
                }

                double zoomFactor = 0.2; // 20% per scroll tick

                if (e.Delta > 0)
                {
                    // Zoom in - shrink visible range, centered on mouse position
                    double shrink = xRange * zoomFactor;
                    double newMin = xMin + shrink * mouseXFraction;
                    double newMax = xMax - shrink * (1 - mouseXFraction);

                    if (newMax - newMin > 5) // minimum ~5 data points visible
                        xAxis.ScaleView.Zoom(newMin, newMax);
                }
                else
                {
                    // Zoom out - expand visible range
                    double expand = xRange * zoomFactor;
                    double newMin = xMin - expand * mouseXFraction;
                    double newMax = xMax + expand * (1 - mouseXFraction);

                    // Clamp to full data range
                    double dataMin = xAxis.Minimum;
                    double dataMax = xAxis.Maximum;
                    if (double.IsNaN(dataMin)) dataMin = xAxis.ScaleView.ViewMinimum;
                    if (double.IsNaN(dataMax)) dataMax = xAxis.ScaleView.ViewMaximum;

                    if (newMin <= dataMin && newMax >= dataMax)
                    {
                        // Fully zoomed out - reset view
                        xAxis.ScaleView.ZoomReset();
                    }
                    else
                    {
                        if (newMin < dataMin) newMin = dataMin;
                        if (newMax > dataMax) newMax = dataMax;
                        xAxis.ScaleView.Zoom(newMin, newMax);
                    }
                }

                // Recalculate Y-axis for visible data
                RecalculateYAxis();
            }
            catch
            {
                // Ignore zoom errors (e.g. no data loaded)
            }
        }

        // ============================================================
        // AxisViewChanged - auto-adjust Y-axis to visible data range
        // ============================================================
        private void chartStock_AxisViewChanged(object sender, ViewEventArgs e)
        {
            // Only respond to X-axis changes (zoom/scroll on time axis)
            if (e.Axis == chartStock.ChartAreas["MainArea"].AxisX)
            {
                RecalculateYAxis();
            }
        }

        // ============================================================
        // Recalculate Y-axis to fit visible data: 10% above, 8% below
        // ============================================================
        private void RecalculateYAxis()
        {
            if (_cachedDates.Count == 0) return;

            var area = chartStock.ChartAreas["MainArea"];
            var xAxis = area.AxisX;
            var yAxis = area.AxisY;

            double viewXMin = xAxis.ScaleView.ViewMinimum;
            double viewXMax = xAxis.ScaleView.ViewMaximum;

            // When fully zoomed out, ViewMinimum/ViewMaximum return NaN — use full data range
            if (double.IsNaN(viewXMin)) viewXMin = 0;
            if (double.IsNaN(viewXMax)) viewXMax = _cachedDates.Count - 1;

            // Find cached data points within the visible X range
            decimal visibleHigh = decimal.MinValue;
            decimal visibleLow = decimal.MaxValue;
            bool found = false;

            int iMin = Math.Max(0, (int)Math.Floor(viewXMin));
            int iMax = Math.Min(_cachedDates.Count - 1, (int)Math.Ceiling(viewXMax));

            for (int i = iMin; i <= iMax; i++)
            {
                if (_cachedHighs[i] > visibleHigh) visibleHigh = _cachedHighs[i];
                if (_cachedLows[i] < visibleLow) visibleLow = _cachedLows[i];
                found = true;
            }

            if (!found) return;

            // 10% padding above highest and 10% below lowest visible price
            decimal range = visibleHigh - visibleLow;
            decimal padding = range * 0.10m;
            double yMax = (double)(visibleHigh + padding);
            double yMin = (double)(visibleLow - padding);
            if (yMin < 0) yMin = 0;

            yAxis.Minimum = yMin;
            yAxis.Maximum = yMax;
        }

        // ============================================================
        // Apply custom X-axis labels showing dates at bar indices
        // ============================================================
        private void ApplyCustomXAxisLabels()
        {
            var area = chartStock.ChartAreas["MainArea"];
            area.AxisX.CustomLabels.Clear();

            if (_cachedDates.Count == 0) return;

            // Determine label interval based on data count to avoid clutter
            int totalBars = _cachedDates.Count;
            int labelInterval;
            if (totalBars <= 60) labelInterval = 5;
            else if (totalBars <= 120) labelInterval = 10;
            else if (totalBars <= 500) labelInterval = 20;
            else if (totalBars <= 1000) labelInterval = 50;
            else if (totalBars <= 5000) labelInterval = 100;
            else labelInterval = 200;

            // Detect if data has intraday times (5-minute bars)
            bool hasIntraday = _cachedDates.Count >= 2 &&
                               _cachedDates[0].Date == _cachedDates[1].Date &&
                               _cachedDates[0].TimeOfDay != TimeSpan.Zero;

            for (int i = 0; i < totalBars; i++)
            {
                if (i % labelInterval == 0 || i == totalBars - 1)
                {
                    string dateLabel;
                    if (hasIntraday)
                    {
                        // Show date on first bar of each day, otherwise just time
                        bool isNewDay = (i == 0) || _cachedDates[i].Date != _cachedDates[i - 1].Date;
                        dateLabel = isNewDay
                            ? _cachedDates[i].ToString("MM/dd HH:mm")
                            : _cachedDates[i].ToString("HH:mm");
                    }
                    else
                    {
                        dateLabel = _cachedDates[i].ToString("MM/dd/yy");
                    }
                    area.AxisX.CustomLabels.Add(i - 0.5, i + 0.5, dateLabel);
                }
            }
        }

        // ============================================================
        // Chart Type toggle: Candlestick ↔ Line
        // ============================================================
        private void rbChartType_CheckedChanged(object sender, EventArgs e)
        {
            // Only act on the one becoming checked (avoid double-fire)
            var rb = sender as RadioButton;
            if (rb == null || !rb.Checked) return;
            if (_cachedDates.Count == 0) return;

            RebuildPriceSeriesFromCache();
        }

        // ============================================================
        // Draw vertical lines on the X-axis at every change in calendar date
        // ============================================================
        private void DrawDayDividers()
        {
            var area = chartStock.ChartAreas["MainArea"];

            // Remove any previously drawn day/month divider strip lines
            for (int i = area.AxisX.StripLines.Count - 1; i >= 0; i--)
            {
                if (area.AxisX.StripLines[i].Tag is string tag &&
                    (tag == "DayDivider" || tag == "MonthDivider"))
                    area.AxisX.StripLines.RemoveAt(i);
            }

            if (_cachedDates.Count < 2) return;

            DateTime prevDate = _cachedDates[0].Date;
            for (int i = 1; i < _cachedDates.Count; i++)
            {
                DateTime thisDate = _cachedDates[i].Date;
                if (thisDate == prevDate) continue;

                bool newMonth = thisDate.Year != prevDate.Year || thisDate.Month != prevDate.Month;

                var strip = new StripLine();
                strip.Interval = 0;
                strip.IntervalOffset = i;

                if (newMonth)
                {
                    // Red line — new month boundary, slightly wider so it stands out
                    strip.Tag = "MonthDivider";
                    strip.StripWidth = 0.0003;
                    strip.BackColor = Color.FromArgb(200, Color.Crimson);
                    strip.BorderColor = Color.Transparent;
                    strip.BorderWidth = 0;
                }
                else
                {
                    // Purple band — fills the overnight gap between trading days
                    strip.Tag = "DayDivider";
                    strip.StripWidth = 1.0;
                    strip.BackColor = Color.FromArgb(60, 128, 0, 192);   // semi-transparent purple
                    strip.BorderColor = Color.Transparent;
                    strip.BorderWidth = 0;
                }

                area.AxisX.StripLines.Add(strip);
                prevDate = thisDate;
            }
        }

        // ============================================================
        // Events button — fetch earnings, SEC filings, news and correlate with gaps
        // ============================================================
        private async void btnEvents_Click(object sender, EventArgs e)
        {
            string symbol = cboSymbols.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(symbol) || _cachedDates.Count == 0)
            {
                MessageBox.Show("Please load a symbol first.", "Events", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnEvents.Enabled = false;
            btnEvents.Text = "Loading...";

            try
            {
                await FetchAndCorrelateEventsAsync(symbol);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Events failed:\n\n{ex.Message}", "Events Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Events fetch failed.";
            }
            finally
            {
                btnEvents.Enabled = true;
                btnEvents.Text = "Events";
            }
        }

        private async Task FetchAndCorrelateEventsAsync(string symbol)
        {
            var dbg = _debugLog;
            dbg.Location = new Point(this.Right + 8, this.Top);
            dbg.Show();
            dbg.BringToFront();
            dbg.LogSection($"EVENTS  —  {symbol}  —  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            // Step 1: Read API key
            string apiKey = await ReadFmpApiKeyAsync(dbg);
            if (string.IsNullOrEmpty(apiKey)) return;

            // Step 2: Detect gaps in cached price data
            if (!decimal.TryParse(txtGapThreshold.Text, out decimal threshold))
                threshold = 0.5m;
            _gapThreshold = threshold;

            _detectedGaps = GapAnalyzer.DetectGaps(_cachedDates, _cachedOpens, _cachedCloses, threshold);
            dbg.LogInfo($"Gap detection: {_detectedGaps.Count} gaps >= {threshold:F1}% found");

            DateTime fromDate = _cachedDates[0].Date;
            DateTime toDate   = _cachedDates[_cachedDates.Count - 1].Date;

            // Step 3: Fetch from all sources
            var allEvents = new List<StockEvent>();

            using (var fetcher = new EventFetcher())
            {
                dbg.LogSection("FMP — Earnings Calendar");
                var earnings = await fetcher.FetchEarningsAsync(symbol, fromDate, toDate, apiKey, dbg);
                allEvents.AddRange(earnings);
                dbg.LogInfo($"Earnings: {earnings.Count} events");

                dbg.LogSection("FMP — Press Releases");
                var news = await fetcher.FetchPressReleasesAsync(symbol, fromDate, toDate, apiKey, dbg);
                allEvents.AddRange(news);
                dbg.LogInfo($"Press releases: {news.Count} significant items");

                dbg.LogSection("FMP — SEC Filings");
                var filings = await fetcher.FetchSecFilingsAsync(symbol, fromDate, toDate, apiKey, dbg);
                allEvents.AddRange(filings);
                dbg.LogInfo($"SEC filings: {filings.Count} filings");

                dbg.LogSection("SQL — [News Flash] table");
                var newsFlash = await fetcher.FetchNewsFlashAsync(symbol, fromDate, toDate, _config.ConnectionString, dbg);
                allEvents.AddRange(newsFlash);
                dbg.LogInfo($"News Flash DB: {newsFlash.Count} records");
            }

            dbg.LogInfo($"Total events from all sources: {allEvents.Count}");

            // Step 4: Resolve bar indices and correlate with gaps
            ResolveEventBarIndices(allEvents);
            CorrelateEventsWithGaps(allEvents);

            _stockEvents = allEvents;

            // Build O(1) lookup for hover panel
            _eventsByBarIndex.Clear();
            foreach (var evt in _stockEvents)
            {
                if (evt.BarIndex < 0) continue;
                if (!_eventsByBarIndex.ContainsKey(evt.BarIndex))
                    _eventsByBarIndex[evt.BarIndex] = new List<StockEvent>();
                _eventsByBarIndex[evt.BarIndex].Add(evt);
            }

            // Step 5: Log detailed correlation summary
            dbg.LogSection("Gap ↔ Event Correlation Summary");
            int correlated = 0;
            foreach (var gap in _detectedGaps)
            {
                string direction = gap.IsGapUp ? "▲ GAP UP  " : "▼ GAP DOWN";
                var matchedEvents = new List<string>();
                foreach (var ev in allEvents)
                {
                    int dayDiff = (int)(gap.GapDate - ev.Date.Date).TotalDays;
                    if (dayDiff >= -1 && dayDiff <= 2)
                        matchedEvents.Add($"[{GetEventLabel(ev.EventType)}] {ev.Title}");
                }
                if (matchedEvents.Count > 0)
                {
                    dbg.LogInfo($"{direction}  {gap.GapDate:MMM dd}  {gap.GapPercent:+0.00;-0.00}%  →  {matchedEvents.Count} event(s):");
                    foreach (var m in matchedEvents)
                        dbg.LogInfo($"    {m}");
                    correlated++;
                }
                else
                {
                    dbg.LogWarning($"{direction}  {gap.GapDate:MMM dd}  {gap.GapPercent:+0.00;-0.00}%  →  no matching event found");
                }
            }

            // Step 6: Draw on chart
            DrawEventMarkers();

            lblStatus.Text = $"Events: {allEvents.Count} total  |  {_detectedGaps.Count} gaps  |  {correlated} correlated";
            dbg.LogSuccess($"DONE — {allEvents.Count} events  |  {_detectedGaps.Count} gaps  |  {correlated} correlated");
            dbg.SetSummary($"{symbol} — {allEvents.Count} events | {_detectedGaps.Count} gaps | {correlated} correlated");
        }

        private async Task<string> ReadFmpApiKeyAsync(FmpDebugForm dbg)
        {
            dbg.LogSql("SELECT [Key1] FROM [APIKeys] WHERE [Api Compnay] = 'FMP'");
            using (var conn = await _db.GetOpenConnectionAsync())
            {
                var cmd = new SqlCommand("SELECT [Key1] FROM [APIKeys] WHERE [Api Compnay] = 'FMP'", conn);
                var result = await cmd.ExecuteScalarAsync();
                string key = result as string ?? "";
                if (string.IsNullOrEmpty(key))
                    dbg.LogSqlError("FMP API key not found in [APIKeys].");
                else
                    dbg.LogSqlOk($"API key retrieved ({key.Substring(0, Math.Min(8, key.Length))}***)");
                return key;
            }
        }

        // ============================================================
        // AI Review — month-end news title analysis via Claude API
        // ============================================================
        private async void btnAiReview_Click(object sender, EventArgs e)
        {
            string symbol = cboSymbols.Text.Trim().ToUpper();
            if (string.IsNullOrEmpty(symbol) || _cachedDates.Count == 0)
            {
                MessageBox.Show("Please load a symbol first.", "AI Review", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnAiReview.Enabled = false;
            btnAiReview.Text = "Analyzing...";

            try
            {
                await RunMonthEndAnalysisAsync(symbol);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"AI Review failed:\n\n{ex.Message}", "AI Review Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "AI Review failed.";
            }
            finally
            {
                btnAiReview.Enabled = true;
                btnAiReview.Text = "AI Review";
            }
        }

        private async Task<string> ReadAnthropicApiKeyAsync()
        {
            using var conn = await _db.GetOpenConnectionAsync();
            var cmd = new SqlCommand(
                "SELECT [Key1] FROM [APIKeys] WHERE [Api Compnay] = 'Anthropic'", conn);
            var result = await cmd.ExecuteScalarAsync();
            return result as string ?? "";
        }

        private async Task RunMonthEndAnalysisAsync(string symbol)
        {
            // Show / create the AI review window
            if (_aiReviewForm == null || _aiReviewForm.IsDisposed)
                _aiReviewForm = new AiReviewForm();

            _aiReviewForm.Location = new Point(
                Math.Max(0, this.Right + 8),
                this.Top);
            _aiReviewForm.Show();
            _aiReviewForm.BringToFront();
            _aiReviewForm.Focus();
            _aiReviewForm.ResetTally();
            _aiReviewForm.SetSummary($"{symbol} — reading API key...");

            lblStatus.Text = "AI Review: reading API key...";
            Application.DoEvents();

            // ── Read Anthropic API key ───────────────────────────────
            _aiReviewForm.LogSection($"{symbol}  —  AI Month-End Analysis  —  {DateTime.Now:yyyy-MM-dd HH:mm}");
            _aiReviewForm.LogSql("SELECT [Key1] FROM [APIKeys] WHERE [Api Compnay] = 'Anthropic'");

            string anthropicKey = await ReadAnthropicApiKeyAsync();
            if (string.IsNullOrEmpty(anthropicKey))
            {
                _aiReviewForm.LogSqlError("Anthropic API key not found in [APIKeys]. Add a row with [Api Compnay]='Anthropic' and [Key1]=<your key>.");
                throw new Exception("Anthropic API key not found.");
            }
            _aiReviewForm.LogSqlOk($"API key retrieved ({anthropicKey.Substring(0, Math.Min(8, anthropicKey.Length))}***)");

            // ── Build unique sorted trading dates ────────────────────
            var tradingDates = new List<DateTime>();
            var seenDates = new HashSet<DateTime>();
            foreach (var dt in _cachedDates)
            {
                DateTime d = dt.Date;
                if (seenDates.Add(d)) tradingDates.Add(d);
            }
            tradingDates.Sort();

            // ── Find last trading day of each month ──────────────────
            var monthEnds = new List<DateTime>();
            for (int i = 0; i < tradingDates.Count; i++)
            {
                bool isLastOfMonth = (i == tradingDates.Count - 1) ||
                    (tradingDates[i].Month != tradingDates[i + 1].Month ||
                     tradingDates[i].Year  != tradingDates[i + 1].Year);
                if (isLastOfMonth)
                    monthEnds.Add(tradingDates[i]);
            }

            if (monthEnds.Count == 0)
            {
                _aiReviewForm.LogSqlError("No month-end dates found in loaded data.");
                lblStatus.Text = "AI Review: no month-end dates found.";
                return;
            }

            _aiReviewForm.LogDiag($"Loaded range: {tradingDates[0]:yyyy-MM-dd} → {tradingDates[tradingDates.Count - 1]:yyyy-MM-dd}  ({tradingDates.Count} trading days,  {monthEnds.Count} month-ends)");

            // ── Diagnostic: total [News Flash] rows for this symbol ──
            try
            {
                string diagSql = "SELECT COUNT(*) FROM [News Flash] WHERE [Symbol] = @Sym";
                _aiReviewForm.LogSql($"SELECT COUNT(*) FROM [News Flash] WHERE [Symbol] = '{symbol}'");
                using var diagConn = await _db.GetOpenConnectionAsync();
                using var diagCmd  = new SqlCommand(diagSql, diagConn);
                diagCmd.Parameters.AddWithValue("@Sym", symbol);
                diagCmd.CommandTimeout = 15;
                int totalRows = (int)await diagCmd.ExecuteScalarAsync();
                _aiReviewForm.LogSqlOk($"[News Flash] total rows for {symbol}: {totalRows}");
            }
            catch (Exception ex)
            {
                _aiReviewForm.LogSqlError($"Diagnostic query failed: {ex.Message}");
            }

            ClearMonthEndSignals();

            // ── Build date-to-last-barIndex map ──────────────────────
            var dateToLastBar = new Dictionary<DateTime, int>();
            for (int i = 0; i < _cachedDates.Count; i++)
            {
                DateTime d = _cachedDates[i].Date;
                dateToLastBar[d] = i;
            }

            var results = new List<(DateTime date, int barIndex, string signal)>();

            using var http = new System.Net.Http.HttpClient();
            http.Timeout = TimeSpan.FromSeconds(60);
            http.DefaultRequestHeaders.Add("x-api-key", anthropicKey);
            http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            // SQL: pull all titles for entire calendar month up to month-end date
            string sql = @"
                SELECT [Title]
                FROM [News Flash]
                WHERE [Symbol] = @Sym
                  AND CAST([PublishedDate] AS date) >= @MonthStart
                  AND CAST([PublishedDate] AS date) <= @MonthEnd
                ORDER BY [PublishedDate] ASC";

            int processed = 0;
            foreach (DateTime monthEndDate in monthEnds)
            {
                processed++;
                DateTime monthStart = new DateTime(monthEndDate.Year, monthEndDate.Month, 1);

                lblStatus.Text = $"AI Review: {monthEndDate:MMM yyyy}  ({processed}/{monthEnds.Count})...";
                _aiReviewForm.SetSummary($"{symbol} — {monthEndDate:MMM yyyy}  ({processed}/{monthEnds.Count})");
                Application.DoEvents();

                // ── Fetch titles for the whole month ─────────────────
                var titles = new List<string>();
                _aiReviewForm.LogSql($"[News Flash] WHERE Symbol='{symbol}' AND Date BETWEEN {monthStart:yyyy-MM-dd} AND {monthEndDate:yyyy-MM-dd}");

                try
                {
                    using var conn = await _db.GetOpenConnectionAsync();
                    using var cmd  = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Sym",        symbol);
                    cmd.Parameters.AddWithValue("@MonthStart", monthStart);
                    cmd.Parameters.AddWithValue("@MonthEnd",   monthEndDate);
                    cmd.CommandTimeout = 15;

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (!reader.IsDBNull(0))
                        {
                            string t = reader.GetString(0).Trim();
                            if (!string.IsNullOrEmpty(t)) titles.Add(t);
                        }
                    }
                    _aiReviewForm.LogSqlOk($"{titles.Count} title(s) returned");
                }
                catch (Exception ex)
                {
                    _aiReviewForm.LogSqlError($"Query failed: {ex.Message}");
                }

                _aiReviewForm.LogMonthHeader(monthEndDate, titles.Count);

                if (titles.Count == 0)
                {
                    _aiReviewForm.LogNoTitles();
                    continue;
                }

                for (int i = 0; i < titles.Count; i++)
                    _aiReviewForm.LogTitle(i + 1, titles[i]);

                // ── Build Claude prompt ───────────────────────────────
                string titlesBlock = string.Join("\n", titles.Select((t, idx) => $"{idx + 1}. {t}"));
                string prompt =
                    $"You are a financial analyst. The following are news headlines for {symbol} " +
                    $"during {monthEndDate:MMMM yyyy} (last trading day: {monthEndDate:MMM d, yyyy}).\n\n" +
                    $"{titlesBlock}\n\n" +
                    $"Based solely on the sentiment and content of these headlines, provide a brief " +
                    $"executive summary (2-3 sentences) and end your response with exactly one of these " +
                    $"three words on its own line: BUY, SELL, or HOLD.";

                // ── Call Claude API ───────────────────────────────────
                string signal      = "HOLD";
                string responseText = "";
                _aiReviewForm.LogApiCall();

                try
                {
                    var requestBody = new
                    {
                        model    = "claude-haiku-4-5-20251001",
                        max_tokens = 350,
                        messages = new[]
                        {
                            new { role = "user", content = prompt }
                        }
                    };

                    string requestJson = System.Text.Json.JsonSerializer.Serialize(requestBody);
                    var httpContent = new System.Net.Http.StringContent(
                        requestJson, System.Text.Encoding.UTF8, "application/json");

                    var response     = await http.PostAsync("https://api.anthropic.com/v1/messages", httpContent);
                    string responseJson = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(responseJson);
                        if (doc.RootElement.TryGetProperty("content", out var contentArr) &&
                            contentArr.GetArrayLength() > 0)
                        {
                            var first = contentArr[0];
                            if (first.TryGetProperty("text", out var textProp))
                                responseText = textProp.GetString() ?? "";
                        }

                        if (string.IsNullOrEmpty(responseText))
                            _aiReviewForm.LogApiRaw(responseJson.Length > 300 ? responseJson.Substring(0, 300) : responseJson);

                        string upper = responseText.ToUpper();
                        if      (upper.Contains("BUY"))  signal = "BUY";
                        else if (upper.Contains("SELL")) signal = "SELL";
                        else                             signal = "HOLD";
                    }
                    else
                    {
                        _aiReviewForm.LogApiError($"HTTP {(int)response.StatusCode} — {(responseJson.Length > 200 ? responseJson.Substring(0, 200) : responseJson)}");
                    }
                }
                catch (Exception ex)
                {
                    _aiReviewForm.LogApiError($"{ex.GetType().Name}: {ex.Message}");
                    signal = "HOLD";
                }

                if (!string.IsNullOrEmpty(responseText))
                    _aiReviewForm.LogSummaryText(responseText);

                _aiReviewForm.LogSignal(signal);

                if (dateToLastBar.TryGetValue(monthEndDate, out int barIdx))
                    results.Add((monthEndDate, barIdx, signal));
            }

            DrawMonthEndSignals(results);

            int buys  = results.Count(r => r.signal == "BUY");
            int sells = results.Count(r => r.signal == "SELL");
            int holds = results.Count(r => r.signal == "HOLD");

            _aiReviewForm.LogComplete(monthEnds.Count, results.Count);
            _aiReviewForm.SetSummary($"{symbol} — DONE  |  ▲ BUY:{buys}  ▼ SELL:{sells}  ◆ HOLD:{holds}  |  {results.Count} months with data");
            lblStatus.Text = $"AI Review: {results.Count} months analyzed — BUY:{buys}  SELL:{sells}  HOLD:{holds}";
        }

        private void DrawMonthEndSignals(List<(DateTime date, int barIndex, string signal)> results)
        {
            // Remove old series
            ClearMonthEndSignals();

            // Buy series — green filled triangle
            _aiSignalBuySeries = new Series("AI_Buy");
            _aiSignalBuySeries.ChartType = SeriesChartType.Point;
            _aiSignalBuySeries.ChartArea = "MainArea";
            _aiSignalBuySeries.MarkerStyle = MarkerStyle.Triangle;
            _aiSignalBuySeries.MarkerSize = 14;
            _aiSignalBuySeries.MarkerColor = Color.LimeGreen;
            _aiSignalBuySeries.MarkerBorderColor = Color.DarkGreen;
            _aiSignalBuySeries.MarkerBorderWidth = 1;
            _aiSignalBuySeries.IsVisibleInLegend = false;

            // Sell series — red filled triangle
            _aiSignalSellSeries = new Series("AI_Sell");
            _aiSignalSellSeries.ChartType = SeriesChartType.Point;
            _aiSignalSellSeries.ChartArea = "MainArea";
            _aiSignalSellSeries.MarkerStyle = MarkerStyle.Triangle;
            _aiSignalSellSeries.MarkerSize = 14;
            _aiSignalSellSeries.MarkerColor = Color.Red;
            _aiSignalSellSeries.MarkerBorderColor = Color.DarkRed;
            _aiSignalSellSeries.MarkerBorderWidth = 1;
            _aiSignalSellSeries.IsVisibleInLegend = false;

            // Hold series — hollow black triangle (white fill with black border)
            _aiSignalHoldSeries = new Series("AI_Hold");
            _aiSignalHoldSeries.ChartType = SeriesChartType.Point;
            _aiSignalHoldSeries.ChartArea = "MainArea";
            _aiSignalHoldSeries.MarkerStyle = MarkerStyle.Triangle;
            _aiSignalHoldSeries.MarkerSize = 14;
            _aiSignalHoldSeries.MarkerColor = Color.White;
            _aiSignalHoldSeries.MarkerBorderColor = Color.Black;
            _aiSignalHoldSeries.MarkerBorderWidth = 2;
            _aiSignalHoldSeries.IsVisibleInLegend = false;

            foreach (var (date, barIndex, signal) in results)
            {
                if (barIndex < 0 || barIndex >= _cachedLows.Count) continue;
                double yPos = (double)(_cachedLows[barIndex] * 0.993m); // just below the candle

                switch (signal)
                {
                    case "BUY":
                        _aiSignalBuySeries.Points.AddXY(barIndex, yPos);
                        break;
                    case "SELL":
                        _aiSignalSellSeries.Points.AddXY(barIndex, yPos);
                        break;
                    default:
                        _aiSignalHoldSeries.Points.AddXY(barIndex, yPos);
                        break;
                }
            }

            chartStock.Series.Add(_aiSignalBuySeries);
            chartStock.Series.Add(_aiSignalSellSeries);
            chartStock.Series.Add(_aiSignalHoldSeries);
        }

        private void ClearMonthEndSignals()
        {
            foreach (string name in new[] { "AI_Buy", "AI_Sell", "AI_Hold" })
            {
                var s = chartStock.Series.FindByName(name);
                if (s != null) chartStock.Series.Remove(s);
            }
            _aiSignalBuySeries  = null;
            _aiSignalSellSeries = null;
            _aiSignalHoldSeries = null;
        }

        private void ResolveEventBarIndices(List<StockEvent> events)
        {
            var dateToFirstBar = new Dictionary<DateTime, int>();
            for (int i = 0; i < _cachedDates.Count; i++)
            {
                DateTime d = _cachedDates[i].Date;
                if (!dateToFirstBar.ContainsKey(d))
                    dateToFirstBar[d] = i;
            }

            foreach (var evt in events)
            {
                DateTime evtDate = evt.Date.Date;
                if (dateToFirstBar.TryGetValue(evtDate, out int barIdx))
                {
                    evt.BarIndex = barIdx;
                }
                else
                {
                    // Event on weekend/holiday — roll forward to next trading day
                    for (int offset = 1; offset <= 5; offset++)
                    {
                        if (dateToFirstBar.TryGetValue(evtDate.AddDays(offset), out int nextIdx))
                        {
                            evt.BarIndex = nextIdx;
                            break;
                        }
                    }
                }
            }
        }

        private void CorrelateEventsWithGaps(List<StockEvent> events)
        {
            foreach (var evt in events)
            {
                DateTime evtDate = evt.Date.Date;
                foreach (var gap in _detectedGaps)
                {
                    int dayDiff = (int)(gap.GapDate - evtDate).TotalDays;
                    // Gap occurs same day or up to 2 days after event (e.g. AMC earnings → next-day gap)
                    // Also handle event filed 1 day after gap (e.g. 8-K filed day after earnings)
                    if (dayDiff >= -1 && dayDiff <= 2)
                    {
                        evt.IsGapEvent = true;
                        evt.GapPercent = gap.GapPercent;
                        break;
                    }
                }
            }
        }

        private void DrawEventMarkers()
        {
            ClearEventMarkers();
            if (_cachedDates.Count == 0) return;

            var area = chartStock.ChartAreas["MainArea"];

            // Gap StripLines — gold/red for correlated, gray for unexplained
            foreach (var gap in _detectedGaps)
            {
                bool hasEvent = false;
                foreach (var ev in _stockEvents)
                    if (ev.BarIndex == gap.BarIndex && ev.IsGapEvent) { hasEvent = true; break; }

                var strip = new StripLine();
                strip.Interval = 0;
                strip.IntervalOffset = gap.BarIndex;
                strip.Tag = "EventGap";
                strip.BorderColor = Color.Transparent;
                strip.BorderWidth = 0;

                if (hasEvent)
                {
                    strip.StripWidth = 0.0006;
                    strip.BackColor  = gap.IsGapUp
                        ? Color.FromArgb(160, Color.Gold)
                        : Color.FromArgb(160, Color.OrangeRed);
                }
                else
                {
                    strip.StripWidth = 0.0003;
                    strip.BackColor  = Color.FromArgb(55, Color.DimGray);
                }
                area.AxisX.StripLines.Add(strip);
            }

            // Group events by bar index to combine labels (e.g. "E/8K")
            var byBar = new Dictionary<int, List<StockEvent>>();
            foreach (var ev in _stockEvents)
            {
                if (ev.BarIndex < 0) continue;
                if (!byBar.ContainsKey(ev.BarIndex))
                    byBar[ev.BarIndex] = new List<StockEvent>();
                byBar[ev.BarIndex].Add(ev);
            }

            foreach (var kvp in byBar)
            {
                int barIdx = kvp.Key;
                if (barIdx >= _cachedHighs.Count) continue;

                var evList = kvp.Value;
                decimal highAtBar = _cachedHighs[barIdx];

                var labels = new List<string>();
                foreach (var ev in evList)
                {
                    string lbl = GetEventLabel(ev.EventType);
                    if (!labels.Contains(lbl)) labels.Add(lbl);
                }

                var ann = new TextAnnotation();
                ann.Text = string.Join("/", labels);
                ann.Font = new Font("Segoe UI", 7F, FontStyle.Bold);
                ann.ForeColor = GetEventLabelColor(evList[0].EventType);
                ann.BackColor = Color.Transparent;
                ann.X = barIdx;
                ann.Y = (double)(highAtBar * 1.006m);
                ann.AnchorAlignment = ContentAlignment.BottomCenter;
                ann.ClipToChartArea = "MainArea";
                ann.Alignment = ContentAlignment.MiddleCenter;

                chartStock.Annotations.Add(ann);
                _eventAnnotations.Add(ann);
            }
        }

        private void ClearEventMarkers()
        {
            if (chartStock.ChartAreas.Count > 0)
            {
                var area = chartStock.ChartAreas["MainArea"];
                for (int i = area.AxisX.StripLines.Count - 1; i >= 0; i--)
                {
                    if (area.AxisX.StripLines[i].Tag is string tag && tag == "EventGap")
                        area.AxisX.StripLines.RemoveAt(i);
                }
            }
            foreach (var ann in _eventAnnotations)
            {
                if (chartStock.Annotations.Contains(ann))
                    chartStock.Annotations.Remove(ann);
            }
            _eventAnnotations.Clear();
        }

        private static string GetEventLabel(StockEventType t)
        {
            switch (t)
            {
                case StockEventType.EarningsReport: return "E";
                case StockEventType.PressRelease:   return "PR";
                case StockEventType.SecFiling8K:    return "8K";
                case StockEventType.SecFiling10Q:   return "Q";
                case StockEventType.SecFiling10K:   return "K";
                case StockEventType.NewsFlash:      return "N";
                default:                            return "?";
            }
        }

        private static Color GetEventLabelColor(StockEventType t)
        {
            switch (t)
            {
                case StockEventType.EarningsReport: return Color.Gold;
                case StockEventType.PressRelease:   return Color.DeepSkyBlue;
                case StockEventType.SecFiling8K:    return Color.OrangeRed;
                case StockEventType.SecFiling10Q:   return Color.MediumPurple;
                case StockEventType.SecFiling10K:   return Color.MediumSeaGreen;
                case StockEventType.NewsFlash:      return Color.Cyan;
                default:                            return Color.White;
            }
        }

        private void txtGapThreshold_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (decimal.TryParse(txtGapThreshold.Text, out decimal val) && val > 0)
                    _gapThreshold = val;
                else
                    txtGapThreshold.Text = _gapThreshold.ToString("F1");
                e.SuppressKeyPress = true;
            }
        }

        private void txtGapThreshold_Leave(object sender, EventArgs e)
        {
            if (!decimal.TryParse(txtGapThreshold.Text, out decimal val) || val <= 0)
                txtGapThreshold.Text = _gapThreshold.ToString("F1");
            else
                _gapThreshold = val;
        }

        private void RebuildPriceSeriesFromCache()
        {
            bool isCandlestick = rbCandlestick.Checked;

            // Remove old Price series
            if (chartStock.Series.FindByName("Price") != null)
                chartStock.Series.Remove(chartStock.Series["Price"]);

            var priceSeries = new Series("Price");
            priceSeries.ChartArea = "MainArea";
            priceSeries.IsVisibleInLegend = false;

            if (isCandlestick)
            {
                priceSeries.ChartType = SeriesChartType.Candlestick;
                priceSeries.YValuesPerPoint = 4;
                priceSeries["PriceUpColor"] = "Green";
                priceSeries["PriceDownColor"] = "Red";
                priceSeries["ShowOpenClose"] = "Both";

                priceSeries.Points.SuspendUpdates();
                for (int i = 0; i < _cachedDates.Count; i++)
                {
                    int idx = priceSeries.Points.AddXY(
                        i,
                        (double)_cachedHighs[i],
                        (double)_cachedLows[i],
                        (double)_cachedOpens[i],
                        (double)_cachedCloses[i]);

                    var pt = priceSeries.Points[idx];
                    if (_cachedCloses[i] >= _cachedOpens[i])
                    {
                        pt.Color = Color.Green;
                        pt.BorderColor = Color.DarkGreen;
                    }
                    else
                    {
                        pt.Color = Color.Red;
                        pt.BorderColor = Color.DarkRed;
                    }
                }
                priceSeries.Points.ResumeUpdates();
            }
            else
            {
                // Line chart - plot Close prices
                priceSeries.ChartType = SeriesChartType.Line;
                priceSeries.YValuesPerPoint = 1;
                priceSeries.Color = Color.RoyalBlue;
                priceSeries.BorderWidth = 2;

                priceSeries.Points.SuspendUpdates();
                for (int i = 0; i < _cachedDates.Count; i++)
                {
                    priceSeries.Points.AddXY(
                        i,
                        (double)_cachedCloses[i]);
                }
                priceSeries.Points.ResumeUpdates();
            }

            // Insert Price series at position 0 so it renders behind indicators
            chartStock.Series.Insert(0, priceSeries);

            ApplyCustomXAxisLabels();
            DrawDayDividers();
            RecalculateYAxis();
        }

        // ============================================================
        // Pivot Threshold TextBox - Enter key applies
        // ============================================================
        private void txtPivotThreshold_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ApplyPivotThreshold();
            }
        }

        private void txtPivotThreshold_Leave(object sender, EventArgs e)
        {
            ApplyPivotThreshold();
        }

        private void ApplyPivotThreshold()
        {
            if (decimal.TryParse(txtPivotThreshold.Text, out decimal newThreshold) && newThreshold > 0 && newThreshold <= 100)
            {
                if (newThreshold != _pivotThreshold)
                {
                    _pivotThreshold = newThreshold;
                    txtPivotThreshold.Text = _pivotThreshold.ToString("F1");
                    DetectAndDrawPivots();
                }
            }
            else
            {
                // Reset to current valid value
                txtPivotThreshold.Text = _pivotThreshold.ToString("F1");
            }
        }

        // ============================================================
        // Detect pivot points where price moved by threshold percentage
        // (Adapted from ZStock Machine IndicatorAnalysisViewModel)
        // ============================================================
        private List<PivotPoint> DetectPivotPoints(decimal thresholdPercent)
        {
            var pivots = new List<PivotPoint>();
            if (_cachedCloses.Count < 3) return pivots;

            decimal lastPivotPrice = _cachedCloses[0];
            PivotType lookingFor = PivotType.Peak;

            decimal extremePrice = lastPivotPrice;
            DateTime extremeDate = _cachedDates[0];
            int extremeIndex = 0;

            for (int i = 1; i < _cachedCloses.Count; i++)
            {
                decimal close = _cachedCloses[i];

                if (lookingFor == PivotType.Peak)
                {
                    if (close > extremePrice)
                    {
                        extremePrice = close;
                        extremeDate = _cachedDates[i];
                        extremeIndex = i;
                    }

                    if (extremePrice > 0)
                    {
                        decimal declinePercent = ((extremePrice - close) / extremePrice) * 100;
                        if (declinePercent >= thresholdPercent)
                        {
                            decimal changeFromLastPivot = lastPivotPrice > 0
                                ? ((extremePrice - lastPivotPrice) / lastPivotPrice) * 100
                                : 0;

                            pivots.Add(new PivotPoint
                            {
                                Date = extremeDate,
                                Price = extremePrice,
                                Type = PivotType.Peak,
                                PercentChange = changeFromLastPivot,
                                BarIndex = extremeIndex
                            });

                            lastPivotPrice = extremePrice;
                            lookingFor = PivotType.Trough;
                            extremePrice = close;
                            extremeDate = _cachedDates[i];
                            extremeIndex = i;
                        }
                    }
                }
                else
                {
                    if (close < extremePrice)
                    {
                        extremePrice = close;
                        extremeDate = _cachedDates[i];
                        extremeIndex = i;
                    }

                    if (extremePrice > 0)
                    {
                        decimal gainPercent = ((close - extremePrice) / extremePrice) * 100;
                        if (gainPercent >= thresholdPercent)
                        {
                            decimal changeFromLastPivot = lastPivotPrice > 0
                                ? ((extremePrice - lastPivotPrice) / lastPivotPrice) * 100
                                : 0;

                            pivots.Add(new PivotPoint
                            {
                                Date = extremeDate,
                                Price = extremePrice,
                                Type = PivotType.Trough,
                                PercentChange = changeFromLastPivot,
                                BarIndex = extremeIndex
                            });

                            lastPivotPrice = extremePrice;
                            lookingFor = PivotType.Peak;
                            extremePrice = close;
                            extremeDate = _cachedDates[i];
                            extremeIndex = i;
                        }
                    }
                }
            }

            return pivots;
        }

        // ============================================================
        // Detect pivots and draw markers: red circles on peaks, green circles on troughs
        // ============================================================
        private void DetectAndDrawPivots()
        {
            // Remove previous pivot markers
            if (_pivotPeakSeries != null && chartStock.Series.FindByName("PivotPeaks") != null)
                chartStock.Series.Remove(_pivotPeakSeries);
            if (_pivotTroughSeries != null && chartStock.Series.FindByName("PivotTroughs") != null)
                chartStock.Series.Remove(_pivotTroughSeries);

            foreach (var ann in _pivotAnnotations)
            {
                if (chartStock.Annotations.Contains(ann))
                    chartStock.Annotations.Remove(ann);
            }
            _pivotAnnotations.Clear();

            if (_cachedCloses.Count == 0) return;

            // Detect pivots
            _pivotPoints = DetectPivotPoints(_pivotThreshold);

            // --- Peak series (red hollow circles) ---
            _pivotPeakSeries = new Series("PivotPeaks");
            _pivotPeakSeries.ChartType = SeriesChartType.Point;
            _pivotPeakSeries.ChartArea = "MainArea";
            _pivotPeakSeries.MarkerStyle = MarkerStyle.Circle;
            _pivotPeakSeries.MarkerSize = 12;
            _pivotPeakSeries.MarkerColor = Color.Transparent;
            _pivotPeakSeries.MarkerBorderColor = Color.Red;
            _pivotPeakSeries.MarkerBorderWidth = 3;
            _pivotPeakSeries.IsVisibleInLegend = false;

            foreach (var pivot in _pivotPoints.Where(p => p.Type == PivotType.Peak))
            {
                _pivotPeakSeries.Points.AddXY(pivot.BarIndex, (double)pivot.Price);
            }

            chartStock.Series.Add(_pivotPeakSeries);

            // --- Trough series (green hollow circles) ---
            _pivotTroughSeries = new Series("PivotTroughs");
            _pivotTroughSeries.ChartType = SeriesChartType.Point;
            _pivotTroughSeries.ChartArea = "MainArea";
            _pivotTroughSeries.MarkerStyle = MarkerStyle.Circle;
            _pivotTroughSeries.MarkerSize = 12;
            _pivotTroughSeries.MarkerColor = Color.Transparent;
            _pivotTroughSeries.MarkerBorderColor = Color.Green;
            _pivotTroughSeries.MarkerBorderWidth = 3;
            _pivotTroughSeries.IsVisibleInLegend = false;

            foreach (var pivot in _pivotPoints.Where(p => p.Type == PivotType.Trough))
            {
                _pivotTroughSeries.Points.AddXY(pivot.BarIndex, (double)pivot.Price);
            }

            chartStock.Series.Add(_pivotTroughSeries);

            int peakCount = _pivotPoints.Count(p => p.Type == PivotType.Peak);
            int troughCount = _pivotPoints.Count(p => p.Type == PivotType.Trough);
            lblStatus.Text = $"Pivots: {peakCount} peaks (red), {troughCount} troughs (green) - threshold: {_pivotThreshold:F1}%";
        }

        // ============================================================
        // Draw horizontal black close-price tick on each candlestick
        // ============================================================
        private void DrawClosePriceTicks(object sender, ChartPaintEventArgs e)
        {
            if (!rbCandlestick.Checked) return;
            if (_cachedCloses.Count == 0) return;

            var priceSeries = chartStock.Series.FindByName("Price");
            if (priceSeries == null || priceSeries.Points.Count == 0) return;

            var area = chartStock.ChartAreas["MainArea"];
            var g = e.ChartGraphics.Graphics;

            // Determine visible X range
            double xMin = area.AxisX.ScaleView.ViewMinimum;
            double xMax = area.AxisX.ScaleView.ViewMaximum;
            if (double.IsNaN(xMin)) xMin = area.AxisX.Minimum;
            if (double.IsNaN(xMax)) xMax = area.AxisX.Maximum;

            // Half-width of a candle body in X axis units (pixels ÷ total bars × view range)
            RectangleF plotArea = e.ChartGraphics.GetAbsoluteRectangle(area.InnerPlotPosition.ToRectangleF());
            double viewRange = xMax - xMin;
            if (viewRange <= 0) return;

            double pixelsPerBar = plotArea.Width / viewRange;
            float halfTickPx = Math.Max(1f, (float)(pixelsPerBar * 0.45));

            using var pen = new Pen(Color.Black, 1.5f);

            foreach (var pt in priceSeries.Points)
            {
                double xVal = pt.XValue;
                if (xVal < xMin - 1 || xVal > xMax + 1) continue;

                double closeVal = pt.YValues[3]; // High, Low, Open, Close

                // Convert data coordinates to pixel coordinates
                double xPx = area.AxisX.ValueToPixelPosition(xVal);
                double yPx = area.AxisY.ValueToPixelPosition(closeVal);

                float x1 = (float)(xPx - halfTickPx);
                float x2 = (float)(xPx + halfTickPx);
                float y  = (float)yPx;

                g.DrawLine(pen, x1, y, x2, y);
            }
        }
    }
}
