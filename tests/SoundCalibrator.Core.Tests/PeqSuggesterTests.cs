using System;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class PeqSuggesterTests
{
    [Fact]
    public void IsolatedPeak_SuggestsAccurateCorrectiveCutFilter()
    {
        // Generar un espectro con un pico resonante en 260 Hz (+6 dB de error)
        int bins = 500;
        float[] freqs = new float[bins];
        float[] delta = new float[bins];

        for (int i = 0; i < bins; i++)
        {
            float f = 20f + i * 20f;
            freqs[i] = f;

            float df = f - 260f;
            delta[i] = 6.0f / (1.0f + (df * df) / 400f); // Pico de +6 dB
        }

        var filters = PeqSuggester.SuggestFilters(freqs, delta, maxFilters: 3, minDbThreshold: 2.0f);

        Assert.NotEmpty(filters);
        var primary = filters[0];

        Assert.Equal(260f, primary.FrequencyHz, 5f);
        Assert.InRange(primary.GainDb, -6.2f, -5.8f);
        Assert.True(primary.Q > 0.5f && primary.Q < 20f, $"Q debe ser razonable, obtenido: {primary.Q}");
    }

    [Fact]
    public void FlatResponse_SuggestsZeroFilters()
    {
        float[] freqs = [100f, 200f, 500f, 1000f, 2000f];
        float[] delta = [0.2f, -0.1f, 0.4f, -0.3f, 0.0f];

        var filters = PeqSuggester.SuggestFilters(freqs, delta, maxFilters: 5, minDbThreshold: 2.0f);

        Assert.Empty(filters);
    }
}
