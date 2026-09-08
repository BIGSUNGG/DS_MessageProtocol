---
project: DS_MessageProtocol
type: pending
status: open
tags: [ai, pending]
updated: 2026-09-09
---

# PENDING

- ~~[릴리스 대기]~~ **종결(2026-09-08)** — v2.3.5 태그 푸시·Actions 성공·nuget.org 업로드 확인.
- ~~[원장 LOW 2건 — 진단 품질]~~ **종결(2026-09-08)** — 오류형 ClassId 항목 건너뛰기(1차 컴파일 오류가 원인 지목) + MSGPROT005 폴백 속성명 "MessageAttribute" 로 수정. 회귀 테스트 1개 추가(245).
- ~~[보류 질문 — major 결정] 버전 내성 스키마 도입 여부·시점~~ **종결(2026-09-09)** — **미도입 결정**. Unity·서버가 같은 라이브러리를 함께 소비하므로 스키마 진화 불필요. [ADR-0006](../05-Decisions/ADR-0006-No-Schema-Evolution.md) 참조. 제안 노트는 기각 상태로 보존. 재검토 조건(외부 배포)은 ADR 결정 3.
- **[보류 질문 — minor 후보]** `IBufferWriter<byte>` 송신 진입(무복사 할당 사다리 완성). / 왜: DS_Communication 송신 경로가 byte[] 기반 — 수요 미확정. / 임시 조치: 제안 노트의 대안 후보 섹션에 기록, 수요 확정 시 승격.

- ~~[추후 검증] 게시 2.3.9 소비 검증~~ **종결(2026-09-09)** — 색인 전파 완료 확인(flatcontainer 목록·직접 nupkg GET 200) 후 순수 nuget.org 소스로 DRPC 전체 솔루션 복원(전역 캐시 제거·--no-cache — 진짜 다운로드 강제)·빌드 0 오류·테스트 58/58 통과. 게시 아티팩트 소비자 이행 검증 완료.
