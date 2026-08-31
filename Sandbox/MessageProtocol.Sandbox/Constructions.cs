using MessageProtocol;

namespace SandboxMessages;

// ---------- S12: 분산 선언 ----------
// 선언부(Envelope<T>)를 수정하지 않고 별도 캐리어 타입으로 구성을 추가한다.
[GenericMessage(typeof(Envelope<TreeNode>), ClassId = 3)]
static class EnvelopeExtraConstructions { }
