using System;
using SoundCalibrator.Core.DSP;

namespace SoundCalibrator.Core.Analysis;

/// <summary>
/// Calculador de Retardo de Grupo (Group Delay) y Retardo de Fase (Phase Delay).
/// Tau_g(f) = - (1 / 360) * (dPhase / df) [en milisegundos].
/// Esencial para evaluar dispersión temporal, alineación de vías y resonancias de sintonía bass-reflex.
/// </summary>
public static class GroupDelayCalculator
{
    /// <summary>
    /// Calcula el retardo de grupo en milisegundos a partir de frecuencias y fase desenvuelta (unwrapped).
    /// Si la fase suministrada está envuelta (-180 a +180), primero se desenvuelve internamente.
    /// </summary>
    public static void CalculateGroupDelayMs(
        ReadOnlySpan<float> frequencies,
        ReadOnlySpan<float> phaseDeg,
        Span<float> destinationGroupDelayMs,
        bool isAlreadyUnwrapped = false)
    {
        int count = Math.Min(frequencies.Length, Math.Min(phaseDeg.Length, destinationGroupDelayMs.Length));
        if (count == 0) return;

        if (count < 2)
        {
            destinationGroupDelayMs[0] = 0f;
            return;
        }

        Span<float> unwrappedPhase = stackalloc float[count <= 1024 ? count : 0];
        float[]? rentedArray = null;

        if (count > 1024)
        {
            rentedArray = System.Buffers.ArrayPool<float>.Shared.Rent(count);
            unwrappedPhase = rentedArray.AsSpan(0, count);
        }

        try
        {
            if (isAlreadyUnwrapped)
            {
                phaseDeg[..count].CopyTo(unwrappedPhase);
            }
            else
            {
                PhaseUnwrapper.UnwrapDegrees(phaseDeg[..count], unwrappedPhase);
            }

            // Diferenciación numérica central: -1/360 * (dPhase / df) * 1000 [ms]
            // = - (1000.0 / 360.0) * dPhase / df = - (25.0 / 9.0) * dPhase / df
            const float ScaleFactor = -1000.0f / 360.0f;

            // Primer punto (diferencia hacia adelante)
            float df0 = frequencies[1] - frequencies[0];
            if (df0 > 1e-6f)
            {
                float dphi0 = unwrappedPhase[1] - unwrappedPhase[0];
                destinationGroupDelayMs[0] = ScaleFactor * (dphi0 / df0);
            }
            else
            {
                destinationGroupDelayMs[0] = 0f;
            }

            // Puntos interiores (diferencia central para máxima precisión)
            for (int i = 1; i < count - 1; i++)
            {
                float df = frequencies[i + 1] - frequencies[i - 1];
                if (df > 1e-6f)
                {
                    float dphi = unwrappedPhase[i + 1] - unwrappedPhase[i - 1];
                    destinationGroupDelayMs[i] = ScaleFactor * (dphi / df);
                }
                else
                {
                    destinationGroupDelayMs[i] = destinationGroupDelayMs[i - 1];
                }
            }

            // Último punto (diferencia hacia atrás)
            int last = count - 1;
            float dfLast = frequencies[last] - frequencies[last - 1];
            if (dfLast > 1e-6f)
            {
                float dphiLast = unwrappedPhase[last] - unwrappedPhase[last - 1];
                destinationGroupDelayMs[last] = ScaleFactor * (dphiLast / dfLast);
            }
            else
            {
                destinationGroupDelayMs[last] = destinationGroupDelayMs[last - 1];
            }
        }
        finally
        {
            if (rentedArray != null)
            {
                System.Buffers.ArrayPool<float>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Calcula el retardo de fase (Phase Delay) en milisegundos:
    /// Tau_p(f) = - (Phase / (360 * f)) * 1000 [ms].
    /// </summary>
    public static void CalculatePhaseDelayMs(
        ReadOnlySpan<float> frequencies,
        ReadOnlySpan<float> phaseDeg,
        Span<float> destinationPhaseDelayMs)
    {
        int count = Math.Min(frequencies.Length, Math.Min(phaseDeg.Length, destinationPhaseDelayMs.Length));
        const float Scale = -1000f / 360f;

        for (int i = 0; i < count; i++)
        {
            float f = frequencies[i];
            if (f > 0.1f)
            {
                destinationPhaseDelayMs[i] = Scale * (phaseDeg[i] / f);
            }
            else
            {
                destinationPhaseDelayMs[i] = 0f;
            }
        }
    }
}
