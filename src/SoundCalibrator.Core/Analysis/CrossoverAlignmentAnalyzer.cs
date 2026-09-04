using System;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.Analysis;

public readonly record struct AlignmentSuggestion(
    float CrossoverFreqHz,
    float PhaseSubDeg,
    float PhaseMainDeg,
    float PhaseDeltaDeg,
    float RecommendedDelayMs,
    float RecommendedDistanceMeters,
    bool RecommendPolarityInversion,
    float PredictedSummationGainDb);

public static class CrossoverAlignmentAnalyzer
{
    public static AlignmentSuggestion Analyze(
        AcousticTrace subTrace,
        AcousticTrace mainTrace,
        float crossoverFreqHz = 80f,
        float speedOfSound = 343f)
    {
        float phaseSub = InterpolatePhaseAt(subTrace, crossoverFreqHz);
        float phaseMain = InterpolatePhaseAt(mainTrace, crossoverFreqHz);

        // Diferencia de fase normalizada a [-180, +180]
        float deltaDeg = NormalizeDegrees(phaseMain - phaseSub);

        // Si el desfase es mayor a 120 grados, invertir polaridad produce un acople mucho más directo
        bool recommendInvert = MathF.Abs(deltaDeg) > 120f;
        float effectiveDelta = recommendInvert 
            ? NormalizeDegrees(deltaDeg + 180f) 
            : deltaDeg;

        // Retardo para corregir el desfase: t = delta / (360 * fc)
        float delaySec = effectiveDelta / (360f * crossoverFreqHz);
        float delayMs = delaySec * 1000f;
        float distMeters = delaySec * speedOfSound;

        // Ganancia de suma acústica antes de cualquier corrección:
        // Sum = 20 * log10( 2 * cos(delta / 2) )
        float deltaRad = deltaDeg * MathF.PI / 180f;
        float cosVal = MathF.Abs(MathF.Cos(deltaRad * 0.5f));
        float sumGainDb = cosVal < 0.01f ? -40f : 20f * MathF.Log10(MathF.Max(0.001f, 2f * cosVal));

        return new AlignmentSuggestion(
            crossoverFreqHz,
            phaseSub,
            phaseMain,
            deltaDeg,
            delayMs,
            distMeters,
            recommendInvert,
            sumGainDb);
    }

    private static float InterpolatePhaseAt(AcousticTrace trace, float freq)
    {
        int count = trace.Frequencies.Length;
        if (count == 0) return 0f;
        if (freq <= trace.Frequencies[0])
        {
            trace.GetDisplayValues(0, out _, out float p, out _);
            return p;
        }
        if (freq >= trace.Frequencies[^1])
        {
            trace.GetDisplayValues(count - 1, out _, out float p, out _);
            return p;
        }

        for (int i = 0; i < count - 1; i++)
        {
            float f0 = trace.Frequencies[i];
            float f1 = trace.Frequencies[i + 1];

            if (freq >= f0 && freq <= f1)
            {
                trace.GetDisplayValues(i, out _, out float p0, out _);
                trace.GetDisplayValues(i + 1, out _, out float p1, out _);

                // Unwrapped delta
                float diff = NormalizeDegrees(p1 - p0);
                float t = (freq - f0) / (f1 - f0);
                return NormalizeDegrees(p0 + t * diff);
            }
        }

        trace.GetDisplayValues(0, out _, out float fallback, out _);
        return fallback;
    }

    private static float NormalizeDegrees(float deg)
    {
        return ((deg + 180f) % 360f + 360f) % 360f - 180f;
    }
}
