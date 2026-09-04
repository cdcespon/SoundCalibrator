using System;

namespace SoundCalibrator.Core.DSP;

public static class PhaseUnwrapper
{
    public static void UnwrapDegrees(ReadOnlySpan<float> wrappedPhase, Span<float> unwrappedPhase)
    {
        int length = Math.Min(wrappedPhase.Length, unwrappedPhase.Length);
        if (length == 0) return;

        unwrappedPhase[0] = wrappedPhase[0];
        float cumulativeOffset = 0f;

        for (int i = 1; i < length; i++)
        {
            float delta = wrappedPhase[i] - wrappedPhase[i - 1];

            if (delta > 180f)
            {
                cumulativeOffset -= 360f;
            }
            else if (delta < -180f)
            {
                cumulativeOffset += 360f;
            }

            unwrappedPhase[i] = wrappedPhase[i] + cumulativeOffset;
        }
    }
}
