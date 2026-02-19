using System;
using System.Collections.Generic;
using System.Drawing;

namespace Graphing_Test_Application
{
    // ============================================================
    // Signal Types and Result
    // ============================================================
    public enum SignalType { Buy, Sell, Hold }

    public class SignalResult
    {
        public SignalType Signal { get; set; }
        public string DisplayValue { get; set; }
        public Color SignalColor { get; set; }

        public static readonly Color BuyColor = Color.FromArgb(0, 230, 118);
        public static readonly Color SellColor = Color.FromArgb(255, 82, 82);
        public static readonly Color HoldColor = Color.FromArgb(255, 193, 7);

        public static SignalResult Buy(string value) =>
            new SignalResult { Signal = SignalType.Buy, DisplayValue = value, SignalColor = BuyColor };

        public static SignalResult Sell(string value) =>
            new SignalResult { Signal = SignalType.Sell, DisplayValue = value, SignalColor = SellColor };

        public static SignalResult Hold(string value) =>
            new SignalResult { Signal = SignalType.Hold, DisplayValue = value, SignalColor = HoldColor };

        public static SignalResult NoData() =>
            new SignalResult { Signal = SignalType.Hold, DisplayValue = "N/A", SignalColor = Color.Gray };
    }

    // ============================================================
    // IndicatorSignalEvaluator - computes Buy/Sell/Hold for all 21
    // ============================================================
    public static class IndicatorSignalEvaluator
    {
        public static Dictionary<string, SignalResult> EvaluateAll(
            List<decimal> opens, List<decimal> highs, List<decimal> lows,
            List<decimal> closes, List<decimal> volumes)
        {
            var signals = new Dictionary<string, SignalResult>();

            if (closes == null || closes.Count < 2)
            {
                foreach (var def in IndicatorRegistry.GetAll())
                    signals[def.Key] = SignalResult.NoData();
                return signals;
            }

            int last = closes.Count - 1;
            decimal close = closes[last];

            // --- SMA ---
            signals["SMA"] = EvaluateSMA(closes, close);

            // --- EMA ---
            signals["EMA"] = EvaluateEMA(closes, close);

            // --- MACD ---
            signals["MACD"] = EvaluateMACD(closes);

            // --- ADX ---
            signals["ADX"] = EvaluateADX(highs, lows, closes);

            // --- Parabolic SAR ---
            signals["PSAR"] = EvaluatePSAR(highs, lows, close);

            // --- Ichimoku ---
            signals["ICHI"] = EvaluateIchimoku(highs, lows, closes, close);

            // --- RSI ---
            signals["RSI"] = EvaluateRSI(closes);

            // --- Stochastic ---
            signals["STOCH"] = EvaluateStochastic(highs, lows, closes);

            // --- Momentum ---
            signals["MOM"] = EvaluateMomentum(closes);

            // --- ROC ---
            signals["ROC"] = EvaluateROC(closes);

            // --- CCI ---
            signals["CCI"] = EvaluateCCI(highs, lows, closes);

            // --- Williams %R ---
            signals["WILLR"] = EvaluateWilliamsR(highs, lows, closes);

            // --- Bollinger Bands ---
            signals["BB"] = EvaluateBB(closes, close);

            // --- Keltner Channels ---
            signals["KC"] = EvaluateKC(highs, lows, closes, close);

            // --- ATR ---
            signals["ATR"] = EvaluateATR(highs, lows, closes);

            // --- OBV ---
            signals["OBV"] = EvaluateOBV(closes, volumes);

            // --- MFI ---
            signals["MFI"] = EvaluateMFI(highs, lows, closes, volumes);

            // --- CMF ---
            signals["CMF"] = EvaluateCMF(highs, lows, closes, volumes);

            // --- A/D Line ---
            signals["ADL"] = EvaluateADL(highs, lows, closes, volumes);

            // --- VWAP ---
            signals["VWAP"] = EvaluateVWAP(highs, lows, closes, volumes, close);

            // --- Aroon ---
            signals["AROON"] = EvaluateAroon(highs, lows);

            return signals;
        }

        public static Dictionary<string, SignalResult> EvaluateAllAtIndex(
            List<decimal> opens, List<decimal> highs, List<decimal> lows,
            List<decimal> closes, List<decimal> volumes, int barIndex)
        {
            if (closes == null || barIndex < 1 || barIndex >= closes.Count)
                return EvaluateAll(opens, highs, lows, closes, volumes);

            int count = barIndex + 1;
            return EvaluateAll(
                opens.GetRange(0, count),
                highs.GetRange(0, count),
                lows.GetRange(0, count),
                closes.GetRange(0, count),
                volumes.GetRange(0, count));
        }

