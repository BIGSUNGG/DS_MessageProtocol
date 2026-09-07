# AGENTS.md — DS_MessageProtocol

## Document vault (required)

This repo keeps an Obsidian vault at `Document/`. Agents must **read and write** it whenever they analyze, change, or test code.

1. **Start**: read `Document/00-AI/CONTEXT.md`, then `GLOSSARY.md` and related Architecture/Reference notes.
2. **Same turn**: any change under `Source/`, `Test/`, or `Sandbox/` must update `Document/` (`Legacy/` is a read-only archive; `Examples/`·`TemplateSource/` do not exist in this repo).
3. **Structure**: package, folder, or public API changes belong in `02-Architecture/` and `03-Reference/` (see mapping in skill `ds-document-vault`).
4. **Meta**: refresh YAML `updated` / `status`; append `_meta/Changelog.md` when docs change.
5. **Conventions**: `Document/00-AI/CONVENTIONS.md` and skill `ds-document-vault`.

Human entry: `Document/01-Overview/Home.md`.

## Project

- Display name: DS_MessageProtocol
- Sibling stack: DS_RPC depends on DS_MessageProtocol and DS_Communication.