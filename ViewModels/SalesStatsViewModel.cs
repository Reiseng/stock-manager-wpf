using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using StockControl.Data;
using StockControl.Services;
using StockControl.Utils;

namespace StockControl.ViewModels
{
    // A bar ready to draw: Fill/Rest split the available space (star sizes)
    public class BarItem
    {
        public string Label { get; init; } = "";
        public string Tooltip { get; init; } = "";
        public string ValueText { get; init; } = "";
        public GridLength Fill { get; init; }
        public GridLength Rest { get; init; }
        public bool IsPeak { get; init; }
    }

    // Sales charts on the admin's home screen
    public class SalesStatsViewModel : INotifyPropertyChanged
    {
        private static readonly CultureInfo Ar = CultureInfo.GetCultureInfo("es-AR");
        private readonly SalesStatsPersistence _stats;

        public SalesStatsViewModel(SalesStatsPersistence stats)
        {
            _stats = stats;
        }

        private StatsRange _range = StatsRange.Last30Days;
        public StatsRange Range
        {
            get => _range;
            set
            {
                if (_range == value) return;
                _range = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Is7Days));
                OnPropertyChanged(nameof(Is30Days));
                OnPropertyChanged(nameof(Is12Weeks));
                OnPropertyChanged(nameof(Is12Months));
                Refresh();
            }
        }

        // For the range buttons (RadioButtons)
        public bool Is7Days { get => Range == StatsRange.Last7Days; set { if (value) Range = StatsRange.Last7Days; } }
        public bool Is30Days { get => Range == StatsRange.Last30Days; set { if (value) Range = StatsRange.Last30Days; } }
        public bool Is12Weeks { get => Range == StatsRange.Last12Weeks; set { if (value) Range = StatsRange.Last12Weeks; } }
        public bool Is12Months { get => Range == StatsRange.Last12Months; set { if (value) Range = StatsRange.Last12Months; } }

        public string PeriodText { get; private set; } = "";
        public string NetText { get; private set; } = "";
        public string CountText { get; private set; } = "";
        public string AverageText { get; private set; } = "";
        public string ChangeText { get; private set; } = "";
        public bool ChangeIsUp { get; private set; }
        public bool ChangeIsDown { get; private set; }
        public string ArcaText { get; private set; } = "";
        public string TimelineTitle { get; private set; } = "";
        public string TimelineMaxText { get; private set; } = "";
        public string BestHourText { get; private set; } = "";
        public string BestDayText { get; private set; } = "";
        public bool HasData { get; private set; }
        public bool HasTopProducts { get; private set; }
        public string ErrorText { get; private set; } = "";

        public List<BarItem> Timeline { get; private set; } = new();
        public List<BarItem> ByHour { get; private set; } = new();
        public List<BarItem> ByWeekday { get; private set; } = new();
        public List<BarItem> TopProducts { get; private set; } = new();

        public void Refresh()
        {
            try
            {
                var today = DateTime.Today;
                var (from, to) = SalesStats.Period(Range, today);
                var (pFrom, pTo) = SalesStats.PreviousPeriod(Range, today);
                var summary = SalesStats.Build(Range, today, _stats.GetSales(from, to), _stats.GetSales(pFrom, pTo));
                var top = _stats.GetTopProducts(from, to, 8);

                PeriodText = $"Del {summary.From:dd/MM/yyyy} al {summary.To.AddDays(-1):dd/MM/yyyy}";
                NetText = Money(summary.Net);
                CountText = summary.SalesCount.ToString("N0", Ar);
                AverageText = Money(summary.AverageTicket);
                ChangeIsUp = summary.ChangePercent > 0;
                ChangeIsDown = summary.ChangePercent < 0;
                ChangeText = summary.ChangePercent is decimal pct
                    ? $"{(pct > 0 ? "▲ " : pct < 0 ? "▼ " : "")}{Math.Abs(pct).ToString("0.#", Ar)}% vs. el período anterior"
                    : "Sin datos del período anterior para comparar";
                ArcaText = summary.Net > 0 && summary.InvoicedInArca != 0
                    ? $"{Money(summary.InvoicedInArca)} facturado en ARCA ({(summary.InvoicedInArca / summary.Net * 100).ToString("0", Ar)}%)"
                    : "Nada facturado en ARCA en este período";
                TimelineTitle = Range switch
                {
                    StatsRange.Last12Weeks => "Ventas por semana",
                    StatsRange.Last12Months => "Ventas por mes",
                    _ => "Ventas por día",
                };

                Timeline = Bars(summary.Timeline);
                ByHour = Bars(summary.ByHour);
                ByWeekday = Bars(summary.ByWeekday);
                var timelineMax = summary.Timeline.Count == 0 ? 0 : summary.Timeline.Max(p => p.Value);
                TimelineMaxText = timelineMax > 0 ? Compact(timelineMax) : "";

                var bestHour = summary.ByHour.Where(p => p.Value > 0).OrderByDescending(p => p.Value).FirstOrDefault();
                BestHourText = bestHour == null ? "" : $"Se vende más de {bestHour.Label}:00 a {int.Parse(bestHour.Label) + 1}:00";
                var bestDay = summary.ByWeekday.Where(p => p.Value > 0).OrderByDescending(p => p.Value).FirstOrDefault();
                BestDayText = bestDay == null ? "" : $"El día más fuerte es el {bestDay.Detail.Split(' ')[0]}";

                var topMax = top.Count == 0 ? 0 : top.Max(p => p.Amount);
                TopProducts = top.Select((p, i) => new BarItem
                {
                    Label = p.Name,
                    ValueText = Money(p.Amount),
                    Tooltip = $"{p.Name}: {p.Quantity.ToString("0.###", Ar)} unidades, {Money(p.Amount)}",
                    Fill = Star(topMax == 0 ? 0 : p.Amount / topMax),
                    Rest = Star(topMax == 0 ? 1 : 1 - p.Amount / topMax),
                    IsPeak = i == 0,
                }).ToList();

                HasData = summary.SalesCount > 0 || summary.Net != 0;
                HasTopProducts = TopProducts.Count > 0;
                ErrorText = "";
            }
            catch (Exception ex)
            {
                // The charts must never get in the way of the rest of the program
                ErrorLog.Write("Gráficas de ventas", ex);
                ErrorText = "No se pudieron calcular las estadísticas de ventas.";
                HasData = false;
            }
            OnPropertyChanged(string.Empty); // everything changed
        }

        private static List<BarItem> Bars(List<ChartPoint> points)
        {
            var max = points.Count == 0 ? 0 : points.Max(p => p.Value);
            return points.Select(p =>
            {
                var ratio = max <= 0 || p.Value <= 0 ? 0 : p.Value / max;
                return new BarItem
                {
                    Label = p.Label,
                    ValueText = Compact(p.Value),
                    Tooltip = $"{Capitalize(p.Detail)}\n{Money(p.Value)}",
                    Fill = Star(ratio),
                    Rest = Star(1 - ratio),
                    IsPeak = max > 0 && p.Value == max,
                };
            }).ToList();
        }

        private static GridLength Star(decimal ratio)
            => new((double)Math.Clamp(ratio, 0, 1), GridUnitType.Star);

        private static string Money(decimal value) => value.ToString("C0", Ar);

        // $ 1,2 M / $ 350 mil / $ 900
        public static string Compact(decimal value)
        {
            var abs = Math.Abs(value);
            if (abs >= 1_000_000) return "$ " + (value / 1_000_000).ToString("0.#", Ar) + " M";
            if (abs >= 1_000) return "$ " + (value / 1_000).ToString("0.#", Ar) + " mil";
            return "$ " + value.ToString("0", Ar);
        }

        private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
