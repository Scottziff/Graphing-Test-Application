using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;

namespace Graphing_Test_Application
{
    // ============================================================
    // Enums
    // ============================================================
    public enum IndicatorCategory { Trend, Momentum, Volatility, Volume }

    public enum ChartAreaType
    {
        PriceOverlay,   // Drawn on MainArea (same Y-axis as candlesticks)
        SeparatePane    // Gets its own ChartArea below the main chart
    }

    // ============================================================
    // IndicatorSeriesInfo - describes one chart series for an indicator
    // ============================================================
    public class IndicatorSeriesInfo
    {
        public string SeriesName { get; set; }
        public Color Color { get; set; }
        public SeriesChartType ChartType { get; set; }
        public int BorderWidth { get; set; }
        public ChartDashStyle DashStyle { get; set; }
        public int MarkerSize { get; set; }

        public IndicatorSeriesInfo(string name, Color color,
            SeriesChartType chartType = SeriesChartType.Line,
            int borderWidth = 2,
            ChartDashStyle dashStyle = ChartDashStyle.Solid,
            int markerSize = 0)
        {
            SeriesName = name;
            Color = color;
            ChartType = chartType;
            BorderWidth = borderWidth;
            DashStyle = dashStyle;
            MarkerSize = markerSize;
        }
    }

    // ============================================================
    // IndicatorDefinition - full metadata for one indicator
    // ============================================================
    public class IndicatorDefinition
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public IndicatorCategory Category { get; set; }
        public decimal Weight { get; set; }
        public ChartAreaType AreaType { get; set; }
        public string ChartAreaName { get; set; }
        public double? YAxisMin { get; set; }
        public double? YAxisMax { get; set; }
        public List<IndicatorSeriesInfo> SeriesList { get; set; }