        // ----- Individual evaluators -----

        private static SignalResult EvaluateSMA(List<decimal> closes, decimal close)
        {
            var sma = TechnicalIndicatorCalcs.CalculateSMA(closes, 20);
            var val = GetLastValue(sma);
            if (!val.HasValue) return SignalResult.NoData();
            string disp = val.Value.ToString("F2");
            return close > val.Value ? SignalResult.Buy(disp) : SignalResult.Sell(disp);
        }

        private static SignalResult EvaluateEMA(List<decimal> closes, decimal close)
        {
            var ema = TechnicalIndicatorCalcs.CalculateEMA(closes, 12);
            var val = GetLastValue(ema);
            if (!val.HasValue) return SignalResult.NoData();
            string disp = val.Value.ToString("F2");
            return close > val.Value ? SignalResult.Buy(disp) : SignalResult.Sell(disp);
        }

        private static SignalResult EvaluateMACD(List<decimal> closes)
        {
            var macd = TechnicalIndicatorCalcs.CalculateMACD(closes, 12, 26, 9);
            var hist = GetLastValue(macd.Histogram);
            if (!hist.HasValue) return SignalResult.NoData();
            string disp = hist.Value.ToString("F2");
            return hist.Value > 0 ? SignalResult.Buy(disp) : SignalResult.Sell(disp);
        }

        private static SignalResult EvaluateADX(List<decimal> highs, List<decimal> lows, List<decimal> closes)
        {
            var adx = TechnicalIndicatorCalcs.CalculateADXWithDI(highs, lows, closes, 14);
            var adxVal = GetLastValue(adx.ADX);
            var pdi = GetLastValue(adx.PlusDI);
            var mdi = GetLastValue(adx.MinusDI);
            if (!adxVal.HasValue || !pdi.HasValue || !mdi.HasValue) return SignalResult.NoData();
            string disp = adxVal.Value.ToString("F1");
            if (adxVal.Value < 20) return SignalResult.Hold(disp);
            return pdi.Value > mdi.Value ? SignalResult.Buy(disp) : SignalResult.Sell(disp);
        }

        private static SignalResult EvaluatePSAR(List<decimal> highs, List<decimal> lows, decimal close)
        {
            var psar = TechnicalIndicatorCalcs.CalculateParabolicSAR(highs, lows, 0.02m, 0.2m);
            var val = GetLastValue(psar);
            if (!val.HasValue) return SignalResult.NoData();
            string disp = val.Value.ToString("F2");
            return val.Value < close ? SignalResult.Buy(disp) : SignalResult.Sell(disp);
        }

        private static SignalResult EvaluateIchimoku(List<decimal> highs, List<decimal> lows, List<decimal> closes, decimal close)
        {
            var ichi = TechnicalIndicatorCalcs.CalculateIchimoku(highs, lows, closes);
            var spanA = GetLastValue(ichi.SenkouSpanA);
            var spanB = GetLastValue(ichi.SenkouSpanB);
            if (!spanA.HasValue || !spanB.HasValue) return SignalResult.NoData();
            decimal cloudTop = Math.Max(spanA.Value, spanB.Value);
            decimal cloudBottom = Math.Min(spanA.Value, spanB.Value);
            if (close > cloudTop) return SignalResult.Buy("Above");
            if (close < cloudBottom) return SignalResult.Sell("Below");
            return SignalResult.Hold("In Cloud");
        }

        private static SignalResult EvaluateRSI(List<decimal> closes)
        {
            var rsi = TechnicalIndicatorCalcs.CalculateRSI(closes, 14);
            var val = GetLastValue(rsi);
            if (!val.HasValue) return SignalResult.NoData();
            string disp = val.Value.ToString("F1");
            if (val.Value < 30) return SignalResult.Buy(disp);
            if (val.Value > 70) return SignalResult.Sell(disp);
            return SignalResult.Hold(disp);
        }

        private static SignalResult EvaluateStochastic(List<decimal> highs, List<decimal> lows, List<decimal> closes)
        {
            var stoch = TechnicalIndicatorCalcs.CalculateStochastic(highs, lows, closes, 14, 3);
            var k = GetLastValue(stoch.PercentK);
            if (!k.HasValue) return SignalResult.NoData();
            string disp = k.Value.ToString("F1");
            if (k.Value < 20) return SignalResult.Buy(disp);
            if (k.Value > 80) return SignalResult.Sell(disp);
            return SignalResult.Hold(disp);
        }

