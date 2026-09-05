; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 2.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSGPROT001 | MessageProtocol | Error | Message type must be partial
MSGPROT002 | MessageProtocol | Error | Nested message type's containing type(s) must be partial
MSGPROT003 | MessageProtocol | Error | Element message must have a root message
MSGPROT004 | MessageProtocol | Error | Root message cannot have a root message as parent
MSGPROT005 | MessageProtocol | Error | Message attribute value is out of range
MSGPROT006 | MessageProtocol | Error | Unsupported member type
MSGPROT007 | MessageProtocol | Warning | Message attributes are mutually exclusive
MSGPROT008 | MessageProtocol | Error | Invalid GenericMessage declaration
