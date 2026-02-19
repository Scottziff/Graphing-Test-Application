using System;

namespace Graphing_Test_Application
{
    public enum StockEventType
    {
        EarningsReport,
        PressRelease,
        SecFiling8K,
        SecFiling10Q,
        SecFiling10K,
        NewsFlash          // From local [News Flash] SQL table
    }

    public class StockEvent
    {
        public DateTime Date { get; set; }
        public StockEventType EventType { get; set; }
        public string Title { get; set; }       // Short label shown on chart annotation
        public string Detail { get; set; }      // Full text shown in hover panel
        public string Url { get; set; }
        public int BarIndex { get; set; } = -1;
        public decimal GapPercent { get; set; }
        public bool IsGapEvent { get; set; }
        public string Source { get; set; }      // "FMP", "SEC", "NewsFlash"
    }

    public class GapBar
    {
        public DateTime GapDate { get; set; }
        public int BarIndex { get; set; }
        public decimal PreviousClose { get; set; }
        public decimal GapOpen { get; set; }
        public decimal GapPercent { get; set; }
        public bool IsGapUp { get; set; }
    }
}
