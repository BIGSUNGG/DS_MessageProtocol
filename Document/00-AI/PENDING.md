---
project: DS_MessageProtocol
type: pending
status: open
tags: [ai, pending]
updated: 2026-09-08
---

# PENDING

- ~~[릴리스 대기]~~ **종결(2026-09-08)** — v2.3.5 태그 푸시·Actions 성공·nuget.org 업로드 확인.
- ~~[원장 LOW 2건 — 진단 품질]~~ **종결(2026-09-08)** — 오류형 ClassId 항목 건너뛰기(1차 컴파일 오류가 원인 지목) + MSGPROT005 폴백 속성명 "MessageAttribute" 로 수정. 회귀 테스트 1개 추가(245).
- **[보류 질문 — major 결정]** 버전 내성 스키마(롤링 배포 대응) 도입 여부·시점. / 왜: 와이어 형식 변경을 수반하는 major 결정이라 사용자 판단 영역. 설계 옵션 3종·마이그레이션(공존 기간 양쪽 형식 판독)·검증 계획은 `02-Architecture/Proposal-VersionTolerantSchema.md`. / 임시 조치: 제안 노트로만 기록, 코드 무변경 — 승인 시 별도 사이클에서 구현.
- **[보류 질문 — minor 후보]** `IBufferWriter<byte>` 송신 진입(무복사 할당 사다리 완성). / 왜: DS_Communication 송신 경로가 byte[] 기반 — 수요 미확정. / 임시 조치: 제안 노트의 대안 후보 섹션에 기록, 수요 확정 시 승격.

- **[추후 검증]** nuget.org 게시 2.3.9 아티팩트의 소비자 이행 검증(색인·검증 파이프라인 대기). / 왜: 푸시는 수락됐으나(Actions x3) flatcontainer 색인·직접 nupkg GET 이 아직 404 — nuget.org 검증 창(2.3.1 때도 동일 관측, 수 시간 내 해소). 완료 판정 기준은 Packages.md 의 발행 후 실재 확인 절차. / 임시 조치: 전파 후 전역 캐시에서 2.3.9 제거하고 `dotnet restore DRPC.slnx -p:MessageProtocolPackageVersion=2.3.9 --source https://api.nuget.org/v3/index.json --no-cache --force` + 빌드·테스트로 소비자 경로 확인. 모든 로컬 팩 회귀는 이미 통과.
