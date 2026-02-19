using System;
using System.Collections.Generic;

namespace Graphing_Test_Application
{
    // ============================================================
    // Simulation Strategy Enum (extensible for future strategies)
    // ============================================================
    public enum SimulationStrategy
    {
        Contrarian,
        IndicatorCollection,
        PivotPointTransaction
    }

    // ============================================================
    // SimulationTransaction - one row in the transaction table
    // ============================================================
    public class SimulationTransaction
    {
        public DateTime Date { get; set; }
        public string Action { get; set; }       // "BUY" or "SELL"
        public decimal Price { get; set; }
        public int SharesTraded { get; set; }     // always 100
        public int SharesHeld { get; set; }       // running total (can be negative for short)
        public decimal CashBalance { get; set; }  // running total
        public int BarIndex { get; set; }         // index into cached data for chart navigation
    }

    // ============================================================
    // SimulationResult - complete output of a simulation run
    // ============================================================
    public class SimulationResult
    {
        public List<SimulationTransaction> Transactions { get; set; } = new List<SimulationTransaction>();
        public decimal FinalCashBalance { get; set; }
        public int FinalSharesHeld { get; set; }
        public decimal FinalStockValue { get; set; }
        public decimal FinalPnL { get; set; }
        public string StartingPosition { get; set; }
        public string StrategyName { get; set; }
    }

    // ============================================================
    // TradingSimulationEngine - runs strategy simulations
    // ============================================================
    public static class TradingSimulationEngine
    {
        // The 6 key indicator keys for contrarian evaluation
        private static readonly string[] ContrarianIndicatorKeys =
            { "SMA", "EMA", "MACD", "ADX", "PSAR", "ICHI" };

        public static SimulationResult RunSimulation(
            List<decimal> opens, List<decimal> highs, List<decimal> lows,
            List<decimal> closes, List<decimal> volumes,
            List<DateTime> dates,
            SimulationStrategy strategy,
            bool startWithBuy,
            decimal pivotThresholdPercent,
            Dictionary<string, SignalType> buyAnalysis = null,
            Dictionary<string, SignalType> sellAnalysis = null,
            int sharesPerTransaction = 100,
            List<PivotPoint> pivotPoints = null)
        {
            switch (strategy)
            {
                case SimulationStrategy.Contrarian:
                    return RunContrarian(opens, highs, lows, closes, volumes, dates,
                        startWithBuy, pivotThresholdPercent, sharesPerTransaction);
                case SimulationStrategy.IndicatorCollection:
                    return RunIndicatorCollection(opens, highs, lows, closes, volumes, dates,
                        startWithBuy, pivotThresholdPercent, buyAnalysis, sellAnalysis, sharesPerTransaction);
                case SimulationStrategy.PivotPointTransaction:
                    return RunPivotPointTransaction(closes, dates, pivotPoints, sharesPerTransaction);
                default:
                    return new SimulationResult();
            }
        }

