using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Weikio.TypeGenerator.Tests
{
    public class CodeToAssemblyGeneratorTests
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private CodeToAssemblyGenerator _generator;

        public CodeToAssemblyGeneratorTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _generator = new CodeToAssemblyGenerator();
        }

        [Fact]
        public void CanGenerateAssemblyFromCode()
        {
            var code = @"public class MyClass
                   {
                       public void RunThings()
                       {
                           var y = 0;
                           var a = 1;
           
                           a = y + 10;
                       }
                   }";

            var result = _generator.GenerateAssembly(code);

            var type = result.GetExportedTypes().Single();

            Assert.Equal("MyClass", type.Name);
        }

        [Fact]
        public void PersistsAssemblyByDefault()
        {
            var code = @"public class MyClass
                   {
                       public void RunThings()
                       {
                           var y = 0;
                           var a = 1;
           
                           a = y + 10;
                       }
                   }";

            var result = _generator.GenerateAssembly(code);
            Assert.NotEmpty(result.Location);
        }

        [Fact]
        public void CanGenerateMemoryOnlyAssembly()
        {
            var code = @"public class MyClass
                   {
                       public void RunThings()
                       {
                           var y = 0;
                           var a = 1;
           
                           a = y + 10;
                       }
                   }";

            var result = new CodeToAssemblyGenerator(persist: false).GenerateAssembly(code);
            Assert.Empty(result.Location);
        }

        [Fact]
        public void CanReferenceOutputOfFirstGeneration()
        {
            var firstCode = @"public class MyClass
                   {
                       public void RunThings()
                       {
                           var y = 0;
                           var a = 1;
           
                           a = y + 10;
                       }
                   }";

            var firstGenerator = new CodeToAssemblyGenerator();
            var firstAssembly = firstGenerator.GenerateAssembly(firstCode);

            var secondCode = @"public class AnotherClass
                   {
                       public void RunThings()
                       {
                           var y = new MyClass();
                           y.RunThings();
                       }
                   }";

            var secondGenerator = new CodeToAssemblyGenerator();
            secondGenerator.ReferenceAssembly(firstAssembly);

            var secondAssembly = secondGenerator.GenerateAssembly(secondCode);

            var type = secondAssembly.GetExportedTypes().Single();

            Assert.Equal("AnotherClass", type.Name);
        }

        [Fact]
        public void AssemblyNameShouldntContainExtension()
        {
            var code = @"public class MyClass
                   {
                       public void RunThings()
                       {
                           var y = 0;
                           var a = 1;
           
                           a = y + 10;
                       }
                   }";

            var result = _generator.GenerateAssembly(code);
            var assemblyName = result.GetName().Name;

            var trimmedAssemblyName = Path.GetFileNameWithoutExtension(assemblyName);
            Assert.Equal(assemblyName, trimmedAssemblyName);
        }
    }
}
