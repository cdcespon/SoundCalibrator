using System;
using SoundCalibrator.Core.Analysis;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class FeedbackHunterTests
{
    [Fact]
    public void NarrowScreamingFeedback_IsIdentifiedAccurately()
    {
        // Espectro de 1000 puntos entre 20 Hz y 20 kHz (paso 20 Hz)
        int bins = 1000;
        float[] freqs = new float[bins];
        float[] mag = new float[bins];
        Array.Fill(mag, -45.0f); // Piso de ruido/música en -45 dBFS

        for (int i = 0; i < bins; i++) freqs[i] = i * 20f;

        // Feedback agudo en 3140 Hz (index 157): pico estrecho a -12 dBFS (prominencia de +33 dB)
        mag[156] = -25f;
        mag[157] = -12f;
        mag[158] = -26f;

        var feedbacks = FeedbackHunter.Detect(freqs, mag, prominenceThresholdDb: 12.0f, minQ: 5.0f);

        Assert.NotEmpty(feedbacks);
        var primary = feedbacks[0];

        Assert.Equal(3140f, primary.FrequencyHz, 30f);
        Assert.Equal(-12f, primary.LevelDb, 1.0f);
        Assert.True(primary.ProminenceDb > 20f, $"Prominencia detectada ({primary.ProminenceDb} dB) debe ser > 20 dB");
        Assert.True(primary.Q >= 5.0f, $"Q ({primary.Q}) debe indicar pico resonante estrecho");
    }

    [Fact]
    public void BroadVocalFormant_IsNotClassifiedAsFeedback()
    {
        // Un formante vocal ancho (baja Q ~ 1.5) no debe ser catalogado como acople de feedback
        int bins = 1000;
        float[] freqs = new float[bins];
        float[] mag = new float[bins];
        Array.Fill(mag, -50.0f);

        for (int i = 0; i < bins; i++) freqs[i] = i * 20f;

        // Formante ancho en 1000 Hz (ancho de 400 Hz => Q ~ 2.5)
        for (int i = 40; i <= 60; i++)
        {
            float df = freqs[i] - 1000f;
            mag[i] = -30f + 15.0f * (1.0f - (df * df) / 40000f);
        }

        var feedbacks = FeedbackHunter.Detect(freqs, mag, prominenceThresholdDb: 12.0f, minQ: 6.0f);

        Assert.Empty(feedbacks); // Q < 6.0 => no es feedback
    }
}
