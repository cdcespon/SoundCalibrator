using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SoundCalibrator.Audio.Engine;

namespace SoundCalibrator.App.Controls;

public sealed class AcousticGraphControl : Control
{
    private MeasurementSnapshot? _currentSnapshot;
    private Point? _mousePosition;

    // Colores inspirados en Open Sound Meter (Dark Tech Aesthetics)
    private static readonly Color BgColor = Color.Parse("#101318");
    private static readonly Color GridColor = Color.Parse("#1F2530");
    private static readonly Color TextColor = Color.Parse("#7E8B9B");
    private static readonly Color MagLineColor = Color.Parse("#00E5FF"); // Cyan brillante
    private static readonly Color PhaseLineColor = Color.Parse("#FF9100"); // Naranja neón
    private static readonly Color CohLineColor = Color.Parse("#00E676"); // Verde esmeralda
    private static readonly Color CrosshairColor = Color.Parse("#50FFFFFF");

    private static readonly Typeface LabelFont = new("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);

    public void UpdateSnapshot(MeasurementSnapshot snapshot)
    {
        _currentSnapshot = snapshot;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _mousePosition = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _mousePosition = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        double w = bounds.Width;
        double h = bounds.Height;
        if (w <= 10 || h <= 10) return;

        // 1. Fondo
        context.FillRectangle(new SolidColorBrush(BgColor), bounds);

        // Subdivisión de pantalla:
        // Top: Magnitud (dB) y Fase (grados) (75% del alto)
        // Bottom: Coherencia (0 - 100%) (25% del alto)
        double mainH = h * 0.75;
        double cohH = h * 0.25;
        double cohTop = mainH;

        // 2. Grilla Logarítmica de Frecuencia (20 Hz a 20 kHz)
        DrawFrequencyGrid(context, w, mainH, cohTop, cohH);

        // 3. Grilla de Magnitud (-36 dB a +18 dB) y Fase (-180 a +180)
        DrawMagnitudeAndPhaseGrid(context, w, mainH);

        // 4. Grilla de Coherencia (0% a 100%)
        DrawCoherenceGrid(context, w, cohTop, cohH);

        // 5. Curvas acústicas
        if (_currentSnapshot != null)
        {
            DrawDataCurves(context, w, mainH, cohTop, cohH, _currentSnapshot);
        }

        // 6. Crosshair y Readout numérico interactivo
        if (_mousePosition.HasValue && _currentSnapshot != null)
        {
            DrawCrosshairAndReadout(context, w, mainH, cohTop, cohH, _mousePosition.Value, _currentSnapshot);
        }
    }

    private void DrawFrequencyGrid(DrawingContext context, double w, double mainH, double cohTop, double cohH)
    {
        float[] majorFreqs = [20f, 50f, 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f, 20000f];
        var gridPen = new Pen(new SolidColorBrush(GridColor), 1);

        foreach (float f in majorFreqs)
        {
            double x = FreqToX(f, w);
            if (x < 0 || x > w) continue;

            context.DrawLine(gridPen, new Point(x, 0), new Point(x, mainH + cohH));

            string text = f >= 1000f ? $"{f / 1000f:0.#}k" : $"{f:0}";
            var formattedText = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFont,
                11,
                new SolidColorBrush(TextColor));

            context.DrawText(formattedText, new Point(x + 3, cohTop - 15));
        }
    }

    private void DrawMagnitudeAndPhaseGrid(DrawingContext context, double w, double mainH)
    {
        var gridPen = new Pen(new SolidColorBrush(GridColor), 1, DashStyle.Dash);

        // Magnitud: +18 dB a -36 dB (paso 6 dB)
        for (float db = -36f; db <= 18f; db += 6f)
        {
            double y = DbToY(db, mainH);
            context.DrawLine(gridPen, new Point(0, y), new Point(w, y));

            var label = new FormattedText(
                $"{db:+0;-0;0} dB",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFont,
                10,
                new SolidColorBrush(Color.Parse("#00E5FF")));
            context.DrawText(label, new Point(5, y - 12));
        }

        // Fase: +180, +90, 0, -90, -180
        float[] phaseTicks = [180f, 90f, 0f, -90f, -180f];
        foreach (float deg in phaseTicks)
        {
            double y = PhaseToY(deg, mainH);
            var label = new FormattedText(
                $"{deg:+0;-0;0}°",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelFont,
                10,
                new SolidColorBrush(Color.Parse("#FF9100")));
            context.DrawText(label, new Point(w - 35, y - 12));
        }
    }

