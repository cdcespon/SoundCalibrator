using System;

namespace SoundCalibrator.Core.DSP;

/// <summary>
/// Buffer circular de alta eficiencia para almacenar el historial de espectros de magnitud
/// con destino a la visualización de espectrograma / cascada 2D (Waterfall).
/// Garantiza 0 alocaciones en el bucle caliente.
/// </summary>
public sealed class SpectrogramBuffer
{
    private readonly float[] _data;
    private readonly int _capacity;
    private readonly int _binCount;
    private int _head;
    private int _count;

    public int Capacity => _capacity;
    public int BinCount => _binCount;
    public int Count => _count;

    public SpectrogramBuffer(int capacity, int binCount)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (binCount <= 0) throw new ArgumentOutOfRangeException(nameof(binCount));

        _capacity = capacity;
        _binCount = binCount;
        _data = new float[capacity * binCount];
        _head = 0;
        _count = 0;
    }

    public void PushFrame(ReadOnlySpan<float> magnitudeDb)
    {
        int copyCount = Math.Min(magnitudeDb.Length, _binCount);
        var targetSpan = _data.AsSpan(_head * _binCount, copyCount);
        magnitudeDb[..copyCount].CopyTo(targetSpan);

        _head = (_head + 1) % _capacity;
        if (_count < _capacity)
        {
            _count++;
        }
    }

    public ReadOnlySpan<float> GetFrame(int indexFromNewest)
    {
        if (indexFromNewest < 0 || indexFromNewest >= _count)
        {
            throw new ArgumentOutOfRangeException(nameof(indexFromNewest));
        }

        int actualFrame = (_head - 1 - indexFromNewest + _capacity * 2) % _capacity;
        return _data.AsSpan(actualFrame * _binCount, _binCount);
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
        Array.Clear(_data);
    }
}
