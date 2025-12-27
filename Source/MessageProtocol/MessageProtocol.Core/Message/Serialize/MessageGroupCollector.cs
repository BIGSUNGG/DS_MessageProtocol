using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DS.MessageProtocol;

namespace DS.MessageProtocol.Serialize
{
    internal class MessageGroupCollector
    {
        public static MessageGroupCollector Instance => _instance.Value;
        static Lazy<MessageGroupCollector> _instance = new Lazy<MessageGroupCollector>(() => new MessageGroupCollector());

        private readonly Dictionary<Type, MessageGroupRootWrapper> _messageGroupRoots = new Dictionary<Type, MessageGroupRootWrapper>();
        public IReadOnlyDictionary<Type, MessageGroupRootWrapper> MessageGroupRoots => _messageGroupRoots;

        private MessageGroupCollector()
        {
            InitializeMessageGroups();
        }

        private void InitializeMessageGroups()
        {
            // 현재 도메인에 로드된 모든 어셈블리를 포함하도록 수정
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var allTypes = assemblies.SelectMany(a => {
                    try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                }).ToArray();

            // 1단계: MessageGroupRoot와 MessageGroupElement 어트리뷰트를 가진 클래스 수집
            var rootTypes = new List<Type>();
            var elementTypes = new List<Type>();

            foreach (var type in allTypes)
            {
                if (!type.IsClass || type.IsAbstract || type.IsInterface)
                    continue;

                var rootAttribute = type.GetCustomAttribute<MessageGroupRoot>(false);
                var elementAttribute = type.GetCustomAttribute<MessageGroupElement>(false);

                if (rootAttribute != null && elementAttribute != null)
                {
                    throw new InvalidOperationException(
                        $"Type {type.FullName} cannot have both MessageGroupRoot and MessageGroupElement attributes");
                }

                if (rootAttribute != null)
                {
                    rootTypes.Add(type);
                }

                if (elementAttribute != null)
                {
                    elementTypes.Add(type);
                }
            }

            // 2단계: Root에서 Element를 찾는 방식으로 매핑 (성능 최적화)
            foreach (var rootType in rootTypes)
            {
                var rootAttribute = rootType.GetCustomAttribute<MessageGroupRoot>(false);
                if (rootAttribute == null)
                    continue;

                var elementWrappers = new Dictionary<Type, MessageGroupElementWrapper>();

                // 해당 Root 타입을 상속받거나 구현하는 모든 Element 찾기
                foreach (var elementType in elementTypes)
                {
                    // Element가 Root의 자식 타입인지 확인
                    if (rootType.IsAssignableFrom(elementType))
                    {
                        var elementWrapper = new MessageGroupElementWrapper(rootAttribute, elementType);
                        elementWrappers[elementType] = elementWrapper;
                    }
                }

                // MessageGroupRootWrapper 생성 및 캐싱
                var rootWrapper = new MessageGroupRootWrapper(rootType, elementWrappers);
                _messageGroupRoots[rootType] = rootWrapper;
            }
        }
    }
}   