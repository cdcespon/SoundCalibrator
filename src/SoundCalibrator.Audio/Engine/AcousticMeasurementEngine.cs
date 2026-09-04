using System;
using System.Threading;
using SoundCalibrator.Audio.Buffers;
using SoundCalibrator.Audio.Interfaces;
using SoundCalibrator.Core.Averaging;
using SoundCalibrator.Core.Calibration;
using SoundCalibrator.Core.DSP;
using SoundCalibrator.Core.Models;
using SoundCalibrator.Core.Smoothing;

namespace SoundCalibrator.Audio.Engine;

public sealed class MeasurementSnapshot
{
    public float[] Frequencies { get; }
    public float[] MagnitudeDb { get; }
    public float[] PhaseDegrees { get; }
    public float[] Coherence { get; }
    public float[] RtaDb { get; }
    public float[] RtaMaxHoldDb { get; }

    public int FftSize { get; }
    public float SampleRate { get; }
    public int AverageCount { get; }
    public int BinCount => Frequencies.Length;
    public DelayResult Delay { get; set; } = new();
    public bool IsRtaMode { get; set; }

    public MeasurementSnapshot(int fftSize, float sampleRate, int averageCount)
    {
        FftSize = fftSize;
        SampleRate = sampleRate;
        AverageCount = averageCount;
        int count = fftSize / 2 + 1;

        Frequencies = new float[count];
        MagnitudeDb = new float[count];
        PhaseDegrees = new float[count];
        Coherence = new float[count];
        RtaDb = new float[count];
        RtaMaxHoldDb = new float[count];

        float deltaF = sampleRate / fftSize;
        for (int i = 0; i < count; i++)
        {
            Frequencies[i] = i * deltaF;
        }
    }
}

public sealed class AcousticMeasurementEngine : IDisposable
{
    private readonly AudioFifoBuffer _refFifo;
    private readonly AudioFifoBuffer _measFifo;
    private TransferFunctionCalculator _calculator;
    private SpectralAverager _averager;
    private TransferFunctionResult _rawResult;
    private ImpulseResponseCalculator _irCalculator;
    private RtaCalculator _rtaCalculator;

    private float[] _refChunk;
    private float[] _measChunk;
    private float[] _irBuffer;

    private IAudioCaptureDevice? _device;
    private bool _isProcessing;
    private bool _disposed;
    private readonly Thread _processingThread;
    private readonly object _configLock = new();

    public int FftSize => _calculator.FftSize;
    public float SampleRate => _device?.SampleRate ?? 48000f;

    public WindowType WindowType { get; set; } = WindowType.Hann;
    public OctaveSmoothingType Smoothing { get; set; } = OctaveSmoothingType.None;
    public AveragingType Averaging
    {
        get => _averager.Mode;
        set => _averager.Mode = value;
    }

    public MicrophoneCalibration Calibration { get; } = new();
    public float DelayCompensationMs { get; set; } = 0f;
    public bool IsRtaMode { get; set; } = false;

    public event Action<MeasurementSnapshot>? SnapshotReady;

    public AcousticMeasurementEngine(int fftSize = 1024)
    {
        int capacity = fftSize * 8;
        _refFifo = new AudioFifoBuffer(capacity);
        _measFifo = new AudioFifoBuffer(capacity);

        _calculator = new TransferFunctionCalculator(fftSize, WindowType.Hann);
        _averager = new SpectralAverager(fftSize);
        _rawResult = new TransferFunctionResult(fftSize);
        _irCalculator = new ImpulseResponseCalculator(fftSize);
        _rtaCalculator = new RtaCalculator(fftSize, WindowType.Hann);

        _refChunk = new float[fftSize];
        _measChunk = new float[fftSize];
        _irBuffer = new float[fftSize];

        _processingThread = new Thread(ProcessingLoop)
        {
            IsBackground = true,
            Name = "AcousticDspWorker",
            Priority = ThreadPriority.AboveNormal
        };
    }

