; Unshipped analyzer release
; <https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md>

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSGPROT010 | MessageProtocol | Error | Message type must be constructible
MSGPROT011 | MessageProtocol | Error | Message member must be assignable
MSGPROT012 | MessageProtocol | Warning | Message member serializes by declared type
MSGPROT013 | MessageProtocol | Error | Message category value is out of range
