namespace SoundCalibrator.Core.Smoothing;

public enum OctaveSmoothingType
{
    None,
    Octave1_1,
    Octave1_3,
    Octave1_6,
    Octave1_12,
    Octave1_24,
    Octave1_48
}

public static class OctaveSmoother
{
    public static void Smooth(ReadOnlySpan<float> input, Span<float> output, OctaveSmoothingType type, float sampleRate, int fftSize)
    {
        int count = Math.Min(input.Length, output.Length);
        if (count == 0) return;

        if (type == OctaveSmoothingType.None)
        {
            input[..count].CopyTo(output[..count]);
            return;
        }

        float fraction = type switch
        {
            OctaveSmoothingType.Octave1_1 => 1.0f,
            OctaveSmoothingType.Octave1_3 => 1.0f / 3.0f,
            OctaveSmoothingType.Octave1_6 => 1.0f / 6.0f,
            OctaveSmoothingType.Octave1_12 => 1.0f / 12.0f,
            OctaveSmoothingType.Octave1_24 => 1.0f / 24.0f,
            OctaveSmoothingType.Octave1_48 => 1.0f / 48.0f,
            _ => 0f
        };

        float deltaF = sampleRate / fftSize;
        float factor = MathF.Pow(2.0f, fraction / 2.0f);

        for (int k = 0; k < count; k++)
        {
            float freq = k * deltaF;
            if (freq < 20f || k == 0)
            {
                output[k] = input[k];
                continue;
            }

            float lowerFreq = freq / factor;
            float upperFreq = freq * factor;

            int lowerBin = Math.Max(0, (int)MathF.Floor(lowerFreq / deltaF));
            int upperBin = Math.Min(count - 1, (int)MathF.Ceiling(upperFreq / deltaF));

            float sum = 0f;
            int numBins = 0;
            for (int j = lowerBin; j <= upperBin; j++)
            {
                sum += input[j];
                numBins++;
            }

            output[k] = numBins > 0 ? sum / numBins : input[k];
        }
    }
}
