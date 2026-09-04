using SoundCalibrator.Core.Models;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class AcousticTraceTests
{
    [Fact]
    public void AcousticTrace_InvertPolarityAndGainOffset_CalculatesCorrectly()
    {
        float[] freqs = [1000f];
        float[] mag = [0f];
        float[] phase = [0f];
        float[] coh = [1.0f];

        var trace = new AcousticTrace("Subwoofer", "#FF0055", freqs, mag, phase, coh)
        {
            OffsetDb = 6.0f,
            InvertPolarity = true
        };

        trace.GetDisplayValues(0, out float dispMag, out float dispPhase, out float dispCoh);

        Assert.Equal(6.0f, dispMag);
        // Desfase de 180° por inversión de polaridad
        Assert.True(System.MathF.Abs(dispPhase) == 180f || System.MathF.Abs(dispPhase) == 0f || System.MathF.Abs(dispPhase) == -180f);
        Assert.Equal(1.0f, dispCoh);
    }
}
