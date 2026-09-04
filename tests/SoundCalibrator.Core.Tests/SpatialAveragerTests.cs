using System;
using System.Collections.Generic;
using SoundCalibrator.Core.Averaging;
using SoundCalibrator.Core.Models;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class SpatialAveragerTests
{
    [Fact]
    public void CalculateSpatialAverage_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(SpatialAverager.CalculateSpatialAverage(null!));
        Assert.Null(SpatialAverager.CalculateSpatialAverage(Array.Empty<AcousticTrace>()));
    }

    [Fact]
    public void CalculateSpatialAverage_SingleTrace_ReturnsClonedValues()
    {
        float[] freqs = [100f, 1000f, 10000f];
        float[] mag = [-3f, 0f, 3f];
        float[] phase = [45f, 0f, -45f];
        float[] coh = [0.8f, 0.95f, 0.9f];

        var trace = new AcousticTrace("Mic 1", "#FF0000", freqs, mag, phase, coh);
        var result = SpatialAverager.CalculateSpatialAverage([trace]);

        Assert.NotNull(result);
        Assert.Equal(freqs.Length, result.Frequencies.Length);
        Assert.Equal(0f, result.MagnitudeDb[1], precision: 3);
        Assert.Equal(0.95f, result.Coherence[1], precision: 3);
    }

    [Fact]
    public void CalculateSpatialAverage_TwoEqualTraces_PowerAverage_EqualsOriginal()
    {
        float[] freqs = [1000f];
        float[] mag = [-6f];
        float[] phase = [0f];
        float[] coh = [1.0f];

        var t1 = new AcousticTrace("Mic 1", "#FF0000", freqs, mag, phase, coh);
        var t2 = new AcousticTrace("Mic 2", "#00FF00", freqs, mag, phase, coh);

        var result = SpatialAverager.CalculateSpatialAverage([t1, t2], SpatialAverageMode.Power);

        Assert.NotNull(result);
        Assert.Equal(-6f, result.MagnitudeDb[0], precision: 2);
    }

    [Fact]
    public void CalculateSpatialAverage_TwoOppositePhaseTraces_ComplexVector_Cancels()
    {
        float[] freqs = [1000f];
        float[] mag = [0f];
        float[] phase1 = [0f];
        float[] phase2 = [180f];
        float[] coh = [1.0f];

        var t1 = new AcousticTrace("Mic 1", "#FF0000", freqs, mag, phase1, coh);
        var t2 = new AcousticTrace("Mic 2", "#00FF00", freqs, mag, phase2, coh);

        var result = SpatialAverager.CalculateSpatialAverage([t1, t2], SpatialAverageMode.ComplexVector);

        Assert.NotNull(result);
        Assert.True(result.MagnitudeDb[0] < -80f, $"Magnitude should cancel, was {result.MagnitudeDb[0]}");
    }

    [Fact]
    public void CalculateSpatialAverage_CoherenceWeightedPower_PrioritizesHighCoherence()
    {
        float[] freqs = [1000f];
        float[] mag1 = [0f];   // 0 dB, high coherence (0.9)
        float[] mag2 = [-20f]; // -20 dB, very low coherence (0.1)
        float[] phase = [0f];
        float[] coh1 = [0.9f];
        float[] coh2 = [0.1f];

        var t1 = new AcousticTrace("Mic 1", "#FF0000", freqs, mag1, phase, coh1);
        var t2 = new AcousticTrace("Mic 2", "#00FF00", freqs, mag2, phase, coh2);

        var result = SpatialAverager.CalculateSpatialAverage([t1, t2], SpatialAverageMode.CoherenceWeightedPower);

        Assert.NotNull(result);
        // Debe estar muy cerca de 0 dB (t1) y lejos de -20 dB (t2)
        Assert.True(result.MagnitudeDb[0] > -1.5f, $"Weighted average should be close to 0 dB, was {result.MagnitudeDb[0]}");
    }
}
