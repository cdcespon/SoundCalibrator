using System;
using System.Threading;
using SoundCalibrator.Audio.Buffers;
using SoundCalibrator.Audio.Interfaces;
using SoundCalibrator.Core.Averaging;
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
    public int FftSize { get; }
    public float SampleRate { get; }
    public int AverageCount { get; }

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
    private readonly TransferFunctionCalculator _calculator;
    private readonly SpectralAverager _averager;
    private readonly TransferFunctionResult _rawResult;

    private readonly float[] _refChunk;
    private readonly float[] _measChunk;

    private IAudioCaptureDevice? _device;
    private bool _isProcessing;
    private bool _disposed;
    private readonly Thread _processingThread;

    public int FftSize => _calculator.FftSize;
    public float SampleRate => _device?.SampleRate ?? 48000f;

    public WindowType WindowType { get; set; } = WindowType.Hann;
    public OctaveSmoothingType Smoothing { get; set; } = OctaveSmoothingType.None;
    public AveragingType Averaging
    {
        get => _averager.Mode;
        set => _averager.Mode = value;
    }

    public event Action<MeasurementSnapshot>? SnapshotReady;

    public AcousticMeasurementEngine(int fftSize = 1024)
    {
        int capacity = fftSize * 8;
        _refFifo = new AudioFifoBuffer(capacity);
        _measFifo = new AudioFifoBuffer(capacity);

        _calculator = new TransferFunctionCalculator(fftSize, WindowType.Hann);
        _averager = new SpectralAverager(fftSize);
        _rawResult = new TransferFunctionResult(fftSize);

        _refChunk = new float[fftSize];
        _measChunk = new float[fftSize];

        _processingThread = new Thread(ProcessingLoop)
        {
            IsBackground = true,
            Name = "AcousticDspWorker",
            Priority = ThreadPriority.AboveNormal
        };
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

            int fft = FftSize;
            if (_refFifo.AvailableRead >= fft && _measFifo.AvailableRead >= fft)
            {
                _refFifo.Read(_refChunk);
                _measFifo.Read(_measChunk);

                // Calcular Función de Transferencia con Promediado Espectral
                _calculator.Calculate(_refChunk, _measChunk, _averager, _rawResult);

                // Generar Snapshot para UI
                var snapshot = new MeasurementSnapshot(fft, SampleRate, _averager.SampleCount);
                
                // Aplicar suavizado de octava opcional a la magnitud
                OctaveSmoother.Smooth(_rawResult.MagnitudeDb, snapshot.MagnitudeDb, Smoothing, SampleRate, fft);

                Array.Copy(_rawResult.PhaseDegrees, snapshot.PhaseDegrees, _rawResult.BinCount);
                Array.Copy(_rawResult.Coherence, snapshot.Coherence, _rawResult.BinCount);

                SnapshotReady?.Invoke(snapshot);

                // Tasa de refresco amigable: ~30-60 FPS
                Thread.Sleep(15);
            }
            else
            {
                Thread.Sleep(5);
            }
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
