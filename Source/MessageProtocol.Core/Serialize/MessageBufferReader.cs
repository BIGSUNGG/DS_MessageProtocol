using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace MessageProtocol.Serialize
{
    /// <summary>
    /// Forward-only, ReadOnlySpan 기반 버퍼 리더. 생성된 역직렬화 코드가 사용합니다.
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

        public decimal ReadDecimal()
        {
            EnsureRemaining(16);
            var span = _buffer.Slice(_position);
            var bits = new int[4];
            bits[0] = BinaryPrimitives.ReadInt32LittleEndian(span);
            bits[1] = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4));
            bits[2] = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(8));
            bits[3] = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(12));
            _position += 16;
            return new decimal(bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            EnsureRemaining(length);
            var span = _buffer.Slice(_position, length);
            _position += length;
            return span;
        }

        public string? ReadString()
        {
            int length = ReadInt32();
            if (length < 0) return null;
            if (length == 0) return string.Empty;
            var bytes = ReadBytes(length);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Skip(int count)
        {
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
    }
}
