using System;
using System.Buffers;

namespace MessageProtocol.Serialize
{
    /// <summary>
    /// ArrayPool 에서 대여한 byte[] 를 소유하는 직렬화 결과 버퍼.
    /// 사용 후 <see cref="Dispose"/> 로 풀에 반환한다. 이중 Dispose 는 안전하다.
    /// </summary>
    /// <remarks>
    /// 소유 상태는 참조형 <see cref="Owner"/> 홀더에 둔다 — 이 타입은 struct 이므로 대입·전달마다 사본이 만들어지는데,
    /// 사본끼리 홀더를 공유하지 않으면 어느 한 사본의 Dispose 가 다른 사본에 보이지 않아 같은 배열이 풀에
    /// 두 번 반납된다(다음 대여자가 남의 데이터를 보는 손상 — Known-Issues KI-37). 공유 홀더로 모든 사본이
    /// 단일 반환 상태를 보고, 어떤 사본이 먼저 Dispose 해도 나머지 사본은 빈 뷰를 본다.
    /// </remarks>
    public struct PooledBuffer : IDisposable
    {
        /// <summary>대여 배열의 실제 소유자. struct 사본 전체가 이 인스턴스를 공유한다.</summary>
        sealed class Owner
        {
            public byte[]? Buffer;
            public int Length;
            public bool FromPool;

            public void ReturnToPoolOnce()
            {
                if (!FromPool || Buffer is null) return;
                ArrayPool<byte>.Shared.Return(Buffer);
                Buffer = null;
                Length = 0;
                FromPool = false;
            }
        }

        Owner? _owner;

        PooledBuffer(Owner owner)
        {
            _owner = owner;
        }

        [Obsolete("Unused across DS_MessageProtocol, its tests, Sandbox, and the DS_RPC sibling stack (audited 2026-09-08); candidate for removal in the next major version.", error: false)]
        public static PooledBuffer Empty => default;

        public static PooledBuffer FromRented(byte[] rented, int length)
        {
            if (rented == null) throw new ArgumentNullException(nameof(rented));
            if ((uint)length > (uint)rented.Length) throw new ArgumentOutOfRangeException(nameof(length));
            // 0길이 배열은 Array.Empty 싱글턴일 수 있다 — 풀 반납 대상이 아니다(공용 풀의 0길이 조기 반환은
            // 문서화되지 않은 내부 동작이고 커스텀 풀에서는 예외가 될 수 있다).
            return new PooledBuffer(new Owner { Buffer = rented, Length = length, FromPool = rented.Length > 0 });
        }

        public int Length => _owner?.Length ?? 0;

        public ReadOnlySpan<byte> Span => _owner?.Buffer is { } buffer
            ? buffer.AsSpan(0, _owner.Length)
            : ReadOnlySpan<byte>.Empty;

        [Obsolete("Unused across DS_MessageProtocol, its tests, Sandbox, and the DS_RPC sibling stack (audited 2026-09-08); use Span instead. Candidate for removal in the next major version.", error: false)]
        public ReadOnlyMemory<byte> Memory => _owner?.Buffer is { } buffer
            ? buffer.AsMemory(0, _owner.Length)
            : ReadOnlyMemory<byte>.Empty;

        /// <summary>풀 반환 없는 뷰. 배열이 재사용될 수 있으니 수명 관리에 주의.</summary>
        [Obsolete("Unused across DS_MessageProtocol, its tests, Sandbox, and the DS_RPC sibling stack (audited 2026-09-08); use Span or ToArray instead. Candidate for removal in the next major version.", error: false)]
        public ArraySegment<byte> UnsafeArraySegment => _owner?.Buffer is { } buffer
            ? new ArraySegment<byte>(buffer, 0, _owner.Length)
            : default;

        public byte[] ToArray()
        {
            if (_owner?.Buffer is not { } source || _owner.Length == 0) return Array.Empty<byte>();
            var result = new byte[_owner.Length];
            Buffer.BlockCopy(source, 0, result, 0, _owner.Length);
            return result;
        }

        public void Dispose()
        {
            // 사본이어도 같은 Owner 를 본다 — 정확히 한 번만 풀에 반납되고, 이후 모든 사본의 뷰는 비어 있다.
            _owner?.ReturnToPoolOnce();
        }
    }
}
