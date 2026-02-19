using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Graphing_Test_Application
{
    // ============================================================
    // EventFetcher — retrieves earnings, press releases, SEC filings
    // from FMP API and news from the local [News Flash] SQL table,
    // then correlates with detected price gaps.
    // ============================================================
    public class EventFetcher : IDisposable
    {
        private readonly HttpClient _http;

        public EventFetcher()
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(30);
        }

        // ── Earnings Calendar ───────────────────────────────────────
        public async Task<List<StockEvent>> FetchEarningsAsync(
            string symbol, DateTime from, DateTime to,
            string apiKey, FmpDebugForm dbg)
        {
            var events = new List<StockEvent>();
            string fromStr = from.ToString("yyyy-MM-dd");
            string toStr   = to.ToString("yyyy-MM-dd");

            string safeUrl = $"https://financialmodelingprep.com/stable/earnings-calendar" +
                             $"?symbol={symbol}&from={fromStr}&to={toStr}&apikey=***";
            string url     = $"https://financialmodelingprep.com/stable/earnings-calendar" +
                             $"?symbol={symbol}&from={fromStr}&to={toStr}&apikey={apiKey}";

            dbg.LogFmpRequest(safeUrl);
            try
            {
                var response = await _http.GetAsync(url);
                string json  = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    dbg.LogFmpError($"HTTP {(int)response.StatusCode}");
                    return events;
                }

                if (!json.TrimStart().StartsWith("[")) { dbg.LogFmpResponse(0, "", "", empty: true); return events; }

                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    string dateStr = el.GetProperty("date").GetString() ?? "";
                    if (!DateTime.TryParse(dateStr, out DateTime dt)) continue;

                    string eps         = SafeDecimalStr(el, "eps");
                    string epsEst      = SafeDecimalStr(el, "epsEstimated");
                    string rev         = SafeDecimalStr(el, "revenue");
                    string time        = SafeStr(el, "time");
                    string timeLabel   = time == "bmo" ? "BMO" : time == "amc" ? "AMC" : time.ToUpper();
                    string title       = $"Earnings [{timeLabel}]  EPS: {eps}  Est: {epsEst}";
                    string detail      = $"Earnings Report [{timeLabel}]\nEPS: {eps}  Est: {epsEst}\nRevenue: {rev}";

                    events.Add(new StockEvent
                    {
                        Date      = dt,
                        EventType = StockEventType.EarningsReport,
                        Title     = title,
                        Detail    = detail,
                        Source    = "FMP"
                    });
                }
                dbg.LogFmpResponse(events.Count, events.Count > 0 ? events[0].Date.ToString("yyyy-MM-dd") : "",
                                   events.Count > 0 ? events[events.Count - 1].Date.ToString("yyyy-MM-dd") : "");
            }
            catch (Exception ex) { dbg.LogFmpError($"Earnings: {ex.Message}"); }

            await Task.Delay(300);
            return events;
        }

        // ── Press Releases / News ───────────────────────────────────
        public async Task<List<StockEvent>> FetchPressReleasesAsync(
            string symbol, DateTime from, DateTime to,
            string apiKey, FmpDebugForm dbg)
        {
            var events = new List<StockEvent>();

            string safeUrl = $"https://financialmodelingprep.com/stable/news?tickers={symbol}&limit=200&apikey=***";
            string url     = $"https://financialmodelingprep.com/stable/news?tickers={symbol}&limit=200&apikey={apiKey}";

            dbg.LogFmpRequest(safeUrl);
            try
            {
                var response = await _http.GetAsync(url);
                string json  = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode) { dbg.LogFmpError($"HTTP {(int)response.StatusCode}"); return events; }
                if (!json.TrimStart().StartsWith("[")) { dbg.LogFmpResponse(0, "", "", empty: true); return events; }

                // Significant keywords that indicate material news
                var keywords = new[] { "earnings", "revenue", "guidance", "results", "quarterly",
                                       "annual", "dividend", "acquisition", "merger", "fda",
                                       "recall", "settlement", "lawsuit", "investigation", "upgrade",
                                       "downgrade", "analyst", "outlook" };

                int added = 0;
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    string publishedStr = SafeStr(el, "publishedDate");
                    if (!DateTime.TryParse(publishedStr, out DateTime dt)) continue;
                    if (dt.Date < from.Date || dt.Date > to.Date) continue;

                    string title  = SafeStr(el, "title");
                    string titleL = title.ToLower();

                    bool significant = false;
                    foreach (var kw in keywords)
                        if (titleL.Contains(kw)) { significant = true; break; }
                    if (!significant) continue;

                    string shortTitle = title.Length > 60 ? title.Substring(0, 57) + "..." : title;
                    events.Add(new StockEvent
                    {
                        Date      = dt,
                        EventType = StockEventType.PressRelease,
                        Title     = shortTitle,
                        Detail    = title,
                        Url       = SafeStr(el, "url"),
                        Source    = "FMP"
                    });
                    added++;
                }
                dbg.LogInfo($"  Press releases: {added} significant items in date range");
            }
            catch (Exception ex) { dbg.LogFmpError($"News: {ex.Message}"); }

            await Task.Delay(300);
            return events;
        }

        // ── SEC Filings ─────────────────────────────────────────────
        public async Task<List<StockEvent>> FetchSecFilingsAsync(
            string symbol, DateTime from, DateTime to,
            string apiKey, FmpDebugForm dbg)
        {
            var events = new List<StockEvent>();

            // Fetch 8-K (material events), 10-Q (quarterly), 10-K (annual)
            var types = new[] { "8-K", "10-Q", "10-K" };

            foreach (string fileType in types)
            {
                string safeUrl = $"https://financialmodelingprep.com/stable/sec-filings" +
                                 $"?symbol={symbol}&type={fileType}&limit=50&apikey=***";
                string url     = $"https://financialmodelingprep.com/stable/sec-filings" +
                                 $"?symbol={symbol}&type={fileType}&limit=50&apikey={apiKey}";

                dbg.LogFmpRequest(safeUrl);
                try
                {
                    var response = await _http.GetAsync(url);
                    string json  = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode) { dbg.LogFmpError($"HTTP {(int)response.StatusCode} for {fileType}"); continue; }
                    if (!json.TrimStart().StartsWith("[")) { dbg.LogFmpResponse(0, "", "", empty: true); continue; }

                    StockEventType evtType = fileType == "8-K"  ? StockEventType.SecFiling8K
                                          : fileType == "10-Q" ? StockEventType.SecFiling10Q
                                                                : StockEventType.SecFiling10K;
                    int added = 0;
                    using var doc = JsonDocument.Parse(json);
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        string dateStr = SafeStr(el, "date");
                        if (!DateTime.TryParse(dateStr, out DateTime dt)) continue;
                        if (dt.Date < from.Date || dt.Date > to.Date) continue;

                        string title = SafeStr(el, "title");
                        if (string.IsNullOrEmpty(title)) title = SafeStr(el, "description");
                        if (string.IsNullOrEmpty(title)) title = $"SEC {fileType} Filing";

                        string shortTitle = title.Length > 60 ? title.Substring(0, 57) + "..." : title;
                        events.Add(new StockEvent
                        {
                            Date      = dt,
                            EventType = evtType,
                            Title     = shortTitle,
                            Detail    = $"SEC {fileType}: {title}",
                            Url       = SafeStr(el, "link"),
                            Source    = "SEC"
                        });
                        added++;
                    }
                    dbg.LogInfo($"  SEC {fileType}: {added} filings in date range");
                }
                catch (Exception ex) { dbg.LogFmpError($"SEC {fileType}: {ex.Message}"); }

                await Task.Delay(300);
            }
            return events;
        }

        // ── News Flash (local SQL table) ────────────────────────────
        public async Task<List<StockEvent>> FetchNewsFlashAsync(
            string symbol, DateTime from, DateTime to,
            string connStr, FmpDebugForm dbg)
        {
            var events = new List<StockEvent>();

            string sql = @"
                SELECT [Id], [PublishedDate], [Title], [NewsText], [Site], [Url], [Flag1]
                FROM [News Flash]
                WHERE [Symbol] = @Sym
                  AND [PublishedDate] >= @From
                  AND [PublishedDate] <= @To
                ORDER BY [PublishedDate] ASC";

            dbg.LogSql($"SELECT from [News Flash] WHERE Symbol='{symbol}' between {from:yyyy-MM-dd} and {to:yyyy-MM-dd}");
            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Sym",  symbol);
                cmd.Parameters.AddWithValue("@From", from.Date);
                cmd.Parameters.AddWithValue("@To",   to.Date.AddDays(1).AddTicks(-1));
                cmd.CommandTimeout = 30;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    DateTime dt = reader.IsDBNull(1) ? DateTime.MinValue
                                : reader.GetDateTime(1);
                    if (dt == DateTime.MinValue) continue;

                    string title    = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    string newsText = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    string site     = reader.IsDBNull(4) ? "" : reader.GetString(4);
                    string url      = reader.IsDBNull(5) ? "" : reader.GetString(5);
                    string flag1    = reader.IsDBNull(6) ? "" : reader.GetString(6);

                    string shortTitle = title.Length > 60 ? title.Substring(0, 57) + "..." : title;
                    string detail     = string.IsNullOrEmpty(newsText)
                                        ? title
                                        : (newsText.Length > 200 ? newsText.Substring(0, 197) + "..." : newsText);
                    if (!string.IsNullOrEmpty(flag1)) detail += $"\n[{flag1}]";

                    events.Add(new StockEvent
                    {
                        Date      = dt,
                        EventType = StockEventType.NewsFlash,
                        Title     = shortTitle,
                        Detail    = detail,
                        Url       = url,
                        Source    = string.IsNullOrEmpty(site) ? "NewsFlash" : site
                    });
                }
                dbg.LogSqlOk($"[News Flash]: {events.Count} records found for {symbol}");
            }
            catch (Exception ex) { dbg.LogSqlError($"[News Flash] query failed: {ex.Message}"); }

            return events;
        }

        // ── Helpers ─────────────────────────────────────────────────
        private static string SafeStr(JsonElement el, string prop)
        {
            if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? "";
            return "";
        }

        private static string SafeDecimalStr(JsonElement el, string prop)
        {
            if (!el.TryGetProperty(prop, out var v)) return "N/A";
            if (v.ValueKind == JsonValueKind.Number)
            {
                if (v.TryGetDecimal(out decimal d)) return d.ToString("F2");
            }
            if (v.ValueKind == JsonValueKind.String)
            {
                string s = v.GetString() ?? "";
                if (decimal.TryParse(s, out decimal d2)) return d2.ToString("F2");
                return s;
            }
            return "N/A";
        }

        public void Dispose() { _http?.Dispose(); }
    }
}
