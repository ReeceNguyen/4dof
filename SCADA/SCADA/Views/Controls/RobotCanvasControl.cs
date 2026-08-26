using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SCADA.Models;

namespace SCADA.Views.Controls;

public enum VisualizerViewMode
{
    Isometric3D,
    SideElevation2D,
    TopDown2D
}

public class RobotCanvasControl : Control
{
    public static readonly StyledProperty<RobotParameters?> RobotProperty =
        AvaloniaProperty.Register<RobotCanvasControl, RobotParameters?>(nameof(Robot));

    public static readonly StyledProperty<VisualizerViewMode> ViewModeProperty =
        AvaloniaProperty.Register<RobotCanvasControl, VisualizerViewMode>(nameof(ViewMode), VisualizerViewMode.Isometric3D);

    public static readonly StyledProperty<bool> ShowGhostArmProperty =
        AvaloniaProperty.Register<RobotCanvasControl, bool>(nameof(ShowGhostArm), true);

    public static readonly StyledProperty<bool> ShowTrailProperty =
        AvaloniaProperty.Register<RobotCanvasControl, bool>(nameof(ShowTrail), true);

    public static readonly StyledProperty<bool> ShowWorkspaceBoundaryProperty =
        AvaloniaProperty.Register<RobotCanvasControl, bool>(nameof(ShowWorkspaceBoundary), true);

    public RobotParameters? Robot
    {
        get => GetValue(RobotProperty);
        set => SetValue(RobotProperty, value);
    }

