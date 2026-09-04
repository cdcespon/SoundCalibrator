using System;

namespace SoundCalibrator.Audio.Interfaces;

public sealed class AudioBlockEventArgs : EventArgs
{
    public float[] Reference { get; }
    public float[] Measurement { get; }
    public int SampleCount { get; }

    public AudioBlockEventArgs(float[] reference, float[] measurement, int sampleCount)
    {
        Reference = reference;
        Measurement = measurement;
        SampleCount = sampleCount;
    }
}

public interface IAudioCaptureDevice : IDisposable
{
    string DeviceName { get; }
    int SampleRate { get; }
    int Channels { get; }
    bool IsRunning { get; }

    event EventHandler<AudioBlockEventArgs>? AudioBlockAvailable;

    void Start();
    void Stop();
}
