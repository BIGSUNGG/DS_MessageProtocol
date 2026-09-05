using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>
        /// forward-only wire format 의 참조 유형 태그. 값 0/1/2 는 와이어 규격의 일부라 변경할 수 없다.
        /// </summary>
        public enum ReferenceKind : byte
        {
            Null = 0,
            NewObject = 1,
            BackReference = 2,
        }

        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new();

            bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>
        /// 한 번의 직렬화 동안 공유·순환 참조를 추적하는 컨텍스트.
        /// 첫 객체는 슬롯만 쓰고, Dictionary 는 두 번째 등록부터 할당된다.
        /// </summary>
        public struct SerializeContext
        {
            object? _firstObject;
            Dictionary<object, int>? _objectIds;
            int _nextObjectId;

            /// <summary>
            /// 이미 등록한 객체의 id 를 찾는다. null 은 거부한다 — null 참조는 <see cref="ReferenceKind.Null"/> 로 써야 하고,
            /// id 조회 대상으로 삼으면 `_firstObject is null`(빈 슬롯 sentinel)과 구분이 사라진다 (Known-Issues KI-30).
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetObjectId(object value, out int objectId)
            {
                if (value is null) ThrowNullReferenceValue(nameof(value));

                if (_objectIds is not null)
                {
                    return _objectIds.TryGetValue(value, out objectId);
                }

                if (_firstObject is not null && ReferenceEquals(_firstObject, value))
                {
                    objectId = 1;
                    return true;
                }

                objectId = 0;
                return false;
            }

            /// <summary>
            /// 새 객체를 등록하고 할당된 id 를 반환한다. id 는 1 부터 시작한다.
            /// null 은 거부한다 — null 을 등록하면 첫 슬롯이 비어 있는 상태로 남아 **다음 객체도 id 1 을 받아**
            /// 백레퍼런스가 다른 객체를 가리키게 된다(조용한 객체 그래프 손상) — Known-Issues KI-30.
            /// </summary>
            public int RegisterObject(object value)
            {
                if (value is null) ThrowNullReferenceValue(nameof(value));

                if (_objectIds is not null)
                {
                    int id = _nextObjectId++;
                    _objectIds[value] = id;
                    return id;
                }

                if (_firstObject is null)
                {
                    _firstObject = value;
                    _nextObjectId = 2;
                    return 1;
                }

                _objectIds = new Dictionary<object, int>(ReferenceComparer.Instance)
                {
                    [_firstObject] = 1,
                };
                _firstObject = null;
                int promotedId = _nextObjectId++;
                _objectIds[value] = promotedId;
                return promotedId;
            }
        }

        /// <summary>
        /// 한 번의 역직렬화 동안 id → 객체 역참조를 복원하는 테이블.
        /// 첫 객체는 슬롯만 쓰고, Dictionary 는 두 번째 등록부터 할당된다.
        /// </summary>
        public struct DeserializeContext
        {
            object? _firstObject;
            Dictionary<int, object>? _objects;
            int _nextObjectId;

            /// <summary>
            /// 새로 생성된 객체를 등록하고 id 를 반환한다 (직렬화 시와 동일한 순서여야 한다).
            /// null 은 거부한다 — <see cref="SerializeContext.RegisterObject"/> 와 같은 이유로 id 1 이 중복 발급되어
            /// <see cref="GetObject"/> 이 백레퍼런스를 잘못 된 인스턴스로 해석한다 (Known-Issues KI-30).
            /// </summary>
            public int RegisterNewObject(object value)
            {
                if (value is null) ThrowNullReferenceValue(nameof(value));

                if (_objects is not null)
                {
                    int id = _nextObjectId++;
                    _objects[id] = value;
                    return id;
                }

                if (_firstObject is null)
                {
                    _firstObject = value;
                    _nextObjectId = 2;
                    return 1;
                }

                _objects = new Dictionary<int, object>
                {
                    [1] = _firstObject,
                };
                _firstObject = null;
                int promotedId = _nextObjectId++;
                _objects[promotedId] = value;
                return promotedId;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public object GetObject(int objectId)
            {
                if (_objects is not null)
                {
                    if (!_objects.TryGetValue(objectId, out var value))
                    {
                        ThrowMissingObject(objectId);
                    }
                    return value!;
                }

                if (objectId == 1 && _firstObject is not null)
                {
                    return _firstObject;
                }

                ThrowMissingObject(objectId);
                return null!;
            }

            static void ThrowMissingObject(int id)
            {
                throw new InvalidDataException($"Back-reference to object id {id} could not be resolved.");
            }
        }

        /// <summary>
        /// 참조 추적 컨텍스트의 null 거부 — 두 컨텍스트가 공유하는 단일 사유 메시지.
        /// `_firstObject is null` 이 "빈 슬롯" sentinel 이라서 null 을 등록·조회하면 슬롯이 차지되지 않아
        /// **id 1 이 중복 발급**되고 백레퍼런스가 다른 인스턴스로 해석된다 (Known-Issues KI-30).
        /// </summary>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowNullReferenceValue(string paramName)
        {
            throw new ArgumentNullException(paramName,
                "A null reference is serialized as ReferenceKind.Null and must not be registered in or looked up from " +
                "the reference-tracking context: registering null leaves the first slot empty, so object id 1 is issued " +
                "twice and back-references resolve to the wrong instance.");
        }
    }
}
