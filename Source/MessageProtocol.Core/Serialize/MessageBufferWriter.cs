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
        /// <summary>
        /// 기본 중첩 객체 직렬화 깊이 상한. reader 기본 상한과 **의도적으로 동일**하다 —
        /// 써서 보낼 수 있는 그래프를 상대가 기본 설정으로 읽지 못하는 비대칭을 막는다 (Known-Issues KI-25).
        /// </summary>
        public const int DefaultMaxNestingDepth = MessageBufferReader.DefaultMaxNestingDepth;

        byte[] _buffer;
        int _position;
        int _depth;
        int _maxNestingDepth;

        MessageBufferWriter(byte[] buffer, int maxNestingDepth)
        {
            _buffer = buffer;
            _position = 0;
            _depth = 0;
            _maxNestingDepth = maxNestingDepth;
        }

        public static MessageBufferWriter Create(int initialCapacity = 256)
        {
            return Create(initialCapacity, DefaultMaxNestingDepth);
        }

        /// <param name="initialCapacity">초기 대여 용량(0 이하는 빈 버퍼로 시작해 첫 쓰기에서 증설).</param>
        /// <param name="maxNestingDepth">
        /// 중첩 객체 직렬화 깊이 상한. 합법적으로 깊은 객체 그래프를 다루는 호출자가 올리는 탈출구이며,
        /// 수신 측도 읽으려면 <see cref="MessageBufferReader(ReadOnlySpan{byte}, int)"/> 로 같은 상한을 맞춰야 한다. 0 이하 거부.
        /// </param>
        public static MessageBufferWriter Create(int initialCapacity, int maxNestingDepth)
        {
            if (maxNestingDepth <= 0) ThrowInvalidMaxNestingDepth(maxNestingDepth);
            var buffer = initialCapacity <= 0
                ? Array.Empty<byte>()
                : ArrayPool<byte>.Shared.Rent(initialCapacity);
            return new MessageBufferWriter(buffer, maxNestingDepth);
        }

        public int Length => _position;
        public int Capacity => _buffer.Length;
        public Span<byte> WrittenSpan => _buffer.AsSpan(0, _position);
        public ReadOnlySpan<byte> WrittenReadOnlySpan => _buffer.AsSpan(0, _position);

        /// <summary>이 writer 가 허용하는 중첩 객체 깊이 상한.</summary>
        public int MaxNestingDepth => _maxNestingDepth;

        /// <summary>현재 중첩 깊이 — <see cref="EnterNestedObject"/>·<see cref="LeaveNestedObject"/> 가 관리한다.</summary>
        public int NestingDepth => _depth;

        /// <summary>
        /// 중첩 객체 기록 시작을 알린다. 상한 도달 시 <see cref="InvalidOperationException"/> —
        /// 객체 그래프가 너무 깊거나(긴 연결 리스트·깊은 트리), 런타임 디스패치 멤버를 통해 순환이 흘렀다
        /// (디스패치 경로는 백레퍼런스를 추적하지 않는다). 가드가 없으면 두 경우 모두 재귀가 스택을
        /// 소진해 **catch 불가한 스택 오버플로**로 프로세스가 죽는다 (Known-Issues KI-25).
        /// 생성 코드·<c>SerializeToWriter</c> 가 재귀 지점에서 호출한다.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterNestedObject()
        {
            if (_depth >= _maxNestingDepth) ThrowNestingTooDeep(_maxNestingDepth);
            _depth++;
        }

        /// <summary>
        /// 중첩 객체 기록 종료를 알린다. 짝이 맞지 않는 호출(기록 중 예외)은 깊이를 부풀리기만 하므로
        /// 가드는 실패 방향으로 안전하다. 음수로 내려가지 않도록 0 에서 클램프(음수 깊이가 상한을 무력화하는 것 차단).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LeaveNestedObject()
        {
            if (_depth > 0) _depth--;
        }

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
            _depth = 0;
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

        // writer 쪽 한계 위반은 호출자 데이터·상태 문제라 reader(와이어 내용 불법 = InvalidDataException)와 달리
        // InvalidOperationException 으로 보고한다 — `ThrowAdvanceBeyondCapacity` 와 동일 기조.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowNestingTooDeep(int maxNestingDepth)
        {
            throw new InvalidOperationException(
                $"Nested object depth exceeds the maximum of {maxNestingDepth}. " +
                $"The object graph is too deep to serialize (long chain, deep tree, or a cycle through a " +
                $"runtime-dispatched member such as a type parameter or an abstract message type). " +
                $"Use 'MessageBufferWriter.Create(initialCapacity, maxNestingDepth)' if this graph is legitimately deeper, " +
                $"and raise the receiving reader's limit to match.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowInvalidMaxNestingDepth(int maxNestingDepth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxNestingDepth), maxNestingDepth, "Max nesting depth must be positive.");
        }
    }
}
