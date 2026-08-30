using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MobusTCP.Models;

namespace MobusTCP.Views.Controls;

public enum ChartDataType
{
    JointPositions,
    JointVelocities,
    JointTorques,
    CartesianPositions
}

public class TelemetryChartControl : Control
{
    public static readonly StyledProperty<List<TrajectoryPoint>?> DataPointsProperty =
        AvaloniaProperty.Register<TelemetryChartControl, List<TrajectoryPoint>?>(nameof(DataPoints));

    public static readonly StyledProperty<ChartDataType> DataTypeProperty =
        AvaloniaProperty.Register<TelemetryChartControl, ChartDataType>(nameof(DataType), ChartDataType.JointPositions);

    public static readonly StyledProperty<double> CurrentTimeProperty =
        AvaloniaProperty.Register<TelemetryChartControl, double>(nameof(CurrentTime), 0.0);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<TelemetryChartControl, string>(nameof(Title), "Joint Positions (deg)");

    public List<TrajectoryPoint>? DataPoints
    {
        get => GetValue(DataPointsProperty);
        set => SetValue(DataPointsProperty, value);
    }

    public ChartDataType DataType
    {
        get => GetValue(DataTypeProperty);
        set => SetValue(DataTypeProperty, value);
    }

    public double CurrentTime
    {
        get => GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    static TelemetryChartControl()
    {
        AffectsRender<TelemetryChartControl>(DataPointsProperty, DataTypeProperty, CurrentTimeProperty, TitleProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 50 || h <= 50) return;

        // Dark slate container background
        var bgBrush = new SolidColorBrush(Color.FromRgb(15, 23, 42)); // Slate 900
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 1);
        context.DrawRectangle(bgBrush, borderPen, new Rect(0, 0, w, h), 6, 6);

        // Chart plot area padding
        double padLeft = 45;
        double padRight = 15;
        double padTop = 30;
        double padBottom = 25;

        double plotW = w - padLeft - padRight;
        double plotH = h - padTop - padBottom;

        if (plotW <= 0 || plotH <= 0) return;

        // Title and Legend
        DrawHeader(context, w);

        var points = DataPoints;
        if (points == null || points.Count == 0)
        {
            var noDataBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            var text = new FormattedText(
                "No Trajectory Generated",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Italic, FontWeight.Normal),
                12,
                noDataBrush);
            context.DrawText(text, new Point((w - text.Width) / 2, (h - text.Height) / 2));
            return;
        }

        // Calculate Min and Max values for Y-axis scaling
        double minY = double.MaxValue;
        double maxY = double.MinValue;
        double maxTime = points[^1].Time;
        if (maxTime <= 0) maxTime = 1.0;

        foreach (var pt in points)
        {
            var vals = GetPointValues(pt);
            foreach (var v in vals)
            {
                if (v < minY) minY = v;
                if (v > maxY) maxY = v;
            }
        }

        if (Math.Abs(maxY - minY) < 1e-4)
        {
            minY -= 1.0;
            maxY += 1.0;
        }
        else
        {
            double pad = (maxY - minY) * 0.1;
            minY -= pad;
            maxY += pad;
        }

        // Draw Y Grid lines & Labels
        DrawGrid(context, padLeft, padTop, plotW, plotH, minY, maxY, maxTime);

        // Pens for 4 channels
        var pen1 = new Pen(new SolidColorBrush(Color.FromRgb(59, 130, 246)), 2);  // Blue (Q1/X)
        var pen2 = new Pen(new SolidColorBrush(Color.FromRgb(249, 115, 22)), 2);  // Orange (Q2/Y)
        var pen3 = new Pen(new SolidColorBrush(Color.FromRgb(16, 185, 129)), 2);  // Green (Q3/Z)
        var pen4 = new Pen(new SolidColorBrush(Color.FromRgb(139, 92, 246)), 2);  // Purple (Q4/Pitch)

        Pen[] pens = [pen1, pen2, pen3, pen4];

