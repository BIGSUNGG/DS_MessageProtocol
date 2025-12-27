using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DS.MessageProtocol;

namespace DS.MessageProtocol
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
            // 현재 어셈블리와 이를 참조하는 어셈블리만 스캔 (성능 최적화)
            var currentAssembly = Assembly.GetExecutingAssembly();
            var currentAssemblyName = currentAssembly.GetName();
            var assembliesToScan = new HashSet<Assembly> { currentAssembly };
            
            // 현재 도메인의 모든 어셈블리 중에서 현재 어셈블리를 참조하는 어셈블리 찾기 (역참조)
            var allLoadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in allLoadedAssemblies)
            {
                try
                {
                    // 현재 어셈블리를 참조하는지 확인
                    var referencedNames = assembly.GetReferencedAssemblies();
                    if (referencedNames.Any(name => 
                        name.Name == currentAssemblyName.Name && 
                        name.Version == currentAssemblyName.Version))
                    {
                        assembliesToScan.Add(assembly);
                    }
                }
                catch
                {
                    Trace.WriteLine($"Failed to load assembly: {assembly.FullName}");
                    continue;
                }
            }

            // EntryAssembly가 있으면 추가 (실행 중인 애플리케이션의 메인 어셈블리)
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null && entryAssembly != currentAssembly)
            {
                assembliesToScan.Add(entryAssembly);
            }

            // 시스템 어셈블리 필터링 (성능 최적화)
            var systemAssemblyPrefixes = new[] { "System.", "Microsoft.", "mscorlib", "netstandard" };
            var filteredAssemblies = assembliesToScan
                .Where(a => !systemAssemblyPrefixes.Any(prefix => a.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var allTypes = filteredAssemblies.SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Array.Empty<Type>(); }
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