    private void DrawCoherenceGrid(DrawingContext context, double w, double cohTop, double cohH)
    {
        var gridPen = new Pen(new SolidColorBrush(GridColor), 1);
        context.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#2C3440")), 1.5), new Point(0, cohTop), new Point(w, cohTop));

        // 50% y 100%
        double y100 = cohTop;
        double y50 = cohTop + cohH * 0.5;

        context.DrawLine(gridPen, new Point(0, y50), new Point(w, y50));

        var label100 = new FormattedText("1.0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 9, new SolidColorBrush(CohLineColor));
        context.DrawText(label100, new Point(5, y100 + 2));

        var label50 = new FormattedText("0.5", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 9, new SolidColorBrush(TextColor));
        context.DrawText(label50, new Point(5, y50 - 10));
    }

    private void DrawDataCurves(DrawingContext context, double w, double mainH, double cohTop, double cohH, MeasurementSnapshot snap)
    {
        var magGeometry = new StreamGeometry();
        var phaseGeometry = new StreamGeometry();
        var cohGeometry = new StreamGeometry();

        int count = snap.Frequencies.Length;
        bool magStarted = false;
        bool phaseStarted = false;
        bool cohStarted = false;

        using (var magCtx = magGeometry.Open())
        using (var phaseCtx = phaseGeometry.Open())
        using (var cohCtx = cohGeometry.Open())
        {
            for (int i = 1; i < count; i++)
            {
                float freq = snap.Frequencies[i];
                if (freq < 20f || freq > 20000f) continue;

                double x = FreqToX(freq, w);

                // Magnitud
                double yMag = DbToY(snap.MagnitudeDb[i], mainH);
                if (!magStarted)
                {
                    magCtx.BeginFigure(new Point(x, yMag), false);
                    magStarted = true;
                }
                else
                {
                    magCtx.LineTo(new Point(x, yMag));
                }

                // Fase
                double yPhase = PhaseToY(snap.PhaseDegrees[i], mainH);
                if (!phaseStarted)
                {
                    phaseCtx.BeginFigure(new Point(x, yPhase), false);
                    phaseStarted = true;
                }
                else
                {
                    phaseCtx.LineTo(new Point(x, yPhase));
                }

                // Coherencia
                double yCoh = cohTop + cohH * (1.0 - Math.Clamp(snap.Coherence[i], 0f, 1f));
                if (!cohStarted)
                {
                    cohCtx.BeginFigure(new Point(x, yCoh), false);
                    cohStarted = true;
                }
                else
                {
                    cohCtx.LineTo(new Point(x, yCoh));
                }
            }
        }

        // Trazar curvas con grosores diferenciados
        if (magStarted)
        {
            context.DrawGeometry(null, new Pen(new SolidColorBrush(MagLineColor), 2.2), magGeometry);
        }
        if (phaseStarted)
        {
            context.DrawGeometry(null, new Pen(new SolidColorBrush(PhaseLineColor), 1.6), phaseGeometry);
        }
        if (cohStarted)
        {
            context.DrawGeometry(null, new Pen(new SolidColorBrush(CohLineColor), 1.8), cohGeometry);
        }
    }

    private void DrawCrosshairAndReadout(DrawingContext context, double w, double mainH, double cohTop, double cohH, Point mouse, MeasurementSnapshot snap)
    {
        var crossPen = new Pen(new SolidColorBrush(CrosshairColor), 1, DashStyle.Dash);
        context.DrawLine(crossPen, new Point(mouse.X, 0), new Point(mouse.X, mainH + cohH));
        context.DrawLine(crossPen, new Point(0, mouse.Y), new Point(w, mouse.Y));

        float mouseFreq = XToFreq(mouse.X, w);
        int closestBin = Math.Clamp((int)Math.Round(mouseFreq / (snap.SampleRate / snap.FftSize)), 0, snap.Frequencies.Length - 1);

        float fVal = snap.Frequencies[closestBin];
        float magVal = snap.MagnitudeDb[closestBin];
        float phaseVal = snap.PhaseDegrees[closestBin];
        float cohVal = snap.Coherence[closestBin];

        string readout = $"{fVal:0.#} Hz | Mag: {magVal:+0.00;-0.00;0.00} dB | Phase: {phaseVal:+0.0;-0.0;0.0}° | Coh: {cohVal * 100f:0.0}%";
        var readoutText = new FormattedText(readout, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont, 13, new SolidColorBrush(Color.Parse("#E0E6ED")));

        var badgeRect = new Rect(w / 2 - 170, 10, 340, 26);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#CC1A202C")), badgeRect, 4);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#3A4556")), 1), badgeRect, 4);
        context.DrawText(readoutText, new Point(w / 2 - 160, 14));
    }

    private static double FreqToX(float freq, double w)
    {
        double minLog = Math.Log10(20.0);
        double maxLog = Math.Log10(20000.0);
        double fLog = Math.Log10(Math.Clamp(freq, 20f, 20000f));
        return ((fLog - minLog) / (maxLog - minLog)) * w;
    }

    private static float XToFreq(double x, double w)
    {
        double minLog = Math.Log10(20.0);
        double maxLog = Math.Log10(20000.0);
        double fLog = minLog + (x / w) * (maxLog - minLog);
        return (float)Math.Pow(10.0, fLog);
    }

    private static double DbToY(float db, double mainH)
    {
        const float maxDb = 18f;
        const float minDb = -36f;
        float norm = (Math.Clamp(db, minDb, maxDb) - minDb) / (maxDb - minDb);
        return mainH * (1.0 - norm);
    }

    private static double PhaseToY(float deg, double mainH)
    {
        const float maxDeg = 180f;
        const float minDeg = -180f;
        float norm = (Math.Clamp(deg, minDeg, maxDeg) - minDeg) / (maxDeg - minDeg);
        return mainH * (1.0 - norm);
    }
}
