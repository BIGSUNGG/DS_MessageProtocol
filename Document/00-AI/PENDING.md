---
project: DS_MessageProtocol
type: pending
status: open
tags: [ai, pending]
updated: 2026-09-08
---

# PENDING

- **[릴리스 대기]** KI-40(히트 이름 충돌 AD0001 수정) 커밋이 feature/improvement 에 있고 아직 릴리스되지 않았다(2.3.4 이후 미릴리스 상태). / 임시 조치: 다음 릴리스 사이클에서 v2.3.5 패치로 태그 푸시하면 된다(릴리스 절차는 Document/_meta/Changelog.md 의 2.3.4 항목 참조).
- **[원장 LOW 2건 — 진단 품질]** 오류형 `ClassId = <error>` 표현식이 "missing ClassId" 로 오안내(GenericConstruction.ParseConstructionEntries), MSGPROT005 폴백 속성명이 "NonIdMessageAttribute" 로 부정확(TypeMetadataValidator). / 임시 조치: 동작·크래시 무 — 수요 생기면 진단 문구만 손본다.