    public void ReconfigureFft(int newFftSize, WindowType windowType)
    {
        lock (_configLock)
        {
            WindowType = windowType;
            _calculator = new TransferFunctionCalculator(newFftSize, windowType);
            _averager = new SpectralAverager(newFftSize);
            _rawResult = new TransferFunctionResult(newFftSize);
            _irCalculator = new ImpulseResponseCalculator(newFftSize);
            _rtaCalculator = new RtaCalculator(newFftSize, windowType);

            _refChunk = new float[newFftSize];
            _measChunk = new float[newFftSize];
            _irBuffer = new float[newFftSize];

            Reset();
        }
    }

    public void ResetRtaMaxHold()
    {
        lock (_configLock)
        {
            _rtaCalculator.ResetMaxHold();
        }
    }

    public void AttachDevice(IAudioCaptureDevice device)
    {
        if (_device != null)
        {
            _device.AudioBlockAvailable -= OnAudioBlockAvailable;
            _device.Stop();
        }

        _device = device;
        _device.AudioBlockAvailable += OnAudioBlockAvailable;
        Reset();
    }

    public void Start()
    {
        if (_isProcessing) return;

        _isProcessing = true;
        _device?.Start();

        if (!_processingThread.IsAlive)
        {
            _processingThread.Start();
        }
    }

    public void Stop()
    {
        _isProcessing = false;
        _device?.Stop();
    }

    public void Reset()
    {
        _refFifo.Clear();
        _measFifo.Clear();
        _averager.Reset();
        _rtaCalculator?.ResetMaxHold();
    }

    private void OnAudioBlockAvailable(object? sender, AudioBlockEventArgs e)
    {
        if (!_isProcessing) return;

        _refFifo.Write(e.Reference);
        _measFifo.Write(e.Measurement);
    }

    private void ProcessingLoop()
    {
        while (!_disposed)
        {
            if (!_isProcessing || _device == null)
            {
                Thread.Sleep(20);
                continue;
            }

            lock (_configLock)
            {
                int fft = FftSize;
                if (_refFifo.AvailableRead >= fft && _measFifo.AvailableRead >= fft)
                {
                    _refFifo.Read(_refChunk);
                    _measFifo.Read(_measChunk);

                    var snapshot = new MeasurementSnapshot(fft, SampleRate, _averager.SampleCount)
                    {
                        IsRtaMode = IsRtaMode
                    };

                    if (IsRtaMode)
                    {
                        // Modo RTA de 1 canal sobre la señal medida
                        _rtaCalculator.Calculate(_measChunk, snapshot.RtaDb, snapshot.RtaMaxHoldDb);
                        
                        // Aplicar suavizado de octava opcional al espectro RTA
                        OctaveSmoother.Smooth(snapshot.RtaDb, snapshot.MagnitudeDb, Smoothing, SampleRate, fft);
                    }
                    else
                    {
                        // Modo Transfer Function Dual-Channel
                        _calculator.Calculate(_refChunk, _measChunk, _averager, _rawResult);
                        OctaveSmoother.Smooth(_rawResult.MagnitudeDb, snapshot.MagnitudeDb, Smoothing, SampleRate, fft);

                        Array.Copy(_rawResult.PhaseDegrees, snapshot.PhaseDegrees, _rawResult.BinCount);
                        Array.Copy(_rawResult.Coherence, snapshot.Coherence, _rawResult.BinCount);

                        if (!Calibration.IsEmpty)
                        {
                            Calibration.ApplyCorrection(snapshot.Frequencies, snapshot.MagnitudeDb, snapshot.PhaseDegrees);
                        }

                        _irCalculator.CalculateImpulseResponse(snapshot.MagnitudeDb, snapshot.PhaseDegrees, _irBuffer, SampleRate, snapshot.Delay);

                        if (MathF.Abs(DelayCompensationMs) > 0.001f)
                        {
                            float compSeconds = DelayCompensationMs / 1000f;
                            for (int k = 0; k < snapshot.BinCount; k++)
                            {
                                float f = snapshot.Frequencies[k];
                                float compensated = snapshot.PhaseDegrees[k] + (360f * f * compSeconds);
                                snapshot.PhaseDegrees[k] = ((compensated + 180f) % 360f + 360f) % 360f - 180f;
                            }
                        }
                    }

                    SnapshotReady?.Invoke(snapshot);
                }
            }

            Thread.Sleep(15);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _device?.Dispose();
    }
}
