using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Affinity
{
    public sealed class AffinityChartPoint
    {
        public AffinityChartPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }
    }

    public sealed class AffinityDonutSegment
    {
        public AffinityDonutSegment(double startAngleDegrees, double sweepAngleDegrees)
        {
            StartAngleDegrees = startAngleDegrees;
            SweepAngleDegrees = sweepAngleDegrees;
        }

        public double StartAngleDegrees { get; }

        public double SweepAngleDegrees { get; }
    }

    public sealed class AffinityDonutLayout
    {
        public AffinityDonutLayout(double radius, double strokeThickness)
        {
            Radius = radius;
            StrokeThickness = strokeThickness;
        }

        public double Radius { get; }

        public double StrokeThickness { get; }
    }

    public static class AffinityPatternChartGeometry
    {
        public static IReadOnlyList<AffinityChartPoint> NormalizeSparkline(IReadOnlyList<double> values)
        {
            if (values == null)
                return Array.Empty<AffinityChartPoint>();

            double[] finiteValues = values.Where(IsFinite).ToArray();
            if (finiteValues.Length == 0)
                return Array.Empty<AffinityChartPoint>();

            double minimum = finiteValues.Min();
            double maximum = finiteValues.Max();
            bool isConstant = Math.Abs(maximum - minimum) < 0.0000001;
            var points = new List<AffinityChartPoint>(finiteValues.Length);
            for (int index = 0; index < finiteValues.Length; index++)
            {
                double x = finiteValues.Length == 1 ? 0.5 : index / (double)(finiteValues.Length - 1);
                double y = isConstant ? 0.5 : 1 - ((finiteValues[index] - minimum) / (maximum - minimum));
                points.Add(new AffinityChartPoint(x, y));
            }

            return points;
        }

        public static IReadOnlyList<AffinityDonutSegment> BuildDonutSegments(IReadOnlyList<double> shares)
        {
            if (shares == null || shares.Count == 0)
                return Array.Empty<AffinityDonutSegment>();

            double[] positiveShares = shares.Select(value => IsFinite(value) && value > 0 ? value : 0).ToArray();
            double total = positiveShares.Sum();
            if (total <= 0)
                return Array.Empty<AffinityDonutSegment>();

            var segments = new List<AffinityDonutSegment>();
            double start = 0;
            for (int index = 0; index < positiveShares.Length; index++)
            {
                if (positiveShares[index] <= 0)
                    continue;
                double sweep = positiveShares[index] / total * 360;
                segments.Add(new AffinityDonutSegment(start, sweep));
                start += sweep;
            }

            if (segments.Count > 0)
            {
                AffinityDonutSegment last = segments[segments.Count - 1];
                segments[segments.Count - 1] = new AffinityDonutSegment(last.StartAngleDegrees, 360 - last.StartAngleDegrees);
            }

            return segments;
        }

        public static AffinityDonutLayout CalculateDonutLayout(double diameter)
        {
            double outerRadius = Math.Max(0, diameter / 2 - 4);
            double thickness = Math.Min(outerRadius, Math.Max(8, outerRadius * 0.2));
            return new AffinityDonutLayout(Math.Max(0, outerRadius - thickness / 2), thickness);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public sealed class AffinityDonutChart : FrameworkElement
    {
        private static readonly Brush[] SegmentBrushes =
        {
            CreateBrush("#2CB7F0"),
            CreateBrush("#55C9F7"),
            CreateBrush("#79D8FA"),
            CreateBrush("#9BE5FC"),
            CreateBrush("#5AA6C7")
        };

        private INotifyCollectionChanged observedCollection;

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<AffinityTrackShare>),
            typeof(AffinityDonutChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

        public IEnumerable<AffinityTrackShare> ItemsSource
        {
            get => (IEnumerable<AffinityTrackShare>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            double diameter = Math.Min(ActualWidth, ActualHeight);
            if (diameter <= 0)
                return;

            Point center = new Point(ActualWidth / 2, ActualHeight / 2);
            AffinityDonutLayout layout = AffinityPatternChartGeometry.CalculateDonutLayout(diameter);
            drawingContext.DrawEllipse(null, new Pen(CreateBrush("#3A3A3A"), layout.StrokeThickness), center, layout.Radius, layout.Radius);

            AffinityTrackShare[] shares = ItemsSource?.Where(item => item != null).ToArray() ?? Array.Empty<AffinityTrackShare>();
            IReadOnlyList<AffinityDonutSegment> segments = AffinityPatternChartGeometry.BuildDonutSegments(shares.Select(item => item.SharePercent).ToArray());
            for (int index = 0; index < segments.Count; index++)
                DrawSegment(drawingContext, center, layout.Radius, layout.StrokeThickness, segments[index], SegmentBrushes[index % SegmentBrushes.Length]);

            AffinityTrackShare leader = shares.OrderByDescending(item => item.SharePercent).FirstOrDefault();
            if (leader != null)
            {
                DrawCenteredText(drawingContext, center, $"{leader.SharePercent:F0}%", 21, Brushes.White, -7, layout.Radius * 1.25);
                DrawCenteredText(drawingContext, center, leader.TrackDisplayName ?? leader.Label ?? string.Empty, 10, CreateBrush("#B8B8B8"), 13, layout.Radius * 1.25);
            }
        }

        private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            var chart = (AffinityDonutChart)dependencyObject;
            if (chart.observedCollection != null)
                chart.observedCollection.CollectionChanged -= chart.ItemsCollectionChanged;
            chart.observedCollection = args.NewValue as INotifyCollectionChanged;
            if (chart.observedCollection != null)
                chart.observedCollection.CollectionChanged += chart.ItemsCollectionChanged;
            chart.InvalidateVisual();
        }

        private void ItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            InvalidateVisual();
        }

        private static void DrawSegment(DrawingContext context, Point center, double radius, double thickness, AffinityDonutSegment segment, Brush brush)
        {
            var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            if (segment.SweepAngleDegrees >= 359.999)
            {
                context.DrawEllipse(null, pen, center, radius, radius);
                return;
            }

            double startRadians = (segment.StartAngleDegrees - 90) * Math.PI / 180;
            double endRadians = (segment.StartAngleDegrees + segment.SweepAngleDegrees - 90) * Math.PI / 180;
            Point start = new Point(center.X + Math.Cos(startRadians) * radius, center.Y + Math.Sin(startRadians) * radius);
            Point end = new Point(center.X + Math.Cos(endRadians) * radius, center.Y + Math.Sin(endRadians) * radius);
            var geometry = new StreamGeometry();
            using (StreamGeometryContext geometryContext = geometry.Open())
            {
                geometryContext.BeginFigure(start, false, false);
                geometryContext.ArcTo(end, new Size(radius, radius), 0, segment.SweepAngleDegrees > 180, SweepDirection.Clockwise, true, false);
            }
            geometry.Freeze();
            context.DrawGeometry(null, pen, geometry);
        }

        private void DrawCenteredText(DrawingContext context, Point center, string text, double size, Brush brush, double yOffset, double maximumWidth)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            formatted.MaxTextWidth = Math.Max(20, maximumWidth);
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
            formatted.TextAlignment = TextAlignment.Center;
            context.DrawText(formatted, new Point(center.X - formatted.MaxTextWidth / 2, center.Y - formatted.Height / 2 + yOffset));
        }

        private static Brush CreateBrush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }
    }

    public sealed class AffinitySparkline : FrameworkElement
    {
        private INotifyCollectionChanged observedCollection;

        public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
            nameof(Values),
            typeof(IEnumerable<double>),
            typeof(AffinitySparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

        public IEnumerable<double> Values
        {
            get => (IEnumerable<double>)GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0)
                return;

            drawingContext.DrawLine(new Pen(CreateBrush("#3A3A3A"), 1), new Point(0, height - 1), new Point(width, height - 1));
            IReadOnlyList<AffinityChartPoint> points = AffinityPatternChartGeometry.NormalizeSparkline(Values?.ToArray());
            if (points.Count == 0)
                return;

            const double padding = 3;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext geometryContext = geometry.Open())
            {
                Point first = ToRenderPoint(points[0], width, height, padding);
                geometryContext.BeginFigure(first, false, false);
                if (points.Count == 1)
                    geometryContext.LineTo(new Point(first.X + 0.01, first.Y), true, false);
                else
                    geometryContext.PolyLineTo(points.Skip(1).Select(point => ToRenderPoint(point, width, height, padding)).ToList(), true, false);
            }
            geometry.Freeze();
            drawingContext.DrawGeometry(null, new Pen(CreateBrush("#2CB7F0"), 2), geometry);
        }

        private static void OnValuesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            var sparkline = (AffinitySparkline)dependencyObject;
            if (sparkline.observedCollection != null)
                sparkline.observedCollection.CollectionChanged -= sparkline.ValuesCollectionChanged;
            sparkline.observedCollection = args.NewValue as INotifyCollectionChanged;
            if (sparkline.observedCollection != null)
                sparkline.observedCollection.CollectionChanged += sparkline.ValuesCollectionChanged;
            sparkline.InvalidateVisual();
        }

        private void ValuesCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            InvalidateVisual();
        }

        private static Point ToRenderPoint(AffinityChartPoint point, double width, double height, double padding)
        {
            return new Point(padding + point.X * Math.Max(0, width - padding * 2), padding + point.Y * Math.Max(0, height - padding * 2));
        }

        private static Brush CreateBrush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }
    }
}
