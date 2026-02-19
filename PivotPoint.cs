using System;

namespace Graphing_Test_Application
{
    public enum PivotType
    {
        Peak,
        Trough
    }

    public class PivotPoint
    {
        public DateTime Date { get; set; }
        public decimal Price { get; set; }
        public PivotType Type { get; set; }
        public decimal PercentChange { get; set; }
        public int BarIndex { get; set; }
    }
}
