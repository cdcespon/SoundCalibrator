using System;
using SoundCalibrator.Core.Analysis;
using SoundCalibrator.Core.Models;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class CrossoverAlignmentTests
{
    [Fact]
    public void PerfectlyInPhase_Predicts6DbSummationAndZeroDelay()
    {
        float[] freqs = [60f, 80f, 100f];
        float[] mag = [0f, 0f, 0f];
        float[] phaseSub = [-30f, 0f, 30f];
        float[] phaseMain = [-30f, 0f, 30f];
        float[] coh = [1f, 1f, 1f];

        var sub = new AcousticTrace("Sub", "#FF0000", freqs, mag, phaseSub, coh);
        var main = new AcousticTrace("Main", "#00FF00", freqs, mag, phaseMain, coh);

        var result = CrossoverAlignmentAnalyzer.Analyze(sub, main, crossoverFreqHz: 80f);

        Assert.Equal(0f, result.PhaseDeltaDeg, 0.5f);
        Assert.Equal(0f, result.RecommendedDelayMs, 0.05f);
        Assert.False(result.RecommendPolarityInversion);
        Assert.Equal(6.02f, result.PredictedSummationGainDb, 0.1f);
    }

    [Fact]
    public void ReversePolarity180Deg_RecommendsPolarityInvertAndDetectsCancellation()
    {
        float[] freqs = [60f, 80f, 100f];
        float[] mag = [0f, 0f, 0f];
        float[] phaseSub = [0f, 0f, 0f];
        float[] phaseMain = [180f, 180f, 180f];
        float[] coh = [1f, 1f, 1f];

        var sub = new AcousticTrace("Sub", "#FF0000", freqs, mag, phaseSub, coh);
        var main = new AcousticTrace("Main", "#00FF00", freqs, mag, phaseMain, coh);

        var result = CrossoverAlignmentAnalyzer.Analyze(sub, main, crossoverFreqHz: 80f);

        Assert.True(result.RecommendPolarityInversion, "Should recommend polarity inversion for 180 deg offset");
        Assert.True(result.PredictedSummationGainDb < -15f, "Summation should cancel out deeply");
    }

    [Fact]
    public void QuarterCycleOffset90Deg_ComputesExactDelayAdjustment()
    {
        float[] freqs = [80f];
        float[] mag = [0f];
        // At 80 Hz, a full cycle is 1000/80 = 12.5 ms.
        // 90 deg difference corresponds to 12.5 / 4 = 3.125 ms.
        float[] phaseSub = [0f];
        float[] phaseMain = [90f];
        float[] coh = [1f];

        var sub = new AcousticTrace("Sub", "#FF0000", freqs, mag, phaseSub, coh);
        var main = new AcousticTrace("Main", "#00FF00", freqs, mag, phaseMain, coh);

        var result = CrossoverAlignmentAnalyzer.Analyze(sub, main, crossoverFreqHz: 80f);

        Assert.Equal(90f, result.PhaseDeltaDeg, 0.5f);
        Assert.InRange(result.RecommendedDelayMs, 3.0f, 3.25f);
        Assert.InRange(result.PredictedSummationGainDb, 2.9f, 3.1f); // 90 deg sum is +3.01 dB
    }
}
