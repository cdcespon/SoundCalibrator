using System;
using System.Threading.Tasks;
using SoundCalibrator.Audio.Buffers;
using Xunit;

namespace SoundCalibrator.Core.Tests;

public class AudioFifoBufferTests
{
    [Fact]
    public void BasicWriteAndRead_PreservesDataIntegrity()
    {
        var fifo = new AudioFifoBuffer(100);
        float[] input = [1f, 2f, 3f, 4f, 5f];
        float[] output = new float[5];

        int written = fifo.Write(input);
        int read = fifo.Read(output);

        Assert.Equal(5, written);
        Assert.Equal(5, read);
        Assert.Equal(input, output);
        Assert.Equal(0, fifo.AvailableRead);
    }

    [Fact]
    public void CircularWrapAround_WorksCorrectly()
    {
        var fifo = new AudioFifoBuffer(10);
        float[] chunk1 = [1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f];
        float[] out1 = new float[8];

        fifo.Write(chunk1);
        fifo.Read(out1);

        float[] chunk2 = [10f, 20f, 30f, 40f, 50f];
        float[] out2 = new float[5];

        int written = fifo.Write(chunk2);
        int read = fifo.Read(out2);

        Assert.Equal(5, written);
        Assert.Equal(5, read);
        Assert.Equal(chunk2, out2);
    }

    [Fact]
    public async Task ConcurrentReadWrite_StressTest()
    {
        var fifo = new AudioFifoBuffer(4096);
        const int totalItems = 50000;

        var producer = Task.Run(async () =>
        {
            float[] block = new float[64];
            int sent = 0;
            while (sent < totalItems)
            {
                int count = Math.Min(block.Length, totalItems - sent);
                for (int i = 0; i < count; i++)
                {
                    block[i] = sent + i;
                }
                int written = fifo.Write(block.AsSpan(0, count));
                sent += written;
                if (written == 0) await Task.Delay(1);
            }
        });

        var consumer = Task.Run(async () =>
        {
            float[] block = new float[128];
            int received = 0;
            while (received < totalItems)
            {
                int read = fifo.Read(block);
                for (int i = 0; i < read; i++)
                {
                    Assert.Equal((float)(received + i), block[i]);
                }
                received += read;
                if (read == 0) await Task.Delay(1);
            }
        });

        await Task.WhenAll(producer, consumer);
    }
}
