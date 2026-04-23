using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace MessageProtocol.Serialize
{
    /// <summary>
    /// Forward-only, pooled byte buffer writer used by generated serialize code.
    /// 소유권 있는 byte[] 를 ArrayPool 에서 대여하여 사용하며, 필요 시 자동으로 커집니다.
    /// </summary>
    public ref struct MessageBufferWriter
    {
        byte[] _buffer;
        int _position;

        public static MessageBufferWriter Create(int initialCapacity = 256)
        {
            var buffer = initialCapacity <= 0
                ? Array.Empty<byte>()
                : ArrayPool<byte>.Shared.Rent(initialCapacity);
            return new MessageBufferWriter(buffer);
        }

        MessageBufferWriter(byte[] buffer)
        {
            _buffer = buffer;
            _position = 0;
        }

        public int Length => _position;
        public int Capacity => _buffer.Length;
        public Span<byte> WrittenSpan => _buffer.AsSpan(0, _position);
        public ReadOnlySpan<byte> WrittenReadOnlySpan => _buffer.AsSpan(0, _position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan(int size)
        {
            EnsureCapacity(size);
            var span = _buffer.AsSpan(_position, size);
            _position += size;
            return span;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            if ((uint)(_position + count) > (uint)_buffer.Length)
            {
                ThrowAdvanceBeyondCapacity();
            }
            _position += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int additional)
        {
            if (_position + additional > _buffer.Length)
            {
                Grow(additional);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void Grow(int additional)
        {
            int required = checked(_position + additional);
            int newCapacity = Math.Max(_buffer.Length == 0 ? 256 : _buffer.Length * 2, required);
            var newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);
            if (_position > 0)
            {
                Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _position);
            }
            if (_buffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
            }
            _buffer = newBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(byte value)
        {
            EnsureCapacity(1);
            _buffer[_position++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSByte(sbyte value) => WriteByte((byte)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt16(short value)
        {
            EnsureCapacity(2);
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.AsSpan(_position), value);
            _position += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt16(ushort value)
        {
            EnsureCapacity(2);
            BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_position), value);
            _position += 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(int value)
        {
            EnsureCapacity(4);
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_position), value);
            _position += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt32(uint value)
        {
            EnsureCapacity(4);
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_position), value);
            _position += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64(long value)
        {
            EnsureCapacity(8);
            BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_position), value);
            _position += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUInt64(ulong value)
        {
            EnsureCapacity(8);
            BinaryPrimitives.WriteUInt64LittleEndian(_buffer.AsSpan(_position), value);
            _position += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSingle(float value)
        {
            EnsureCapacity(4);
            BinaryPrimitives.WriteSingleLittleEndian(_buffer.AsSpan(_position), value);
            _position += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value)
        {
            EnsureCapacity(8);
            BinaryPrimitives.WriteDoubleLittleEndian(_buffer.AsSpan(_position), value);
            _position += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChar(char value) => WriteUInt16(value);

        public void WriteDecimal(decimal value)
        {
            EnsureCapacity(16);
            var bits = decimal.GetBits(value);
            var span = _buffer.AsSpan(_position);
            BinaryPrimitives.WriteInt32LittleEndian(span, bits[0]);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4), bits[1]);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8), bits[2]);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(12), bits[3]);
            _position += 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            EnsureCapacity(value.Length);
            value.CopyTo(_buffer.AsSpan(_position));
            _position += value.Length;
        }

        /// <summary>
        /// null = int32(-1); empty = int32(0); otherwise int32(utf8ByteCount) + utf8 bytes.
        /// </summary>
        public void WriteString(string? value)
        {
            if (value is null)
            {
                WriteInt32(-1);
                return;
            }
            if (value.Length == 0)
            {
                WriteInt32(0);
                return;
            }

            int maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);
            EnsureCapacity(4 + maxBytes);
            int written = Encoding.UTF8.GetBytes(value, _buffer.AsSpan(_position + 4));
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_position), written);
            _position += 4 + written;
        }

        /// <summary>
        /// 지정된 오프셋 위치에 int32 값을 기록합니다. forward-only 경로에서는 사용하지 않지만,
        /// 드문 경우(외부 프레이밍이 필요할 때)를 위해 노출합니다.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PatchInt32(int offset, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(offset), value);
        }

        /// <summary>
        /// 버퍼 소유권을 PooledBuffer 로 이전합니다. 이후 writer 는 비어있는 상태가 됩니다.
        /// </summary>
        public PooledBuffer ToPooledBuffer()
        {
            var owner = PooledBuffer.FromRented(_buffer, _position);
            _buffer = Array.Empty<byte>();
            _position = 0;
            return owner;
        }

        /// <summary>
        /// 기록된 내용을 별도 byte[] 로 복사해 반환합니다. 호환 API 용도입니다.
        /// </summary>
        public byte[] ToArray()
        {
            if (_position == 0) return Array.Empty<byte>();
            var result = new byte[_position];
            Buffer.BlockCopy(_buffer, 0, result, 0, _position);
            return result;
        }

        public void Dispose()
        {
            if (_buffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = Array.Empty<byte>();
                _position = 0;
            }
        }

        static void ThrowAdvanceBeyondCapacity()
        {
            throw new InvalidOperationException("Advance would move position beyond buffer capacity.");
        }
    }
}
