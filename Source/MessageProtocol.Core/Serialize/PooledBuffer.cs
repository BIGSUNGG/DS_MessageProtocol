using System;
using System.Buffers;

namespace MessageProtocol.Serialize
{
    /// <summary>
    /// ArrayPool 에서 대여한 byte[] 를 소유하는 직렬화 결과 버퍼.
    /// 사용 후 <see cref="Dispose"/> 로 풀에 반환한다. 이중 Dispose 는 안전하다.
    /// </summary>
    public struct PooledBuffer : IDisposable
    {
        byte[]? _buffer;
        int _length;
        bool _fromPool;

        PooledBuffer(byte[]? buffer, int length, bool fromPool)
        {
            _buffer = buffer;
            _length = length;
            _fromPool = fromPool;
        }

        public static PooledBuffer Empty => default;

        public static PooledBuffer FromRented(byte[] rented, int length)
        {
            if (rented == null) throw new ArgumentNullException(nameof(rented));
            if ((uint)length > (uint)rented.Length) throw new ArgumentOutOfRangeException(nameof(length));
            return new PooledBuffer(rented, length, fromPool: true);
        }

        public int Length => _length;

        public ReadOnlySpan<byte> Span => _buffer is null ? ReadOnlySpan<byte>.Empty : _buffer.AsSpan(0, _length);

        public ReadOnlyMemory<byte> Memory => _buffer is null ? ReadOnlyMemory<byte>.Empty : _buffer.AsMemory(0, _length);

        /// <summary>풀 반환 없는 뷰. 배열이 재사용될 수 있으니 수명 관리에 주의.</summary>
        public ArraySegment<byte> UnsafeArraySegment => _buffer is null ? default : new ArraySegment<byte>(_buffer, 0, _length);

        public byte[] ToArray()
        {
            if (_buffer is null || _length == 0) return Array.Empty<byte>();
            var result = new byte[_length];
            Buffer.BlockCopy(_buffer, 0, result, 0, _length);
            return result;
        }

        public void Dispose()
        {
            if (_fromPool && _buffer != null)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = null;
                _fromPool = false;
                _length = 0;
            }
        }
    }
}
