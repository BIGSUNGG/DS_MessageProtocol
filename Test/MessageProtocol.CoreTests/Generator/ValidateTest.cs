using Xunit;

namespace MessageProtocol.Tests.Generator
{
    public class ValidateTest
    {
        [Fact]
        public void NonPartialMessage_Should_Report_MSGPROT001()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    [NonIdMessage]
    public class InvalidMessage
    {
        public int Value { get; set; }
    }
}
""";

            var (_, _, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("MSGPROT001", diagnostic.Id);
        }

        [Fact]
        public void NestedNonIdMessage_WithNonPartialContainingType_Should_Report_MSGPROT002()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    public class Outer
    {
        [NonIdMessage]
        public partial class NestedMessage
        {
            public int Value { get; set; }
        }
    }
}
""";

            var (_, _, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("MSGPROT002", diagnostic.Id);
        }

        [Fact]
        public void GroupElement_WithoutRoot_Should_Report_MSGPROT003()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    [GroupElementMessage(1)]
    public partial class InvalidElement
    {
        public int Value { get; set; }
    }
}
""";

            var (_, _, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("MSGPROT003", diagnostic.Id);
        }

        [Fact]
        public void RootMessage_WithRootParent_Should_Report_MSGPROT004()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    [GroupRootMessage(1)]
    public partial class BaseRoot
    {
    }

    [GroupRootMessage(2)]
    public partial class DerivedRoot : BaseRoot
    {
    }
}
""";

            var (_, _, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("MSGPROT004", diagnostic.Id);
        }

        [Fact]
        public void OutOfRangeAttributeValue_Should_Report_MSGPROT005()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    [StandaloneMessage(16777216u)]
    public partial class InvalidMessage
    {
    }
}
""";

            var (_, _, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            var diagnostic = Assert.Single(diagnostics);
            Assert.Equal("MSGPROT005", diagnostic.Id);
        }

        [Fact]
        public void AbstractRootMessage_Should_NotGenerateCode()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    [GroupRootMessage(1)]
    public abstract partial class AbstractRoot
    {
        public int Value { get; set; }
    }
}
""";

            var (runResult, outputCompilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics);
            Assert.Empty(runResult.GeneratedTrees);
            Assert.Single(outputCompilation.SyntaxTrees);
        }
    }
}
