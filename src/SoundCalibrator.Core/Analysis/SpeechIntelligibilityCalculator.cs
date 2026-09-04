using System;
using SoundCalibrator.Core.Operations;

namespace SoundCalibrator.Core.Analysis;

public readonly record struct StiResult(
    float Sti,
    float AlConsPercent,
    string Rating);

/// <summary>
/// Calculador de Inteligibilidad de la Palabra Hablada (STI / %ALCons)
/// conforme a la norma internacional IEC 60268-16 (Speech Transmission Index).
/// Esencial para sistemas de evacuación por voz (PA/VA), estadios, terminales y recintos acústicos.
/// </summary>
public static class SpeechIntelligibilityCalculator
{
    // Frecuencias de modulación estándar IEC 60268-16 (14 frecuencias en Hz)
    private static readonly float[] ModulationFrequencies =
    [
        0.63f, 0.80f, 1.00f, 1.25f, 1.60f, 2.00f, 2.50f,
        3.15f, 4.00f, 5.00f, 6.30f, 8.00f, 10.00f, 12.50f
    ];

    /// <summary>
    /// Calcula el STI y %ALCons a partir del tiempo de reverberación RT60 y la relación señal-ruido SNR.
    /// Utiliza el modelo analítico de Schroeder-Houtgast para la función de transferencia de modulación (MTF).
    /// </summary>
    public static StiResult CalculateFromRt60AndSnr(float rt60Seconds, float snrDb = 30.0f)
    {
        float t60 = Math.Max(0.01f, rt60Seconds);
        float snrLin = MathF.Pow(10f, -snrDb / 10f); // 10^(-SNR/10)
        float snrFactor = 1.0f / (1.0f + snrLin);

        double sumTi = 0.0;
        int count = ModulationFrequencies.Length;

        for (int i = 0; i < count; i++)
        {
            float fMod = ModulationFrequencies[i];

            // Reducción de modulación por reverberación: m_rev = 1 / sqrt(1 + (2*pi*fMod * T60 / 13.8)^2)
            float arg = (2f * MathF.PI * fMod * t60) / 13.8f;
            float mRev = 1.0f / MathF.Sqrt(1.0f + arg * arg);

            // Modulación combinada con ruido ambiente
            float m = Math.Clamp(mRev * snrFactor, 0.0001f, 0.9999f);

            // SNR aparente (dB): SNR_app = 10 * log10(m / (1 - m))
            float snrApp = 10f * MathF.Log10(m / (1.0f - m));

            // Limitar a rango dinámico [-15 dB, +15 dB]
            float clampedSnr = Math.Clamp(snrApp, -15.0f, 15.0f);

            // Índice de transmisión TI: (SNR_app + 15) / 30
            float ti = (clampedSnr + 15.0f) / 30.0f;
            sumTi += ti;
        }

        float sti = Math.Clamp((float)(sumTi / count), 0.0f, 1.0f);

        // %ALCons según fórmula empírica de Farah / Peutz:
        // %ALCons = 170.5305 * exp(-5.419 * STI)
        float alCons = Math.Clamp(170.5305f * MathF.Exp(-5.419f * sti), 0.0f, 100.0f);

        string rating = sti switch
        {
            >= 0.75f => "Excellent",
            >= 0.60f => "Good",
            >= 0.45f => "Fair",
            >= 0.30f => "Poor",
            _ => "Bad"
        };

        return new StiResult(sti, alCons, rating);
    }

    /// <summary>
    /// Calcula el STI a partir de la respuesta al impulso temporal h(t) mediante integración de Schroeder
    /// para derivar el RT60 y posteriormente calcular el STI.
    /// </summary>
    public static StiResult CalculateFromImpulseResponse(
        ReadOnlySpan<float> impulseResponse,
        int sampleRate,
        float snrDb = 30.0f)
    {
        var rt60 = ReverberationTimeCalculator.Calculate(impulseResponse, sampleRate);
        float estimatedT60 = rt60.T20Seconds > 0.05f ? rt60.T20Seconds : (rt60.EdtSeconds > 0.05f ? rt60.EdtSeconds : 0.5f);
        return CalculateFromRt60AndSnr(estimatedT60, snrDb);
    }
}
