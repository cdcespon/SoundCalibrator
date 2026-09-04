using System;
using SoundCalibrator.Core.DSP;
using SoundCalibrator.Core.Models;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class RtaCalculatorTests
{
    [Fact]
    public void Calculate_SineTone_ProducesPeakNearZeroDbFs()
    {
        const int fftSize = 1024;
        const float sampleRate = 48000f;
        var rta = new RtaCalculator(fftSize, WindowType.Hann);

        float[] signal = new float[fftSize];
        for (int i = 0; i < fftSize; i++)
        {
            signal[i] = MathF.Sin(2f * MathF.PI * 1000f * i / sampleRate);
        }

        float[] rtaDb = new float[rta.BinCount];
        float[] maxHoldDb = new float[rta.BinCount];

        rta.Calculate(signal, rtaDb, maxHoldDb);

        int bin1k = (int)MathF.Round(1000f / (sampleRate / fftSize));

        // Para seno amplitud 1.0 con ventana Hann, el pico principal está en torno a -6 dBFS (o 0 con corrección)
        Assert.InRange(rtaDb[bin1k], -8.0f, 0.5f);
        Assert.Equal(rtaDb[bin1k], maxHoldDb[bin1k]);
    }

    [Fact]
    public void Calculate_MaxHold_RetainsPeakWhenSignalDrops()
    {
        const int fftSize = 1024;
        var rta = new RtaCalculator(fftSize);

        float[] strongSignal = new float[fftSize];
        for (int i = 0; i < fftSize; i++)
        {
            strongSignal[i] = 1.0f * MathF.Sin(2f * MathF.PI * 500f * i / 48000f);
        }

        float[] rtaDb = new float[rta.BinCount];
        float[] maxHoldDb = new float[rta.BinCount];

        // Paso 1: Señal fuerte
        rta.Calculate(strongSignal, rtaDb, maxHoldDb);
        int bin500 = (int)MathF.Round(500f / (48000f / fftSize));
        float peakFirst = maxHoldDb[bin500];

        // Paso 2: Señal débil (silencio)
        float[] weakSignal = new float[fftSize];
        rta.Calculate(weakSignal, rtaDb, maxHoldDb);

        // RTA debe haber caído
        Assert.True(rtaDb[bin500] < peakFirst - 20f);
        // Max Hold debe preservar el valor previo
        Assert.Equal(peakFirst, maxHoldDb[bin500]);
    }
}