        // Draw line series
        for (int channel = 0; channel < 4; channel++)
        {
            Point? prevPt = null;
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                var vals = GetPointValues(pt);
                double val = vals[channel];

                double px = padLeft + (pt.Time / maxTime) * plotW;
                double py = padTop + plotH - ((val - minY) / (maxY - minY)) * plotH;
                Point curPt = new(px, py);

                if (prevPt.HasValue)
                {
                    context.DrawLine(pens[channel], prevPt.Value, curPt);
                }
                prevPt = curPt;
            }
        }

        // Draw Current Playback Time Cursor line
        if (CurrentTime >= 0 && CurrentTime <= maxTime)
        {
            double cursorX = padLeft + (CurrentTime / maxTime) * plotW;
            var cursorPen = new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 1.5, DashStyle.Dash);
            context.DrawLine(cursorPen, new Point(cursorX, padTop), new Point(cursorX, padTop + plotH));
        }
    }

    private double[] GetPointValues(TrajectoryPoint pt)
    {
        return DataType switch
        {
            ChartDataType.JointVelocities => [pt.Q1Dot, pt.Q2Dot, pt.Q3Dot, pt.Q4Dot],
            ChartDataType.JointTorques => [pt.Tau1, pt.Tau2, pt.Tau3, pt.Tau4],
            ChartDataType.CartesianPositions => [pt.X, pt.Y, pt.Z, pt.Pitch],
            ChartDataType.JointPositions => [pt.Q1, pt.Q2, pt.Q3, pt.Q4],
            _ => [pt.Q1, pt.Q2, pt.Q3, pt.Q4]
        };
    }

    private void DrawHeader(DrawingContext context, double w)
    {
        var titleBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        var tf = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
        var titleText = new FormattedText(Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, 11, titleBrush);
        context.DrawText(titleText, new Point(12, 8));

        // Legend labels (e.g. Q1, Q2, Q3, Q4)
        string[] labels = DataType == ChartDataType.CartesianPositions
            ? ["X", "Y", "Z", "Pitch"]
            : DataType == ChartDataType.JointTorques
            ? ["τ1", "τ2", "τ3", "τ4"]
            : DataType == ChartDataType.JointVelocities
            ? ["q̇1", "q̇2", "q̇3", "q̇4"]
            : ["Q1", "Q2", "Q3", "Q4"];

        Color[] colors =
        [
            Color.FromRgb(59, 130, 246),
            Color.FromRgb(249, 115, 22),
            Color.FromRgb(16, 185, 129),
            Color.FromRgb(139, 92, 246)
        ];

        double lx = w - 180;
        for (int i = 0; i < 4; i++)
        {
            context.DrawRectangle(new SolidColorBrush(colors[i]), null, new Rect(lx, 11, 8, 8), 2, 2);
            var lText = new FormattedText(labels[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, 10, titleBrush);
            context.DrawText(lText, new Point(lx + 11, 9));
            lx += 42;
        }
    }

    private void DrawGrid(DrawingContext context, double x0, double y0, double pw, double ph, double minY, double maxY, double maxTime)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), 1);
        var labelBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        var tf = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);

        // 4 Horizontal Grid Lines
        int numYLines = 4;
        for (int i = 0; i <= numYLines; i++)
        {
            double py = y0 + (i / (double)numYLines) * ph;
            context.DrawLine(gridPen, new Point(x0, py), new Point(x0 + pw, py));

            double val = maxY - (i / (double)numYLines) * (maxY - minY);
            var valText = new FormattedText(
                $"{val:F0}",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                tf,
                9,
                labelBrush);
            context.DrawText(valText, new Point(x0 - valText.Width - 4, py - valText.Height / 2));
        }

        // Time labels
        var t0Text = new FormattedText("0s", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, 9, labelBrush);
        var tfText = new FormattedText($"{maxTime:F1}s", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, 9, labelBrush);
        context.DrawText(t0Text, new Point(x0, y0 + ph + 4));
        context.DrawText(tfText, new Point(x0 + pw - tfText.Width, y0 + ph + 4));
    }
}
