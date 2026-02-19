using System;
using System.Collections.Generic;

namespace Graphing_Test_Application
{
    // ============================================================
    // GapAnalyzer — detects close-to-open price gaps at day boundaries
    // A gap occurs when the first bar of a trading day opens significantly
    // higher or lower than the last bar of the previous trading day's close.
    // ============================================================
    public static class GapAnalyzer
    {
        public static List<GapBar> DetectGaps(
            List<DateTime> dates,
            List<decimal> opens,
            List<decimal> closes,
            decimal thresholdPercent)
        {
            var gaps = new List<GapBar>();
            if (dates == null || dates.Count < 2) return gaps;

            for (int i = 1; i < dates.Count; i++)
            {
                // Only trigger at day boundaries
                if (dates[i].Date == dates[i - 1].Date) continue;

                decimal prevClose = closes[i - 1];
                decimal thisOpen  = opens[i];

                if (prevClose == 0) continue;

                decimal gapPct = (thisOpen - prevClose) / prevClose * 100m;

                if (Math.Abs(gapPct) >= thresholdPercent)
                {
                    gaps.Add(new GapBar
                    {
                        GapDate       = dates[i].Date,
                        BarIndex      = i,
                        PreviousClose = prevClose,
                        GapOpen       = thisOpen,
                        GapPercent    = gapPct,
                        IsGapUp       = gapPct > 0
                    });
                }
            }
            return gaps;
        }
    }
}
