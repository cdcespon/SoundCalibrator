using System;
using SoundCalibrator.Core.Averaging;
using SoundCalibrator.Core.DSP;
using SoundCalibrator.Core.Models;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class SpectralAveragerTests
{
    private const int FftSize = 1024;
    private const float SampleRate = 48000f;

    [Fact]
    public void Averaging_WithConsistentSignal_MaintainsUnityCoherence()
    {
        var calculator = new TransferFunctionCalculator(FftSize, WindowType.Hann);
        var averager = new SpectralAverager(FftSize) { Mode = AveragingType.ExponentialFast };
        var result = new TransferFunctionResult(FftSize);

        var refSignal = new float[FftSize];
        var measSignal = new float[FftSize];

        for (int frame = 0; frame < 10; frame++)
        {
            for (int i = 0; i < FftSize; i++)
            {
                float t = (float)(frame * FftSize + i) / SampleRate;
                refSignal[i] = MathF.Sin(2f * MathF.PI * 1000f * t);
                measSignal[i] = refSignal[i]; // Loopback perfecto
            }

            calculator.Calculate(refSignal, measSignal, averager, result);
        }

        int bin1k = (int)MathF.Round(1000f / (SampleRate / FftSize));
        Assert.InRange(result.MagnitudeDb[bin1k], -0.1f, 0.1f);
        Assert.InRange(result.Coherence[bin1k], 0.99f, 1.0f);
    }

    [Fact]
    public void Averaging_WithUncorrelatedNoise_ReducesCoherence()
    {
        var calculator = new TransferFunctionCalculator(FftSize, WindowType.Hann);
        var averager = new SpectralAverager(FftSize) { Mode = AveragingType.ExponentialSlow };
        var result = new TransferFunctionResult(FftSize);

        var refSignal = new float[FftSize];
        var measSignal = new float[FftSize];
        var rng = new Random(42);

        // Alimentar 40 frames donde ref es ruido blanco y meas es ruido blanco totalmente no correlacionado
        for (int frame = 0; frame < 40; frame++)
        {
            for (int i = 0; i < FftSize; i++)
            {
                refSignal[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
                measSignal[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            }

            calculator.Calculate(refSignal, measSignal, averager, result);
        }

        // Para señales no correlacionadas promediadas, la coherencia en casi todos los bins debe caer cerca de 0 (< 0.25)
        float avgCoherence = 0f;
        for (int k = 10; k < result.BinCount - 10; k++)
        {
            avgCoherence += result.Coherence[k];
        }
        avgCoherence /= (result.BinCount - 20);

        Assert.True(avgCoherence < 0.25f, $"Expected average coherence of uncorrelated noise to be < 0.25, got {avgCoherence}");
    }
}
