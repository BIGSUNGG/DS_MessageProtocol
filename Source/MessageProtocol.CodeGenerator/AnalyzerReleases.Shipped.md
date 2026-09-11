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
## Release 3.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSGPROT010 | MessageProtocol | Error | Message type must be constructible
MSGPROT011 | MessageProtocol | Error | Message member must be assignable
MSGPROT012 | MessageProtocol | Warning | Message member serializes by declared type
MSGPROT013 | MessageProtocol | Error | Message category value is out of range
MSGPROT014 | MessageProtocol | Error | Duplicate wire MessageId
MSGPROT015 | MessageProtocol | Error | Duplicate generic construction runtime key
MSGPROT016 | MessageProtocol | Error | Message full-name hash MessageId collision
MSGPROT017 | MessageProtocol | Error | Message child hash resolved to 0
MSGPROT018 | MessageProtocol | Error | Message constructor arguments do not match the declared kind

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MSGPROT007 | MessageProtocol | Disabled | Message is now the only message attribute and AllowMultiple is false, so duplicate attachment is a compile error
