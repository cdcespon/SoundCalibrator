using System;
using SoundCalibrator.Core.Analysis;
using SoundCalibrator.Core.DSP;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class BiquadFilterTests
{
    [Fact]
    public void PeqFilter_PeakGainMatchesSpecifiedGain()
    {
        // PEQ Bell en 1000 Hz, Q = 2.0, Gain = +6.0 dB a 48 kHz
        var filter = BiquadFilter.CreatePeq(1000f, 6.0f, 2.0f, 48000f);

        float gainCenter = filter.EvaluateDb(1000f);
        float gainFarLow = filter.EvaluateDb(50f);
        float gainFarHigh = filter.EvaluateDb(15000f);

        Assert.InRange(gainCenter, 5.9f, 6.1f);
        Assert.InRange(gainFarLow, -0.1f, 0.1f);
        Assert.InRange(gainFarHigh, -0.1f, 0.1f);
    }

    [Fact]
    public void EvaluateCascade_SumsGainsOfMultipleFilters()
    {
        var filters = new[]
        {
            new PeqFilterSuggestion(200f, 3.0f, 1.4f, 1.0f),
            new PeqFilterSuggestion(2000f, -4.0f, 2.0f, 0.7f)
        };

        float[] freqs = [200f, 2000f];
        float[] response = new float[2];

        BiquadFilter.EvaluateCascade(filters, freqs, response, 48000f);

        Assert.InRange(response[0], 2.8f, 3.2f);
        Assert.InRange(response[1], -4.2f, -3.8f);
    }
}
