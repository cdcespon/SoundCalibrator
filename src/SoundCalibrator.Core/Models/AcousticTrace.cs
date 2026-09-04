using System;

namespace SoundCalibrator.Core.Models;

public sealed class AcousticTrace
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; }
    public string HexColor { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool InvertPolarity { get; set; } = false;
    public float OffsetDb { get; set; } = 0f;
    public float OffsetDelayMs { get; set; } = 0f;

    public float[] Frequencies { get; }
    public float[] MagnitudeDb { get; }
    public float[] PhaseDegrees { get; }
    public float[] Coherence { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public AcousticTrace(string name, string hexColor, float[] freqs, float[] magDb, float[] phaseDeg, float[] coh)
    {
        Name = name;
        HexColor = hexColor;
        Frequencies = (float[])freqs.Clone();
        MagnitudeDb = (float[])magDb.Clone();
        PhaseDegrees = (float[])phaseDeg.Clone();
        Coherence = (float[])coh.Clone();
    }

    public void GetDisplayValues(int index, out float mag, out float phase, out float coh)
    {
        mag = MagnitudeDb[index] + OffsetDb;
        phase = PhaseDegrees[index];

        if (InvertPolarity)
        {
            phase = ((phase + 180f) + 180f) % 360f - 180f;
        }

        if (MathF.Abs(OffsetDelayMs) > 0.001f)
        {
            float f = Frequencies[index];
            float delta = 360f * f * (OffsetDelayMs / 1000f);
            phase = ((phase + delta + 180f) % 360f + 360f) % 360f - 180f;
        }

        coh = Coherence[index];
    }
}
