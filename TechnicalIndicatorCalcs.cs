using System;
using System.Collections.Generic;
using System.Linq;

namespace Graphing_Test_Application
{
    // ============================================================
    // CalcHelpers - Ported from TechnicalAnalysisHelpers.cs
    // ============================================================
    internal static class CalcHelpers
    {
        public static decimal GetMaxInWindow(List<decimal> values, int startIndex, int windowSize)
        {
            if (values.Count == 0 || startIndex < 0) return 0;
            decimal max = decimal.MinValue;
            int endIndex = Math.Min(startIndex + windowSize, values.Count);
            for (int i = startIndex; i < endIndex; i++)
            {
                if (values[i] > max) max = values[i];
            }
            return max;
        }

        public static decimal GetMinInWindow(List<decimal> values, int startIndex, int windowSize)
        {
            if (values.Count == 0 || startIndex < 0) return 0;
            decimal min = decimal.MaxValue;
            int endIndex = Math.Min(startIndex + windowSize, values.Count);
            for (int i = startIndex; i < endIndex; i++)
            {
                if (values[i] < min) min = values[i];
            }
            return min;
        }
    }

    // ============================================================
    // Result Classes
    // ============================================================
    public class MACDResult
    {
        public List<decimal?> MACDLine { get; set; } = new List<decimal?>();
        public List<decimal?> SignalLine { get; set; } = new List<decimal?>();
        public List<decimal?> Histogram { get; set; } = new List<decimal?>();
    }

    public class BollingerBandsResult
    {
        public List<decimal?> UpperBand { get; set; } = new List<decimal?>();
        public List<decimal?> MiddleBand { get; set; } = new List<decimal?>();
        public List<decimal?> LowerBand { get; set; } = new List<decimal?>();
        public List<decimal?> Bandwidth { get; set; } = new List<decimal?>();
        public List<decimal?> PercentB { get; set; } = new List<decimal?>();
    }

    public class StochasticResult
    {
        public List<decimal?> PercentK { get; set; } = new List<decimal?>();
        public List<decimal?> PercentD { get; set; } = new List<decimal?>();
    }

    public class IchimokuResult
    {
        public List<decimal?> TenkanSen { get; set; } = new List<decimal?>();
        public List<decimal?> KijunSen { get; set; } = new List<decimal?>();
        public List<decimal?> SenkouSpanA { get; set; } = new List<decimal?>();
        public List<decimal?> SenkouSpanB { get; set; } = new List<decimal?>();
        public List<decimal?> ChikouSpan { get; set; } = new List<decimal?>();
        public int Displacement { get; set; }
    }

    public class ADXResult
    {
        public List<decimal?> ADX { get; set; } = new List<decimal?>();
        public List<decimal?> PlusDI { get; set; } = new List<decimal?>();
        public List<decimal?> MinusDI { get; set; } = new List<decimal?>();
    }

    public class KeltnerChannelResult
    {
        public List<decimal?> UpperChannel { get; set; } = new List<decimal?>();
        public List<decimal?> MiddleLine { get; set; } = new List<decimal?>();
        public List<decimal?> LowerChannel { get; set; } = new List<decimal?>();
    }

    public class AroonResult
    {
        public List<decimal?> AroonUp { get; set; } = new List<decimal?>();
        public List<decimal?> AroonDown { get; set; } = new List<decimal?>();
        public List<decimal?> Oscillator { get; set; } = new List<decimal?>();
    }

    // ============================================================
    // TechnicalIndicatorCalcs - All 21 indicator algorithms
    // Ported from AlpacaTrader TechnicalAnalysisService.cs
    // ============================================================
    public static class TechnicalIndicatorCalcs
    {
        // -------------------------------------------------------
        // SMA - Simple Moving Average
        // -------------------------------------------------------
        public static List<decimal?> CalculateSMA(List<decimal> prices, int period)
        {
            var result = new List<decimal?>();
            if (period <= 0) return result;

            var runningSum = 0m;

            for (int i = 0; i < prices.Count; i++)
            {
                runningSum += prices[i];

                if (i < period - 1)
                {
                    result.Add(null);
                }
                else
                {
                    if (i >= period)
                        runningSum -= prices[i - period];

                    result.Add(runningSum / period);
                }
            }

            return result;
        }

