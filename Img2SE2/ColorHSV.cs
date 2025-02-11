using System;
using System.Drawing;
using SixLabors.ImageSharp.PixelFormats;

namespace Img2SE2;

public class ColorHSV
{
    public float Hue { get; private set; } // Hue in the range [0, 1]
    public float Saturation { get; private set; } // Saturation in the range [0, 1]
    public float Value { get; private set; } // Value in the range [0, 1]
    public static ColorHSV White { get; set; } = new ColorHSV(new Rgba32(255, 255, 255, 255));

    // Constructor that accepts a System.Drawing.Color
    public ColorHSV(Rgba32 color)
    {
        // Convert the RGB color to HSV
        FromRgb(color.R, color.G, color.B);
    }

    // Method to convert RGB to HSV
    private void FromRgb(int r, int g, int b)
    {
        float rNorm = r / 255f;
        float gNorm = g / 255f;
        float bNorm = b / 255f;

        float max = Math.Max(rNorm, Math.Max(gNorm, bNorm));
        float min = Math.Min(rNorm, Math.Min(gNorm, bNorm));
        float delta = max - min;

        // Calculate Hue
        if (delta == 0)
        {
            Hue = 0; // Undefined hue
        }
        else if (max == rNorm)
        {
            Hue = 60 * (((gNorm - bNorm) / delta) % 6);
        }
        else if (max == gNorm)
        {
            Hue = 60 * (((bNorm - rNorm) / delta) + 2);
        }
        else // max == bNorm
        {
            Hue = 60 * (((rNorm - gNorm) / delta) + 4);
        }

        if (Hue < 0)
        {
            Hue += 360;
        }

        // Normalize Hue to [0, 1]
        Hue /= 360;

        // Calculate Saturation
        Saturation = max == 0 ? 0 : (delta / max);

        // Calculate Value
        Value = max;
    }

    public override string ToString()
    {
        return $"HSV({Hue:F2}, {Saturation:F2}, {Value:F2})";
    }
}