        private static SignalResult EvaluateMomentum(List<decimal> closes)
        {
            var mom = TechnicalIndicatorCalcs.CalculateMomentum(closes, 10);
            var val = GetLastValue(mom);
            if (!val.HasValue) return SignalResult.NoData();
            string disp = val.Value.ToString("F2");
            return val.Value > 0 ? SignalResult.Buy(disp) : SignalResult.Sell(disp);
        }

        private static SignalResult EvaluateROC(List<decimal> closes)
        {
            var roc = TechnicalIndicatorCalcs.CalculateROC(closes, 12);
            var val = GetLastValue(roc);
            if (!val.HasValue) return SignalResult.NoData();
            string disp = val.Value.ToString("F2") + "%";
            return val.Value > 0 ? SignalResult.Buy(disp) : SignalResult.Sell(disp);
        }

        private static SignalResult EvaluateCCI(List<decimal> highs, List<decimal> lows, List<decimal> closes)
        {
            var cci = TechnicalIndicatorCalcs.CalculateCCI(highs, lows, closes, 20);
            var val = GetLastValue(cci);
            if (!val.HasValue) return SignalResult.NoData();
            string disp = val.Value.ToString("F1");
            if (val.Value < -100) return SignalResult.Buy(disp);
            if (val.Value > 100) return SignalResult.Sell(disp);
            return SignalResult.Hold(disp);
        }

        private static SignalResult EvaluateWilliamsR(List<decimal> highs, List<decimal> lows, List<decimal> closes)
        {
            var willr = TechnicalIndicatorCalcs.CalculateWilliamsR(highs, lows, closes, 14);
            var val = GetLastValue(willr);
            if (!val.HasValue) return SignalResult.NoData();
            string disp = val.Value.ToString("F1");
            if (val.Value < -80) return SignalResult.Buy(disp);
            if (val.Value > -20) return SignalResult.Sell(disp);
            return SignalResult.Hold(disp);
        }

        private static SignalResult EvaluateBB(List<decimal> closes, decimal close)
        {
            var bb = TechnicalIndicatorCalcs.CalculateBollingerBands(closes, 20, 2.0m);
            var upper = GetLastValue(bb.UpperBand);
            var lower = GetLastValue(bb.LowerBand);
            if (!upper.HasValue || !lower.HasValue) return SignalResult.NoData();
            if (close < lower.Value) return SignalResult.Buy("Below");
            if (close > upper.Value) return SignalResult.Sell("Above");
            return SignalResult.Hold("Inside");
        }

        private static SignalResult EvaluateKC(List<decimal> highs, List<decimal> lows, List<decimal> closes, decimal close)
        {
            var kc = TechnicalIndicatorCalcs.CalculateKeltnerChannels(highs, lows, closes, 20, 10, 2.0m);
            var upper = GetLastValue(kc.UpperChannel);
            var lower = GetLastValue(kc.LowerChannel);
            if (!upper.HasValue || !lower.HasValue) return SignalResult.NoData();
            if (close < lower.Value) return SignalResult.Buy("Below");
            if (close > upper.Value) return SignalResult.Sell("Above");
            return SignalResult.Hold("Inside");
        }

        private static SignalResult EvaluateATR(List<decimal> highs, List<decimal> lows, List<decimal> closes)
        {
            var atr = TechnicalIndicatorCalcs.CalculateATR(highs, lows, closes, 14);
            int last = atr.Count - 1;
            if (last < 1 || !atr[last].HasValue) return SignalResult.NoData();
            string disp = atr[last].Value.ToString("F2");
            // Compare current ATR to previous ATR
            var prev = last > 0 ? atr[last - 1] : null;
            if (!prev.HasValue) return SignalResult.Hold(disp);
            if (atr[last].Value > prev.Value) return SignalResult.Buy(disp);   // Rising volatility = opportunity
            if (atr[last].Value < prev.Value) return SignalResult.Sell(disp);   // Falling volatility
            return SignalResult.Hold(disp);
        }

