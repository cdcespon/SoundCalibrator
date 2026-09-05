using System;
using SoundCalibrator.Core.DSP;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class FrequencyDependentWindowTests
{
    [Fact]
    public void ApplyFdw_WithDiracDelta_ProducesFlatResponse()
    {
        // Arrange: Dirac delta puro en muestra 200 de 1024
        float[] ir = new float[1024];
        ir[200] = 1.0f;

        float[] freqs = [100f, 500f, 1000f, 2000f, 5000f, 10000f];
        float[] magDb = new float[freqs.Length];

        // Act
        FrequencyDependentWindow.ApplyFdw(ir, freqs, magDb, cycles: 10.0f, sampleRate: 48000f);

        // Assert: un impulso unitario centrado debe resultar en 0 dB en todas las frecuencias
        for (int i = 0; i < magDb.Length; i++)
        {
            Assert.InRange(magDb[i], -0.1f, 0.1f);
        }
    }

    [Fact]
    public void ApplyFdw_WithReflection_EliminatesHighFrequencyCombFiltering()
    {
        // Arrange: Sonido directo en muestra 200 (amplitud 1.0)
        // y una reflexión fuerte en muestra 440 (+240 muestras = 5 ms, amplitud 0.5)
        float[] ir = new float[2048];
        ir[200] = 1.0f;
        ir[440] = 0.5f;

        // Frecuencias altas alrededor de 5 kHz (período = 0.2 ms = 9.6 muestras)
        // Con 10 ciclos, semi-ventana = 5 ciclos = 48 muestras.
        // La reflexión está a +240 muestras, por lo que queda totalmente fuera de la ventana.
        float[] highFreqs = [4900f, 5000f, 5100f, 5200f];
        float[] fdwMagDb = new float[highFreqs.Length];

        // Act
        FrequencyDependentWindow.ApplyFdw(ir, highFreqs, fdwMagDb, cycles: 10.0f, sampleRate: 48000f);

        // Assert: a alta frecuencia la reflexión está excluida, por lo que no hay ondulación de filtro peine (~0 dB)
        for (int i = 0; i < fdwMagDb.Length; i++)
        {
            Assert.InRange(fdwMagDb[i], -0.1f, 0.1f);
        }
    }

    [Fact]
    public void ApplyFdw_WithInvalidParameters_Throws()
    {
        float[] ir = new float[128];
        float[] freqs = [100f, 1000f];
        float[] magDb = new float[freqs.Length];

        Assert.Throws<ArgumentException>(() =>
            FrequencyDependentWindow.ApplyFdw(ReadOnlySpan<float>.Empty, freqs, magDb));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrequencyDependentWindow.ApplyFdw(ir, freqs, magDb, cycles: -1f));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrequencyDependentWindow.ApplyFdw(ir, freqs, magDb, sampleRate: 0f));
    }
}
