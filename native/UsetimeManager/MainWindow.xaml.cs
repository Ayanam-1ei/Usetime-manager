using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using UsetimeManager.Controls;
using UsetimeManager.Services;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace UsetimeManager;

public partial class MainWindow : Window
{
    private string _period = "day";
    private DateTime _date = DateTime.Today;
    private bool _sortByName;
    private readonly DispatcherTimer _refreshTimer;
    private readonly SessionStore _store;
    private readonly TrackerService _tracker;

    public MainWindow(SessionStore store, TrackerService tracker)
    {
        _store = store;
        _tracker = tracker;
        InitializeComponent();
        UpdateChrome();
        Loaded += (_, _) => RefreshData();
        RefreshData();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += (_, _) => RefreshData();
        _refreshTimer.Start();
    }

    private void UpdateChrome()
    {
        DateLabel.Text = _period switch
        {
            "day" => $"{_date:yyyy年M月d日}",
            "week" => WeekLabel(_date),
            "month" => $"{_date:yyyy年M月}",
            _ => _date.ToString("yyyy年M月d日")
        };

        SummaryTitle.Text = _period switch
        {
            "day" when _date == DateTime.Today => "今日屏幕使用时长",
            "day" => "当日屏幕使用时长",
            "week" => "本周屏幕使用时长",
            "month" => "本月屏幕使用时长",
            _ => "屏幕使用时长"
        };

        AppListTitle.Text = _period switch
        {
            "day" when _date == DateTime.Today => "今日应用使用情况",
            "day" => "当日应用使用情况",
            "week" => "本周应用使用情况",
            "month" => "本月应用使用情况",
            _ => "应用使用情况"
        };

        SetTabStyle(TabDay, _period == "day");
        SetTabStyle(TabWeek, _period == "week");
        SetTabStyle(TabMonth, _period == "month");

        SortButton.Content = _sortByName ? "名称 ⇅" : "时长 ⇅";
    }

    private static string WeekLabel(DateTime date)
    {
        var dow = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        var start = date.AddDays(1 - dow);
        var end = start.AddDays(6);
        return $"{start:yyyy年M月d日} – {end:M月d日}";
    }

    private static void SetTabStyle(System.Windows.Controls.Button btn, bool active)
    {
        btn.Background = active
            ? (Brush)Application.Current.Resources["CardBrush"]
            : Brushes.Transparent;
        btn.Foreground = (Brush)Application.Current.Resources[
            active ? "InkBrush" : "Ink2Brush"];
    }

    private void RefreshData()
    {
        // 含「进行中会话」，避免统计不全
        var sessions = _tracker.SnapshotAll();
        var stats = StatsService.Compute(sessions, _period, _date);

        SummaryTotal.Text = BarChart.FormatDuration(stats.TotalMs);
        SummaryDelta.Text = stats.TotalMs <= 0 && stats.PrevTotalMs <= 0
            ? "暂无数据"
            : BarChart.FormatDelta(stats.DeltaMs, _period);

        if (stats.Points.Any(p => p.Ms > 0))
        {
            ChartPlaceholder.Visibility = Visibility.Collapsed;
            Chart.Visibility = Visibility.Visible;
            Chart.Width = Math.Max(400, ChartHost.ActualWidth > 0 ? ChartHost.ActualWidth : 700);
            Chart.Render(stats.Points);
        }
        else
        {
            ChartPlaceholder.Visibility = Visibility.Visible;
            Chart.Visibility = Visibility.Collapsed;
        }

        RenderApps(stats.Apps);
    }

    private void RenderApps(System.Collections.Generic.List<AppUsageItem> apps)
    {
        var host = AppListHost;
        if (host is null) return;
        host.Children.Clear();

        if (apps.Count == 0)
        {
            AppListEmpty.Visibility = Visibility.Visible;
            return;
        }
        AppListEmpty.Visibility = Visibility.Collapsed;

        var list = _sortByName
            ? apps.OrderBy(a => a.ProcessName, StringComparer.CurrentCultureIgnoreCase).ToList()
            : apps.OrderByDescending(a => a.TotalMs).ToList();

        foreach (var item in list.Take(20))
        {
            var row = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };

            var iconBorder = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(12),
                Background = (Brush)Application.Current.Resources["TrackBrush"],
                Margin = new Thickness(0, 0, 14, 0),
                ClipToBounds = true
            };
            DockPanel.SetDock(iconBorder, Dock.Left);
            row.Children.Add(iconBorder);

            var img = IconExtractor.GetIcon(item.ExePath);
            if (img != null)
            {
                iconBorder.Child = new System.Windows.Controls.Image
                {
                    Source = img,
                    Width = 36,
                    Height = 36,
                    Stretch = Stretch.Uniform
                };
            }
            else
            {
                iconBorder.Child = new TextBlock
                {
                    Text = item.ProcessName.Length > 0 ? item.ProcessName[..1].ToUpper() : "?",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)Application.Current.Resources["Ink2Brush"],
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
            }

            var stack = new StackPanel
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = item.ProcessName,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["InkBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameText, 0);
            top.Children.Add(nameText);

            var timeText = new TextBlock
            {
                Text = BarChart.FormatDuration(item.TotalMs),
                FontSize = 15,
                Foreground = (Brush)Application.Current.Resources["Ink2Brush"],
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(timeText, 1);
            top.Children.Add(timeText);

            stack.Children.Add(top);

            var track = new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = (Brush)Application.Current.Resources["TrackBrush"],
                Margin = new Thickness(0, 8, 0, 0)
            };
            var fill = new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = (Brush)Application.Current.Resources["BlueBrush"],
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Width = Math.Max(8, 680 * item.Ratio)
            };
            track.Child = fill;
            stack.Children.Add(track);

            row.Children.Add(stack);
            host.Children.Add(row);
        }
    }

    private void TabDay_Click(object sender, RoutedEventArgs e)
    {
        _period = "day";
        UpdateChrome();
        RefreshData();
    }

    private void TabWeek_Click(object sender, RoutedEventArgs e)
    {
        _period = "week";
        UpdateChrome();
        RefreshData();
    }

    private void TabMonth_Click(object sender, RoutedEventArgs e)
    {
        _period = "month";
        UpdateChrome();
        RefreshData();
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        _date = _period switch
        {
            "day" => _date.AddDays(-1),
            "week" => _date.AddDays(-7),
            "month" => new DateTime(_date.Year, _date.Month, 1).AddMonths(-1),
            _ => _date.AddDays(-1)
        };
        UpdateChrome();
        RefreshData();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        var next = _period switch
        {
            "day" => _date.AddDays(1),
            "week" => _date.AddDays(7),
            "month" => new DateTime(_date.Year, _date.Month, 1).AddMonths(1),
            _ => _date.AddDays(1)
        };
        if (next > DateTime.Today) return;
        _date = next;
        UpdateChrome();
        RefreshData();
    }

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        _sortByName = !_sortByName;
        UpdateChrome();
        RefreshData();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshData();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
        }
        base.OnKeyDown(e);
    }
}
