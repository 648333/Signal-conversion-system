namespace DH.Acquisition;

public sealed class RingBuffer<T> where T : struct
{
    private readonly T[] _buffer;
    private int _writePos;
    private int _readPos;
    private readonly object _lock = new();

    public int Capacity { get; }
    public int Available { get; private set; }

    public RingBuffer(int capacity)
    {
        Capacity = capacity;
        _buffer = new T[capacity];
        _writePos = 0;
        _readPos = 0;
        Available = 0;
    }

    public int Write(T[] data, int offset, int count)
    {
        lock (_lock)
        {
            var toWrite = Math.Min(count, Capacity - Available);
            for (int i = 0; i < toWrite; i++)
            {
                _buffer[_writePos] = data[offset + i];
                _writePos = (_writePos + 1) % Capacity;
            }
            Available += toWrite;
            return toWrite;
        }
    }

    public int Read(T[] data, int offset, int count)
    {
        lock (_lock)
        {
            var toRead = Math.Min(count, Available);
            for (int i = 0; i < toRead; i++)
            {
                data[offset + i] = _buffer[_readPos];
                _readPos = (_readPos + 1) % Capacity;
            }
            Available -= toRead;
            return toRead;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _writePos = 0;
            _readPos = 0;
            Available = 0;
        }
    }
}
