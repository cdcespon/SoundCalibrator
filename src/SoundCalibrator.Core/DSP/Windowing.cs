using System;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.DSP;

public static class Windowing
{
    public static float[] Create(WindowType type, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        var window = new float[length];
        Fill(type, window);
        return window;
    }

    public static void Fill(WindowType type, Span<float> destination)
    {
        int n = destination.Length;
        if (n == 0) return;

        if (type == WindowType.Rectangular)
        {
            destination.Fill(1.0f);
            return;
        }

        float factor = 2.0f * MathF.PI / (n - 1);

        for (int i = 0; i < n; i++)
        {
            destination[i] = type switch
            {
                WindowType.Hann => 0.5f * (1.0f - MathF.Cos(factor * i)),
                WindowType.BlackmanHarris => 
                    0.35875f 
                    - 0.48829f * MathF.Cos(factor * i) 
                    + 0.14128f * MathF.Cos(2f * factor * i) 
                    - 0.01168f * MathF.Cos(3f * factor * i),
                _ => 1.0f
            };
        }
    }

    public static void Apply(ReadOnlySpan<float> source, ReadOnlySpan<float> window, Span<float> destination)
    {
        int length = Math.Min(source.Length, Math.Min(window.Length, destination.Length));
        for (int i = 0; i < length; i++)
        {
            destination[i] = source[i] * window[i];
        }
    }
}
