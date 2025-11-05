using System;
using System.Collections.Concurrent;

namespace Windows_ServerTest.ServerCore
{

    /// </summary>
    public class MemoryPool<T> where T : class, new()
    {
        private readonly ConcurrentBag<T> _pool;
        private readonly int _maxSize;

        public MemoryPool(int initialSize = 500, int maxSize = 5000)
        {
            _pool = new ConcurrentBag<T>();
            _maxSize = maxSize;
            
            for (int i = 0; i < initialSize; i++)
                _pool.Add(new T());
        }

        public T Rent()
        {
            if (_pool.TryTake(out var item))
                return item;

            return new T();
        }

        public void Return(T item)
        {
            var sb = item as SendBuffer;
            if (sb != null)
                sb.Clear();
            var sb2 = item as RecvBuffer;
            if (sb2 != null)
                sb2.Clear();

            if (_pool.Count < _maxSize)
                _pool.Add(item);
        }

        public int Count => _pool.Count;
    }
}
