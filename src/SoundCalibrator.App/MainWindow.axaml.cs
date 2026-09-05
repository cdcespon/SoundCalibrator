using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SoundCalibrator.Audio.Devices;
using SoundCalibrator.Audio.Engine;
using SoundCalibrator.Audio.Generators;
using SoundCalibrator.Core.Analysis;
using SoundCalibrator.Core.Averaging;
using SoundCalibrator.Core.Calibration;
using SoundCalibrator.Core.Models;
using SoundCalibrator.Core.Operations;
using SoundCalibrator.Core.Reporting;
using SoundCalibrator.Core.Serialization;
using SoundCalibrator.Core.Smoothing;
using SoundCalibrator.App.Dialogs;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Controls.Primitives;

namespace SoundCalibrator.App;

public partial class MainWindow : Window
{
    private readonly AcousticMeasurementEngine _engine;
    private readonly SplMeter _splMeter = new();
    private SyntheticAudioGenerator? _syntheticGen;
    private IDisposable? _wasapiOutputDevice;

    private MeasurementSnapshot? _lastSnapshot;
    private float _lastDetectedDelayMs = 4.18f;
    private bool _isAligned;
    private bool _isPaused;
    private bool _isDacOutputActive;
    private bool _channelsSwapped;
    private AlignmentSuggestion? _lastAlignmentSuggestion;
    private DelayMatrixReport? _lastDelayMatrixReport;
    private IReadOnlyList<PeqFilterSuggestion>? _lastPeqSuggestions = [];

    private bool _leftSidebarExpanded = true;
    private bool _rightSidebarExpanded = true;
    private bool _isLightMode;

    private static readonly string[] TraceColors = ["#A855F7", "#10B981", "#F59E0B", "#EC4899", "#3B82F6", "#00F0FF"];

    public MainWindow()
    {
        InitializeComponent();

        _engine = new AcousticMeasurementEngine(1024);

        // Initialize Synthetic Audio Generator with realistic acoustic room response (4.18 ms delay)
        _syntheticGen = new SyntheticAudioGenerator(sampleRate: 48000, blockSize: 512)
        {
            SignalType = TestSignalType.PinkNoise,
            DelayMs = 4.18f,
            SimulateAcousticRoom = true
        };

        _engine.AttachDevice(_syntheticGen);
        _engine.SnapshotReady += OnSnapshotReady;

        WireControls();
        InitPrepopulatedTraces();
        UpdateModeButtons(0); // Transfer Function default

        _engine.Start();
        Loaded += (s, e) => HideFullScreenArrows();
    }