        // -------------------------------------------------------
        // EMA - Exponential Moving Average
        // -------------------------------------------------------
        public static List<decimal?> CalculateEMA(List<decimal> prices, int period)
        {
            var result = new List<decimal?>();
            if (period <= 0) return result;
            var multiplier = 2.0m / (period + 1);
            decimal? previousEma = null;

            for (int i = 0; i < prices.Count; i++)
            {
                if (i < period - 1)
                {
                    result.Add(null);
                }
                else if (i == period - 1)
                {
                    var sum = 0m;
                    for (int j = 0; j < period; j++)
                    {
                        sum += prices[i - j];
                    }
                    previousEma = sum / period;
                    result.Add(previousEma);
                }
                else
                {
                    var ema = (prices[i] - previousEma.Value) * multiplier + previousEma.Value;
                    result.Add(ema);
                    previousEma = ema;
                }
            }

            return result;
        }

        // -------------------------------------------------------
        // MACD - Moving Average Convergence Divergence
        // -------------------------------------------------------
        public static MACDResult CalculateMACD(List<decimal> prices, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
        {
            var fastEma = CalculateEMA(prices, fastPeriod);
            var slowEma = CalculateEMA(prices, slowPeriod);

            var macdLine = new List<decimal?>();
            for (int i = 0; i < prices.Count; i++)
            {
                if (fastEma[i].HasValue && slowEma[i].HasValue)
                    macdLine.Add(fastEma[i].Value - slowEma[i].Value);
                else
                    macdLine.Add(null);
            }

            var macdValues = macdLine.Where(m => m.HasValue).Select(m => m.Value).ToList();
            var signalEma = CalculateEMA(macdValues, signalPeriod);

            var signalLine = new List<decimal?>();
            var histogram = new List<decimal?>();

            int macdIdx = 0;
            for (int i = 0; i < prices.Count; i++)
            {
                if (macdLine[i].HasValue)
                {
                    if (macdIdx < signalEma.Count && signalEma[macdIdx].HasValue)
                    {
                        signalLine.Add(signalEma[macdIdx]);
                        histogram.Add(macdLine[i].Value - signalEma[macdIdx].Value);
                    }
                    else
                    {
                        signalLine.Add(null);
                        histogram.Add(null);
                    }
                    macdIdx++;
                }
                else
                {
                    signalLine.Add(null);
                    histogram.Add(null);
                }
            }

            return new MACDResult
            {
                MACDLine = macdLine,
                SignalLine = signalLine,
                Histogram = histogram
            };
        }

        // -------------------------------------------------------
        // RSI - Relative Strength Index
        // -------------------------------------------------------
        public static List<decimal?> CalculateRSI(List<decimal> prices, int period = 14)
        {
            var result = new List<decimal?>();
            if (period <= 0) return result;
            var gains = new List<decimal>();
            var losses = new List<decimal>();

            for (int i = 1; i < prices.Count; i++)
            {
                var change = prices[i] - prices[i - 1];
                gains.Add(change > 0 ? change : 0);
                losses.Add(change < 0 ? Math.Abs(change) : 0);
            }

            result.Add(null);

            decimal avgGain = 0;
            decimal avgLoss = 0;

            for (int i = 0; i < gains.Count; i++)
            {
                if (i < period - 1)
                {
                    result.Add(null);
                }
                else if (i == period - 1)
                {
                    avgGain = gains.Take(period).Average();
                    avgLoss = losses.Take(period).Average();

                    if (avgLoss == 0)
                        result.Add(100);
                    else
                    {
                        var rs = avgGain / avgLoss;
                        result.Add(100 - (100 / (1 + rs)));
                    }
                }
                else
                {
                    avgGain = (avgGain * (period - 1) + gains[i]) / period;
                    avgLoss = (avgLoss * (period - 1) + losses[i]) / period;

                    if (avgLoss == 0)
                        result.Add(100);
                    else
                    {
                        var rs = avgGain / avgLoss;
                        result.Add(100 - (100 / (1 + rs)));
                    }
                }
            }

            return result;
        }

        // -------------------------------------------------------
        // Bollinger Bands
        // -------------------------------------------------------
        public static BollingerBandsResult CalculateBollingerBands(List<decimal> prices, int period = 20, decimal stdDevMultiplier = 2.0m)
        {
            if (period <= 0) return new BollingerBandsResult();

            var sma = CalculateSMA(prices, period);
            var upperBand = new List<decimal?>();
            var lowerBand = new List<decimal?>();
            var bandwidth = new List<decimal?>();
            var percentB = new List<decimal?>();

            for (int i = 0; i < prices.Count; i++)
            {
                if (i < period - 1 || !sma[i].HasValue)
                {
                    upperBand.Add(null);
                    lowerBand.Add(null);
                    bandwidth.Add(null);
                    percentB.Add(null);
                }
                else
                {
                    var sum = 0m;
                    for (int j = 0; j < period; j++)
                    {
                        var diff = prices[i - j] - sma[i].Value;
                        sum += diff * diff;
                    }
                    var stdDev = (decimal)Math.Sqrt((double)(sum / period));

                    var upper = sma[i].Value + (stdDevMultiplier * stdDev);
                    var lower = sma[i].Value - (stdDevMultiplier * stdDev);

                    upperBand.Add(upper);
                    lowerBand.Add(lower);
                    bandwidth.Add(sma[i].Value != 0 ? (upper - lower) / sma[i].Value * 100 : 0);

                    if (upper != lower)
                        percentB.Add((prices[i] - lower) / (upper - lower));
                    else
                        percentB.Add(0.5m);
                }
            }

            return new BollingerBandsResult
            {
                MiddleBand = sma,
                UpperBand = upperBand,
                LowerBand = lowerBand,
                Bandwidth = bandwidth,
                PercentB = percentB
            };
        }

        // -------------------------------------------------------
        // ATR - Average True Range
        // -------------------------------------------------------
        public static List<decimal?> CalculateATR(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period = 14)
        {
            var trueRanges = new List<decimal>();

            for (int i = 0; i < closes.Count; i++)
            {
                if (i == 0)
                {
                    trueRanges.Add(highs[i] - lows[i]);
                }
                else
                {
                    var tr1 = highs[i] - lows[i];
                    var tr2 = Math.Abs(highs[i] - closes[i - 1]);
                    var tr3 = Math.Abs(lows[i] - closes[i - 1]);
                    trueRanges.Add(Math.Max(tr1, Math.Max(tr2, tr3)));
                }
            }

            return CalculateEMA(trueRanges, period);
        }

        // -------------------------------------------------------
        // VWAP - Volume Weighted Average Price
        // -------------------------------------------------------
        public static List<decimal?> CalculateVWAP(List<decimal> highs, List<decimal> lows, List<decimal> closes, List<decimal> volumes)
        {
            var result = new List<decimal?>();
            decimal cumulativeTPV = 0;
            decimal cumulativeVolume = 0;

            for (int i = 0; i < closes.Count; i++)
            {
                var typicalPrice = (highs[i] + lows[i] + closes[i]) / 3;
                cumulativeTPV += typicalPrice * volumes[i];
                cumulativeVolume += volumes[i];

                if (cumulativeVolume > 0)
                    result.Add(cumulativeTPV / cumulativeVolume);
                else
                    result.Add(null);
            }

            return result;
        }

        // -------------------------------------------------------
        // OBV - On Balance Volume
        // -------------------------------------------------------
        public static List<decimal?> CalculateOBV(List<decimal> closes, List<decimal> volumes)
        {
            var result = new List<decimal?>();
            decimal obv = 0;

            for (int i = 0; i < closes.Count; i++)
            {
                if (i == 0)
                    obv = volumes[i];
                else if (closes[i] > closes[i - 1])
                    obv += volumes[i];
                else if (closes[i] < closes[i - 1])
                    obv -= volumes[i];

                result.Add(obv);
            }

            return result;
        }

        // -------------------------------------------------------
        // Stochastic Oscillator
        // -------------------------------------------------------
        public static StochasticResult CalculateStochastic(List<decimal> highs, List<decimal> lows, List<decimal> closes, int kPeriod = 14, int dPeriod = 3)
        {
            if (kPeriod <= 0 || dPeriod <= 0)
                return new StochasticResult();

            var kValues = new List<decimal?>();

            for (int i = 0; i < closes.Count; i++)
            {
                if (i < kPeriod - 1)
                {
                    kValues.Add(null);
                }
                else
                {
                    var highestHigh = 0m;
                    var lowestLow = decimal.MaxValue;

                    for (int j = 0; j < kPeriod; j++)
                    {
                        highestHigh = Math.Max(highestHigh, highs[i - j]);
                        lowestLow = Math.Min(lowestLow, lows[i - j]);
                    }

                    if (highestHigh != lowestLow)
                        kValues.Add((closes[i] - lowestLow) / (highestHigh - lowestLow) * 100);
                    else
                        kValues.Add(50);
                }
            }

            var dValues = new List<decimal?>();
            for (int i = 0; i < kValues.Count; i++)
            {
                if (i < kPeriod + dPeriod - 2 || !kValues[i].HasValue)
                {
                    dValues.Add(null);
                }
                else
                {
                    var sum = 0m;
                    var count = 0;
                    for (int j = 0; j < dPeriod; j++)
                    {
                        if (kValues[i - j].HasValue)
                        {
                            sum += kValues[i - j].Value;
                            count++;
                        }
                    }
                    dValues.Add(count > 0 ? sum / count : (decimal?)null);
                }
            }

            return new StochasticResult
            {
                PercentK = kValues,
                PercentD = dValues
            };
        }

        // -------------------------------------------------------
        // CCI - Commodity Channel Index
        // -------------------------------------------------------
        public static List<decimal?> CalculateCCI(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period = 20)
        {
            var result = new List<decimal?>();
            if (period <= 0) return result;

            var typicalPrices = new List<decimal>();
            for (int i = 0; i < closes.Count; i++)
            {
                typicalPrices.Add((highs[i] + lows[i] + closes[i]) / 3);
            }

            var sma = CalculateSMA(typicalPrices, period);

            for (int i = 0; i < closes.Count; i++)
            {
                if (i < period - 1 || !sma[i].HasValue)
                {
                    result.Add(null);
                }
                else
                {
                    var sum = 0m;
                    for (int j = 0; j < period; j++)
                    {
                        sum += Math.Abs(typicalPrices[i - j] - sma[i].Value);
                    }
                    var meanDeviation = sum / period;

                    if (meanDeviation != 0)
                        result.Add((typicalPrices[i] - sma[i].Value) / (0.015m * meanDeviation));
                    else
                        result.Add(0);
                }
            }

            return result;
        }

        // -------------------------------------------------------
        // Williams %R
        // -------------------------------------------------------
        public static List<decimal?> CalculateWilliamsR(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period = 14)
        {
            var result = new List<decimal?>();
            if (period <= 0) return result;

            for (int i = 0; i < closes.Count; i++)
            {
                if (i < period - 1)
                {
                    result.Add(null);
                }
                else
                {
                    var highestHigh = 0m;
                    var lowestLow = decimal.MaxValue;

                    for (int j = 0; j < period; j++)
                    {
                        highestHigh = Math.Max(highestHigh, highs[i - j]);
                        lowestLow = Math.Min(lowestLow, lows[i - j]);
                    }

                    if (highestHigh != lowestLow)
                        result.Add(-100 * (highestHigh - closes[i]) / (highestHigh - lowestLow));
                    else
                        result.Add(-50);
                }
            }

            return result;
        }

        // -------------------------------------------------------
        // MFI - Money Flow Index
        // -------------------------------------------------------
        public static List<decimal?> CalculateMFI(List<decimal> highs, List<decimal> lows, List<decimal> closes, List<decimal> volumes, int period = 14)
        {
            var result = new List<decimal?>();
            if (period <= 0) return result;

            var typicalPrices = new List<decimal>();
            var rawMoneyFlow = new List<decimal>();

            for (int i = 0; i < closes.Count; i++)
            {
                var tp = (highs[i] + lows[i] + closes[i]) / 3;
                typicalPrices.Add(tp);
                rawMoneyFlow.Add(tp * volumes[i]);
            }

            result.Add(null);

            for (int i = 1; i < closes.Count; i++)
            {
                if (i < period)
                {
                    result.Add(null);
                }
                else
                {
                    decimal positiveFlow = 0;
                    decimal negativeFlow = 0;

                    for (int j = i - period + 1; j <= i; j++)
                    {
                        if (typicalPrices[j] > typicalPrices[j - 1])
                            positiveFlow += rawMoneyFlow[j];
                        else if (typicalPrices[j] < typicalPrices[j - 1])
                            negativeFlow += rawMoneyFlow[j];
                    }

                    if (negativeFlow == 0)
                        result.Add(100);
                    else
                    {
                        var mfRatio = positiveFlow / negativeFlow;
                        result.Add(100 - (100 / (1 + mfRatio)));
                    }
                }
            }

            return result;
        }

        // -------------------------------------------------------
        // Ichimoku Cloud
        // -------------------------------------------------------
        public static IchimokuResult CalculateIchimoku(List<decimal> highs, List<decimal> lows, List<decimal> closes)
        {
            const int tenkanPeriod = 9;
            const int kijunPeriod = 26;
            const int senkouBPeriod = 52;
            const int displacement = 26;

            var tenkanSen = new List<decimal?>();
            var kijunSen = new List<decimal?>();
            var senkouSpanA = new List<decimal?>();
            var senkouSpanB = new List<decimal?>();
            var chikouSpan = new List<decimal?>();

            for (int i = 0; i < closes.Count; i++)
            {
                if (i >= tenkanPeriod - 1)
                {
                    var high = CalcHelpers.GetMaxInWindow(highs, i - tenkanPeriod + 1, tenkanPeriod);
                    var low = CalcHelpers.GetMinInWindow(lows, i - tenkanPeriod + 1, tenkanPeriod);
                    tenkanSen.Add((high + low) / 2);
                }
                else
                {
                    tenkanSen.Add(null);
                }

                if (i >= kijunPeriod - 1)
                {
                    var high = CalcHelpers.GetMaxInWindow(highs, i - kijunPeriod + 1, kijunPeriod);
                    var low = CalcHelpers.GetMinInWindow(lows, i - kijunPeriod + 1, kijunPeriod);
                    kijunSen.Add((high + low) / 2);
                }
                else
                {
                    kijunSen.Add(null);
                }

                chikouSpan.Add(closes[i]);
            }

            for (int i = 0; i < closes.Count; i++)
            {
                if (tenkanSen[i].HasValue && kijunSen[i].HasValue)
                    senkouSpanA.Add((tenkanSen[i].Value + kijunSen[i].Value) / 2);
                else
                    senkouSpanA.Add(null);
            }

            for (int i = 0; i < closes.Count; i++)
            {
                if (i >= senkouBPeriod - 1)
                {
                    var high = CalcHelpers.GetMaxInWindow(highs, i - senkouBPeriod + 1, senkouBPeriod);
                    var low = CalcHelpers.GetMinInWindow(lows, i - senkouBPeriod + 1, senkouBPeriod);
                    senkouSpanB.Add((high + low) / 2);
                }
                else
                {
                    senkouSpanB.Add(null);
                }
            }

            return new IchimokuResult
            {
                TenkanSen = tenkanSen,
                KijunSen = kijunSen,
                SenkouSpanA = senkouSpanA,
                SenkouSpanB = senkouSpanB,
                ChikouSpan = chikouSpan,
                Displacement = displacement
            };
        }

        // -------------------------------------------------------
        // Parabolic SAR
        // -------------------------------------------------------
        public static List<decimal?> CalculateParabolicSAR(List<decimal> highs, List<decimal> lows, decimal accelerationStart = 0.02m, decimal accelerationMax = 0.2m)
        {
            var result = new List<decimal?>();

            if (highs.Count < 2)
            {
                return highs.Select(_ => (decimal?)null).ToList();
            }

            bool isUptrend = highs[1] > highs[0];
            decimal sar = isUptrend ? lows[0] : highs[0];
            decimal ep = isUptrend ? highs[0] : lows[0];
            decimal af = accelerationStart;

            result.Add(null);

            for (int i = 1; i < highs.Count; i++)
            {
                sar = sar + af * (ep - sar);

                if (isUptrend)
                {
                    sar = Math.Min(sar, lows[i - 1]);
                    if (i > 1) sar = Math.Min(sar, lows[i - 2]);
                }
                else
                {
                    sar = Math.Max(sar, highs[i - 1]);
                    if (i > 1) sar = Math.Max(sar, highs[i - 2]);
                }

                bool reverse = false;

                if (isUptrend)
                {
                    if (lows[i] < sar)
                    {
                        reverse = true;
                        sar = ep;
                        ep = lows[i];
                        af = accelerationStart;
                    }
                }
                else
                {
                    if (highs[i] > sar)
                    {
                        reverse = true;
                        sar = ep;
                        ep = highs[i];
                        af = accelerationStart;
                    }
                }

                if (reverse)
                {
                    isUptrend = !isUptrend;
                }
                else
                {
                    if (isUptrend)
                    {
                        if (highs[i] > ep)
                        {
                            ep = highs[i];
                            af = Math.Min(af + accelerationStart, accelerationMax);
                        }
                    }
                    else
                    {
                        if (lows[i] < ep)
                        {
                            ep = lows[i];
                            af = Math.Min(af + accelerationStart, accelerationMax);
                        }
                    }
                }

                result.Add(sar);
            }

            return result;
        }

        // -------------------------------------------------------
        // ADX with +DI and -DI
        // -------------------------------------------------------
        public static ADXResult CalculateADXWithDI(List<decimal> highs, List<decimal> lows, List<decimal> closes, int period = 14)
        {
            if (period <= 0) return new ADXResult();

            var plusDI = new List<decimal?>();
            var minusDI = new List<decimal?>();
            var adx = new List<decimal?>();

            var tr = new List<decimal>();
            var plusDM = new List<decimal>();
            var minusDM = new List<decimal>();

            var runningTR = 0m;
            var runningPlusDM = 0m;
            var runningMinusDM = 0m;

            for (int i = 0; i < closes.Count; i++)
            {
                if (i == 0)
                {
                    var trVal = highs[i] - lows[i];
                    tr.Add(trVal);
                    plusDM.Add(0);
                    minusDM.Add(0);
                    runningTR += trVal;
                    plusDI.Add(null);
                    minusDI.Add(null);
                    adx.Add(null);
                }
                else
                {
                    var tr1 = highs[i] - lows[i];
                    var tr2 = Math.Abs(highs[i] - closes[i - 1]);
                    var tr3 = Math.Abs(lows[i] - closes[i - 1]);
                    var trVal = Math.Max(tr1, Math.Max(tr2, tr3));
                    tr.Add(trVal);

                    var upMove = highs[i] - highs[i - 1];
                    var downMove = lows[i - 1] - lows[i];

                    var pdm = upMove > downMove && upMove > 0 ? upMove : 0;
                    var mdm = downMove > upMove && downMove > 0 ? downMove : 0;
                    plusDM.Add(pdm);
                    minusDM.Add(mdm);

                    runningTR += trVal;
                    runningPlusDM += pdm;
                    runningMinusDM += mdm;

                    if (i >= period)
                    {
                        runningTR -= tr[i - period];
                        runningPlusDM -= plusDM[i - period];
                        runningMinusDM -= minusDM[i - period];
                    }

                    if (i < period)
                    {
                        plusDI.Add(null);
                        minusDI.Add(null);
                        adx.Add(null);
                    }
                    else
                    {
                        var pdi = runningTR > 0 ? 100 * runningPlusDM / runningTR : 0;
                        var mdi = runningTR > 0 ? 100 * runningMinusDM / runningTR : 0;

                        plusDI.Add(pdi);
                        minusDI.Add(mdi);

                        if (i >= period * 2 - 1)
                        {
                            var dxSum = 0m;
                            var dxCount = 0;
                            for (int j = Math.Max(period, i - period + 1); j <= i; j++)
                            {
                                if (plusDI[j].HasValue && minusDI[j].HasValue)
                                {
                                    var s = plusDI[j].Value + minusDI[j].Value;
                                    if (s > 0)
                                    {
                                        dxSum += 100 * Math.Abs(plusDI[j].Value - minusDI[j].Value) / s;
                                        dxCount++;
                                    }
                                }
                            }
                            adx.Add(dxCount > 0 ? dxSum / dxCount : (decimal?)null);
                        }
                        else
                        {
                            adx.Add(null);
                        }
                    }
                }
            }

            return new ADXResult
            {
                ADX = adx,
                PlusDI = plusDI,
                MinusDI = minusDI
            };
        }

        // -------------------------------------------------------
        // ROC - Rate of Change
        // -------------------------------------------------------
        public static List<decimal?> CalculateROC(List<decimal> prices, int period = 12)
        {
            var result = new List<decimal?>();

            for (int i = 0; i < prices.Count; i++)
            {
                if (i < period)
                    result.Add(null);
                else
                {
                    if (prices[i - period] != 0)
                        result.Add((prices[i] - prices[i - period]) / prices[i - period] * 100);
                    else
                        result.Add(null);
                }
            }

            return result;
        }

        // -------------------------------------------------------
        // Momentum
        // -------------------------------------------------------
        public static List<decimal?> CalculateMomentum(List<decimal> prices, int period = 10)
        {
            var result = new List<decimal?>();

            for (int i = 0; i < prices.Count; i++)
            {
                if (i < period)
                    result.Add(null);
                else
                    result.Add(prices[i] - prices[i - period]);
            }

            return result;
        }

        // -------------------------------------------------------
        // Keltner Channels
        // -------------------------------------------------------
        public static KeltnerChannelResult CalculateKeltnerChannels(List<decimal> highs, List<decimal> lows, List<decimal> closes, int emaPeriod = 20, int atrPeriod = 10, decimal multiplier = 2.0m)
        {
            var ema = CalculateEMA(closes, emaPeriod);
            var atr = CalculateATR(highs, lows, closes, atrPeriod);

            var upper = new List<decimal?>();
            var lower = new List<decimal?>();

            for (int i = 0; i < closes.Count; i++)
            {
                if (ema[i].HasValue && atr[i].HasValue)
                {
                    upper.Add(ema[i].Value + (multiplier * atr[i].Value));
                    lower.Add(ema[i].Value - (multiplier * atr[i].Value));
                }
                else
                {
                    upper.Add(null);
                    lower.Add(null);
                }
            }

            return new KeltnerChannelResult
            {
                MiddleLine = ema,
                UpperChannel = upper,
                LowerChannel = lower
            };
        }

        // -------------------------------------------------------
        // Chaikin Money Flow (CMF)
        // -------------------------------------------------------
        public static List<decimal?> CalculateChaikinMoneyFlow(List<decimal> highs, List<decimal> lows, List<decimal> closes, List<decimal> volumes, int period = 20)
        {
            var result = new List<decimal?>();
            var mfv = new List<decimal>();

            for (int i = 0; i < closes.Count; i++)
            {
                var range = highs[i] - lows[i];
                if (range > 0)
                {
                    var mfMultiplier = ((closes[i] - lows[i]) - (highs[i] - closes[i])) / range;
                    mfv.Add(mfMultiplier * volumes[i]);
                }
                else
                {
                    mfv.Add(0);
                }
            }

            var runningSumMFV = 0m;
            var runningSumVolume = 0m;

            for (int i = 0; i < closes.Count; i++)
            {
                runningSumMFV += mfv[i];
                runningSumVolume += volumes[i];

                if (i < period - 1)
                {
                    result.Add(null);
                }
                else
                {
                    if (i >= period)
                    {
                        runningSumMFV -= mfv[i - period];
                        runningSumVolume -= volumes[i - period];
                    }

                    if (runningSumVolume > 0)
                        result.Add(runningSumMFV / runningSumVolume);
                    else
                        result.Add(0);
                }
            }

            return result;
        }

        // -------------------------------------------------------
        // ADL - Accumulation/Distribution Line
        // -------------------------------------------------------
        public static List<decimal?> CalculateADL(List<decimal> highs, List<decimal> lows, List<decimal> closes, List<decimal> volumes)
        {
            var result = new List<decimal?>();
            if (closes.Count == 0) return result;

            decimal adl = 0;

            for (int i = 0; i < closes.Count; i++)
            {
                var range = highs[i] - lows[i];
                if (range > 0)
                {
                    var mfMultiplier = ((closes[i] - lows[i]) - (highs[i] - closes[i])) / range;
                    var mfVolume = mfMultiplier * volumes[i];
                    adl += mfVolume;
                }

                result.Add(adl);
            }

            return result;
        }

        // -------------------------------------------------------
        // Aroon with Up/Down and Oscillator
        // -------------------------------------------------------
        public static AroonResult CalculateAroonWithOscillator(List<decimal> highs, List<decimal> lows, int period = 25)
        {
            if (period <= 0) return new AroonResult();

            var aroonUp = new List<decimal?>();
            var aroonDown = new List<decimal?>();
            var oscillator = new List<decimal?>();

            for (int i = 0; i < highs.Count; i++)
            {
                if (i < period)
                {
                    aroonUp.Add(null);
                    aroonDown.Add(null);
                    oscillator.Add(null);
                }
                else
                {
                    var highestIdx = 0;
                    var lowestIdx = 0;
                    var highestVal = decimal.MinValue;
                    var lowestVal = decimal.MaxValue;

                    for (int j = 0; j <= period; j++)
                    {
                        if (highs[i - j] > highestVal)
                        {
                            highestVal = highs[i - j];
                            highestIdx = j;
                        }
                        if (lows[i - j] < lowestVal)
                        {
                            lowestVal = lows[i - j];
                            lowestIdx = j;
                        }
                    }

                    var up = 100m * (period - highestIdx) / period;
                    var down = 100m * (period - lowestIdx) / period;

                    aroonUp.Add(up);
                    aroonDown.Add(down);
                    oscillator.Add(up - down);
                }
            }

            return new AroonResult
            {
                AroonUp = aroonUp,
                AroonDown = aroonDown,
                Oscillator = oscillator
            };
        }
    }
}
