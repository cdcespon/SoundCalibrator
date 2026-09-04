using System;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class SpeechIntelligibilityCalculatorTests
{
    [Fact]
    public void CalculateFromRt60AndSnr_AcousticallyTreatedRoom_ReturnsExcellent()
    {
        // Sala de conferencia con RT60 = 0.3s y SNR = 35dB
        var res = SpeechIntelligibilityCalculator.CalculateFromRt60AndSnr(rt60Seconds: 0.3f, snrDb: 35.0f);

        Assert.True(res.Sti >= 0.80f, $"Expected STI >= 0.80, got {res.Sti}");
        Assert.True(res.AlConsPercent < 3.0f, $"Expected %ALCons < 3%, got {res.AlConsPercent}");
        Assert.Equal("Excellent", res.Rating);
    }

    [Fact]
    public void CalculateFromRt60AndSnr_ReverberantCathedral_ReturnsPoorOrBad()
    {
        // Catedral cavernosa con RT60 = 4.0s y ruido de fondo (SNR = 10dB)
        var res = SpeechIntelligibilityCalculator.CalculateFromRt60AndSnr(rt60Seconds: 4.0f, snrDb: 10.0f);

        Assert.True(res.Sti <= 0.40f, $"Expected STI <= 0.40, got {res.Sti}");
        Assert.True(res.AlConsPercent >= 15.0f, $"Expected %ALCons >= 15%, got {res.AlConsPercent}");
        Assert.True(res.Rating == "Poor" || res.Rating == "Bad");
    }

    [Fact]
    public void CalculateFromRt60AndSnr_ModerateAuditorium_ReturnsGoodOrFair()
    {
        // Auditorio típico con RT60 = 0.9s y SNR = 25dB
        var res = SpeechIntelligibilityCalculator.CalculateFromRt60AndSnr(rt60Seconds: 0.9f, snrDb: 25.0f);

        Assert.InRange(res.Sti, 0.55f, 0.75f);
        Assert.True(res.Rating == "Good" || res.Rating == "Fair");
    }

    [Fact]
    public void CalculateFromImpulseResponse_SyntheticDecay_ReturnsConsistentResult()
    {
        int sampleRate = 48000;
        int samples = sampleRate * 1; // 1 segundo
        float[] ir = new float[samples];

        // Decaimiento exponencial correspondiente a RT60 aprox 0.5s
        // tau = 0.5 / ln(1000) = 0.5 / 6.9077 = 0.07238 s
        float tau = 0.5f / 6.907755f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            ir[i] = MathF.Exp(-t / tau);
        }

        var res = SpeechIntelligibilityCalculator.CalculateFromImpulseResponse(ir, sampleRate, snrDb: 30f);

        Assert.InRange(res.Sti, 0.65f, 0.85f);
        Assert.True(res.Rating == "Good" || res.Rating == "Excellent");
    }
}
