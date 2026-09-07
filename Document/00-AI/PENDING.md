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
