using System;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class ReverberationTimeTests
{
    [Fact]
    public void SimulatedExponentialDecay_AccuratelyCalculatesRt60()
    {
        // Generar una respuesta al impulso sintética con decaimiento exponencial conocido:
        // h(t) = e^(-t / tau), con RT60 = 1.0 segundo.
        // En 1 segundo el nivel debe caer 60 dB (factor 10^-3 en presión).
        // e^(-1 / tau) = 10^-3 => -1/tau = ln(10^-3) = -6.9077 => tau = 1 / 6.9077 = 0.14476 s.
        int sampleRate = 48000;
        int totalSamples = 48000 * 2; // 2 segundos
        float[] ir = new float[totalSamples];
        float tau = 1.0f / 6.907755f;

        var rand = new Random(42);
        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = MathF.Exp(-t / tau);
            float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
            ir[i] = envelope * noise;
        }

        var result = ReverberationTimeCalculator.Calculate(ir, sampleRate);

        Assert.True(result.IsValid, "Reverberation calculation must be valid for clean synthetic IR");
        Assert.InRange(result.EdtSeconds, 0.90f, 1.10f);
        Assert.InRange(result.T20Seconds, 0.90f, 1.10f);
        Assert.InRange(result.T30Seconds, 0.90f, 1.10f);
    }

    [Fact]
    public void SilentImpulseResponse_ReturnsInvalidResult()
    {
        float[] silentIr = new float[4800];
        var result = ReverberationTimeCalculator.Calculate(silentIr, 48000);

        Assert.False(result.IsValid);
        Assert.Equal(0f, result.T20Seconds);
    }
}