        private static SimulationResult RunContrarian(
            List<decimal> opens, List<decimal> highs, List<decimal> lows,
            List<decimal> closes, List<decimal> volumes,
            List<DateTime> dates,
            bool startWithBuy,
            decimal pivotThresholdPercent,
            int sharesPerTransaction)
        {
            var result = new SimulationResult();
            result.StrategyName = "Contrarian";
            result.StartingPosition = startWithBuy ? "Buy" : "Sell";

            if (closes.Count == 0) return result;

            decimal cash = 0m;
            int shares = 0;

            // Initial position at bar 0
            decimal initialPrice = closes[0];
            decimal lastTradePrice = initialPrice;
            string lastAction;

            if (startWithBuy)
            {
                // Initial buy: user puts up capital, starts with 100 shares and $0 cash
                shares = sharesPerTransaction;
                cash = 0m;
                lastAction = "BUY";
                result.Transactions.Add(new SimulationTransaction
                {
                    Date = dates[0],
                    Action = "BUY",
                    Price = initialPrice,
                    SharesTraded = sharesPerTransaction,
                    SharesHeld = shares,
                    CashBalance = cash,
                    BarIndex = 0
                });
            }
            else
            {
                // Initial sell: user starts short, 0 shares with proceeds as cash
                shares = 0;
                cash = sharesPerTransaction * initialPrice;
                lastAction = "SELL";
                result.Transactions.Add(new SimulationTransaction
                {
                    Date = dates[0],
                    Action = "SELL",
                    Price = initialPrice,
                    SharesTraded = sharesPerTransaction,
                    SharesHeld = shares,
                    CashBalance = cash,
                    BarIndex = 0
                });
            }

            // Walk bar by bar from index 1 to end
            for (int i = 1; i < closes.Count; i++)
            {
                var signals = IndicatorSignalEvaluator.EvaluateAllAtIndex(
                    opens, highs, lows, closes, volumes, i);

                int buyCount = 0, sellCount = 0;
                foreach (var key in ContrarianIndicatorKeys)
                {
                    if (signals.ContainsKey(key))
                    {
                        if (signals[key].Signal == SignalType.Buy) buyCount++;
                        else if (signals[key].Signal == SignalType.Sell) sellCount++;
                    }
                }

                decimal price = closes[i];

                // Check minimum price move from last trade (pivot threshold %)
                decimal priceChangePercent = lastTradePrice > 0
                    ? Math.Abs((price - lastTradePrice) / lastTradePrice) * 100
                    : 0;
                if (priceChangePercent < pivotThresholdPercent)
                    continue;

                // Contrarian: 5+ Buy signals (5 or 6 of 6) → contrarian SELL
                // Must alternate: only SELL if last action was BUY
                // Max 100 shares: only SELL if shares > -sharesPerTransaction
                if (buyCount > 4 && lastAction == "BUY" && shares > -sharesPerTransaction)
                {
                    cash += sharesPerTransaction * price;
                    shares -= sharesPerTransaction;
                    lastTradePrice = price;
                    lastAction = "SELL";
                    result.Transactions.Add(new SimulationTransaction
                    {
                        Date = dates[i],
                        Action = "SELL",
                        Price = price,
                        SharesTraded = sharesPerTransaction,
                        SharesHeld = shares,
                        CashBalance = cash,
                        BarIndex = i
                    });
                }
                // Contrarian: 5+ Sell signals (5 or 6 of 6) → contrarian BUY
                // Must alternate: only BUY if last action was SELL
                // Max 100 shares: only BUY if shares < sharesPerTransaction
                else if (sellCount > 4 && lastAction == "SELL" && shares < sharesPerTransaction)
                {
                    cash -= sharesPerTransaction * price;
                    shares += sharesPerTransaction;
                    lastTradePrice = price;
                    lastAction = "BUY";
                    result.Transactions.Add(new SimulationTransaction
                    {
                        Date = dates[i],
                        Action = "BUY",
                        Price = price,
                        SharesTraded = sharesPerTransaction,
                        SharesHeld = shares,
                        CashBalance = cash,
                        BarIndex = i
                    });
                }
            }

            // Compute final P&L
            decimal lastPrice = closes[closes.Count - 1];
            result.FinalCashBalance = cash;
            result.FinalSharesHeld = shares;
            result.FinalStockValue = shares * lastPrice;
            result.FinalPnL = cash + (shares * lastPrice);

            return result;
        }

