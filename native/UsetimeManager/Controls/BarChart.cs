using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UsetimeManager.Controls;

/// <summary>轻量柱状图（无第三方依赖）</summary>
public sealed class BarChart : Canvas
{
    public void Render(IReadOnlyList<Services.ChartPoint> points)
    {
        Children.Clear();
        if (points.Count == 0) return;

        var width = Math.Max(200, ActualWidth > 0 ? ActualWidth : 700);
        var height = Math.Max(160, ActualHeight > 0 ? ActualHeight : 280);

        var maxMs = points.Max(p => p.Ms);
        if (maxMs <= 0) maxMs = 1;

        // 网格 + 轴标签
        var padLeft = 48;
        var padBottom = 28;
        var padTop = 16;
        var padRight = 12;
        var plotW = width - padLeft - padRight;
        var plotH = height - padTop - padBottom;

        for (var i = 0; i <= 3; i++)
        {
            var y = padTop + plotH * i / 3.0;
            var line = new Line
            {
                X1 = padLeft,
                X2 = width - padRight,
                Y1 = y,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB)),
                StrokeThickness = 1
            };
            if (i < 3)
            {
                line.StrokeDashArray = new DoubleCollection { 3, 3 };
            }
            Children.Add(line);

            var label = FormatMinutes(maxMs * (3 - i) / 3.0);
            var tb = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF))
            };
            SetLeft(tb, 4);
            SetTop(tb, y - 8);
            Children.Add(tb);
        }

        var n = points.Count;
        var slot = plotW / n;
        var barW = Math.Min(18, Math.Max(4, slot * 0.55));
        var showEvery = n > 20 ? 4 : n > 12 ? 2 : 1;

        for (var i = 0; i < n; i++)
        {
            var p = points[i];
            var h = plotH * (p.Ms / maxMs);
            if (h < 0.5 && p.Ms <= 0) continue;

            var rect = new Rectangle
            {
                Width = barW,
                Height = Math.Max(2, h),
                Fill = new SolidColorBrush(Color.FromRgb(0x2B, 0x6C, 0xFF)),
                RadiusX = 3,
                RadiusY = 3
            };
            var x = padLeft + slot * i + (slot - barW) / 2;
            var y = padTop + plotH - rect.Height;
            SetLeft(rect, x);
            SetTop(rect, y);
            Children.Add(rect);

            if (i % showEvery == 0)
            {
                var tb = new TextBlock
                {
                    Text = p.Label,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF))
                };
                SetLeft(tb, padLeft + slot * i - 10);
                SetTop(tb, height - padBottom + 6);
                Children.Add(tb);
            }
        }
    }

    private static string FormatMinutes(double ms)
    {
        var minutes = ms / 60000.0;
        if (minutes >= 60)
            return $"{minutes / 60.0:0.#}h";
        if (minutes >= 1)
            return $"{minutes:0}m";
        return $"{Math.Max(1, (int)(ms / 1000))}s";
    }

    public static string FormatDuration(double ms)
    {
        if (ms < 1000) return "0分钟";
        var ts = TimeSpan.FromMilliseconds(ms);
        if (ts.TotalHours >= 1)
        {
            var h = (int)ts.TotalHours;
            var m = ts.Minutes;
            return m > 0 ? $"{h}小时{m}分钟" : $"{h}小时";
        }
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}分钟";
        return $"{Math.Max(1, ts.Seconds)}秒";
    }

    public static string FormatDelta(double deltaMs, string period)
    {
        var body = FormatDuration(Math.Abs(deltaMs));
        var label = period switch
        {
            "week" => "较上周",
            "month" => "较上月",
            _ => "较昨日"
        };
        if (deltaMs > 0) return $"{label}增加{body}";
        if (deltaMs < 0) return $"{label}减少{body}";
        return $"与{label[1..]}持平";
    }
}
