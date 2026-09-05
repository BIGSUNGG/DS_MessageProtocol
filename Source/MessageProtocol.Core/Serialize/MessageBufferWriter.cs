using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MessageProtocol.Serialize
{
    /// <summary>
    /// Forward-only 풀링 바이트 버퍼 writer. ArrayPool 에서 대여하고 필요 시 자동 증량한다.
    /// 리틀엔디안 고정 폭 프리미티브 + 길이 접두 문자열/바이트 형식을 쓴다.
    /// </summary>
    public ref struct MessageBufferWriter
    {
        byte[] _buffer;
        int _position;

        MessageBufferWriter(byte[] buffer)
        {
            _buffer = buffer;
            _position = 0;
        }

        public static MessageBufferWriter Create(int initialCapacity = 256)
        {
            var buffer = initialCapacity <= 0
                ? Array.Empty<byte>()
                : ArrayPool<byte>.Shared.Rent(initialCapacity);
            return new MessageBufferWriter(buffer);
        }

        public int Length => _position;
        public int Capacity => _buffer.Length;
        public Span<byte> WrittenSpan => _buffer.AsSpan(0, _position);
        public ReadOnlySpan<byte> WrittenReadOnlySpan => _buffer.AsSpan(0, _position);

        /// <summary><paramref name="size"/> 바이트를 쓸 공간을 확보하고 해당 구간을 반환·전진한다.</summary>
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
            // 음수 Advance 는 위치를 뒤로 돌려 이후 쓰기가 이미 기록한 페이로드를 덮어쓴다 (Known-Issues KI-21).
            if (count < 0) ThrowNegativeCount(count);
            if ((uint)(_position + count) > (uint)_buffer.Length)
            {
                ThrowAdvanceBeyondCapacity();
            }
            _position += count;
        }

        /// <summary><paramref name="additional"/> 바이트를 더 쓸 수 있음을 보장한다. 생성 코드는 고정 크기 구간을 합산해 1회 호출한다.</summary>
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
            BinaryPrimitivesCompat.WriteSingleLittleEndian(_buffer.AsSpan(_position), value);
            _position += 4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDouble(double value)
        {
            EnsureCapacity(8);
            BinaryPrimitivesCompat.WriteDoubleLittleEndian(_buffer.AsSpan(_position), value);
            _position += 8;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteChar(char value) => WriteUInt16(value);

        /// <summary>GetBits 순서(lo, mid, hi, flags)로 16바이트 기록. 중간 할당 없음.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDecimal(decimal value)
        {
            EnsureCapacity(16);
            Span<decimal> temp = stackalloc decimal[1];
            temp[0] = value;
            ReadOnlySpan<int> raw = MemoryMarshal.Cast<decimal, int>(temp);
            var span = _buffer.AsSpan(_position);
            BinaryPrimitives.WriteInt32LittleEndian(span, raw[2]);          // lo
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4), raw[3]); // mid
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8), raw[1]); // hi
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(12), raw[0]);// flags
            _position += 16;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(ReadOnlySpan<byte> value)
        {
            EnsureCapacity(value.Length);
            value.CopyTo(_buffer.AsSpan(_position));
            _position += value.Length;
        }

        /// <summary>null = int32(-1), 빈 문자열 = int32(0), 그 외 int32(utf8 길이) + utf8 바이트.</summary>
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

            // 필요 용량을 long 으로 구한다 — `4 + GetMaxByteCount(int)` 는 초대형 문자열에서 음수로 오버플로해
            // EnsureCapacity 의 증설을 건너뛰게 하고, 그럼 GetBytes 가 내부 ArgumentException 으로 실패한다 (KI-22).
            long required = GetStringBufferRequirement(value.Length);
            if (_position + required > MaxBufferLength)
            {
                ThrowStringTooLarge(value.Length);
            }
            // 위 가드로 required ≤ MaxBufferLength - _position < int.MaxValue 이므로 좁힘과 이후 int 합산이 안전하다.
            EnsureCapacity((int)required);
            int written = StrictUtf8.GetBytes(value, 0, value.Length, _buffer, _position + 4);
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_position), written);
            _position += 4 + written;
        }

        // UTF-8 인코딩 상한 공식(문자당 최대 3바이트 + 프리앰블 3바이트). `Encoding.GetMaxByteCount(int)` 와 같지만
        // 그 메서드는 약 7.15억 자에서 `charCount * 3 + 3` 이 int 를 넘겨 음수를 반환한다 (Known-Issues KI-22).
        const long Utf8MaxBytesPerChar = 3;
        const long Utf8PreambleBytes = 3;
        const int LengthPrefixBytes = 4;

        /// <summary>byte[] 버퍼의 최대 길이(.NET 배열 상한) — 이보다 큰 페이로드는 단일 버퍼에 담을 수 없다.</summary>
        const long MaxBufferLength = 0X7FEFFFFFL;

        /// <summary>문자열 페이로드(길이 접두 4바이트 + UTF-8 상한)에 필요한 버퍼 바이트 수를 long 으로 반환한다.</summary>
        internal static long GetStringBufferRequirement(int charCount)
        {
            return LengthPrefixBytes + (Utf8MaxBytesPerChar * charCount) + Utf8PreambleBytes;
        }

        // 고립 서로게이트를 대체 바이트로 조용히 바꾸면 수신 측이 송신과 다른 문자열을 보므로,
        // 인코딩 실패를 있는 그대로 표면화하는 엄격 폴백을 쓴다 (와이어 무결성 정책 — Known-Issues KI-20).
        static readonly Encoding StrictUtf8 = Encoding.GetEncoding(65001, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

        /// <summary>지정 오프셋에 int32 를 다시 쓴다 (외부 프레이밍 등 드문 용도).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PatchInt32(int offset, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(offset), value);
        }

        /// <summary>버퍼 소유권을 <see cref="PooledBuffer"/> 로 이전하고 writer 는 비운다.</summary>
        public PooledBuffer ToPooledBuffer()
        {
            var owner = PooledBuffer.FromRented(_buffer, _position);
            _buffer = Array.Empty<byte>();
            _position = 0;
            return owner;
        }

        /// <summary>기록된 내용을 새 byte[] 로 복사해 반환한다 (호환 경로).</summary>
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowNegativeCount(int count)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must not be negative.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowStringTooLarge(int charCount)
        {
            throw new ArgumentException(
                $"String of {charCount} characters needs more than the maximum buffer size ({MaxBufferLength} bytes) and cannot be serialized.",
                "value");
        }
    }
}
