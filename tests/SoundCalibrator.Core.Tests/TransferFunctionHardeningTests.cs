using System;
using SoundCalibrator.Core.DSP;
using SoundCalibrator.Core.Models;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class TransferFunctionHardeningTests
{
    [Fact]
    public void HighResolutionFft_16384_RunsStablyAndAccurately()
    {
        const int bigFft = 16384;
        var calculator = new TransferFunctionCalculator(bigFft, WindowType.BlackmanHarris);
        var refSig = new float[bigFft];
        var measSig = new float[bigFft];
        var result = new TransferFunctionResult(bigFft);

        // Señal senoidal a 440 Hz
        for (int i = 0; i < bigFft; i++)
        {
            float t = (float)i / 48000f;
            refSig[i] = MathF.Sin(2f * MathF.PI * 440f * t);
            measSig[i] = refSig[i];
        }

        calculator.Calculate(refSig, measSig, result);

        int bin440 = (int)MathF.Round(440f / (48000f / bigFft));
        Assert.InRange(result.MagnitudeDb[bin440], -0.1f, 0.1f);
        Assert.InRange(result.Coherence[bin440], 0.99f, 1.0f);
    }

    [Fact]
    public void RandomNoiseAndClipping_NeverProducesNaNEvenUnderExtremeStress()
    {
        const int fftSize = 2048;
        var calculator = new TransferFunctionCalculator(fftSize, WindowType.Hann);
        var refSig = new float[fftSize];
        var measSig = new float[fftSize];
        var result = new TransferFunctionResult(fftSize);

        var rng = new Random(42);
        for (int i = 0; i < fftSize; i++)
        {
            // Ruido aleatorio con clipping severo (+/- 5.0 saturado)
            refSig[i] = Math.Clamp((float)(rng.NextDouble() * 10.0 - 5.0), -1.0f, 1.0f);
            measSig[i] = Math.Clamp((float)(rng.NextDouble() * 10.0 - 5.0), -1.0f, 1.0f);
        }

        calculator.Calculate(refSig, measSig, result);

        for (int i = 0; i < result.BinCount; i++)
        {
            Assert.False(float.IsNaN(result.MagnitudeDb[i]));
            Assert.False(float.IsInfinity(result.MagnitudeDb[i]));
            Assert.False(float.IsNaN(result.PhaseDegrees[i]));
            Assert.InRange(result.Coherence[i], 0.0f, 1.0f);
        }
    }
}
