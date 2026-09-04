using System;

namespace SoundCalibrator.Audio.Buffers;

/// <summary>
/// Circular buffer de alto rendimiento para streaming de audio en tiempo real.
/// Diseñado para transferir muestras entre el hilo de captura de audio y el hilo DSP con cero asignaciones.
/// </summary>
public sealed class AudioFifoBuffer
{
    private readonly float[] _buffer;
    private readonly int _capacity;
    private int _readIndex;
    private int _writeIndex;
    private int _available;
    private readonly object _lock = new();

    public int Capacity => _capacity;
    public int AvailableRead
    {
        get
        {
            lock (_lock)
            {
                return _available;
            }
        }
    }

    public AudioFifoBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _buffer = new float[capacity];
    }

    public int Write(ReadOnlySpan<float> source)
    {
        lock (_lock)
        {
            int toWrite = Math.Min(source.Length, _capacity - _available);
            if (toWrite <= 0) return 0;

            int firstChunk = Math.Min(toWrite, _capacity - _writeIndex);
            source[..firstChunk].CopyTo(_buffer.AsSpan(_writeIndex, firstChunk));

            int secondChunk = toWrite - firstChunk;
            if (secondChunk > 0)
            {
                source.Slice(firstChunk, secondChunk).CopyTo(_buffer.AsSpan(0, secondChunk));
            }

            _writeIndex = (_writeIndex + toWrite) % _capacity;
            _available += toWrite;
            return toWrite;
        }
    }

    public int Read(Span<float> destination)
    {
        lock (_lock)
        {
            int toRead = Math.Min(destination.Length, _available);
            if (toRead <= 0) return 0;

            int firstChunk = Math.Min(toRead, _capacity - _readIndex);
            _buffer.AsSpan(_readIndex, firstChunk).CopyTo(destination[..firstChunk]);

            int secondChunk = toRead - firstChunk;
            if (secondChunk > 0)
            {
                _buffer.AsSpan(0, secondChunk).CopyTo(destination.Slice(firstChunk, secondChunk));
            }

            _readIndex = (_readIndex + toRead) % _capacity;
            _available -= toRead;
            return toRead;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _readIndex = 0;
            _writeIndex = 0;
            _available = 0;
        }
    }
}
