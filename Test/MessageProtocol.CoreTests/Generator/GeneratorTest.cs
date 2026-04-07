using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace MessageProtocol.Tests.Generator
{
    public class GeneratorTest
    {
        [Fact]
        public void MessageGroupRootAttribute_Should_GenerateCompilableCode()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    [MessageGroupRoot(1)]
    public partial class TestMessage
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
""";

            var (runResult, outputCompilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics);
            Assert.Single(runResult.GeneratedTrees);
            Assert.Empty(runResult.Diagnostics);
            GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

            var generatedCode = runResult.Results.Single().GeneratedSources[0].SourceText.ToString();
            Assert.Contains("TestMessage", generatedCode);
        }

        [Fact]
        public void MessageGroupElementAttribute_Should_GenerateCompilableCode()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    [MessageGroupRoot(1)]
    public partial class TestRoot
    {
        public int Id { get; set; }
    }

    [MessageGroupElement(10)]
    public partial class TestElement : TestRoot
    {
        public int Value { get; set; }
    }
}
""";

            var (runResult, outputCompilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics);
            Assert.Equal(2, runResult.GeneratedTrees.Length);
            Assert.Empty(runResult.Diagnostics);
            GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);
        }

        [Fact]
        public void MessageStandaloneAttribute_Should_GenerateCompilableCode()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    [MessageStandalone(0)]
    public partial class StandaloneMessage
    {
        public string Data { get; set; }
    }
}
""";

            var (runResult, outputCompilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics);
            Assert.Single(runResult.GeneratedTrees);
            Assert.Empty(runResult.Diagnostics);
            GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);
        }

        [Fact]
        public void MessageAttribute_Should_GenerateCompilableCode()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    [Message]
    public partial class PlainMessage
    {
        public int Value { get; set; }
    }
}
""";

            var (runResult, outputCompilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics);
            Assert.Single(runResult.GeneratedTrees);
            Assert.Empty(runResult.Diagnostics);
            GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);
        }

        [Fact]
        public void MultipleAttributes_Should_GenerateMultipleFiles()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    [MessageGroupRoot(1)]
    public partial class RootMessage
    {
        public int Id { get; set; }
    }

    [MessageGroupElement(10)]
    public partial class ElementMessage : RootMessage
    {
        public string Name { get; set; }
    }

    [MessageStandalone(0)]
    public partial class StandaloneMessage
    {
        public bool Flag { get; set; }
    }
}
""";

            var (runResult, outputCompilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics);
            Assert.Equal(3, runResult.GeneratedTrees.Length);
            Assert.Empty(runResult.Diagnostics);
            GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);
        }

        [Fact]
        public void ClassWithoutAttribute_Should_NotGenerateCode()
        {
            const string source = """
namespace MyCode
{
    public class RegularClass
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

        [Fact]
        public void NestedPartialClass_Should_GenerateContainingTypeDeclarations()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    public partial class Outer
    {
        public partial class Middle
        {
            [Message]
            public partial class NestedMessage
            {
                public int Value { get; set; }
            }
        }
    }
}
""";

            var (runResult, outputCompilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics);
            Assert.Single(runResult.GeneratedTrees);
            GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

            var generatedCode = runResult.Results.Single().GeneratedSources[0].SourceText.ToString();
            Assert.Contains("partial class Outer", generatedCode);
            Assert.Contains("partial class Middle", generatedCode);
            Assert.Contains("public partial class NestedMessage", generatedCode);
        }

        [Fact]
        public void MessageAttribute_OnStruct_Should_GeneratePartialStruct()
        {
            const string source = """
using MessageProtocol;

namespace MyCode
{
    [Message]
    public partial struct StructMessage
    {
        public int Value { get; set; }
    }
}
""";

            var (runResult, outputCompilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

            Assert.Empty(diagnostics);
            Assert.Single(runResult.GeneratedTrees);
            GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

            var generatedCode = runResult.Results.Single().GeneratedSources[0].SourceText.ToString();
            Assert.Contains("public partial struct StructMessage", generatedCode);
            Assert.DoesNotContain("public partial class StructMessage", generatedCode);
        }
    }
}