    private void WireControls()
    {
        // 1. Mode Pill Buttons
        ModeTfBtn.Click += (s, e) => SwitchMode(0);
        ModeRtaBtn.Click += (s, e) => SwitchMode(1);
        ModeEtcBtn.Click += (s, e) => SwitchMode(2);
        ModeSpecBtn.Click += (s, e) => SwitchMode(3);

        // 2. Source Combo
        SourceCombo.SelectionChanged += (s, e) =>
        {
            if (_syntheticGen == null) return;
            int idx = SourceCombo.SelectedIndex;
            _syntheticGen.SignalType = idx switch
            {
                0 => TestSignalType.PinkNoise,
                1 => TestSignalType.SineWave,
                2 => TestSignalType.SineSweep,
                3 => TestSignalType.GatedPinkNoise,
                4 => TestSignalType.IecNoise,
                5 => TestSignalType.PolarityPulse,
                6 => TestSignalType.SmpteImd,
                7 => TestSignalType.CcifImd,
                _ => TestSignalType.PinkNoise
            };
            string name = ((ComboBoxItem)SourceCombo.SelectedItem!).Content?.ToString() ?? "Synthetic";
            StatusDeviceText.Text = $"Source: {name}";
        };

        // 3. DSP Dropdowns (FFT, Window, Smoothing, Averaging)
        FftCombo.SelectionChanged += (s, e) =>
        {
            int size = FftCombo.SelectedIndex switch
            {
                0 => 512,
                1 => 1024,
                2 => 2048,
                3 => 4096,
                4 => 16384,
                _ => 1024
            };
            _engine.ReconfigureFft(size, _engine.WindowType);
        };

        WindowCombo.SelectionChanged += (s, e) =>
        {
            var win = WindowCombo.SelectedIndex switch
            {
                0 => WindowType.Hann,
                1 => WindowType.BlackmanHarris,
                2 => WindowType.Rectangular,
                _ => WindowType.Hann
            };
            _engine.ReconfigureFft(_engine.FftSize, win);
        };

        SmoothingCombo.SelectionChanged += (s, e) =>
        {
            _engine.Smoothing = SmoothingCombo.SelectedIndex switch
            {
                0 => OctaveSmoothingType.None,
                1 => OctaveSmoothingType.Octave1_3,
                2 => OctaveSmoothingType.Octave1_6,
                3 => OctaveSmoothingType.Octave1_12,
                4 => OctaveSmoothingType.Octave1_24,
                _ => OctaveSmoothingType.None
            };
        };

        AveragingCombo.SelectionChanged += (s, e) =>
        {
            _engine.Averaging = AveragingCombo.SelectedIndex switch
            {
                0 => AveragingType.None,
                1 => AveragingType.ExponentialFast,
                2 => AveragingType.ExponentialSlow,
                3 => AveragingType.Linear16,
                4 => AveragingType.Infinite,
                _ => AveragingType.ExponentialFast
            };
        };

        // 4. Target & Blanking
        TargetCombo.SelectionChanged += (s, e) =>
        {
            var preset = TargetCombo.SelectedIndex switch
            {
                1 => TargetCurvePreset.HarmanTarget,
                2 => TargetCurvePreset.BruelKjaer1974,
                3 => TargetCurvePreset.CinemaXCurve,
                4 => TargetCurvePreset.Flat,
                _ => TargetCurvePreset.None
            };
            GraphControl.ActiveTargetCurve = preset == TargetCurvePreset.None ? null : TargetCurve.CreatePreset(preset);
            GraphControl.InvalidateVisual();
        };

        BlankingCombo.SelectionChanged += (s, e) =>
        {
            GraphControl.CoherenceThreshold = BlankingCombo.SelectedIndex switch
            {
                0 => 0.0f,
                1 => 0.3f,
                2 => 0.5f,
                3 => 0.7f,
                _ => 0.3f
            };
            GraphControl.InvalidateVisual();
        };

        // 5. Pro Tools
        MinPhaseBtn.Click += (s, e) =>
        {
            GraphControl.ShowMinimumPhase = !GraphControl.ShowMinimumPhase;
            MinPhaseBtn.Classes.Clear();
            MinPhaseBtn.Classes.Add(GraphControl.ShowMinimumPhase ? "dock-pill-active" : "dock-pill");
            GraphControl.InvalidateVisual();
        };

        FdwBtn.Click += (s, e) =>
        {
            GraphControl.ShowFdwQuasiAnechoic = !GraphControl.ShowFdwQuasiAnechoic;
            FdwBtn.Classes.Clear();
            FdwBtn.Classes.Add(GraphControl.ShowFdwQuasiAnechoic ? "dock-pill-purple" : "dock-pill");
            GraphControl.InvalidateVisual();
        };

        DeltaBtn.Click += (s, e) =>
        {
            GraphControl.ShowDeltaCurve = !GraphControl.ShowDeltaCurve;
            DeltaBtn.Classes.Clear();
            DeltaBtn.Classes.Add(GraphControl.ShowDeltaCurve ? "dock-pill-purple" : "dock-pill");
            GraphControl.InvalidateVisual();
        };

        InvertPolarityBtn.Click += (s, e) =>
        {
            _engine.InvertPolarity = !_engine.InvertPolarity;
            InvertPolarityBtn.Classes.Clear();
            InvertPolarityBtn.Classes.Add(_engine.InvertPolarity ? "dock-pill-active" : "dock-pill");
            GraphControl.InvalidateVisual();
        };

        SwapChannelsBtn.Click += (s, e) =>
        {
            _channelsSwapped = !_channelsSwapped;
            SwapChannelsBtn.Content = _channelsSwapped ? "CH 2:1" : "CH 1:2";
            SwapChannelsBtn.Classes.Clear();
            SwapChannelsBtn.Classes.Add(_channelsSwapped ? "dock-pill-active" : "dock-pill");
            StatusRoutingText.Text = _channelsSwapped ? "REF: CH 2  |  MEAS: CH 1" : "REF: CH 1  |  MEAS: CH 2";
        };

        GenOutputBtn.Click += (s, e) =>
        {
            if (!OperatingSystem.IsWindows() || _syntheticGen == null) return;
#pragma warning disable CA1416
            try
            {
                if (!_isDacOutputActive)
                {
                    var defaultOut = WasapiAudioOutputDevice.GetDefaultPlaybackDevice();
                    if (defaultOut != null)
                    {
                        var outDev = new WasapiAudioOutputDevice(defaultOut);
                        outDev.Start(_syntheticGen);
                        _wasapiOutputDevice = outDev;
                        _isDacOutputActive = true;
                        GenOutputBtn.Content = "DAC: ON";
                        GenOutputBtn.Classes.Clear();
                        GenOutputBtn.Classes.Add("dock-pill-red");
                    }
                }
                else
                {
                    _wasapiOutputDevice?.Dispose();
                    _wasapiOutputDevice = null;
                    _isDacOutputActive = false;
                    GenOutputBtn.Content = "DAC: OFF";
                    GenOutputBtn.Classes.Clear();
                    GenOutputBtn.Classes.Add("dock-pill");
                }
            }
            catch { }
#pragma warning restore CA1416
        };

        // 6. DELAY MODULE (Slider, +/- Buttons, Auto-Align)
        DelaySlider.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == nameof(Slider.Value) && _syntheticGen != null)
            {
                float val = (float)DelaySlider.Value;
                _syntheticGen.DelayMs = val;
                DelayText.Text = $"{val:0.0} ms";
            }
        };

        DelayMinusBtn.Click += (s, e) =>
        {
            DelaySlider.Value = Math.Max(0.0, DelaySlider.Value - 0.1);
        };

        DelayPlusBtn.Click += (s, e) =>
        {
            DelaySlider.Value = Math.Min(50.0, DelaySlider.Value + 0.1);
        };

        AutoAlignBtn.Click += (s, e) =>
        {
            if (!_isAligned)
            {
                _engine.DelayCompensationMs = -_lastDetectedDelayMs;
                _isAligned = true;
                AutoAlignBtn.Content = "UNALIGN (0.0 ms)";
                AutoAlignBtn.Background = SolidColorBrush.Parse("#10B981");
                AutoAlignBtn.Foreground = SolidColorBrush.Parse("#000000");
            }
            else
            {
                _engine.DelayCompensationMs = 0f;
                _isAligned = false;
                AutoAlignBtn.Content = $"⚡ ALIGN (+{_lastDetectedDelayMs:0.1} ms)";
                AutoAlignBtn.Background = SolidColorBrush.Parse("#26F59E0B");
                AutoAlignBtn.Foreground = SolidColorBrush.Parse("#F59E0B");
            }
        };

        // 7. Live Action Buttons
        CaptureTraceBtn.Click += (s, e) =>
        {
            if (_lastSnapshot != null)
            {
                int idx = GraphControl.StoredTraces.Count;
                string color = TraceColors[idx % TraceColors.Length];
                bool isRta = _lastSnapshot.IsRtaMode;
                string name = isRta ? $"RTA {idx + 1}" : $"Trace {idx + 1}";
                float[] mag = isRta && _lastSnapshot.RtaDb != null ? _lastSnapshot.RtaDb : _lastSnapshot.MagnitudeDb;
                var trace = new AcousticTrace(name, color, _lastSnapshot.Frequencies, mag, _lastSnapshot.PhaseDegrees, _lastSnapshot.Coherence)
                {
                    DetectedDelayMs = _lastDetectedDelayMs,
                    IsRtaTrace = isRta
                };
                GraphControl.StoredTraces.Add(trace);
                TracesCountText.Text = $"ACOUSTIC TRACES ({GraphControl.StoredTraces.Count + 1})";
                GraphControl.InvalidateVisual();
                RefreshTraceManagerUI();
            }
        };

        ClearTracesBtn.Click += (s, e) =>
        {
            GraphControl.StoredTraces.Clear();
            TracesCountText.Text = "ACOUSTIC TRACES (1)";
            GraphControl.InvalidateVisual();
            RefreshTraceManagerUI();
        };

        BottomClearBtn.Click += (s, e) => ClearTracesBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        NewTraceBtn.Click += (s, e) => CaptureTraceBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        ResetZoomBtn.Click += (s, e) => GraphControl.ResetZoom();
        GraphResetZoomBtn.Click += (s, e) => GraphControl.ResetZoom();
        ZoomInBtn.Click += (s, e) => GraphControl.Zoom(0.8);
        ZoomOutBtn.Click += (s, e) => GraphControl.Zoom(1.25);

        StartStopBtn.Click += (s, e) =>
        {
            if (_isPaused)
            {
                _engine.Start();
                StartStopBtn.Content = "PAUSE";
                _isPaused = false;
            }
            else
            {
                _engine.Stop();
                StartStopBtn.Content = "RESUME";
                _isPaused = true;
            }
        };

        // 8. Trace Math
        SpatialAvgBtn.Click += (s, e) =>
        {
            var visibleTraces = GraphControl.StoredTraces.Where(t => t.IsVisible).ToList();
            if (visibleTraces.Count >= 2)
            {
                var avgTrace = SpatialAverager.CalculateSpatialAverage(visibleTraces, SpatialAverageMode.CoherenceWeightedPower, "Spatial Average", "#00F0FF");
                if (avgTrace != null)
                {
                    GraphControl.StoredTraces.Add(avgTrace);
                    RefreshTraceManagerUI();
                    GraphControl.InvalidateVisual();
                }
            }
        };

        SimulateSumBtn.Click += (s, e) =>
        {
            var visibleTraces = GraphControl.StoredTraces.Where(t => t.IsVisible && !t.IsRtaTrace).ToList();
            if (visibleTraces.Count >= 2)
            {
                var sumTrace = AcousticSummationSimulator.SimulateSummation(visibleTraces, $"Sum ({visibleTraces.Count} sources)", "#EC4899");
                if (sumTrace != null)
                {
                    GraphControl.StoredTraces.Add(sumTrace);
                    RefreshTraceManagerUI();
                    GraphControl.InvalidateVisual();
                }
            }
        };

        DiffTracesBtn.Click += (s, e) =>
        {
            var visibleTraces = GraphControl.StoredTraces.Where(t => t.IsVisible && !t.IsRtaTrace).ToList();
            if (visibleTraces.Count >= 2)
            {
                var diffTrace = TraceMath.DivideTraces(visibleTraces[0], visibleTraces[1], $"Diff ({visibleTraces[0].Name} / {visibleTraces[1].Name})", "#A855F7");
                if (diffTrace != null)
                {
                    GraphControl.StoredTraces.Add(diffTrace);
                    RefreshTraceManagerUI();
                    GraphControl.InvalidateVisual();
                }
            }
        };

        // 9. Crossover & Delay Matrix
        CalculateAlignmentBtn.Click += (s, e) =>
        {
            if (GraphControl.StoredTraces.Count < 2)
            {
                AlignmentResultText.Text = "Requires at least 2 captured traces (Sub, Main)";
                return;
            }
            var sub = GraphControl.StoredTraces[0];
            var main = GraphControl.StoredTraces[1];
            float fc = CrossoverFreqCombo.SelectedIndex switch
            {
                0 => 60f,
                1 => 80f,
                2 => 100f,
                3 => 120f,
                _ => 80f
            };
            var sug = CrossoverAlignmentAnalyzer.Analyze(sub, main, fc);
            _lastAlignmentSuggestion = sug;
            AlignmentResultText.Text = $"Δθ: {sug.PhaseDeltaDeg:+0.0;-0.0;0.0}° | Fc: {fc:0}Hz\nDelay: {sug.RecommendedDelayMs:+0.00;-0.00;0.00} ms ({sug.RecommendedDistanceMeters:+0.00;-0.00;0.00}m)\nPolarity: {(sug.RecommendPolarityInversion ? "INVERT Ø" : "NORMAL Ø")}\nSum Gain: {sug.PredictedSummationGainDb:+0.0;-0.0;0.0} dB";
            AlignmentResultText.Foreground = SolidColorBrush.Parse("#00F0FF");
            ApplyAlignmentBtn.IsVisible = true;
        };

        ApplyAlignmentBtn.Click += (s, e) =>
        {
            if (_lastAlignmentSuggestion.HasValue && GraphControl.StoredTraces.Count > 0)
            {
                var sub = GraphControl.StoredTraces[0];
                sub.OffsetDelayMs = _lastAlignmentSuggestion.Value.RecommendedDelayMs;
                if (_lastAlignmentSuggestion.Value.RecommendPolarityInversion)
                {
                    sub.InvertPolarity = !sub.InvertPolarity;
                }
                RefreshTraceManagerUI();
                GraphControl.InvalidateVisual();
            }
        };

        CalculateDelayMatrixBtn.Click += (s, e) =>
        {
            var visibleTraces = GraphControl.StoredTraces.Where(t => t.IsVisible).ToList();
            if (visibleTraces.Count < 2) return;
            var zones = visibleTraces.Select(t => (t.Name, t.DetectedDelayMs)).ToList();
            var report = AcousticDelayMatrix.CalculateAlignmentMatrix(zones, anchorIndex: 0, temperatureCelsius: 20.0f);
            _lastDelayMatrixReport = report;
            var sb = new StringBuilder();
            sb.AppendLine($"Anchor: {report.AnchorZoneName} (c={report.SpeedOfSoundMps:0} m/s)");
            for (int i = 1; i < report.Alignments.Count; i++)
            {
                var a = report.Alignments[i];
                string sign = a.RequiredDelayOffsetMs >= 0 ? "+" : "";
                sb.AppendLine($"- {a.Name}: {sign}{a.RequiredDelayOffsetMs:0.0} ms ({sign}{a.RelativeDistanceMeters:0.00} m)");
            }
            DelayMatrixResultText.Text = sb.ToString().TrimEnd();
            DelayMatrixResultText.Foreground = SolidColorBrush.Parse("#FFB300");
        };

        // 10. Export Biquads & Reports
        ExportBiquadsBtn.Click += async (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export MiniDSP Biquads Configuration (.txt)",
                DefaultExtension = "txt",
                SuggestedFileName = $"MiniDSP_Biquads_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            });
            if (file != null)
            {
                var biquads = new List<PeqFilterSuggestion>
                {
                    new(62.5f, -4.2f, 3.4f, 0.42f),
                    new(145.0f, 2.8f, 2.1f, 0.67f),
                    new(520.0f, -3.1f, 4.8f, 0.30f),
                    new(2840.0f, -1.9f, 1.8f, 0.78f)
                };
                string txt = DspFilterExporter.ExportMiniDsp(biquads, 48000);
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(txt);
            }
        };

        // Sidebars Collapse & Expand
        CollapseLeftBtn.Click += (s, e) => ToggleLeftSidebar(false);
        ExpandLeftBtn.Click += (s, e) => ToggleLeftSidebar(true);
        ExpandLeftStripBtn.Click += (s, e) => ToggleLeftSidebar(true);

        CollapseRightBtn.Click += (s, e) => ToggleRightSidebar(false);
        ExpandRightBtn.Click += (s, e) => ToggleRightSidebar(true);
        ExpandRightStripBtn.Click += (s, e) => ToggleRightSidebar(true);

        ThemeToggleBtn.Click += (s, e) => ToggleTheme();

        ExportReportBtn.Click += async (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var dialog = new ExportReportDialog();
            var format = await dialog.ShowDialog<ExportReportFormat?>(this);
            if (format == null) return;

            var reportData = new CalibrationReportData
            {
                ProjectName = "Acoustic Modern Tech Studio Calibration",
                EngineerName = "Audio Engineer",
                SampleRate = (int)_engine.SampleRate,
                FftSize = _engine.FftSize,
                DelayCompensationMs = _engine.DelayCompensationMs,
                Alignment = _lastAlignmentSuggestion,
                DelayMatrix = _lastDelayMatrixReport,
                Traces = GraphControl.StoredTraces,
                PeqFilters = _lastPeqSuggestions
            };

            switch (format.Value)
            {
                case ExportReportFormat.Pdf:
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Export Acoustic Calibration Technical Report (.pdf)",
                        DefaultExtension = "pdf",
                        SuggestedFileName = $"Calibration_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                        FileTypeChoices = [
                            new FilePickerFileType("PDF Document (*.pdf)") { Patterns = ["*.pdf"] }
                        ]
                    });
                    if (file != null)
                    {
                        byte[] pdfBytes = ReportGenerator.GeneratePdf(reportData);
                        await using var stream = await file.OpenWriteAsync();
                        await stream.WriteAsync(pdfBytes);
                    }
                    break;
                }
                case ExportReportFormat.Html:
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Export Acoustic Calibration Technical Report (.html)",
                        DefaultExtension = "html",
                        SuggestedFileName = $"Calibration_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html",
                        FileTypeChoices = [
                            new FilePickerFileType("HTML Document (*.html;*.htm)") { Patterns = ["*.html", "*.htm"] }
                        ]
                    });
                    if (file != null)
                    {
                        string html = ReportGenerator.GenerateHtml(reportData);
                        await using var stream = await file.OpenWriteAsync();
                        await using var writer = new StreamWriter(stream, Encoding.UTF8);
                        await writer.WriteAsync(html);
                    }
                    break;
                }
                case ExportReportFormat.Text:
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Export Acoustic Calibration Technical Report (.txt / .md)",
                        DefaultExtension = "md",
                        SuggestedFileName = $"Calibration_Report_{DateTime.Now:yyyyMMdd_HHmmss}.md",
                        FileTypeChoices = [
                            new FilePickerFileType("Markdown Report (*.md)") { Patterns = ["*.md"] },
                            new FilePickerFileType("Plain Text Document (*.txt)") { Patterns = ["*.txt"] }
                        ]
                    });
                    if (file != null)
                    {
                        string text = ReportGenerator.GenerateText(reportData);
                        await using var stream = await file.OpenWriteAsync();
                        await using var writer = new StreamWriter(stream, Encoding.UTF8);
                        await writer.WriteAsync(text);
                    }
                    break;
                }
            }
        };

        CsvReportBtn.Click += async (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || GraphControl.StoredTraces.Count == 0) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Acoustic Trace (.csv)",
                DefaultExtension = "csv",
                SuggestedFileName = $"{GraphControl.StoredTraces[0].Name}.csv"
            });
            if (file != null)
            {
                string csv = TraceSerializer.ExportToCsv(GraphControl.StoredTraces[0]);
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(csv);
            }
        };
    }

    private void SwitchMode(int mode)
    {
        UpdateModeButtons(mode);
        bool isTf = mode == 0;
        bool isRta = mode == 1;
        bool isImpulseEtc = mode == 2;
        bool isSpectro = mode == 3;

        _engine.IsRtaMode = isRta;
        GraphControl.IsSpectrogramMode = isSpectro;
        GraphControl.ShowImpulseEtc = isImpulseEtc;
        GraphControl.InvalidateVisual();
    }

    private void UpdateModeButtons(int activeIndex)
    {
        Button[] buttons = [ModeTfBtn, ModeRtaBtn, ModeEtcBtn, ModeSpecBtn];
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].Classes.Clear();
            buttons[i].Classes.Add(i == activeIndex ? "dock-pill-active" : "dock-pill");
        }
    }

    private void InitPrepopulatedTraces()
    {
        // Populate the exact 4 demo traces shown in the Modern Tech prototype
        int n = 1024 / 2 + 1;
        float[] freqs = new float[n];
        float[] mag1 = new float[n];
        float[] phase1 = new float[n];
        float[] coh = new float[n];
        for (int i = 0; i < n; i++)
        {
            freqs[i] = i * (48000f / 1024);
            coh[i] = 0.95f;
            mag1[i] = -2.0f + MathF.Sin(i * 0.05f) * 1.5f;
            phase1[i] = ((i * 0.4f) % 360f) - 180f;
        }

        var t1 = new AcousticTrace("Main L - Ground Plane", "#A855F7", freqs, mag1, phase1, coh) { DetectedDelayMs = 3.82f };
        var t2 = new AcousticTrace("Subwoofer Center", "#10B981", freqs, mag1, phase1, coh) { DetectedDelayMs = 12.40f };
        var t3 = new AcousticTrace("Balcony Delay Tower", "#F59E0B", freqs, mag1, phase1, coh) { DetectedDelayMs = 28.15f, OffsetDelayMs = 24.3f };
        var t4 = new AcousticTrace("Simulated Crossover Sum", "#EC4899", freqs, mag1, phase1, coh) { DetectedDelayMs = 0f };

        GraphControl.StoredTraces.Add(t1);
        GraphControl.StoredTraces.Add(t2);
        GraphControl.StoredTraces.Add(t3);
        GraphControl.StoredTraces.Add(t4);

        TracesCountText.Text = "ACOUSTIC TRACES (5)";
        GraphControl.InvalidateVisual();
    }

    private void OnSnapshotReady(MeasurementSnapshot snapshot)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _lastSnapshot = snapshot;
            _lastDetectedDelayMs = snapshot.Delay.DelayMs;
            GraphControl.UpdateSnapshot(snapshot);

            DetectedDelayText.Text = $"+{snapshot.Delay.DelayMs:0.00} ms";
            if (!_isAligned)
            {
                AutoAlignBtn.Content = $"⚡ ALIGN (+{snapshot.Delay.DelayMs:0.1} ms)";
            }

            // Update Floating HUD Readout with live center cursor frequency
            float cursorFreq = 1000f;
            int bin = Math.Clamp((int)MathF.Round(cursorFreq / (snapshot.SampleRate / snapshot.FftSize)), 1, snapshot.Frequencies.Length - 1);
            HudFreqText.Text = $"{snapshot.Frequencies[bin]:0.0} Hz";
            HudMagText.Text = $"{snapshot.MagnitudeDb[bin]:+0.0;-0.0;0.0} dB";
            HudPhaseText.Text = $"{snapshot.PhaseDegrees[bin]:+0;-0;0}°";
            HudCohText.Text = $"{snapshot.Coherence[bin] * 100:0.0}%";
            HudGdText.Text = $"{snapshot.Delay.DelayMs:0.00} ms";
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Space:
                CaptureTraceBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Key.A:
                AutoAlignBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Key.Z:
                ResetZoomBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Key.W:
                FdwBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Key.P:
                SwapChannelsBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Key.M:
                int cur = ModeTfBtn.Classes.Contains("dock-pill-active") ? 0 :
                          ModeRtaBtn.Classes.Contains("dock-pill-active") ? 1 :
                          ModeEtcBtn.Classes.Contains("dock-pill-active") ? 2 : 3;
                SwitchMode((cur + 1) % 4);
                e.Handled = true;
                break;
            case Key.OemOpenBrackets:
                ToggleLeftSidebar();
                e.Handled = true;
                break;
            case Key.OemCloseBrackets:
                ToggleRightSidebar();
                e.Handled = true;
                break;
            case Key.T:
                ToggleTheme();
                e.Handled = true;
                break;
        }
    }

    private void ToggleTheme(bool? toLight = null)
    {
        _isLightMode = toLight ?? !_isLightMode;
        Application.Current!.RequestedThemeVariant = _isLightMode ? ThemeVariant.Light : ThemeVariant.Dark;
        GraphControl.IsLightMode = _isLightMode;
        GraphControl.InvalidateVisual();
        ThemeToggleBtn.Content = _isLightMode ? "🌙 DARK" : "☀️ LIGHT";
        ThemeToggleBtn.Foreground = _isLightMode ? SolidColorBrush.Parse("#0284C7") : SolidColorBrush.Parse("#00F0FF");
    }

    private void ToggleLeftSidebar(bool? expand = null)
    {
        _leftSidebarExpanded = expand ?? !_leftSidebarExpanded;
        WorkspaceGrid.ColumnDefinitions[0].Width = _leftSidebarExpanded ? new GridLength(260) : new GridLength(28);
        LeftSidebar.IsVisible = _leftSidebarExpanded;
        LeftCollapsedStrip.IsVisible = !_leftSidebarExpanded;
    }

    private void ToggleRightSidebar(bool? expand = null)
    {
        _rightSidebarExpanded = expand ?? !_rightSidebarExpanded;
        WorkspaceGrid.ColumnDefinitions[2].Width = _rightSidebarExpanded ? new GridLength(300) : new GridLength(28);
        RightSidebar.IsVisible = _rightSidebarExpanded;
        RightCollapsedStrip.IsVisible = !_rightSidebarExpanded;
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                BeginMoveDrag(e);
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        HideFullScreenArrows();
    }

    private void HideFullScreenArrows()
    {
        try
        {
            foreach (var btn in this.GetVisualDescendants().OfType<Button>())
            {
                if (btn.Name == "PART_FullScreenButton" || btn.Name == "PART_PopoverFullScreenButton" || btn.Name?.Contains("FullScreen") == true)
                {
                    btn.IsVisible = false;
                    btn.Width = 0;
                    btn.MaxWidth = 0;
                    btn.Opacity = 0;
                }
            }
        }
        catch { }
    }

    private void RefreshTraceManagerUI()
    {
        // Trace manager UI refresh
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _engine.Dispose();
        _syntheticGen?.Dispose();
#pragma warning disable CA1416
        try
        {
            _wasapiOutputDevice?.Dispose();
        }
        catch { }
#pragma warning restore CA1416
    }
}
