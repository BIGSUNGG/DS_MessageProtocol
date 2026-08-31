using System;
using System.Collections.Generic;
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetObjectId(object value, out int objectId)
            {
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

            /// <summary>새 객체를 등록하고 할당된 id 를 반환한다. id 는 1 부터 시작한다.</summary>
            public int RegisterObject(object value)
            {
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

            /// <summary>새로 생성된 객체를 등록하고 id 를 반환한다 (직렬화 시와 동일한 순서여야 한다).</summary>
            public int RegisterNewObject(object value)
            {
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
    }
}
