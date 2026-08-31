# Changelog

문서 변경 기록. 최신이 위.

## 2026-08-31

- 리뷰 라운드 2 수정: 생성기 힌트 이름 중첩 구분자를 `+`로 변경(네임스페이스 점과 충돌 제거 + 회귀 테스트), `generated-out` 컴파일 제외 가드를 `DefaultItemExcludes`로 교체(`Compile Remove`는 기본 글롭 이전 평가라 무효).
- 리뷰 라운드 1 수정: 생성기 힌트 이름 충돌 수정(네임스페이스 포함 유일 힌트 + 회귀 테스트), 벤치마크 InProcess 도구 체인 전환(실행 가능), `MessageWireFormat` 상수 2개 복원(`NullSizedPayloadLength`·`DefaultStreamCapacity`), `generated-out` 정리·컴파일 제외 가드, vault 문서 동기화(`00-AI/CONTEXT·GLOSSARY·CONVENTIONS`, `01-Overview/Home`, `02-Architecture/Overview`, `03-Reference/Public-API·Packages`).
- 재작성 구현 완료: `Source/` (Core·CodeGenerator·메타), `Test/` (Tests 54·Benchmarks), `Sandbox/` 인수 조건(전체 통과), 패키지 3종 2.0.0 pack 검증.
- `02-Architecture/Feature-Spec.md` status → approved, "Legacy 대비 재작성 변경점" 추가.
- `05-Decisions/ADR-0001-Rewrite-Bootstrap.md` 신규 — 네임스페이스 유지·테스트 신규 작성·예시 우선·패키지 2.0.0 결정.
- `02-Architecture/Feature-Spec.md` 신규 — 재작성 프로젝트 지원 기능 스펙 (Legacy 기능 패리티 기준).
