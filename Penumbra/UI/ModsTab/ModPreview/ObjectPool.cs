using System;

namespace Penumbra.UI.ModsTab.ModPreview;

public class ObjectPool<T> : IDisposable where T : class
{
    private readonly Func<T> _factory;
    private readonly Action<T> _reset;
    private readonly int _maxSize;
    private readonly Queue<T> _pool = new();
    private bool _disposed;

    public ObjectPool(Func<T> factory, Action<T> reset, int maxSize)
    {
        _factory = factory;
        _reset = reset;
        _maxSize = maxSize;
    }

    public T Get()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ObjectPool<T>));

        lock (_pool)
        {
            return _pool.Count > 0 ? _pool.Dequeue() : _factory();
        }
    }

    public void Return(T item)
    {
        if (_disposed)
            return;

        lock (_pool)
        {
            if (_pool.Count < _maxSize)
            {
                _reset(item);
                _pool.Enqueue(item);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        lock (_pool)
        {
            while (_pool.Count > 0)
            {
                if (_pool.Dequeue() is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
} 