        /// <summary>Reference line Y-values (e.g. RSI 30/70, CCI +/-100)</summary>
        public List<double> ReferenceLines { get; set; }
    }

    // ============================================================
    // IndicatorRegistry - all 21 indicator definitions
    // ============================================================
    public static class IndicatorRegistry
    {
        public static List<IndicatorDefinition> GetAll()
        {
            return new List<IndicatorDefinition>
            {
                // ====================
                // TREND (35%)
                // ====================
                new IndicatorDefinition
                {
                    Key = "SMA", DisplayName = "SMA (20-day)",
                    Description = "Simple Moving Average: averages the closing prices over 20 days to smooth price action and identify trend direction.",
                    Category = IndicatorCategory.Trend, Weight = 0.35m,
                    AreaType = ChartAreaType.PriceOverlay, ChartAreaName = "MainArea",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("SMA_20", Color.DodgerBlue)
                    }
                },
                new IndicatorDefinition
                {
                    Key = "EMA", DisplayName = "EMA (12-day)",
                    Description = "Exponential Moving Average: weights recent prices more heavily than SMA, reacting faster to price changes over 12 days.",
                    Category = IndicatorCategory.Trend, Weight = 0.35m,
                    AreaType = ChartAreaType.PriceOverlay, ChartAreaName = "MainArea",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("EMA_12", Color.Orange)
                    }
                },
                new IndicatorDefinition
                {
                    Key = "MACD", DisplayName = "MACD (12,26,9)",
                    Description = "Moving Average Convergence Divergence: shows the relationship between two EMAs (12 & 26). Signal line crossovers indicate momentum shifts.",
                    Category = IndicatorCategory.Trend, Weight = 0.35m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "MACD_Area",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("MACD_Line", Color.RoyalBlue),
                        new IndicatorSeriesInfo("MACD_Signal", Color.Crimson, borderWidth: 1),
                        new IndicatorSeriesInfo("MACD_Histogram", Color.DarkGray, SeriesChartType.Column, borderWidth: 1)
                    },
                    ReferenceLines = new List<double> { 0 }
                },
                new IndicatorDefinition
                {
                    Key = "ADX", DisplayName = "ADX (14-day)",
                    Description = "Average Directional Index: measures trend strength (not direction). Above 25 = strong trend, below 20 = weak/no trend. +DI/-DI show direction.",
                    Category = IndicatorCategory.Trend, Weight = 0.35m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "ADX_Area",
                    YAxisMin = 0, YAxisMax = 100,
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("ADX_Line", Color.DarkViolet),
                        new IndicatorSeriesInfo("ADX_PlusDI", Color.ForestGreen, borderWidth: 1, dashStyle: ChartDashStyle.Dash),
                        new IndicatorSeriesInfo("ADX_MinusDI", Color.Crimson, borderWidth: 1, dashStyle: ChartDashStyle.Dash)
                    }
                },
                new IndicatorDefinition
                {
                    Key = "PSAR", DisplayName = "Parabolic SAR",
                    Description = "Parabolic Stop and Reverse: dots above price = downtrend, dots below = uptrend. Useful for setting trailing stop-losses.",
                    Category = IndicatorCategory.Trend, Weight = 0.35m,
                    AreaType = ChartAreaType.PriceOverlay, ChartAreaName = "MainArea",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("PSAR_Dots", Color.Magenta, SeriesChartType.Point, borderWidth: 1, markerSize: 4)
                    }
                },
                new IndicatorDefinition
                {
                    Key = "ICHI", DisplayName = "Ichimoku Cloud",
                    Description = "Ichimoku Kinko Hyo: a comprehensive indicator showing support/resistance (cloud), trend direction (Tenkan/Kijun), and momentum (Chikou).",
                    Category = IndicatorCategory.Trend, Weight = 0.35m,
                    AreaType = ChartAreaType.PriceOverlay, ChartAreaName = "MainArea",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("ICHI_Tenkan", Color.Crimson, borderWidth: 1),
                        new IndicatorSeriesInfo("ICHI_Kijun", Color.Navy, borderWidth: 1),
                        new IndicatorSeriesInfo("ICHI_SpanA", Color.LimeGreen, borderWidth: 1),
                        new IndicatorSeriesInfo("ICHI_SpanB", Color.Tomato, borderWidth: 1),
                        new IndicatorSeriesInfo("ICHI_Chikou", Color.DimGray, borderWidth: 1, dashStyle: ChartDashStyle.Dash)
                    }
                },

                // ====================
                // MOMENTUM (25%)
                // ====================
                new IndicatorDefinition
                {
                    Key = "RSI", DisplayName = "RSI (14-day)",
                    Description = "Relative Strength Index: oscillates 0-100 measuring speed of price changes. Above 70 = overbought, below 30 = oversold.",
                    Category = IndicatorCategory.Momentum, Weight = 0.25m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "RSI_Area",
                    YAxisMin = 0, YAxisMax = 100,
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("RSI_Line", Color.MediumPurple)
                    },
                    ReferenceLines = new List<double> { 30, 70 }
                },
                new IndicatorDefinition
                {
                    Key = "STOCH", DisplayName = "Stochastic (14,3)",
                    Description = "Stochastic Oscillator: compares closing price to its range over 14 days. %K/%D crossovers signal reversals. Above 80 = overbought, below 20 = oversold.",
                    Category = IndicatorCategory.Momentum, Weight = 0.25m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "Stochastic_Area",
                    YAxisMin = 0, YAxisMax = 100,
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("STOCH_K", Color.RoyalBlue),
                        new IndicatorSeriesInfo("STOCH_D", Color.OrangeRed, borderWidth: 1, dashStyle: ChartDashStyle.Dash)
                    },
                    ReferenceLines = new List<double> { 20, 80 }
                },
                new IndicatorDefinition
                {
                    Key = "MOM", DisplayName = "Momentum (10-day)",
                    Description = "Momentum: measures the rate of price change over 10 days. Positive = upward momentum, negative = downward. Zero-line crossovers signal shifts.",
                    Category = IndicatorCategory.Momentum, Weight = 0.25m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "Momentum_Area",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("MOM_Line", Color.Teal)
                    },
                    ReferenceLines = new List<double> { 0 }
                },
                new IndicatorDefinition
                {
                    Key = "ROC", DisplayName = "ROC (12-day)",
                    Description = "Rate of Change: percentage change in price over 12 days. Positive = price rising, negative = falling. Extreme values may signal reversals.",
                    Category = IndicatorCategory.Momentum, Weight = 0.25m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "ROC_Area",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("ROC_Line", Color.DarkCyan)
                    },
                    ReferenceLines = new List<double> { 0 }
                },
                new IndicatorDefinition
                {
                    Key = "CCI", DisplayName = "CCI (20-day)",
                    Description = "Commodity Channel Index: measures price deviation from its statistical mean. Above +100 = overbought, below -100 = oversold.",
                    Category = IndicatorCategory.Momentum, Weight = 0.25m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "CCI_Area",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("CCI_Line", Color.Chocolate)
                    },
                    ReferenceLines = new List<double> { -100, 100 }
                },
                new IndicatorDefinition
                {
                    Key = "WILLR", DisplayName = "Williams %R (14)",
                    Description = "Williams %R: oscillates -100 to 0 measuring overbought/oversold levels. Above -20 = overbought, below -80 = oversold.",
                    Category = IndicatorCategory.Momentum, Weight = 0.25m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "WilliamsR_Area",
                    YAxisMin = -100, YAxisMax = 0,
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("WILLR_Line", Color.DarkOliveGreen)
                    },
                    ReferenceLines = new List<double> { -80, -20 }
                },

                // ====================
                // VOLATILITY (15%)
                // ====================
                new IndicatorDefinition
                {
                    Key = "BB", DisplayName = "Bollinger Bands (20,2)",
                    Description = "Bollinger Bands: upper/lower bands at 2 standard deviations from 20-day SMA. Price near upper band = overbought, near lower = oversold. Band width shows volatility.",
                    Category = IndicatorCategory.Volatility, Weight = 0.15m,
                    AreaType = ChartAreaType.PriceOverlay, ChartAreaName = "MainArea",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("BB_Upper", Color.SteelBlue, borderWidth: 1, dashStyle: ChartDashStyle.Dash),
                        new IndicatorSeriesInfo("BB_Middle", Color.SteelBlue, borderWidth: 1),
                        new IndicatorSeriesInfo("BB_Lower", Color.SteelBlue, borderWidth: 1, dashStyle: ChartDashStyle.Dash)
                    }
                },
                new IndicatorDefinition
                {
                    Key = "KC", DisplayName = "Keltner Channels",
                    Description = "Keltner Channels: volatility envelope around EMA using ATR. Breakouts above/below channels signal strong momentum moves.",
                    Category = IndicatorCategory.Volatility, Weight = 0.15m,
                    AreaType = ChartAreaType.PriceOverlay, ChartAreaName = "MainArea",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("KC_Upper", Color.Sienna, borderWidth: 1, dashStyle: ChartDashStyle.Dash),
                        new IndicatorSeriesInfo("KC_Middle", Color.Sienna, borderWidth: 1),
                        new IndicatorSeriesInfo("KC_Lower", Color.Sienna, borderWidth: 1, dashStyle: ChartDashStyle.Dash)
                    }
                },
                new IndicatorDefinition
                {
                    Key = "ATR", DisplayName = "ATR (14-day)",
                    Description = "Average True Range: measures market volatility over 14 days. Higher values = more volatile. Does not indicate direction, only the degree of price movement.",
                    Category = IndicatorCategory.Volatility, Weight = 0.15m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "ATR_Area",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("ATR_Line", Color.IndianRed)
                    }
                },

                // ====================
                // VOLUME (25%)
                // ====================
                new IndicatorDefinition
                {
                    Key = "OBV", DisplayName = "OBV",
                    Description = "On-Balance Volume: cumulative volume that adds volume on up days and subtracts on down days. Rising OBV confirms uptrend, falling confirms downtrend.",
                    Category = IndicatorCategory.Volume, Weight = 0.25m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "OBV_Area",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("OBV_Line", Color.DarkSlateBlue)
                    }
                },
                new IndicatorDefinition
                {
                    Key = "MFI", DisplayName = "MFI (14-day)",
                    Description = "Money Flow Index: volume-weighted RSI measuring buying/selling pressure. Above 80 = overbought, below 20 = oversold.",
                    Category = IndicatorCategory.Volume, Weight = 0.25m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "MFI_Area",
                    YAxisMin = 0, YAxisMax = 100,
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("MFI_Line", Color.CadetBlue)
                    },
                    ReferenceLines = new List<double> { 20, 80 }
                },
                new IndicatorDefinition
                {
                    Key = "CMF", DisplayName = "CMF (20-day)",
                    Description = "Chaikin Money Flow: measures money flow volume over 20 days. Positive = buying pressure (accumulation), negative = selling pressure (distribution).",
                    Category = IndicatorCategory.Volume, Weight = 0.25m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "CMF_Area",
                    YAxisMin = -1, YAxisMax = 1,
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("CMF_Line", Color.DarkGoldenrod)
                    },
                    ReferenceLines = new List<double> { 0 }
                },
                new IndicatorDefinition
                {
                    Key = "ADL", DisplayName = "A/D Line",
                    Description = "Accumulation/Distribution Line: uses volume and price to assess whether a stock is being accumulated (bought) or distributed (sold).",
                    Category = IndicatorCategory.Volume, Weight = 0.25m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "ADL_Area",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("ADL_Line", Color.DarkSlateGray)
                    }
                },
                new IndicatorDefinition
                {
                    Key = "VWAP", DisplayName = "VWAP",
                    Description = "Volume Weighted Average Price: the average price weighted by volume. Price above VWAP = bullish, below = bearish. Used as a benchmark by institutions.",
                    Category = IndicatorCategory.Volume, Weight = 0.25m,
                    AreaType = ChartAreaType.PriceOverlay, ChartAreaName = "MainArea",
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("VWAP_Line", Color.Gold, borderWidth: 2, dashStyle: ChartDashStyle.Dash)
                    }
                },
                new IndicatorDefinition
                {
                    Key = "AROON", DisplayName = "Aroon (25-day)",
                    Description = "Aroon Indicator: Aroon Up/Down measure how recently the highest high and lowest low occurred over 25 days. Crossovers signal trend changes.",
                    Category = IndicatorCategory.Volume, Weight = 0.25m,
                    AreaType = ChartAreaType.SeparatePane, ChartAreaName = "Aroon_Area",
                    YAxisMin = 0, YAxisMax = 100,
                    SeriesList = new List<IndicatorSeriesInfo>
                    {
                        new IndicatorSeriesInfo("AROON_Up", Color.ForestGreen),
                        new IndicatorSeriesInfo("AROON_Down", Color.Crimson, borderWidth: 1)
                    }
                }
            };
        }
    }
}
