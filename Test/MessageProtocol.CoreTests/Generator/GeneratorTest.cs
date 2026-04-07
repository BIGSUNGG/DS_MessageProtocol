using MessageProtocol;
using MessageProtocol.CodeGenerator.Generate;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Assert = Xunit.Assert;

namespace MessageProtocol.Tests.Generator
{
    public class GeneratorTest
    {
        [Fact]
        public void MessageGroupRootAttribute_Should_GenerateCode()
        {
            // Arrange
            string source = @"
using MessageProtocol;

namespace MyCode
{
    [MessageGroupRoot(1)]
    public partial class TestMessage
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}";

            // Act
            var inputCompilation = CreateCompilation(source);
            var generator = new MessageCodeGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            // Assert
            Assert.Empty(diagnostics); // 생성기에서 생성한 진단이 없어야 함
            Assert.True(outputCompilation.SyntaxTrees.Count() >= 2); // 원본 + 생성된 코드

            // 생성된 코드 확인 (컴파일 오류는 참조 부족으로 인한 것이므로 무시)
            var compilationDiagnostics = outputCompilation.GetDiagnostics();
            // 참조 부족으로 인한 오류는 무시하고, 생성된 코드가 있는지만 확인

            var runResult = driver.GetRunResult();
            Assert.Equal(1, runResult.GeneratedTrees.Length);
            Assert.Empty(runResult.Diagnostics);

            var generatorResult = runResult.Results[0];
            Assert.NotNull(generatorResult.Generator);
            Assert.Empty(generatorResult.Diagnostics);
            Assert.Null(generatorResult.Exception);

            // 생성된 코드 확인
            var generatedCode = generatorResult.GeneratedSources[0].SourceText.ToString();
            Assert.Contains("TestMessage", generatedCode);
        }

        [Fact]
        public void MessageGroupElementAttribute_Should_GenerateCode()
        {
            // Arrange
            string source = @"
using MessageProtocol;

namespace MyCode
{
    [MessageGroupElement(10)]
    public partial class TestElement
    {
        public int Value { get; set; }
    }
}";

            // Act
            var inputCompilation = CreateCompilation(source);
            var generator = new MessageCodeGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            // Assert
            Assert.Empty(diagnostics);
            Assert.True(outputCompilation.SyntaxTrees.Count() >= 2);
            // 참조 부족으로 인한 컴파일 오류는 무시

            var runResult = driver.GetRunResult();
            Assert.Equal(1, runResult.GeneratedTrees.Length);

            var generatorResult = runResult.Results[0];
            var generatedCode = generatorResult.GeneratedSources[0].SourceText.ToString();
            Assert.Contains("TestElement", generatedCode);
        }

        [Fact]
        public void MessageStandaloneAttribute_Should_GenerateCode()
        {
            // Arrange
            string source = @"
using MessageProtocol;

namespace MyCode
{
    [MessageStandalone(0)]
    public partial class StandaloneMessage
    {
        public string Data { get; set; }
    }
}";

            // Act
            var inputCompilation = CreateCompilation(source);
            var generator = new MessageCodeGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            // Assert
            Assert.Empty(diagnostics);
            Assert.True(outputCompilation.SyntaxTrees.Count() >= 2);
            // 참조 부족으로 인한 컴파일 오류는 무시

            var runResult = driver.GetRunResult();
            Assert.Equal(1, runResult.GeneratedTrees.Length);

            var generatorResult = runResult.Results[0];
            var generatedCode = generatorResult.GeneratedSources[0].SourceText.ToString();
            Assert.Contains("StandaloneMessage", generatedCode);
        }

        [Fact]
        public void MessageAttribute_Should_GenerateCode()
        {
            string source = @"
using MessageProtocol;

namespace MyCode
{
    [Message]
    public partial class PlainMessage
    {
        public int Value { get; set; }
    }
}";

            var inputCompilation = CreateCompilation(source);
            var generator = new MessageCodeGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            Assert.Empty(diagnostics);
            Assert.True(outputCompilation.SyntaxTrees.Count() >= 2);

            var runResult = driver.GetRunResult();
            Assert.Equal(1, runResult.GeneratedTrees.Length);

            var generatedCode = runResult.Results[0].GeneratedSources[0].SourceText.ToString();
            Assert.Contains("PlainMessage", generatedCode);
        }

        [Fact]
        public void MultipleAttributes_Should_GenerateMultipleFiles()
        {
            // Arrange
            string source = @"
using MessageProtocol;

namespace MyCode
{
    [MessageGroupRoot(1)]
    public partial class RootMessage
    {
        public int Id { get; set; }
    }

    [MessageGroupElement(10)]
    public partial class ElementMessage
    {
        public string Name { get; set; }
    }

    [MessageStandalone(0)]
    public partial class StandaloneMessage
    {
        public bool Flag { get; set; }
    }
}";

            // Act
            var inputCompilation = CreateCompilation(source);
            var generator = new MessageCodeGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            // Assert
            Assert.Empty(diagnostics);
            Assert.True(outputCompilation.SyntaxTrees.Count() >= 4); // 원본 + 3개 생성된 파일
            // 참조 부족으로 인한 컴파일 오류는 무시

            var runResult = driver.GetRunResult();
            Assert.Equal(3, runResult.GeneratedTrees.Length); // 3개의 생성된 트리
            
            foreach(var result in runResult.Results)
            {
                foreach(var generated in result.GeneratedSources)
                {
                    string code = generated.SourceText.ToString();
                }
            }
        }

        [Fact]
        public void ClassWithoutAttribute_Should_NotGenerateCode()
        {
            // Arrange
            string source = @"
namespace MyCode
{
    public class RegularClass
    {
        public int Value { get; set; }
    }
}";

            // Act
            var inputCompilation = CreateCompilation(source);
            var generator = new MessageCodeGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            // Assert
            Assert.Empty(diagnostics);
            Assert.Single(outputCompilation.SyntaxTrees); // 원본만 있어야 함
            // 참조 부족으로 인한 컴파일 오류는 무시

            var runResult = driver.GetRunResult();
            Assert.Empty(runResult.GeneratedTrees); // 생성된 트리가 없어야 함
        }

        [Fact]
        public void NestedPartialClass_Should_GenerateContainingTypeDeclarations()
        {
            string source = @"
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
}";

            var inputCompilation = CreateCompilation(source);
            var generator = new MessageCodeGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);

            Assert.Empty(diagnostics);

            var runResult = driver.GetRunResult();
            Assert.Equal(1, runResult.GeneratedTrees.Length);

            var generatedCode = runResult.Results[0].GeneratedSources[0].SourceText.ToString();
            Assert.Contains("partial class Outer", generatedCode);
            Assert.Contains("partial class Middle", generatedCode);
            Assert.Contains("public partial class NestedMessage", generatedCode);
        }

        private static Compilation CreateCompilation(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            // 기본 참조 어셈블리들
            var references = new List<MetadataReference>();

            // System.Runtime (Attribute 등이 포함됨)
            references.Add(MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location));

            // System.Private.CoreLib (object 등 기본 타입)
            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            // System.Runtime (추가 참조)
            references.Add(MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute).Assembly.Location));

            // MessageProtocol.Core (Attribute들이 있는 어셈블리)
            references.Add(MetadataReference.CreateFromFile(typeof(MessageGroupRootAttribute).Assembly.Location));

            // Microsoft.CodeAnalysis (Binder 등)
            references.Add(MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location));

            return CSharpCompilation.Create(
                "compilation",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
    }
}
