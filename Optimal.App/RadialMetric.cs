using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Optimal.App;

public sealed class RadialMetric : FrameworkElement
{
	public static readonly DependencyProperty PercentageProperty = DependencyProperty.Register("Percentage", typeof(double), typeof(RadialMetric), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)0.0, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register("ValueText", typeof(string), typeof(RadialMetric), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)"0%", FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register("Caption", typeof(string), typeof(RadialMetric), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty AccentProperty = DependencyProperty.Register("Accent", typeof(Brush), typeof(RadialMetric), (PropertyMetadata)(object)new FrameworkPropertyMetadata((object)Brushes.CornflowerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

	public double Percentage
	{
		get
		{
			return (double)((DependencyObject)this).GetValue(PercentageProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(PercentageProperty, (object)value);
		}
	}

	public string ValueText
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(ValueTextProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(ValueTextProperty, (object)value);
		}
	}

	public string Caption
	{
		get
		{
			return (string)((DependencyObject)this).GetValue(CaptionProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(CaptionProperty, (object)value);
		}
	}

	public Brush Accent
	{
		get
		{
			return (Brush)((DependencyObject)this).GetValue(AccentProperty);
		}
		set
		{
			((DependencyObject)this).SetValue(AccentProperty, (object)value);
		}
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		return new Size(124.0, 124.0);
	}

	protected override void OnRender(DrawingContext dc)
	{
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		base.OnRender(dc);
		double num = Math.Min(base.ActualWidth, base.ActualHeight);
		Point center = new(base.ActualWidth / 2.0, base.ActualHeight / 2.0);
		double num2 = Math.Max(10.0, num / 2.0 - 9.0);
		dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(42, 50, 65)), 7.0), center, num2, num2);
		double num3 = Math.Clamp(Percentage, 0.0, 100.0);
		if (num3 > 0.1)
		{
			Point startPoint = PointAt(center, num2, -90.0);
			Point point = PointAt(center, num2, -90.0 + num3 / 100.0 * 359.99);
			PathGeometry geometry = new PathGeometry(new global::_003C_003Ez__ReadOnlySingleElementList<PathFigure>(new PathFigure
			{
				StartPoint = startPoint,
				IsClosed = false,
				Segments = { (PathSegment)new ArcSegment(point, new Size(num2, num2), 0.0, num3 > 50.0, SweepDirection.Clockwise, isStroked: true) }
			}));
			dc.DrawGeometry(null, new Pen(Accent, 7.0)
			{
				StartLineCap = PenLineCap.Round,
				EndLineCap = PenLineCap.Round
			}, geometry);
		}
		DrawCentered(dc, ValueText, 19.0, center.Y - 14.0, Brushes.White, FontWeights.SemiBold);
		DrawCentered(dc, Caption.ToUpperInvariant(), 9.0, center.Y + 13.0, new SolidColorBrush(Color.FromRgb(148, 160, 180)), FontWeights.SemiBold);
	}

	private void DrawCentered(DrawingContext dc, string text, double size, double y, Brush brush, FontWeight weight)
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		FormattedText formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
		{
			TextAlignment = TextAlignment.Center
		};
		dc.DrawText(formattedText, new Point(base.ActualWidth / 2.0, y - formattedText.Height / 2.0));
	}

	private static Point PointAt(Point center, double radius, double angle)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		double num = angle * Math.PI / 180.0;
		return new Point(center.X + radius * Math.Cos(num), center.Y + radius * Math.Sin(num));
	}
}
