using System.Threading.Tasks;
using SoundCalibrator.Audio.Engine;
using SoundCalibrator.Audio.Generators;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class AcousticMeasurementEngineTests
{
    [Fact]
    public async Task Engine_AttachedToSyntheticGenerator_ProducesLiveSnapshots()
    {
        using var engine = new AcousticMeasurementEngine(fftSize: 1024);
        using var generator = new SyntheticAudioGenerator(sampleRate: 48000, blockSize: 256);
        generator.SignalType = TestSignalType.SineWave;
        generator.SineFrequency = 1000f;
        generator.GainDb = 0f;

        engine.AttachDevice(generator);

        var tcs = new TaskCompletionSource<MeasurementSnapshot>();

        engine.SnapshotReady += snapshot =>
        {
            tcs.TrySetResult(snapshot);
        };

        engine.Start();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.True(completed == tcs.Task, "Measurement engine did not produce a snapshot within 2 seconds");

        var snapshot = await tcs.Task;
        Assert.Equal(1024, snapshot.FftSize);
        Assert.Equal(513, snapshot.MagnitudeDb.Length);
        Assert.Equal(513, snapshot.Frequencies.Length);
        Assert.Equal(48000f, snapshot.SampleRate);
    }
}
