using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.Utilities;

namespace Img2SE2.Lib;

/// <summary>
/// A control which decorates a child with a beveled border and background.
/// Clone of <see cref="Avalonia.Controls.Border"/> with a custom corner renderer, aka <see cref="CornerRadius"/>
/// </summary>
public class BeveledBorder : Decorator, ICustomHitTest
{
    /// <summary>
    /// Defines the <see cref="Background"/> property.
    /// </summary>
#pragma warning disable KN023
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<BeveledBorder, IBrush?>(nameof(Background));

    /// <summary>
    /// Defines the <see cref="BorderBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<BeveledBorder, IBrush?>(nameof(BorderBrush));

    /// <summary>
    /// Defines the <see cref="BorderThickness"/> property (asymmetrical borders aren't supported).
    /// </summary>
    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        AvaloniaProperty.Register<BeveledBorder, Thickness>(nameof(BorderThickness));

    /// <summary>
    /// Equivalents to <see cref="Border.CornerRadiusProperty"/>, Defines the <see cref="CornerRadius"/> property.
    /// </summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<BeveledBorder, CornerRadius>(nameof(CornerRadius));
#pragma warning restore KN023

    private Thickness? _layoutThickness;
    private double _scale;

    /// <summary>
    /// Gets or sets a brush with which to paint the background.
    /// </summary>
    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets a brush with which to paint the border.
    /// </summary>
    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the thickness of the border.
    /// </summary>
    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>
    /// Gets or sets the corner radius of the border.
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private Thickness LayoutThickness
    {
        get
        {
            VerifyScale();

            if (!BorderThickness.IsUniform)
                throw new InvalidDataException("BorderThickness must be uniform");

            double borderThickness = BorderThickness.Left;

            _layoutThickness = new Thickness(borderThickness);

            return _layoutThickness.Value;
        }
    }

    /// <summary>
    /// Initializes static members of the <see cref="BeveledBorder"/> class.
    /// </summary>
    static BeveledBorder()
    {
        AffectsRender<BeveledBorder>(
            BackgroundProperty,
            BorderBrushProperty,
            BorderThicknessProperty,
            CornerRadiusProperty);
        AffectsMeasure<BeveledBorder>(
            BorderThicknessProperty,
            CornerRadiusProperty);
    }

    public bool HitTest(Point p)
    {
        return Bounds.Contains(p);
    }

    private void VerifyScale()
    {
        double currentScale = LayoutHelper.GetLayoutScale(this);
        if (MathUtilities.AreClose(currentScale, _scale))
            return;

        _scale = currentScale;
        _layoutThickness = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        PseudoClasses.Add(":pressed");
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        PseudoClasses.Remove(":pressed");
    }

    /// <summary>
    /// Measures the control.
    /// </summary>
    /// <param name="availableSize">The available size.</param>
    /// <returns>The desired size of the control.</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        return LayoutHelper.MeasureChild(Child, availableSize, Padding, LayoutThickness);
    }

    /// <summary>
    /// Arranges the control's child.
    /// </summary>
    /// <param name="finalSize">The size allocated to the control.</param>
    /// <returns>The space taken.</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        return LayoutHelper.ArrangeChild(Child, finalSize, Padding, LayoutThickness);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double maxRadius = Math.Min(Bounds.Width, Bounds.Height);

        CornerRadius clampedBevel = new CornerRadius(
            Math.Min(CornerRadius.TopLeft, maxRadius),
            Math.Min(CornerRadius.TopRight, maxRadius),
            Math.Min(CornerRadius.BottomRight, maxRadius),
            Math.Min(CornerRadius.BottomLeft, maxRadius));

        var geometry = new StreamGeometry();

        double thickness = BorderThickness.Left;

        var pen = new Pen(BorderBrush, thickness);

        var bounds = new Rect(thickness / 2, thickness / 2,
            Bounds.Width - thickness,
            Bounds.Height - thickness);

        /* 
             /A-----------B\
            H               C
            |    content    |
            G               D
             \F-----------E/
        */
        // Points C, E, G, and H are optional depending on the Size of the bezel

        using (var contextGeometry = geometry.Open())
        {
            var pointA = new Point(bounds.Left + clampedBevel.TopLeft, bounds.Top);
            contextGeometry.BeginFigure(pointA, true);
            
            var pointB = new Point(bounds.Right - clampedBevel.TopRight, bounds.Top);
            contextGeometry.LineTo(pointB);

            if (clampedBevel.TopRight != 0)
            {
                var pointC = new Point(bounds.Right, bounds.Top + clampedBevel.TopRight);
                contextGeometry.LineTo(pointC);
            }

            var pointD = new Point(bounds.Right, bounds.Bottom - clampedBevel.BottomRight);
            contextGeometry.LineTo(pointD);
            if (clampedBevel.BottomRight != 0)
            {
                var pointE = new Point(bounds.Right - clampedBevel.BottomRight, bounds.Bottom);
                contextGeometry.LineTo(pointE);
            }

            var pointF = new Point(bounds.Left + clampedBevel.BottomLeft, bounds.Bottom);
            contextGeometry.LineTo(pointF);

            if (clampedBevel.BottomLeft != 0)
            {
                var pointG = new Point(bounds.Left, bounds.Bottom - clampedBevel.BottomLeft);
                contextGeometry.LineTo(pointG);
            }

            if (clampedBevel.TopLeft != 0)
            {
                var pointH = new Point(bounds.Left, bounds.Top + clampedBevel.TopLeft);
                contextGeometry.LineTo(pointH);
            }

            contextGeometry.EndFigure(true);
        }

        context.DrawGeometry(Background, pen, geometry);
    }
}