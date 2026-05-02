using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace MessageProtocol.Serialize
{
    /// <summary>
    /// netstandard2.0 의 <see cref="BinaryPrimitives"/> 에는 float/double 리틀엔디안 API 가 없어 동일 동작을 제공합니다.
    /// </summary>
    static class BinaryPrimitivesCompat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ReadSingleLittleEndian(ReadOnlySpan<byte> source)
        {
            uint u = BinaryPrimitives.ReadUInt32LittleEndian(source);
            unsafe
            {
                return *(float*)&u;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double ReadDoubleLittleEndian(ReadOnlySpan<byte> source)
        {
            ulong ul = BinaryPrimitives.ReadUInt64LittleEndian(source);
            unsafe
            {
                return *(double*)&ul;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteSingleLittleEndian(Span<byte> destination, float value)
        {
            unsafe
            {
                uint u = *(uint*)&value;
                BinaryPrimitives.WriteUInt32LittleEndian(destination, u);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteDoubleLittleEndian(Span<byte> destination, double value)
        {
            unsafe
            {
                ulong u = *(ulong*)&value;
                BinaryPrimitives.WriteUInt64LittleEndian(destination, u);
            }
        }
    }
}
