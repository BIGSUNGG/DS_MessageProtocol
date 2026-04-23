using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MessageProtocol.Serialize
{
    public static partial class MessageSerializer
    {
        /// <summary>
        /// forward-only wire format 에서 쓰는 참조 유형 태그. 값 0/1/2 는 wire format 의 일부이므로 변경하면 안 됩니다.
        /// </summary>
        public enum ReferenceKind : byte
        {
            Null = 0,
            NewObject = 1,
            BackReference = 2,
        }

        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>
        /// 메시지 한 번의 직렬화 동안 공유/순환 참조를 추적합니다.
        /// 구조체로 구현되어 스택에 존재하며, Dictionary 는 필요 시점에만 지연 할당됩니다.
        /// </summary>
        public struct SerializeContext
        {
            Dictionary<object, int>? _objectIds;
            int _nextObjectId;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetObjectId(object value, out int objectId)
            {
                if (_objectIds is null)
                {
                    objectId = 0;
                    return false;
                }
                return _objectIds.TryGetValue(value, out objectId);
            }

            /// <summary>
            /// 새 객체를 등록하고 할당된 id 를 반환합니다. id 는 1 부터 시작합니다.
            /// </summary>
            public int RegisterObject(object value)
            {
                if (_objectIds is null)
                {
                    _objectIds = new Dictionary<object, int>(ReferenceComparer.Instance);
                    _nextObjectId = 1;
                }
                int id = _nextObjectId++;
                _objectIds[value] = id;
                return id;
            }
        }

        /// <summary>
        /// 메시지 한 번의 역직렬화 동안 id 에서 객체로 되돌리기 위한 테이블.
        /// </summary>
        public struct DeserializeContext
        {
            Dictionary<int, object>? _objects;
            int _nextObjectId;

            /// <summary>
            /// 새로 생성된 객체를 등록하고 id 를 반환합니다. (직렬화 시와 동일한 순서로 id 가 매겨져야 합니다.)
            /// </summary>
            public int RegisterNewObject(object value)
            {
                if (_objects is null)
                {
                    _objects = new Dictionary<int, object>();
                    _nextObjectId = 1;
                }
                int id = _nextObjectId++;
                _objects[id] = value;
                return id;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public object GetObject(int objectId)
            {
                if (_objects is null)
                {
                    ThrowMissingObject(objectId);
                }
                return _objects![objectId];
            }

            static void ThrowMissingObject(int id)
            {
                throw new System.IO.InvalidDataException($"Back-reference to object id {id} could not be resolved.");
            }
        }
    }
}