        private static SignalResult EvaluateOBV(List<decimal> closes, List<decimal> volumes)
        {
            var obv = TechnicalIndicatorCalcs.CalculateOBV(closes, volumes);
            int last = obv.Count - 1;
            if (last < 1 || !obv[last].HasValue) return SignalResult.NoData();
            var prev = last > 0 ? obv[last - 1] : null;
            string disp = FormatLargeNumber(obv[last].Value);
            if (!prev.HasValue) return SignalResult.Hold(disp);
            if (obv[last].Value > prev.Value) return SignalResult.Buy(disp);
            if (obv[last].Value < prev.Value) return SignalResult.Sell(disp);
            return SignalResult.Hold(disp);
        }

        private static SignalResult EvaluateMFI(List<decimal> highs, List<decimal> lows, List<decimal> closes, List<decimal> volumes)
        {
            var mfi = TechnicalIndicatorCalcs.CalculateMFI(highs, lows, closes, volumes, 14);
            var val = GetLastValue(mfi);
            if (!val.HasValue) return SignalResult.NoData();
            string disp = val.Value.ToString("F1");
            if (val.Value < 20) return SignalResult.Buy(disp);
            if (val.Value > 80) return SignalResult.Sell(disp);
            return SignalResult.Hold(disp);
        }

        private static SignalResult EvaluateCMF(List<decimal> highs, List<decimal> lows, List<decimal> closes, List<decimal> volumes)
        {
            var cmf = TechnicalIndicatorCalcs.CalculateChaikinMoneyFlow(highs, lows, closes, volumes, 20);
            var val = GetLastValue(cmf);
            if (!val.HasValue) return SignalResult.NoData();
            string disp = val.Value.ToString("F3");
            if (val.Value > 0.05m) return SignalResult.Buy(disp);
            if (val.Value < -0.05m) return SignalResult.Sell(disp);
            return SignalResult.Hold(disp);
        }

        private static SignalResult EvaluateADL(List<decimal> highs, List<decimal> lows, List<decimal> closes, List<decimal> volumes)
        {
            var adl = TechnicalIndicatorCalcs.CalculateADL(highs, lows, closes, volumes);
            int last = adl.Count - 1;
            if (last < 1 || !adl[last].HasValue) return SignalResult.NoData();
            var prev = last > 0 ? adl[last - 1] : null;
            string disp = FormatLargeNumber(adl[last].Value);
            if (!prev.HasValue) return SignalResult.Hold(disp);
            if (adl[last].Value > prev.Value) return SignalResult.Buy(disp);
            if (adl[last].Value < prev.Value) return SignalResult.Sell(disp);
            return SignalResult.Hold(disp);
        }

        private static SignalResult EvaluateVWAP(List<decimal> highs, List<decimal> lows, List<decimal> closes, List<decimal> volumes, decimal close)
        {
            var vwap = TechnicalIndicatorCalcs.CalculateVWAP(highs, lows, closes, volumes);
            var val = GetLastValue(vwap);
            if (!val.HasValue) return SignalResult.NoData();
            string disp = val.Value.ToString("F2");
            return close > val.Value ? SignalResult.Buy(disp) : SignalResult.Sell(disp);
        }

        private static SignalResult EvaluateAroon(List<decimal> highs, List<decimal> lows)
        {
            var aroon = TechnicalIndicatorCalcs.CalculateAroonWithOscillator(highs, lows, 25);
            var up = GetLastValue(aroon.AroonUp);
            var down = GetLastValue(aroon.AroonDown);
            if (!up.HasValue || !down.HasValue) return SignalResult.NoData();
            string disp = $"{up.Value:F0}/{down.Value:F0}";
            if (up.Value > 70 && down.Value < 30) return SignalResult.Buy(disp);
            if (down.Value > 70 && up.Value < 30) return SignalResult.Sell(disp);
            return SignalResult.Hold(disp);
        }

        // ----- Helpers -----

        private static decimal? GetLastValue(List<decimal?> values)
        {
            if (values == null || values.Count == 0) return null;
            for (int i = values.Count - 1; i >= 0; i--)
            {
                if (values[i].HasValue) return values[i];
            }
            return null;
        }

        private static string FormatLargeNumber(decimal value)
        {
            decimal abs = Math.Abs(value);
            if (abs >= 1_000_000_000) return (value / 1_000_000_000).ToString("F1") + "B";
            if (abs >= 1_000_000) return (value / 1_000_000).ToString("F1") + "M";
            if (abs >= 1_000) return (value / 1_000).ToString("F1") + "K";
            return value.ToString("F0");
        }
    }
}
