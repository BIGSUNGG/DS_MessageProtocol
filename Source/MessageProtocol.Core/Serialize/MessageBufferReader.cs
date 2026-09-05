using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MessageProtocol.Serialize
{
    /// <summary>
    /// Forward-only ReadOnlySpan 기반 버퍼 reader. 경계 초과 읽기는 <see cref="EndOfStreamException"/>.
    /// </summary>
    public ref struct MessageBufferReader
    {
        ReadOnlySpan<byte> _buffer;
        int _position;

        public MessageBufferReader(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            _position = 0;
        }

        public int Position => _position;
        public int Remaining => _buffer.Length - _position;
        public ReadOnlySpan<byte> UnreadSpan => _buffer.Slice(_position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte()
        {
            if ((uint)_position >= (uint)_buffer.Length) ThrowEndOfBuffer();
            return _buffer[_position++];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte() => (sbyte)ReadByte();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReadBoolean() => ReadByte() != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short ReadInt16()
        {
            EnsureRemaining(2);
            short value = BinaryPrimitives.ReadInt16LittleEndian(_buffer.Slice(_position));
            _position += 2;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ReadUInt16()
        {
            EnsureRemaining(2);
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Slice(_position));
            _position += 2;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt32()
        {
            EnsureRemaining(4);
            int value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.Slice(_position));
            _position += 4;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint ReadUInt32()
        {
            EnsureRemaining(4);
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(_position));
            _position += 4;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ReadInt64()
        {
            EnsureRemaining(8);
            long value = BinaryPrimitives.ReadInt64LittleEndian(_buffer.Slice(_position));
            _position += 8;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong ReadUInt64()
        {
            EnsureRemaining(8);
            ulong value = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Slice(_position));
            _position += 8;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ReadSingle()
        {
            EnsureRemaining(4);
            float value = BinaryPrimitivesCompat.ReadSingleLittleEndian(_buffer.Slice(_position));
            _position += 4;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ReadDouble()
        {
            EnsureRemaining(8);
            double value = BinaryPrimitivesCompat.ReadDoubleLittleEndian(_buffer.Slice(_position));
            _position += 8;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char ReadChar() => (char)ReadUInt16();

        /// <summary>WriteDecimal 의 GetBits 순서(lo, mid, hi, flags) 16바이트를 복원.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ReadDecimal()
        {
            EnsureRemaining(16);
            var span = _buffer.Slice(_position);
            int lo = BinaryPrimitives.ReadInt32LittleEndian(span);
            int mid = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4));
            int hi = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(8));
            int flags = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(12));
            // flags 규약: 비트 31 부호, 비트 16–23 스케일(0–28), 나머지는 예약(0). 위반 거부 — 잘못된 스케일은 DecCalc 가감산에서 스택 버퍼 오버플로(프로세스 크래시)를 일으킨다.
            uint f = (uint)flags;
            if ((f & 0x7F00FFFFu) != 0 || ((f >> 16) & 0xFFu) > 28u)
            {
                throw new InvalidDataException("Invalid decimal wire bits.");
            }
            _position += 16;

            Span<decimal> temp = stackalloc decimal[1];
            Span<int> raw = MemoryMarshal.Cast<decimal, int>(temp);
            raw[0] = flags;
            raw[1] = hi;
            raw[2] = lo;
            raw[3] = mid;
            return temp[0];
        }

        /// <summary><paramref name="length"/> 바이트 구간을 뷰로 반환하고 전진한다.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            EnsureRemaining(length);
            var span = _buffer.Slice(_position, length);
            _position += length;
            return span;
        }

        /// <summary>int32 길이 접두 문자열. -1 은 null, 0 은 빈 문자열. 무효 UTF-8 은 와이어 손상으로 거부한다.</summary>
        public string? ReadString()
        {
            int length = ReadInt32();
            if (length == -1) return null;
            // null 규약은 -1 뿐이다. 나머지 음수(-2…int.MinValue)는 손상된 길이 접두이므로 null 로 둔갑시켜 조용히 통과시키지 않는다 (Known-Issues KI-6).
            if (length < -1)
            {
                throw new InvalidDataException("String length prefix is negative but not -1.");
            }
            if (length == 0) return string.Empty;
            try
            {
                return StrictUtf8.GetString(ReadBytes(length));
            }
            catch (DecoderFallbackException exception)
            {
                // 경계 위반(EndOfStreamException)과 구분해 와이어 내용 불법을 보고 — ReadDecimal KI-15 정책과 동일.
                throw new InvalidDataException("String payload is not valid UTF-8.", exception);
            }
        }

        // 무효 바이트를 U+FFFD 로 조용히 바꾸면 손상 패킷이 티 없이 복호되므로 엄격 폴백으로 거부한다 (Known-Issues KI-20).
        static readonly Encoding StrictUtf8 = Encoding.GetEncoding(65001, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Skip(int count)
        {
            // 음수 Skip 은 위치를 뒤로 돌려 이미 소비한 바이트를 다시 읽게 한다 — forward-only 규약 위반 (Known-Issues KI-21).
            if (count < 0) ThrowNegativeCount(count);
            EnsureRemaining(count);
            _position += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void EnsureRemaining(int count)
        {
            if ((uint)(_position + count) > (uint)_buffer.Length)
            {
                ThrowEndOfBuffer();
            }
        }

        static void ThrowEndOfBuffer()
        {
            throw new EndOfStreamException("Attempted to read past the end of the buffer.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowNegativeCount(int count)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must not be negative.");
        }
    }
}