    public VisualizerViewMode ViewMode
    {
        get => GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    public bool ShowGhostArm
    {
        get => GetValue(ShowGhostArmProperty);
        set => SetValue(ShowGhostArmProperty, value);
    }

    public bool ShowTrail
    {
        get => GetValue(ShowTrailProperty);
        set => SetValue(ShowTrailProperty, value);
    }

    public bool ShowWorkspaceBoundary
    {
        get => GetValue(ShowWorkspaceBoundaryProperty);
        set => SetValue(ShowWorkspaceBoundaryProperty, value);
    }

    // Camera settings
    private double _camYawDeg = 45.0;
    private double _camPitchDeg = 25.0;
    private double _zoom = 0.55;
    private Point _panOffset = new(0, 50);

    private Point _lastMousePos;
    private bool _isLeftDragging;
    private bool _isRightDragging;

    private readonly List<Point> _trailPoints = new();
    private const int MaxTrailPoints = 300;

    static RobotCanvasControl()
    {
        AffectsRender<RobotCanvasControl>(RobotProperty, ViewModeProperty, ShowGhostArmProperty, ShowTrailProperty, ShowWorkspaceBoundaryProperty);
    }

    public RobotCanvasControl()
    {
        ClipToBounds = true;
    }

    public void AddTrailPoint(double x, double y, double z)
    {
        if (!ShowTrail) return;
        Point screenPt = ProjectPoint(x, y, z, Bounds.Width / 2 + _panOffset.X, Bounds.Height / 2 + _panOffset.Y);
        _trailPoints.Add(screenPt);
        if (_trailPoints.Count > MaxTrailPoints)
        {
            _trailPoints.RemoveAt(0);
        }
        InvalidateVisual();
    }

    public void ClearTrail()
    {
        _trailPoints.Clear();
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var currentPoint = e.GetCurrentPoint(this);
        _lastMousePos = currentPoint.Position;
        if (currentPoint.Properties.IsLeftButtonPressed) _isLeftDragging = true;
        if (currentPoint.Properties.IsRightButtonPressed) _isRightDragging = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isLeftDragging = false;
        _isRightDragging = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var currentPoint = e.GetCurrentPoint(this);
        var pos = currentPoint.Position;

        if (_isLeftDragging && ViewMode == VisualizerViewMode.Isometric3D)
        {
            double dx = pos.X - _lastMousePos.X;
            double dy = pos.Y - _lastMousePos.Y;
            _camYawDeg = (_camYawDeg + dx * 0.5) % 360;
            _camPitchDeg = Math.Clamp(_camPitchDeg - dy * 0.5, -85.0, 85.0);
            InvalidateVisual();
        }
        else if (_isRightDragging)
        {
            double dx = pos.X - _lastMousePos.X;
            double dy = pos.Y - _lastMousePos.Y;
            _panOffset = new Point(_panOffset.X + dx, _panOffset.Y + dy);
            InvalidateVisual();
        }

        _lastMousePos = pos;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        double delta = e.Delta.Y;
        _zoom = Math.Clamp(_zoom * (delta > 0 ? 1.15 : 0.87), 0.15, 3.0);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // Background
        var bgBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(15, 23, 42), 0.0), // Slate 900
                new GradientStop(Color.FromRgb(10, 15, 30), 1.0)
            }
        };
        context.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h));

        double centerX = w / 2 + _panOffset.X;
        double centerY = h / 2 + _panOffset.Y;

        // 1. Draw Grid Floor & Axes
        DrawFloorGrid(context, centerX, centerY);

        if (Robot == null) return;

        // 2. Draw Workspace Boundary
        if (ShowWorkspaceBoundary)
        {
            DrawWorkspaceBoundary(context, centerX, centerY);
        }

        // 3. Draw Motion Trail
        if (ShowTrail && _trailPoints.Count > 1)
        {
            DrawTrail(context);
        }

        // 4. Draw Ghost Target Arm (if active)
        if (ShowGhostArm)
        {
            DrawRobotArm(context, centerX, centerY, isGhost: true);
        }

        // 5. Draw Main Robot Arm
        DrawRobotArm(context, centerX, centerY, isGhost: false);

        // 6. Draw HUD Info Overlay
        DrawHudOverlay(context, w, h);
    }

    private void DrawFloorGrid(DrawingContext context, double cx, double cy)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)), 1);
        var axisXPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 239, 68, 68)), 2);  // Red (X)
        var axisYPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 34, 197, 94)), 2);  // Green (Y)
        var axisZPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 59, 130, 246)), 2); // Blue (Z)

        double gridRadius = 400.0;
        int gridSteps = 8;
        double step = gridRadius / gridSteps;

        // Concentric circles on ground floor
        for (int i = 1; i <= gridSteps; i++)
        {
            double r = i * step;
            DrawGroundCircle(context, cx, cy, r, gridPen);
        }

        // Radial lines
        for (int a = 0; a < 360; a += 45)
        {
            double rad = a * Math.PI / 180.0;
            Point pStart = ProjectPoint(0, 0, 0, cx, cy);
            Point pEnd = ProjectPoint(gridRadius * Math.Cos(rad), gridRadius * Math.Sin(rad), 0, cx, cy);
            context.DrawLine(gridPen, pStart, pEnd);
        }

        // Main Axes
        Point origin = ProjectPoint(0, 0, 0, cx, cy);
        Point axisX = ProjectPoint(150, 0, 0, cx, cy);
        Point axisY = ProjectPoint(0, 150, 0, cx, cy);
        Point axisZ = ProjectPoint(0, 0, 150, cx, cy);

        context.DrawLine(axisXPen, origin, axisX);
        context.DrawLine(axisYPen, origin, axisY);
        context.DrawLine(axisZPen, origin, axisZ);
    }

    private void DrawGroundCircle(DrawingContext context, double cx, double cy, double radius, Pen pen)
    {
        const int segments = 36;
        Point? firstPt = null;
        Point? prevPt = null;

        for (int i = 0; i <= segments; i++)
        {
            double angle = i * 2 * Math.PI / segments;
            double gx = radius * Math.Cos(angle);
            double gy = radius * Math.Sin(angle);
            Point pt = ProjectPoint(gx, gy, 0, cx, cy);

            if (prevPt.HasValue)
            {
                context.DrawLine(pen, prevPt.Value, pt);
            }
            else
            {
                firstPt = pt;
            }
            prevPt = pt;
        }
    }

    private void DrawWorkspaceBoundary(DrawingContext context, double cx, double cy)
    {
        if (Robot == null) return;
        double maxReach = Robot.L2 + Robot.L3 + Robot.L4;
        var boundaryPen = new Pen(new SolidColorBrush(Color.FromArgb(45, 6, 182, 212)), 1, DashStyle.Dash);
        DrawGroundCircle(context, cx, cy, maxReach, boundaryPen);
    }

    private void DrawTrail(DrawingContext context)
    {
        var trailPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 245, 158, 11)), 2);
        for (int i = 1; i < _trailPoints.Count; i++)
        {
            context.DrawLine(trailPen, _trailPoints[i - 1], _trailPoints[i]);
        }
    }

    private void DrawRobotArm(DrawingContext context, double cx, double cy, bool isGhost)
    {
        if (Robot == null) return;

        // Retrieve positions
        var p0 = Robot.BasePos;
        var p1 = isGhost ? Robot.TargetJoint1Pos : Robot.Joint1Pos;
        var p2 = isGhost ? Robot.TargetJoint2Pos : Robot.Joint2Pos;
        var p3 = isGhost ? Robot.TargetJoint3Pos : Robot.Joint3Pos;
        var p4 = isGhost ? Robot.TargetEndEffectorPos : Robot.EndEffectorPos;

        Point pt0 = ProjectPoint(p0.X, p0.Y, p0.Z, cx, cy);
        Point pt1 = ProjectPoint(p1.X, p1.Y, p1.Z, cx, cy);
        Point pt2 = ProjectPoint(p2.X, p2.Y, p2.Z, cx, cy);
        Point pt3 = ProjectPoint(p3.X, p3.Y, p3.Z, cx, cy);
        Point pt4 = ProjectPoint(p4.X, p4.Y, p4.Z, cx, cy);

        // Brushes & Pens
        byte alpha = isGhost ? (byte)70 : (byte)255;
        var baseBrush = new SolidColorBrush(Color.FromArgb(alpha, 71, 85, 105));   // Slate 600
        var link1Brush = new SolidColorBrush(Color.FromArgb(alpha, 59, 130, 246)); // Blue 500
        var link2Brush = new SolidColorBrush(Color.FromArgb(alpha, 16, 185, 129)); // Emerald 500
        var link3Brush = new SolidColorBrush(Color.FromArgb(alpha, 249, 115, 22)); // Orange 500
        var toolBrush = new SolidColorBrush(Color.FromArgb(alpha, 239, 68, 68));   // Red 500
        var jointBrush = new SolidColorBrush(Color.FromArgb(alpha, 241, 245, 249)); // White/Slate

        var link1Pen = new Pen(link1Brush, isGhost ? 8 : 12);
        var link2Pen = new Pen(link2Brush, isGhost ? 7 : 10);
        var link3Pen = new Pen(link3PenWidth(isGhost), isGhost ? 6 : 8);
        var toolPen = new Pen(toolBrush, isGhost ? 4 : 6);
        var basePen = new Pen(baseBrush, isGhost ? 10 : 16);

        // 1. Draw Base Pedestal (P0 to P1)
        context.DrawLine(basePen, pt0, pt1);

        // 2. Draw Upper Arm (P1 to P2)
        context.DrawLine(link1Pen, pt1, pt2);

        // 3. Draw Forearm (P2 to P3)
        context.DrawLine(link2Pen, pt2, pt3);

        // 4. Draw Wrist & Tool (P3 to P4)
        context.DrawLine(link3Pen, pt3, pt4);

        // 5. Draw Gripper Tool Tip at P4
        DrawGripper(context, pt3, pt4, alpha, Robot.IsGripperClosed);

        // 6. Draw Joint Pivots (Spheres)
        if (!isGhost)
        {
            double r = 8 * Math.Clamp(_zoom, 0.7, 1.5);
            context.DrawEllipse(jointBrush, null, pt0, r + 2, r + 2);
            context.DrawEllipse(link1Brush, null, pt1, r, r);
            context.DrawEllipse(link2Brush, null, pt2, r - 1, r - 1);
            context.DrawEllipse(link3Brush, null, pt3, r - 2, r - 2);
            context.DrawEllipse(toolBrush, null, pt4, r - 3, r - 3);
        }
    }

    private static IBrush link3PenWidth(bool isGhost) => isGhost ? new SolidColorBrush(Color.FromArgb(70, 249, 115, 22)) : new SolidColorBrush(Color.FromArgb(255, 249, 115, 22));

    private void DrawGripper(DrawingContext context, Point wristPt, Point tipPt, byte alpha, bool isClosed)
    {
        double dx = tipPt.X - wristPt.X;
        double dy = tipPt.Y - wristPt.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-4) return;

        double nx = -dy / len;
        double ny = dx / len;
        double gripSpread = isClosed ? 3.0 : 10.0;

        var gripPen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 239, 68, 68)), 3);

        Point f1Start = new(tipPt.X + nx * gripSpread, tipPt.Y + ny * gripSpread);
        Point f1End = new(f1Start.X + (dx / len) * 12, f1Start.Y + (dy / len) * 12);

        Point f2Start = new(tipPt.X - nx * gripSpread, tipPt.Y - ny * gripSpread);
        Point f2End = new(f2Start.X + (dx / len) * 12, f2Start.Y + (dy / len) * 12);

        context.DrawLine(gripPen, f1Start, f1End);
        context.DrawLine(gripPen, f2Start, f2End);
        context.DrawLine(gripPen, f1Start, f2Start);
    }

    private Point ProjectPoint(double x, double y, double z, double cx, double cy)
    {
        switch (ViewMode)
        {
            case VisualizerViewMode.SideElevation2D:
                // Elevation: Radius R along X-axis, Z along Y-axis (inverted)
                double r = Math.Sqrt(x * x + y * y) * Math.Sign(x >= 0 ? 1 : -1);
                return new Point(cx + r * _zoom, cy - z * _zoom);

            case VisualizerViewMode.TopDown2D:
                // Top-down: X along screen X, Y along screen Y
                return new Point(cx + x * _zoom, cy - y * _zoom);

            case VisualizerViewMode.Isometric3D:
            default:
                // 3D Camera Orbit Projection
                double yawRad = _camYawDeg * Math.PI / 180.0;
                double pitchRad = _camPitchDeg * Math.PI / 180.0;

                // Rotate around Z axis (Yaw)
                double x1 = x * Math.Cos(yawRad) - y * Math.Sin(yawRad);
                double y1 = x * Math.Sin(yawRad) + y * Math.Cos(yawRad);
                double z1 = z;

                // Rotate around X' axis (Pitch)
                double x2 = x1;
                double y2 = y1 * Math.Cos(pitchRad) - z1 * Math.Sin(pitchRad);
                double z2 = y1 * Math.Sin(pitchRad) + z1 * Math.Cos(pitchRad);

                // Orthographic projection to screen
                return new Point(cx + x2 * _zoom, cy - z2 * _zoom);
        }
    }

    private void DrawHudOverlay(DrawingContext context, double w, double h)
    {
        var textBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Slate 400
        var typeFace = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium);

        string modeStr = ViewMode switch
        {
            VisualizerViewMode.Isometric3D => $"3D Orbit (Yaw: {_camYawDeg:F0}°, Pitch: {_camPitchDeg:F0}°)",
            VisualizerViewMode.SideElevation2D => "2D Side Elevation (R-Z)",
            VisualizerViewMode.TopDown2D => "2D Top Azimuth (X-Y)",
            _ => "Visualizer"
        };

        var text = new FormattedText(
            $"{modeStr} | Zoom: {_zoom * 100:F0}%\n[L-Drag: Rotate | R-Drag: Pan | Wheel: Zoom]",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeFace,
            12,
            textBrush);

        context.DrawText(text, new Point(12, 12));
    }
}
