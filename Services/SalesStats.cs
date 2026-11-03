using StockControl.Data;
using StockControl.Enums;

namespace StockControl.Services
{
    public enum StatsRange
    {
        Last7Days,
        Last30Days,
        Last12Weeks,
        Last12Months
    }

    // One bar of a chart. Label can be empty (crowded axes show only some labels).
    public record ChartPoint(string Label, decimal Value, string Detail);

    public class SalesSummary
    {
        public StatsRange Range { get; init; }
        public DateTime From { get; init; }
        public DateTime To { get; init; }               // exclusive
        public decimal Net { get; init; }               // sales + debit notes - credit notes
        public int SalesCount { get; init; }
        public decimal AverageTicket { get; init; }
        public decimal PreviousNet { get; init; }
        public decimal? ChangePercent { get; init; }    // null when there is nothing to compare with
        public decimal InvoicedInArca { get; init; }
        public List<ChartPoint> Timeline { get; init; } = new();
        public List<ChartPoint> ByHour { get; init; } = new();
        public List<ChartPoint> ByWeekday { get; init; } = new();
    }

    // Pure calculations for the dashboard charts (no database, easy to test)
    public static class SalesStats
    {
        private static readonly string[] Days = { "dom", "lun", "mar", "mié", "jue", "vie", "sáb" };
        private static readonly string[] DayNames = { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };
        private static readonly string[] Months = { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };

        public static string Title(StatsRange range) => range switch
        {
            StatsRange.Last7Days => "Últimos 7 días",
            StatsRange.Last30Days => "Últimos 30 días",
            StatsRange.Last12Weeks => "Últimas 12 semanas",
            _ => "Últimos 12 meses",
        };

        // [from, to) of the range, ending today (included)
        public static (DateTime From, DateTime To) Period(StatsRange range, DateTime today)
        {
            today = today.Date;
            var to = today.AddDays(1);
            return range switch
            {
                StatsRange.Last7Days => (today.AddDays(-6), to),
                StatsRange.Last30Days => (today.AddDays(-29), to),
                StatsRange.Last12Weeks => (Monday(today).AddDays(-7 * 11), to),
                _ => (new DateTime(today.Year, today.Month, 1).AddMonths(-11), to),
            };
        }

        // The same stretch of time just before (to compare against)
        public static (DateTime From, DateTime To) PreviousPeriod(StatsRange range, DateTime today)
        {
            var (from, _) = Period(range, today);
            return range switch
            {
                StatsRange.Last7Days => (from.AddDays(-7), from),
                StatsRange.Last30Days => (from.AddDays(-30), from),
                StatsRange.Last12Weeks => (from.AddDays(-7 * 12), from),
                _ => (from.AddMonths(-12), from),
            };
        }

        public static decimal Signed(SaleRow r) => r.Operation == OperationType.CreditNote ? -r.Total : r.Total;

        public static SalesSummary Build(StatsRange range, DateTime today, IReadOnlyList<SaleRow> allRows, IReadOnlyList<SaleRow> previous)
        {
            var (from, to) = Period(range, today);
            var rows = allRows.Where(r => r.Date >= from && r.Date < to).ToList();

            var net = rows.Sum(Signed);
            var sales = rows.Where(r => r.Operation == OperationType.Sale).ToList();
            var previousNet = previous.Sum(Signed);

            return new SalesSummary
            {
                Range = range,
                From = from,
                To = to,
                Net = net,
                SalesCount = sales.Count,
                AverageTicket = sales.Count == 0 ? 0 : Math.Round(sales.Sum(r => r.Total) / sales.Count, 2),
                PreviousNet = previousNet,
                ChangePercent = previousNet > 0 ? Math.Round((net - previousNet) / previousNet * 100, 1) : null,
                InvoicedInArca = rows.Where(r => r.Fiscal == FiscalStatus.Authorized).Sum(Signed),
                Timeline = Timeline(range, from, to, rows),
                ByHour = ByHour(rows),
                ByWeekday = ByWeekday(rows),
            };
        }

        private static List<ChartPoint> Timeline(StatsRange range, DateTime from, DateTime to, List<SaleRow> rows)
        {
            var points = new List<ChartPoint>();
            switch (range)
            {
                case StatsRange.Last7Days:
                case StatsRange.Last30Days:
                    var dayIndex = 0;
                    var lastDay = to.AddDays(-1);
                    for (var d = from; d < to; d = d.AddDays(1), dayIndex++)
                    {
                        var day = d;
                        var value = rows.Where(r => r.Date.Date == day).Sum(Signed);
                        var label = range == StatsRange.Last7Days
                            ? $"{Days[(int)d.DayOfWeek]} {d.Day}"
                            : (dayIndex % 5 == 0 || d == lastDay ? $"{d.Day}/{d.Month}" : "");
                        points.Add(new ChartPoint(label, value, $"{DayNames[(int)d.DayOfWeek]} {d:dd/MM}"));
                    }
                    break;

                case StatsRange.Last12Weeks:
                    for (var w = from; w < to; w = w.AddDays(7))
                    {
                        var start = w;
                        var end = w.AddDays(7);
                        var value = rows.Where(r => r.Date >= start && r.Date < end).Sum(Signed);
                        points.Add(new ChartPoint($"{w.Day}/{w.Month}", value, $"Semana del {w:dd/MM} al {end.AddDays(-1):dd/MM}"));
                    }
                    break;

                default:
                    for (var m = from; m < to; m = m.AddMonths(1))
                    {
                        var month = m;
                        var value = rows.Where(r => r.Date.Year == month.Year && r.Date.Month == month.Month).Sum(Signed);
                        var label = m.Month == 1 || m == from ? $"{Months[m.Month - 1]} {m:yy}" : Months[m.Month - 1];
                        points.Add(new ChartPoint(label, value, $"{Months[m.Month - 1]} {m.Year}"));
                    }
                    break;
            }
            return points;
        }

        // Only the hours the business is usually open (first to last hour with sales)
        private static List<ChartPoint> ByHour(List<SaleRow> rows)
        {
            var sales = rows.Where(r => r.Operation == OperationType.Sale).ToList();
            int first = sales.Count == 0 ? 8 : sales.Min(r => r.Date.Hour);
            int last = sales.Count == 0 ? 20 : sales.Max(r => r.Date.Hour);
            var points = new List<ChartPoint>();
            for (int h = first; h <= last; h++)
            {
                var hour = h;
                var value = rows.Where(r => r.Date.Hour == hour).Sum(Signed);
                var count = sales.Count(r => r.Date.Hour == hour);
                points.Add(new ChartPoint($"{h}", value, $"De {h}:00 a {h + 1}:00 · {count} {(count == 1 ? "venta" : "ventas")}"));
            }
            return points;
        }

        // Monday first
        private static List<ChartPoint> ByWeekday(List<SaleRow> rows)
        {
            var points = new List<ChartPoint>();
            foreach (var day in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                var value = rows.Where(r => (int)r.Date.DayOfWeek == day).Sum(Signed);
                var count = rows.Count(r => r.Operation == OperationType.Sale && (int)r.Date.DayOfWeek == day);
                points.Add(new ChartPoint(Days[day], value, $"{DayNames[day]} · {count} {(count == 1 ? "venta" : "ventas")}"));
            }
            return points;
        }

        private static DateTime Monday(DateTime d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)).Date;
    }
}