        // ============================================================
        // Indicator Collection strategy:
        // Uses analysis circle colors as the required pattern.
        // At each bar, if every indicator's current signal matches
        // its buy-analysis circle color → execute BUY.
        // If every indicator's signal matches its sell-analysis circle → SELL.
        // Same rules: alternating, max 100 shares, pivot threshold.
        // ============================================================
        private static SimulationResult RunIndicatorCollection(
            List<decimal> opens, List<decimal> highs, List<decimal> lows,
            List<decimal> closes, List<decimal> volumes,
            List<DateTime> dates,
            bool startWithBuy,
            decimal pivotThresholdPercent,
            Dictionary<string, SignalType> buyAnalysis,
            Dictionary<string, SignalType> sellAnalysis,
            int sharesPerTransaction)
        {
            var result = new SimulationResult();
            result.StrategyName = "Indicator Collection";
            result.StartingPosition = startWithBuy ? "Buy" : "Sell";

            if (closes.Count == 0) return result;
            if (buyAnalysis == null || sellAnalysis == null || buyAnalysis.Count == 0)
                return result;

            decimal cash = 0m;
            int shares = 0;

            // Initial position at bar 0
            decimal initialPrice = closes[0];
            decimal lastTradePrice = initialPrice;
            string lastAction;

            if (startWithBuy)
            {
                shares = sharesPerTransaction;
                cash = 0m;
                lastAction = "BUY";
                result.Transactions.Add(new SimulationTransaction
                {
                    Date = dates[0],
                    Action = "BUY",
                    Price = initialPrice,
                    SharesTraded = sharesPerTransaction,
                    SharesHeld = shares,
                    CashBalance = cash,
                    BarIndex = 0
                });
            }
            else
            {
                shares = 0;
                cash = sharesPerTransaction * initialPrice;
                lastAction = "SELL";
                result.Transactions.Add(new SimulationTransaction
                {
                    Date = dates[0],
                    Action = "SELL",
                    Price = initialPrice,
                    SharesTraded = sharesPerTransaction,
                    SharesHeld = shares,
                    CashBalance = cash,
                    BarIndex = 0
                });
            }

            for (int i = 1; i < closes.Count; i++)
            {
                var signals = IndicatorSignalEvaluator.EvaluateAllAtIndex(
                    opens, highs, lows, closes, volumes, i);

                decimal price = closes[i];

                // Check minimum price move from last trade
                decimal priceChangePercent = lastTradePrice > 0
                    ? Math.Abs((price - lastTradePrice) / lastTradePrice) * 100
                    : 0;
                if (priceChangePercent < pivotThresholdPercent)
                    continue;

                // Check if all indicators match their buy-analysis circle color
                // If no buy pattern defined (empty dict), cannot match buy
                bool allMatchBuy = buyAnalysis.Count > 0;
                foreach (var kvp in buyAnalysis)
                {
                    if (!signals.ContainsKey(kvp.Key))
                    {
                        allMatchBuy = false;
                        break;
                    }
                    var sig = signals[kvp.Key];
                    // N/A signals don't match anything
                    if (sig.DisplayValue == "N/A")
                    {
                        allMatchBuy = false;
                        break;
                    }
                    if (sig.Signal != kvp.Value)
                    {
                        allMatchBuy = false;
                        break;
                    }
                }

                // Check if all indicators match their sell-analysis circle color
                // If no sell pattern defined (empty dict), cannot match sell
                bool allMatchSell = sellAnalysis.Count > 0;
                foreach (var kvp in sellAnalysis)
                {
                    if (!signals.ContainsKey(kvp.Key))
                    {
                        allMatchSell = false;
                        break;
                    }
                    var sig = signals[kvp.Key];
                    if (sig.DisplayValue == "N/A")
                    {
                        allMatchSell = false;
                        break;
                    }
                    if (sig.Signal != kvp.Value)
                    {
                        allMatchSell = false;
                        break;
                    }
                }

                // Signal-only mode: when only buy or only sell pattern defined,
                // show all matching dates without alternating trade constraints
                bool buyOnly = buyAnalysis.Count > 0 && sellAnalysis.Count == 0;
                bool sellOnly = sellAnalysis.Count > 0 && buyAnalysis.Count == 0;

                if (buyOnly && allMatchBuy)
                {
                    result.Transactions.Add(new SimulationTransaction
                    {
                        Date = dates[i],
                        Action = "BUY",
                        Price = price,
                        SharesTraded = 0,
                        SharesHeld = 0,
                        CashBalance = 0m,
                        BarIndex = i
                    });
                    lastTradePrice = price;
                }
                else if (sellOnly && allMatchSell)
                {
                    result.Transactions.Add(new SimulationTransaction
                    {
                        Date = dates[i],
                        Action = "SELL",
                        Price = price,
                        SharesTraded = 0,
                        SharesHeld = 0,
                        CashBalance = 0m,
                        BarIndex = i
                    });
                    lastTradePrice = price;
                }
                // Full trade mode: both buy and sell patterns defined
                else if (allMatchBuy && lastAction == "SELL" && shares < sharesPerTransaction)
                {
                    cash -= sharesPerTransaction * price;
                    shares += sharesPerTransaction;
                    lastTradePrice = price;
                    lastAction = "BUY";
                    result.Transactions.Add(new SimulationTransaction
                    {
                        Date = dates[i],
                        Action = "BUY",
                        Price = price,
                        SharesTraded = sharesPerTransaction,
                        SharesHeld = shares,
                        CashBalance = cash,
                        BarIndex = i
                    });
                }
                else if (allMatchSell && lastAction == "BUY" && shares > -sharesPerTransaction)
                {
                    cash += sharesPerTransaction * price;
                    shares -= sharesPerTransaction;
                    lastTradePrice = price;
                    lastAction = "SELL";
                    result.Transactions.Add(new SimulationTransaction
                    {
                        Date = dates[i],
                        Action = "SELL",
                        Price = price,
                        SharesTraded = sharesPerTransaction,
                        SharesHeld = shares,
                        CashBalance = cash,
                        BarIndex = i
                    });
                }
            }

            decimal lastPrice = closes[closes.Count - 1];
            result.FinalCashBalance = cash;
            result.FinalSharesHeld = shares;
            result.FinalStockValue = shares * lastPrice;
            result.FinalPnL = cash + (shares * lastPrice);

            return result;
        }

