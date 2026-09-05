using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SoundCalibrator.Audio.Devices;
using SoundCalibrator.Audio.Engine;
using SoundCalibrator.Audio.Generators;
using SoundCalibrator.Core.Averaging;
using SoundCalibrator.Core.Models;
using SoundCalibrator.Core.Smoothing;
using SoundCalibrator.Core.Analysis;
using Avalonia.Platform.Storage;
using SoundCalibrator.Core.Calibration;
using SoundCalibrator.Core.Serialization;
using SoundCalibrator.Core.Reporting;
using Avalonia;
using Avalonia.Media.Imaging;
namespace SoundCalibrator.App;
public partial class MainWindow : Window
{
    private readonly AcousticMeasurementEngine _engine;
    private readonly SplMeter _splMeter = new();
    private SyntheticAudioGenerator? _syntheticGen;
    private IDisposable? _wasapiDevice;
    private bool _isPaused;
    private float _lastDetectedDelayMs;
    private bool _isAligned;
    private MeasurementSnapshot? _lastSnapshot;
    private AlignmentSuggestion? _lastAlignmentSuggestion;
    private System.Collections.Generic.IReadOnlyList<PeqFilterSuggestion>? _lastPeqSuggestions;
    private StiResult? _lastSti;
    private DelayMatrixReport? _lastDelayMatrixReport;
    private ImdResult? _lastImd;
    private static readonly string[] TraceColors = ["#E040FB", "#76FF03", "#FFD600", "#FF4081", "#00E676", "#448AFF"];
    public MainWindow()
    {
        InitializeComponent();
        _engine = new AcousticMeasurementEngine(1024);
        _syntheticGen = new SyntheticAudioGenerator(sampleRate: 48000, blockSize: 512);
        _engine.AttachDevice(_syntheticGen);
        _engine.SnapshotReady += OnSnapshotReady;
        SetupEventHandlers();
        RefreshTraceManagerUI();
        _engine.Start();
    }
    private void SetupEventHandlers()
    {
        StartStopBtn.Click += OnStartStopClick;
        ResetBtn.Click += (s, e) =>
        {
            _engine.Reset();
            _engine.DelayCompensationMs = 0f;
            _isAligned = false;
            AutoAlignBtn.Content = "ALIGN";
            AutoAlignBtn.Background = Avalonia.Media.SolidColorBrush.Parse("#E65100");
        };
        AutoAlignBtn.Click += (s, e) =>
        {
            if (!_isAligned)
            {
                _engine.DelayCompensationMs = -_lastDetectedDelayMs;
                _isAligned = true;
                AutoAlignBtn.Content = "UNALIGN";
                AutoAlignBtn.Background = Avalonia.Media.SolidColorBrush.Parse("#388E3C");
            }
            else
            {
                _engine.DelayCompensationMs = 0f;
                _isAligned = false;
                AutoAlignBtn.Content = "ALIGN";
                AutoAlignBtn.Background = Avalonia.Media.SolidColorBrush.Parse("#E65100");
            }
        };
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
                TracesCountText.Text = $"Captured Traces: {GraphControl.StoredTraces.Count}";
                GraphControl.InvalidateVisual();
                RefreshTraceManagerUI();
            }
        };
        ClearTracesBtn.Click += (s, e) =>
        {
            GraphControl.StoredTraces.Clear();
            TracesCountText.Text = "Captured Traces: 0";
            GraphControl.InvalidateVisual();
            RefreshTraceManagerUI();
        };
        DelaySlider.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == nameof(Slider.Value) && _syntheticGen != null)
            {
                float val = (float)DelaySlider.Value;
                _syntheticGen.DelayMs = val;
                DelayText.Text = $"{val:0.0} ms";
            }
        };
        InvertPolarityBtn.Click += (s, e) =>
        {
            _engine.InvertPolarity = !_engine.InvertPolarity;
            InvertPolarityBtn.Background = _engine.InvertPolarity
                ? Avalonia.Media.SolidColorBrush.Parse("#D32F2F")
                : Avalonia.Media.SolidColorBrush.Parse("#263238");
            InvertPolarityBtn.Foreground = _engine.InvertPolarity
                ? Avalonia.Media.SolidColorBrush.Parse("#FFFFFF")
                : Avalonia.Media.SolidColorBrush.Parse("#B0BEC5");
        };
        MinPhaseBtn.Click += (s, e) =>
        {
            GraphControl.ShowMinimumPhase = !GraphControl.ShowMinimumPhase;
            MinPhaseBtn.Background = GraphControl.ShowMinimumPhase
                ? Avalonia.Media.SolidColorBrush.Parse("#2E7D32")
                : Avalonia.Media.SolidColorBrush.Parse("#263238");
            MinPhaseBtn.Foreground = GraphControl.ShowMinimumPhase
                ? Avalonia.Media.SolidColorBrush.Parse("#FFFFFF")
                : Avalonia.Media.SolidColorBrush.Parse("#B0BEC5");
            GraphControl.InvalidateVisual();
        };
        ModeCombo.SelectionChanged += OnModeChanged;
        SourceCombo.SelectionChanged += OnSourceChanged;
        FftCombo.SelectionChanged += OnFftOrWindowChanged;
        WindowCombo.SelectionChanged += OnFftOrWindowChanged;
        AveragingCombo.SelectionChanged += OnAveragingChanged;
        SmoothingCombo.SelectionChanged += OnSmoothingChanged;
        BlankingCombo.SelectionChanged += OnBlankingChanged;
        DeltaBtn.Click += (s, e) =>
        {
            GraphControl.ShowDeltaCurve = !GraphControl.ShowDeltaCurve;
            DeltaBtn.Background = GraphControl.ShowDeltaCurve
                ? Avalonia.Media.SolidColorBrush.Parse("#8E24AA")
                : Avalonia.Media.SolidColorBrush.Parse("#263238");
            DeltaBtn.Foreground = GraphControl.ShowDeltaCurve
                ? Avalonia.Media.SolidColorBrush.Parse("#FFFFFF")
                : Avalonia.Media.SolidColorBrush.Parse("#B0BEC5");
            GraphControl.InvalidateVisual();
        };
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
        SaveSessionBtn.Click += async (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save SoundCalibrator Session Project (.scproj)",
                DefaultExtension = "scproj",
                SuggestedFileName = $"Session_{DateTime.Now:yyyyMMdd_HHmmss}.scproj"
            });
            if (file != null)
            {
                var session = new ProjectSession
                {
                    ProjectName = "SoundCalibrator Live Tuning",
                    SampleRate = (int)_engine.SampleRate,
                    FftSize = _engine.FftSize,
                    WindowType = (WindowCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Hann",
                    AveragingType = _engine.Averaging.ToString(),
                    DelayCompensationMs = _engine.DelayCompensationMs,
                    InvertPolarity = _engine.InvertPolarity,
                    SplOffsetDb = _splMeter.SplOffsetDb,
                    TargetCurvePresetName = GraphControl.ActiveTargetCurve?.Name,
                    StoredTraces = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(GraphControl.StoredTraces, AcousticTraceDto.FromModel))
                };
                string json = SessionSerializer.Serialize(session);
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new System.IO.StreamWriter(stream);
                await writer.WriteAsync(json);
                StatusDeviceText.Text = $"Session Saved: {file.Name} ({session.StoredTraces.Count} traces)";
            }
        };

        OpenSessionBtn.Click += async (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open SoundCalibrator Session Project (.scproj, .json)",
                AllowMultiple = false
            });
            if (files.Count > 0)
            {
                var file = files[0];
                await using var stream = await file.OpenReadAsync();
                using var reader = new System.IO.StreamReader(stream);
                string json = await reader.ReadToEndAsync();
                var session = SessionSerializer.Deserialize(json);
                if (session != null)
                {
                    GraphControl.StoredTraces.Clear();
                    foreach (var dto in session.StoredTraces)
                    {
                        GraphControl.StoredTraces.Add(dto.ToModel());
                    }
                    _engine.DelayCompensationMs = session.DelayCompensationMs;
                    _engine.InvertPolarity = session.InvertPolarity;
                    TracesCountText.Text = $"Captured Traces: {GraphControl.StoredTraces.Count}";
                    RefreshTraceManagerUI();
                    GraphControl.InvalidateVisual();
                    StatusDeviceText.Text = $"Session Loaded: {file.Name} ({session.StoredTraces.Count} traces restored)";
                }
            }
        };

        ExportTraceBtn.Click += async (s, e) =>
        {
            if (GraphControl.StoredTraces.Count == 0 && _lastSnapshot == null) return;
            var traceToExport = GraphControl.StoredTraces.Count > 0
                ? GraphControl.StoredTraces[^1]
                : new AcousticTrace("Measurement", "#00E5FF", _lastSnapshot!.Frequencies, _lastSnapshot.MagnitudeDb, _lastSnapshot.PhaseDegrees, _lastSnapshot.Coherence);
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export Acoustic Trace (CSV)",
                    DefaultExtension = "csv",
                    SuggestedFileName = $"{traceToExport.Name.Replace(' ', '_')}.csv"
                });
                if (file != null)
                {
                    string csv = TraceSerializer.ExportToCsv(traceToExport);
                    await using var stream = await file.OpenWriteAsync();
                    await using var writer = new System.IO.StreamWriter(stream);
                    await writer.WriteAsync(csv);
                }
            }
        };
        ToggleTracesPanelBtn.Click += (s, e) =>
        {
            TraceManagerBorder.IsVisible = !TraceManagerBorder.IsVisible;
        };
        CloseTracePanelBtn.Click += (s, e) =>
        {
            TraceManagerBorder.IsVisible = false;
        };
        CalculateDelayMatrixBtn.Click += (s, e) =>
        {
            var visibleTraces = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(GraphControl.StoredTraces, t => t.IsVisible));
            if (visibleTraces.Count < 2)
            {
                DelayMatrixResultText.Text = "Requires â‰¥2 visible traces with detected delay.";
                DelayMatrixResultText.Foreground = Avalonia.Media.SolidColorBrush.Parse("#FF5252");
                return;
            }

            var zones = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(visibleTraces, t => (t.Name, t.DetectedDelayMs)));
            var report = AcousticDelayMatrix.CalculateAlignmentMatrix(zones, anchorIndex: 0, temperatureCelsius: 20.0f);
            _lastDelayMatrixReport = report;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Anchor: {report.AnchorZoneName} (c={report.SpeedOfSoundMps:0} m/s)");
            for (int i = 1; i < report.Alignments.Count; i++)
            {
                var a = report.Alignments[i];
                string sign = a.RequiredDelayOffsetMs >= 0 ? "+" : "";
                sb.AppendLine($"â€¢ {a.Name}: {sign}{a.RequiredDelayOffsetMs:0.0} ms ({sign}{a.RelativeDistanceMeters:0.00} m)");
            }

            DelayMatrixResultText.Text = sb.ToString().TrimEnd();
            DelayMatrixResultText.Foreground = Avalonia.Media.SolidColorBrush.Parse("#FFB300");
        };

        CalculateAlignmentBtn.Click += (s, e) =>
        {
            if (GraphControl.StoredTraces.Count < 2)
            {
                AlignmentResultText.Text = "Please capture at least 2 traces\n(Trace 1 = Sub, Trace 2 = Main)";
                AlignmentResultText.Foreground = Avalonia.Media.SolidColorBrush.Parse("#FF8A80");
                ApplyAlignmentBtn.IsVisible = false;
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
            var suggestion = CrossoverAlignmentAnalyzer.Analyze(sub, main, fc);
            _lastAlignmentSuggestion = suggestion;
            AlignmentResultText.Text = $"Δθ: {suggestion.PhaseDeltaDeg:+0.0;-0.0;0.0}° | Fc: {fc:0}Hz\nDelay: {suggestion.RecommendedDelayMs:+0.00;-0.00;0.00} ms ({suggestion.RecommendedDistanceMeters:+0.00;-0.00;0.00}m)\nPolarity: {(suggestion.RecommendPolarityInversion ? "INVERT Ø" : "NORMAL Ø")}\nSum Gain: {suggestion.PredictedSummationGainDb:+0.0;-0.0;0.0} dB";
            AlignmentResultText.Foreground = Avalonia.Media.SolidColorBrush.Parse("#00E5FF");
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
                StatusDeviceText.Text = $"Aligned {sub.Name}: {sub.OffsetDelayMs:+0.00}ms, Polarity: {(sub.InvertPolarity ? "INV" : "NOR")}";
            }
        };
        ReportBtn.Click += async (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Acoustic Calibration Technical Report",
                DefaultExtension = "md",
                SuggestedFileName = $"Calibration_Report_{DateTime.Now:yyyyMMdd_HHmmss}.md"
            });
            if (file != null)
            {
                var reportData = new CalibrationReportData
                {
                    ProjectName = "SoundCalibrator Live Tuning",
                    SampleRate = (int)_engine.SampleRate,
                    FftSize = _engine.FftSize,
                    WindowType = (WindowCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Hann",
                    AveragingType = _engine.Averaging.ToString(),
                    TargetCurveName = GraphControl.ActiveTargetCurve?.Name ?? "None",
                    DelayCompensationMs = _engine.DelayCompensationMs,
                    InvertPolarity = _engine.InvertPolarity,
                    Rt60 = _lastSnapshot != null && !_lastSnapshot.IsRtaMode && _lastSnapshot.ImpulseResponse.Length > 0 
                        ? ReverberationTimeCalculator.Calculate(_lastSnapshot.ImpulseResponse, (int)_lastSnapshot.SampleRate) 
                        : null,
                    Etc = _lastSnapshot != null && !_lastSnapshot.IsRtaMode && _lastSnapshot.ImpulseResponse.Length > 0
                        ? EtcCalculator.Calculate(_lastSnapshot.ImpulseResponse, (int)_lastSnapshot.SampleRate)
                        : null,
                    Thd = _lastSnapshot != null && _lastSnapshot.IsRtaMode 
                        ? ThdCalculator.Calculate(_lastSnapshot.Frequencies, _lastSnapshot.RtaDb) 
                        : null,
                    Imd = _lastImd,
                    Sti = _lastSti,
                    Alignment = _lastAlignmentSuggestion,
                    DelayMatrix = _lastDelayMatrixReport,
                    PeqFilters = _lastPeqSuggestions,
                    Traces = GraphControl.StoredTraces
                };
                bool isHtml = file.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
                string text = isHtml ? ReportGenerator.GenerateHtml(reportData) : ReportGenerator.GenerateMarkdown(reportData);
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new System.IO.StreamWriter(stream);
                await writer.WriteAsync(text);
                StatusDeviceText.Text = $"Report Saved: {file.Name}";
            }
        };
        ToggleRtaBarsBtn.Click += (s, e) =>
        {
            GraphControl.RtaBarMode = !GraphControl.RtaBarMode;
            ToggleRtaBarsBtn.Background = GraphControl.RtaBarMode
                ? Avalonia.Media.SolidColorBrush.Parse("#F57F17")
                : Avalonia.Media.SolidColorBrush.Parse("#37474F");
            GraphControl.InvalidateVisual();
        };

        CalibrateSplBtn.Click += (s, e) =>
        {
            if (_lastSnapshot != null && _lastSnapshot.IsRtaMode)
            {
                int bin1k = (int)Math.Round(1000f / (_engine.SampleRate / _engine.FftSize));
                bin1k = Math.Clamp(bin1k, 0, _lastSnapshot.RtaDb.Length - 1);
                float measured1k = _lastSnapshot.RtaDb[bin1k];
                _splMeter.CalibrateWithTone(measured1k, 94.0f);
                StatusDeviceText.Text = $"SPL Calibrated: 94.0 dBSPL @ 1kHz (Input: {measured1k:0.0} dBFS, Offset: {_splMeter.SplOffsetDb:0.0} dB)";
            }
            else
            {
                StatusDeviceText.Text = "To calibrate SPL: switch to RTA mode, apply 94dB 1kHz calibrator to mic and click CAL 94dB.";
            }
        };
        SimulateSumBtn.Click += (s, e) =>
        {
            var visibleTraces = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(GraphControl.StoredTraces, t => t.IsVisible && !t.IsRtaTrace));
            if (visibleTraces.Count >= 2)
            {
                var sumTrace = AcousticSummationSimulator.SimulateSummation(visibleTraces, $"Sum ({visibleTraces.Count} sources)", "#FF4081");
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
            var visibleTraces = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(GraphControl.StoredTraces, t => t.IsVisible && !t.IsRtaTrace));
            if (visibleTraces.Count >= 2)
            {
                var diffTrace = SoundCalibrator.Core.Operations.TraceMath.DivideTraces(
                    visibleTraces[0],
                    visibleTraces[1],
                    $"Diff ({visibleTraces[0].Name} / {visibleTraces[1].Name})",
                    "#E040FB");
                if (diffTrace != null)
                {
                    GraphControl.StoredTraces.Add(diffTrace);
                    RefreshTraceManagerUI();
                    GraphControl.InvalidateVisual();
                }
            }
        };

        SpatialAvgBtn.Click += (s, e) =>
        {
            var visibleTraces = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(GraphControl.StoredTraces, t => t.IsVisible));
            if (visibleTraces.Count >= 2)
            {
                var avgTrace = SpatialAverager.CalculateSpatialAverage(
                    visibleTraces, 
                    SpatialAverageMode.CoherenceWeightedPower, 
                    $"Spatial Avg ({visibleTraces.Count} mics)", 
                    "#00E5FF");
                if (avgTrace != null)
                {
                    GraphControl.StoredTraces.Add(avgTrace);
                    RefreshTraceManagerUI();
                }
            }
        };
        SuggestEqBtn.Click += (s, e) =>
        {
            if (_lastSnapshot == null || GraphControl.ActiveTargetCurve == null)
            {
                StatusDeviceText.Text = "Please select a Target Curve (e.g. Harman) to compute PEQ correction.";
                return;
            }
            int count = _lastSnapshot.BinCount;
            float[] delta = new float[count];
            for (int i = 0; i < count; i++)
            {
                float f = _lastSnapshot.Frequencies[i];
                float target = GraphControl.ActiveTargetCurve.Evaluate(f);
                delta[i] = _lastSnapshot.MagnitudeDb[i] - target;
            }
            _lastPeqSuggestions = PeqSuggester.SuggestFilters(_lastSnapshot.Frequencies, delta, maxFilters: 5);
            if (_lastPeqSuggestions.Count == 0)
            {
                StatusDeviceText.Text = "PEQ: System within +/-2dB of target curve. No filters needed.";
                GraphControl.ShowPeqPreview = false;
            }
            else
            {
                GraphControl.ActivePeqFilters = _lastPeqSuggestions;
                GraphControl.ShowPeqPreview = !GraphControl.ShowPeqPreview;
                string summary = string.Join(" | ", System.Linq.Enumerable.Select(_lastPeqSuggestions, f => $"{f.FrequencyHz:0}Hz {f.GainDb:+0.0;-0.0}dB Q:{f.Q:0.0}"));
                StatusDeviceText.Text = $"PEQ Preview {(GraphControl.ShowPeqPreview ? "ON" : "OFF")}: {summary}";
            }
            GraphControl.InvalidateVisual();
        };
        SnapshotBtn.Click += async (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            int w = Math.Max(800, (int)GraphControl.Bounds.Width);
            int h = Math.Max(500, (int)GraphControl.Bounds.Height);
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Acoustic Measurement Snapshot (PNG)",
                DefaultExtension = "png",
                SuggestedFileName = $"SoundCalibrator_Report_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            });
            if (file != null)
            {
                using var rtb = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
                rtb.Render(GraphControl);
                await using var stream = await file.OpenWriteAsync();
#pragma warning disable CS0618
                rtb.Save(stream);
#pragma warning restore CS0618
                StatusDeviceText.Text = $"Snapshot Saved: {file.Name}";
            }
        };
        ImportTraceBtn.Click += async (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Import Trace (.csv) or Mic Calibration (.cal, .txt)",
                    AllowMultiple = false
                });
                if (files.Count > 0)
                {
                    var file = files[0];
                    await using var stream = await file.OpenReadAsync();
                    using var reader = new System.IO.StreamReader(stream);
                    string text = await reader.ReadToEndAsync();
                    if (file.Name.EndsWith(".cal", System.StringComparison.OrdinalIgnoreCase) ||
                        file.Name.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _engine.Calibration.LoadFromText(text);
                        StatusDeviceText.Text = $"Cal Loaded: {file.Name} ({_engine.Calibration.Points.Count} pts)";
                    }
                    else
                    {
                        try
                        {
                            var trace = TraceSerializer.ImportFromCsv(text);
                            GraphControl.StoredTraces.Add(trace);
                            TracesCountText.Text = $"Captured Traces: {GraphControl.StoredTraces.Count}";
                            GraphControl.InvalidateVisual();
                            RefreshTraceManagerUI();
                        }
                        catch
                        {
                            // Formato no reconocido
                        }
                    }
                }
            }
        };
    }
    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Avalonia.Input.Key.Space:
                OnStartStopClick(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Avalonia.Input.Key.C:
                CaptureTraceBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Avalonia.Input.Key.R:
                ResetBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Avalonia.Input.Key.A:
                if (AutoAlignBtn.IsVisible)
                {
                    AutoAlignBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                break;
            case Avalonia.Input.Key.M:
                ModeCombo.SelectedIndex = (ModeCombo.SelectedIndex + 1) % ModeCombo.ItemCount;
                e.Handled = true;
                break;
            case Avalonia.Input.Key.P:
                InvertPolarityBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Avalonia.Input.Key.D:
                DeltaBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Avalonia.Input.Key.T:
                TraceManagerBorder.IsVisible = !TraceManagerBorder.IsVisible;
                e.Handled = true;
                break;
            case Avalonia.Input.Key.S:
                SnapshotBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Avalonia.Input.Key.E:
                SuggestEqBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                break;
            case Avalonia.Input.Key.D1 or Avalonia.Input.Key.NumPad1:
                SmoothingCombo.SelectedIndex = 0;
                e.Handled = true;
                break;
            case Avalonia.Input.Key.D2 or Avalonia.Input.Key.NumPad2:
                SmoothingCombo.SelectedIndex = 1;
                e.Handled = true;
                break;
            case Avalonia.Input.Key.D3 or Avalonia.Input.Key.NumPad3:
                SmoothingCombo.SelectedIndex = 2;
                e.Handled = true;
                break;
            case Avalonia.Input.Key.D4 or Avalonia.Input.Key.NumPad4:
                SmoothingCombo.SelectedIndex = 3;
                e.Handled = true;
                break;
        }
    }
    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        int mode = ModeCombo.SelectedIndex;
        bool isTf = mode == 0;
        bool isRta = mode == 1;
        bool isSpectro = mode == 2;
        bool isGroupDelay = mode == 3;
        bool isImpulseEtc = mode == 4;

        _engine.IsRtaMode = isRta;
        GraphControl.IsSpectrogramMode = isSpectro;
        GraphControl.ShowGroupDelay = isGroupDelay;
        GraphControl.ShowImpulseEtc = isImpulseEtc;
        ToggleRtaBarsBtn.IsVisible = isRta;

        bool showControls = isTf || isGroupDelay;
        BlankingPanel.IsVisible = showControls;
        DelayPanel.IsVisible = showControls || isImpulseEtc;
        AutoDelayBorder.IsVisible = showControls || isImpulseEtc;
        DeltaBtn.IsVisible = showControls;
        InvertPolarityBtn.IsVisible = showControls || isImpulseEtc;
        GraphControl.InvalidateVisual();
    }
    private void OnFftOrWindowChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_engine == null || FftCombo == null || WindowCombo == null) return;
        int fftSize = FftCombo.SelectedIndex switch
        {
            0 => 512,
            1 => 1024,
            2 => 2048,
            3 => 4096,
            _ => 1024
        };
        WindowType winType = WindowCombo.SelectedIndex switch
        {
            0 => WindowType.Hann,
            1 => WindowType.BlackmanHarris,
            2 => WindowType.Rectangular,
            _ => WindowType.Hann
        };
        _engine.ReconfigureFft(fftSize, winType);
        UpdateStatusDeviceText();
    }
    private void OnSnapshotReady(MeasurementSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        _lastDetectedDelayMs = snapshot.Delay.DelayMs;
        Dispatcher.UIThread.Post(() =>
        {
            GraphControl.UpdateSnapshot(snapshot);
            StatusAvgText.Text = $"Averages: {snapshot.AverageCount}";
            DetectedDelayText.Text = $"{snapshot.Delay.DelayMs:0.00} ms";
            if (!snapshot.IsRtaMode && snapshot.ImpulseResponse.Length > 0)
            {
                var rt60 = ReverberationTimeCalculator.Calculate(snapshot.ImpulseResponse, (int)snapshot.SampleRate);
                if (rt60.IsValid)
                {
                    Rt60Text.Text = $"RT60: {rt60.T20Seconds:0.00}s";
                    Rt60Text.IsVisible = true;

                    var sti = SpeechIntelligibilityCalculator.CalculateFromRt60AndSnr(rt60.T20Seconds, snrDb: 30f);
                    _lastSti = sti;
                    StiText.Text = $"STI: {sti.Sti:0.00} ({sti.Rating})";
                    StiText.IsVisible = true;
                }
                else
                {
                    Rt60Text.IsVisible = false;
                    StiText.IsVisible = false;
                }
            }
            else
            {
                Rt60Text.IsVisible = false;
                StiText.IsVisible = false;
            }
            if (snapshot.IsRtaMode)
            {
                var thd = ThdCalculator.Calculate(snapshot.Frequencies, snapshot.RtaDb);
                if (thd.FundamentalDb > -60f)
                {
                    ThdText.Text = $"{thd.ThdPercent:0.00}% ({thd.ThdDb:0.0} dB) @ {thd.FundamentalFreqHz:0} Hz";
                    ThdText.Foreground = thd.ThdPercent < 1.0f 
                        ? Avalonia.Media.SolidColorBrush.Parse("#00E676") 
                        : Avalonia.Media.SolidColorBrush.Parse("#FF5252");
                    ThdBadge.IsVisible = true;
                }
                else
                {
                    ThdBadge.IsVisible = false;
                }

                if (_syntheticGen != null && (_syntheticGen.SignalType == TestSignalType.SmpteImd || _syntheticGen.SignalType == TestSignalType.CcifImd))
                {
                    _lastImd = _syntheticGen.SignalType == TestSignalType.SmpteImd
                        ? ImdCalculator.CalculateSmpte(snapshot.Frequencies, snapshot.RtaDb)
                        : ImdCalculator.CalculateCcif(snapshot.Frequencies, snapshot.RtaDb);

                    ImdText.Text = $"{_lastImd.ImdPercent:0.00}% ({_lastImd.ImdDb:0.0}dB)";
                    ImdBadge.IsVisible = true;
                }
                else
                {
                    ImdBadge.IsVisible = false;
                }
                var spl = _splMeter.CalculateSpl(snapshot.Frequencies, snapshot.RtaDb);
                SplText.Text = $"{spl.DbA:0.0} dBA | {spl.DbC:0.0} dBC";
                SplBadge.IsVisible = true;
                var feedbacks = FeedbackHunter.Detect(snapshot.Frequencies, snapshot.RtaDb);
                if (feedbacks.Count > 0)
                {
                    var worst = feedbacks[0];
                    FeedbackText.Text = $"{worst.FrequencyHz:0} Hz ({worst.ProminenceDb:+0.0} dB)";
                    FeedbackBadge.IsVisible = true;
                }
                else
                {
                    FeedbackBadge.IsVisible = false;
                }
            }
            else
            {
                ThdBadge.IsVisible = false;
                SplBadge.IsVisible = false;
                FeedbackBadge.IsVisible = false;
            }
        });
    }
    private void OnStartStopClick(object? sender, RoutedEventArgs e)
    {
        if (_isPaused)
        {
            _engine.Start();
            StartStopBtn.Content = "PAUSE";
            StartStopBtn.Background = Avalonia.Media.SolidColorBrush.Parse("#1E88E5");
            _isPaused = false;
        }
        else
        {
            _engine.Stop();
            StartStopBtn.Content = "RESUME";
            StartStopBtn.Background = Avalonia.Media.SolidColorBrush.Parse("#2E7D32");
            _isPaused = true;
        }
    }
    private void OnSourceChanged(object? sender, SelectionChangedEventArgs e)
    {
        int idx = SourceCombo.SelectedIndex;
        if (idx == 0) // Pink noise
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.PinkNoise;
            _engine.AttachDevice(_syntheticGen);
            UpdateStatusDeviceText();
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 1) // Sine 1k
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.SineWave;
            _syntheticGen.SineFrequency = 1000f;
            _engine.AttachDevice(_syntheticGen);
            UpdateStatusDeviceText();
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 2) // Sine Sweep
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.SineSweep;
            _engine.AttachDevice(_syntheticGen);
            UpdateStatusDeviceText();
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 3) // Gated Pink Noise
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.GatedPinkNoise;
            _engine.AttachDevice(_syntheticGen);
            UpdateStatusDeviceText();
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 4) // IEC 60268-1 Noise
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.IecNoise;
            _engine.AttachDevice(_syntheticGen);
            UpdateStatusDeviceText();
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 5) // Polarity Pulse
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.PolarityPulse;
            _engine.AttachDevice(_syntheticGen);
            UpdateStatusDeviceText();
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 6) // SMPTE IMD
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.SmpteImd;
            _engine.AttachDevice(_syntheticGen);
            UpdateStatusDeviceText();
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 7) // CCIF IMD
        {
            _syntheticGen ??= new SyntheticAudioGenerator(48000, 512);
            _syntheticGen.SignalType = TestSignalType.CcifImd;
            _engine.AttachDevice(_syntheticGen);
            UpdateStatusDeviceText();
            if (!_isPaused) _engine.Start();
        }
        else if (idx == 8) // WASAPI
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var dev = WasapiAudioCaptureDevice.GetDefaultRecordingDevice();
                    if (dev != null)
                    {
                        var wasapi = new WasapiAudioCaptureDevice(dev);
                        _wasapiDevice = wasapi;
                        _engine.AttachDevice(wasapi);
                        StatusDeviceText.Text = $"WASAPI: {dev.FriendlyName} ({wasapi.SampleRate} Hz, FFT: {_engine.FftSize})";
                        if (!_isPaused) _engine.Start();
                    }
                    else
                    {
                        StatusDeviceText.Text = "WASAPI: No default recording device found";
                    }
                }
                catch (Exception ex)
                {
                    StatusDeviceText.Text = $"WASAPI Error: {ex.Message}";
                }
            }
            else
            {
                StatusDeviceText.Text = "WASAPI is supported only on Windows";
            }
        }
    }
    private void UpdateStatusDeviceText()
    {
        string srcName = SourceCombo.SelectedIndex switch
        {
            0 => "Pink Noise",
            1 => "Sine 1 kHz",
            2 => "Sine Sweep 20Hz-20kHz",
            3 => "Gated Pink Noise",
            4 => "IEC 60268-1 Program Noise",
            5 => "Polarity Pulse",
            6 => "SMPTE IMD (60Hz + 7kHz)",
            7 => "CCIF IMD (19kHz + 20kHz)",
            _ => "Audio Device"
        };
        StatusDeviceText.Text = $"Synthetic: {srcName} ({_engine.SampleRate} Hz, FFT: {_engine.FftSize})";
    }
    private void OnAveragingChanged(object? sender, SelectionChangedEventArgs e)
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
    }
    private void OnSmoothingChanged(object? sender, SelectionChangedEventArgs e)
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
    }
    private void OnBlankingChanged(object? sender, SelectionChangedEventArgs e)
    {
        GraphControl.CoherenceThreshold = BlankingCombo.SelectedIndex switch
        {
            0 => 0.0f,
            1 => 0.3f,
            2 => 0.5f,
            3 => 0.7f,
            _ => 0.0f
        };
        GraphControl.InvalidateVisual();
    }
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _engine.Dispose();
        _syntheticGen?.Dispose();
        _wasapiDevice?.Dispose();
    }
    private void RefreshTraceManagerUI()
    {
        TraceListStack.Children.Clear();
        if (GraphControl.StoredTraces.Count == 0)
        {
            TraceListStack.Children.Add(new TextBlock
            {
                Text = "No stored traces yet.\nPress [C] or 'CAPTURE' to freeze a curve.",
                Foreground = Avalonia.Media.SolidColorBrush.Parse("#556677"),
                FontSize = 11,
                Margin = new Avalonia.Thickness(6, 16, 6, 8),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                TextAlignment = Avalonia.Media.TextAlignment.Center
            });
            return;
        }
        foreach (var trace in GraphControl.StoredTraces)
        {
            var itemBorder = new Border
            {
                Background = Avalonia.Media.SolidColorBrush.Parse("#1A202C"),
                CornerRadius = new Avalonia.CornerRadius(4),
                Padding = new Avalonia.Thickness(6, 5),
                Margin = new Avalonia.Thickness(0, 0, 0, 6),
                BorderBrush = Avalonia.Media.SolidColorBrush.Parse(trace.HexColor),
                BorderThickness = new Avalonia.Thickness(2, 0, 0, 0)
            };
            var mainStack = new StackPanel();
            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
            };
            var chk = new CheckBox
            {
                IsChecked = trace.IsVisible,
                Margin = new Avalonia.Thickness(0, 0, 4, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            chk.IsCheckedChanged += (s, e) =>
            {
                trace.IsVisible = chk.IsChecked ?? true;
                GraphControl.InvalidateVisual();
            };
            Grid.SetColumn(chk, 0);
            headerGrid.Children.Add(chk);
            var nameText = new TextBlock
            {
                Text = trace.Name,
                Foreground = Avalonia.Media.SolidColorBrush.Parse(trace.HexColor),
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                FontSize = 11,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameText, 1);
            headerGrid.Children.Add(nameText);
            var delBtn = new Button
            {
                Content = "✕",
                Background = Avalonia.Media.Brushes.Transparent,
                Foreground = Avalonia.Media.SolidColorBrush.Parse("#7E8B9B"),
                Padding = new Avalonia.Thickness(4, 1),
                FontSize = 10
            };
            delBtn.Click += (s, e) =>
            {
                GraphControl.StoredTraces.Remove(trace);
                TracesCountText.Text = $"Captured Traces: {GraphControl.StoredTraces.Count}";
                GraphControl.InvalidateVisual();
                RefreshTraceManagerUI();
            };
            Grid.SetColumn(delBtn, 2);
            headerGrid.Children.Add(delBtn);
            mainStack.Children.Add(headerGrid);
            var ctrlStack = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Margin = new Avalonia.Thickness(24, 3, 0, 0)
            };
            var gainDown = new Button { Content = "-1dB", Background = Avalonia.Media.SolidColorBrush.Parse("#263238"), Foreground = Avalonia.Media.Brushes.White, FontSize = 9, Padding = new Avalonia.Thickness(3, 1), Margin = new Avalonia.Thickness(0, 0, 3, 0) };
            var gainUp = new Button { Content = "+1dB", Background = Avalonia.Media.SolidColorBrush.Parse("#263238"), Foreground = Avalonia.Media.Brushes.White, FontSize = 9, Padding = new Avalonia.Thickness(3, 1), Margin = new Avalonia.Thickness(0, 0, 4, 0) };
            var gainLabel = new TextBlock { Text = $"{trace.OffsetDb:+0;-0;0}dB", Foreground = Avalonia.Media.SolidColorBrush.Parse("#8C9BAE"), FontSize = 9, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Avalonia.Thickness(0, 0, 6, 0) };
            gainDown.Click += (s, e) =>
            {
                trace.OffsetDb -= 1f;
                gainLabel.Text = $"{trace.OffsetDb:+0;-0;0}dB";
                GraphControl.InvalidateVisual();
            };
            gainUp.Click += (s, e) =>
            {
                trace.OffsetDb += 1f;
                gainLabel.Text = $"{trace.OffsetDb:+0;-0;0}dB";
                GraphControl.InvalidateVisual();
            };
            var polBtn = new Button
            {
                Content = trace.InvertPolarity ? "Ø INV" : "Ø NOR",
                Background = trace.InvertPolarity ? Avalonia.Media.SolidColorBrush.Parse("#D32F2F") : Avalonia.Media.SolidColorBrush.Parse("#263238"),
                Foreground = Avalonia.Media.Brushes.White,
                FontSize = 9,
                Padding = new Avalonia.Thickness(3, 1)
            };
            polBtn.Click += (s, e) =>
            {
                trace.InvertPolarity = !trace.InvertPolarity;
                polBtn.Content = trace.InvertPolarity ? "Ø INV" : "Ø NOR";
                polBtn.Background = trace.InvertPolarity ? Avalonia.Media.SolidColorBrush.Parse("#D32F2F") : Avalonia.Media.SolidColorBrush.Parse("#263238");
                GraphControl.InvalidateVisual();
            };
            ctrlStack.Children.Add(gainDown);
            ctrlStack.Children.Add(gainLabel);
            ctrlStack.Children.Add(gainUp);
            ctrlStack.Children.Add(polBtn);
            mainStack.Children.Add(ctrlStack);
            itemBorder.Child = mainStack;
            TraceListStack.Children.Add(itemBorder);
        }
    }
}
