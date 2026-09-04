using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SoundCalibrator.Core.Models;

namespace SoundCalibrator.Core.Serialization;

public static class TraceSerializer
{
    public static string ExportToCsv(AcousticTrace trace)
    {
        using var writer = new StringWriter();
        writer.WriteLine("# SoundCalibrator Acoustic Trace Export");
        writer.WriteLine($"# Name: {trace.Name}");
        writer.WriteLine($"# Color: {trace.HexColor}");
        writer.WriteLine($"# Date: {trace.CreatedAt:yyyy-MM-ddTHH:mm:ssZ}");
        writer.WriteLine("Frequency_Hz,Magnitude_dB,Phase_Deg,Coherence");

        int count = trace.Frequencies.Length;
        for (int i = 0; i < count; i++)
        {
            float f = trace.Frequencies[i];
            if (f < 20f || f > 20000f) continue;

            trace.GetDisplayValues(i, out float mag, out float phase, out float coh);

            writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:0.0},{1:0.00},{2:0.00},{3:0.0000}", f, mag, phase, coh));
        }

        return writer.ToString();
    }

    public static AcousticTrace ImportFromCsv(string content, string defaultName = "Imported Trace", string defaultColor = "#00E5FF")
    {
        using var reader = new StringReader(content);
        string? line;
        string traceName = defaultName;
        string traceColor = defaultColor;

        var freqs = new List<float>();
        var mag = new List<float>();
        var phase = new List<float>();
        var coh = new List<float>();

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("# Name:"))
            {
                traceName = line["# Name:".Length..].Trim();
                continue;
            }
            if (line.StartsWith("# Color:"))
            {
                traceColor = line["# Color:".Length..].Trim();
                continue;
            }
            if (line.StartsWith('#') || line.StartsWith('*') || line.StartsWith("Freq", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split([',', '\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float f) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float m))
            {
                float p = 0f;
                if (parts.Length >= 3 && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float pVal))
                {
                    p = pVal;
                }

                float c = 1.0f;
                if (parts.Length >= 4 && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float cVal))
                {
                    c = cVal;
                }

                freqs.Add(f);
                mag.Add(m);
                phase.Add(p);
                coh.Add(c);
            }
        }

        if (freqs.Count == 0)
            throw new InvalidDataException("No valid acoustic frequency data found in CSV content.");

        return new AcousticTrace(traceName, traceColor, [.. freqs], [.. mag], [.. phase], [.. coh]);
    }
}
