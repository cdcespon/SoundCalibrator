using System;
using SoundCalibrator.Core.Analysis;
using SoundCalibrator.Core.Models;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class AcousticSummationSimulatorTests
{
    [Fact]
    public void SimulateSummation_TwoIdenticalInPhaseTraces_SumsToPlus6dB()
    {
        float[] freqs = [100f, 1000f];
        float[] mags = [0f, 0f];
        float[] phases = [0f, 0f];
        float[] coh = [1f, 1f];

        var t1 = new AcousticTrace("Source 1", "#FF0000", freqs, mags, phases, coh);
        var t2 = new AcousticTrace("Source 2", "#00FF00", freqs, mags, phases, coh);

        var sum = AcousticSummationSimulator.SimulateSummation([t1, t2]);

        Assert.NotNull(sum);
        // 20 * log10(1 + 1) = 6.0206 dB
        Assert.Equal(6.02f, sum.MagnitudeDb[0], precision: 2);
        Assert.Equal(6.02f, sum.MagnitudeDb[1], precision: 2);
        Assert.Equal(0f, sum.PhaseDegrees[0], precision: 2);
    }

    [Fact]
    public void SimulateSummation_TwoTracesAt90Degrees_SumsToPlus3dB()
    {
        float[] freqs = [1000f];
        float[] mags = [0f];
        float[] phase1 = [0f];
        float[] phase2 = [90f];
        float[] coh = [1f];

        var t1 = new AcousticTrace("Source 1", "#FF0000", freqs, mags, phase1, coh);
        var t2 = new AcousticTrace("Source 2", "#00FF00", freqs, mags, phase2, coh);

        var sum = AcousticSummationSimulator.SimulateSummation([t1, t2]);

        Assert.NotNull(sum);
        // sqrt(1^2 + 1^2) = sqrt(2) => 20 * log10(sqrt(2)) = 3.0103 dB
        Assert.Equal(3.01f, sum.MagnitudeDb[0], precision: 2);
        Assert.Equal(45.0f, sum.PhaseDegrees[0], precision: 1);
    }

    [Fact]
    public void SimulateSummation_WithInvertPolarity_CancelsCompletely()
    {
        float[] freqs = [1000f];
        float[] mags = [0f];
        float[] phases = [0f];
        float[] coh = [1f];

        var t1 = new AcousticTrace("Source 1", "#FF0000", freqs, mags, phases, coh);
        var t2 = new AcousticTrace("Source 2", "#00FF00", freqs, mags, phases, coh)
        {
            InvertPolarity = true
        };

        var sum = AcousticSummationSimulator.SimulateSummation([t1, t2]);

        Assert.NotNull(sum);
        Assert.True(sum.MagnitudeDb[0] < -80f, $"Magnitude should cancel, was {sum.MagnitudeDb[0]}");
    }

    [Fact]
    public void SimulateSummation_DelayOffset_CreatesCombFilterNotch()
    {
        // Con retardo de 5 ms, la primera cancelación destructiva (notch) ocurre en f = 1 / (2 * 0.005) = 100 Hz
        float[] freqs = [100f, 200f]; // 100 Hz es notch (180 deg delta), 200 Hz es pico constructivo (360 deg delta)
        float[] mags = [0f, 0f];
        float[] phases = [0f, 0f];
        float[] coh = [1f, 1f];

        var t1 = new AcousticTrace("Source 1", "#FF0000", freqs, mags, phases, coh);
        var t2 = new AcousticTrace("Source 2", "#00FF00", freqs, mags, phases, coh)
        {
            OffsetDelayMs = 5.0f // 5 ms de retardo
        };

        var sum = AcousticSummationSimulator.SimulateSummation([t1, t2]);

        Assert.NotNull(sum);
        // En 100 Hz debe haber cancelación destructiva profunda
        Assert.True(sum.MagnitudeDb[0] < -50f, $"100 Hz should be a comb notch, was {sum.MagnitudeDb[0]}");
        // En 200 Hz debe haber suma constructiva (+6 dB)
        Assert.Equal(6.02f, sum.MagnitudeDb[1], precision: 2);
    }
}
