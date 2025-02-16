using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Img2SE2.Lib;


/// <summary>
/// Border with Hazard Strips
/// </summary>
public class HazardStripes : Decorator
{
#pragma warning disable KN023
    /// <summary>
    /// Defines the <see cref="Mirrored"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> MirroredProperty = 
        AvaloniaProperty.Register<HazardStripes, bool>(nameof(Mirrored));
    
    /// <summary>
    /// Defines the <see cref="Background"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush> BackgroundProperty =
        AvaloniaProperty.Register<HazardStripes, IBrush>(nameof(Background), new SolidColorBrush(Colors.Transparent));
    
    public static readonly StyledProperty<IBrush> ForegroundProperty = 
        AvaloniaProperty.Register<HazardStripes, IBrush>(nameof(Foreground), new SolidColorBrush(Colors.White));
    
    /// <summary>
    /// Defines the <see cref="LineWidth"/> property.
    /// </summary>
    public static readonly StyledProperty<double> LineWidthProperty =
        AvaloniaProperty.Register<HazardStripes, double>(nameof(LineWidth), 18);
    
    /// <summary>
    /// Defines the <see cref="Spacing"/> property.
    /// </summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<HazardStripes, double>(nameof(Spacing), 18);
        
    /// <summary>
    /// Defines the <see cref="LineAngle"/> property.
    /// </summary>
    public static readonly StyledProperty<double> LineAngleProperty =
        AvaloniaProperty.Register<HazardStripes, double>(nameof(LineAngle), 45);
#pragma warning disable KN023

    public bool Mirrored
    {
        get => GetValue(MirroredProperty);
        set => SetValue(MirroredProperty, value);
    }

    /// <summary>
    /// Gets or sets a brush with which to paint the background.
    /// </summary>
    public IBrush Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }
    
    /// <summary>
    /// Gets or sets a brush with which to paint the hazard strips.
    /// </summary>
    public IBrush Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the Width of the hazard strips.
    /// </summary>
    public double LineWidth
    {
        get => GetValue(LineWidthProperty);
        set => SetValue(LineWidthProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the angle of the hazard strips.
    /// </summary>
    public double LineAngle
    {
        get => GetValue(LineAngleProperty);
        set => SetValue(LineAngleProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the distance between the strips.
    /// </summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double height = Bounds.Height;
        double xOffset = Height * Math.Tan(LineAngle * Math.PI / 180); 
        
        var geometry = new PathGeometry();
        
        context.DrawRectangle(Background, null, new Rect(0, 0, Bounds.Width, Bounds.Height));
        
        using (var contextGeometry = geometry.Open())
        {
            double currentWidth = Mirrored ? Bounds.Width : 0;
            double step = LineWidth + Spacing;

            while ((Mirrored && currentWidth > 0) || (!Mirrored && currentWidth < Bounds.Width))
            {
                if (Mirrored)
                {
                    // Draws from Right to Left when mirrored
                    contextGeometry.BeginFigure(new Point(currentWidth, 0), true);
                    contextGeometry.LineTo(new Point(currentWidth - LineWidth, 0));
                    contextGeometry.LineTo(new Point(currentWidth - LineWidth - xOffset, height));
                    contextGeometry.LineTo(new Point(currentWidth - xOffset, height));
                    currentWidth -= step;
                }
                else
                {
                    contextGeometry.BeginFigure(new Point(currentWidth, 0), true);
                    contextGeometry.LineTo(new Point(currentWidth + LineWidth, 0));
                    contextGeometry.LineTo(new Point(currentWidth + LineWidth + xOffset, height));
                    contextGeometry.LineTo(new Point(currentWidth + xOffset, height));
                    currentWidth += step;
                }

                contextGeometry.EndFigure(true);
            }
        }

        context.DrawGeometry(Foreground, null, geometry);
    }
}