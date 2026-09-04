using System;
using System.Collections.Generic;

namespace SoundCalibrator.Core.Analysis;

public sealed record AcousticZoneAlignment(
    string Name,
    float MeasuredDelayMs,
    float RequiredDelayOffsetMs,
    float RelativeDistanceMeters,
    float RelativeDistanceFeet);

public sealed record DelayMatrixReport(
    string AnchorZoneName,
    float TemperatureCelsius,
    float SpeedOfSoundMps,
    IReadOnlyList<AcousticZoneAlignment> Alignments);

/// <summary>
/// Matriz de alineación acústica multizona y torres de retardo (Delay Finder Matrix).
/// Calcula las diferencias de tiempo de llegada (Time of Arrival), compensaciones de delay relativas
/// y distancias acústicas físicas en función de la temperatura ambiente del recinto.
/// </summary>
public static class AcousticDelayMatrix
{
    /// <summary>
    /// Calcula la velocidad del sonido en el aire en m/s según la temperatura en °C:
    /// c = 331.3 * sqrt(1 + T / 273.15)
    /// </summary>
    public static float CalculateSpeedOfSound(float temperatureCelsius = 20.0f)
    {
        float tempKelvin = Math.Max(1.0f, temperatureCelsius + 273.15f);
        return 331.3f * MathF.Sqrt(tempKelvin / 273.15f);
    }

    /// <summary>
    /// Genera la matriz de alineación temporal multizona tomando una zona como ancla de referencia (Anchor).
    /// </summary>
    public static DelayMatrixReport CalculateAlignmentMatrix(
        IReadOnlyList<(string ZoneName, float DelayMs)> zones,
        int anchorIndex = 0,
        float temperatureCelsius = 20.0f)
    {
        if (zones == null || zones.Count == 0)
        {
            return new DelayMatrixReport("None", temperatureCelsius, CalculateSpeedOfSound(temperatureCelsius), Array.Empty<AcousticZoneAlignment>());
        }

        int clampedAnchor = Math.Clamp(anchorIndex, 0, zones.Count - 1);
        var anchor = zones[clampedAnchor];
        float c = CalculateSpeedOfSound(temperatureCelsius);

        var alignments = new List<AcousticZoneAlignment>(zones.Count);

        for (int i = 0; i < zones.Count; i++)
        {
            var z = zones[i];
            // Delta de retardo respecto al ancla: cuánto antes o después llega el sonido
            // Para alinear la zona 'z' con el ancla:
            // Si el ancla llega a 50ms y la zona Z (ej. torre de delay local) llega a 15ms,
            // la torre de delay debe demorarse (50 - 15) = +35ms para coincidir con la onda del ancla.
            float requiredDelayMs = anchor.DelayMs - z.DelayMs;

            float relativeDistM = (requiredDelayMs / 1000f) * c;
            float relativeDistFt = relativeDistM * 3.28084f;

            alignments.Add(new AcousticZoneAlignment(
                z.ZoneName,
                z.DelayMs,
                requiredDelayMs,
                relativeDistM,
                relativeDistFt));
        }

        return new DelayMatrixReport(anchor.ZoneName, temperatureCelsius, c, alignments);
    }
}
