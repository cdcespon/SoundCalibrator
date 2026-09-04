using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SoundCalibrator.Core.Calibration;

public readonly record struct CalibrationPoint(float Frequency, float MagnitudeDb, float PhaseDegrees);

public sealed class MicrophoneCalibration
{
    private readonly List<CalibrationPoint> _points = [];

    public IReadOnlyList<CalibrationPoint> Points => _points;
    public bool IsEmpty => _points.Count == 0;

    public void LoadFromText(string text)
    {
        _points.Clear();
        using var reader = new StringReader(text);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith('*') || line.StartsWith('"'))
                continue;

            // Separar por espacios o tabulaciones
            var parts = line.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float freq) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float mag))
            {
                float phase = 0f;
                if (parts.Length >= 3 && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float p))
                {
                    phase = p;
                }

                _points.Add(new CalibrationPoint(freq, mag, phase));
            }
        }

        // Ordenar por frecuencia ascendente
        _points.Sort((a, b) => a.Frequency.CompareTo(b.Frequency));
    }

    public void ApplyCorrection(ReadOnlySpan<float> frequencies, Span<float> magnitudeDb, Span<float> phaseDegrees)
    {
        if (IsEmpty) return;

        int count = Math.Min(frequencies.Length, Math.Min(magnitudeDb.Length, phaseDegrees.Length));

        for (int i = 0; i < count; i++)
        {
            float f = frequencies[i];
            if (f <= 0f) continue;

            var (calMag, calPhase) = Interpolate(f);

            // Restar la respuesta del micrófono para linearizar la respuesta
            magnitudeDb[i] -= calMag;
            phaseDegrees[i] -= calPhase;
        }
    }

    public (float MagnitudeDb, float PhaseDegrees) Interpolate(float frequency)
    {
        if (_points.Count == 0) return (0f, 0f);
        if (_points.Count == 1 || frequency <= _points[0].Frequency)
            return (_points[0].MagnitudeDb, _points[0].PhaseDegrees);

        if (frequency >= _points[^1].Frequency)
            return (_points[^1].MagnitudeDb, _points[^1].PhaseDegrees);

        // Búsqueda binaria del intervalo
        int low = 0;
        int high = _points.Count - 1;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            if (_points[mid].Frequency < frequency)
                low = mid + 1;
            else
                high = mid - 1;
        }

        // Intervalo entre low - 1 y low
        var p0 = _points[low - 1];
        var p1 = _points[low];

        // Interpolación lineal en escala logarítmica de frecuencia
        float logF0 = MathF.Log10(p0.Frequency);
        float logF1 = MathF.Log10(p1.Frequency);
        float logF = MathF.Log10(frequency);

        float t = (logF - logF0) / (logF1 - logF0);

        float mag = p0.MagnitudeDb + t * (p1.MagnitudeDb - p0.MagnitudeDb);
        float phase = p0.PhaseDegrees + t * (p1.PhaseDegrees - p0.PhaseDegrees);

        return (mag, phase);
    }
}