        // ============================================================
        // Pivot Point Transaction strategy:
        // Buy at troughs, sell at peaks using detected pivot points.
        // Alternates buy/sell, 100 shares per trade.
        // First trough → BUY, first peak → SELL (whichever comes first).
        // ============================================================
        private static SimulationResult RunPivotPointTransaction(
            List<decimal> closes,
            List<DateTime> dates,
            List<PivotPoint> pivotPoints,
            int sharesPerTransaction)
        {
            var result = new SimulationResult();
            result.StrategyName = "Pivot Point Transaction";
            result.StartingPosition = "Buy";

            if (closes.Count == 0 || pivotPoints == null || pivotPoints.Count == 0)
                return result;

            // Always start with a BUY at bar 0
            decimal initialPrice = closes[0];
            decimal cash = 0m;
            int shares = sharesPerTransaction;
            string lastAction = "BUY";

            result.Transactions.Add(new SimulationTransaction
            {
                Date = dates[0],
                Action = "BUY",
                Price = initialPrice,
                SharesTraded = sharesPerTransaction,
                SharesHeld = shares,
                CashBalance = cash,
                BarIndex = 0
            });

            foreach (var pivot in pivotPoints)
            {
                decimal price = pivot.Price;

                if (pivot.Type == PivotType.Trough)
                {
                    // Trough → BUY (only if last action was not BUY)
                    if (lastAction == "BUY") continue;

                    cash -= sharesPerTransaction * price;
                    shares += sharesPerTransaction;
                    lastAction = "BUY";

                    result.Transactions.Add(new SimulationTransaction
                    {
                        Date = pivot.Date,
                        Action = "BUY",
                        Price = price,
                        SharesTraded = sharesPerTransaction,
                        SharesHeld = shares,
                        CashBalance = cash,
                        BarIndex = pivot.BarIndex
                    });
                }
                else if (pivot.Type == PivotType.Peak)
                {
                    // Peak → SELL (only if last action was not SELL)
                    if (lastAction == "SELL") continue;

                    cash += sharesPerTransaction * price;
                    shares -= sharesPerTransaction;
                    lastAction = "SELL";

                    result.Transactions.Add(new SimulationTransaction
                    {
                        Date = pivot.Date,
                        Action = "SELL",
                        Price = price,
                        SharesTraded = sharesPerTransaction,
                        SharesHeld = shares,
                        CashBalance = cash,
                        BarIndex = pivot.BarIndex
                    });
                }
            }

            // Compute final P&L
            decimal lastPrice = closes[closes.Count - 1];
            result.FinalCashBalance = cash;
            result.FinalSharesHeld = shares;
            result.FinalStockValue = shares * lastPrice;
            result.FinalPnL = cash + (shares * lastPrice);

            return result;
        }
    }
}
