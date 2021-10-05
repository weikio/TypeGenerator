using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.Common.Hosting;
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

        [Fact]
        public void AssemblyShouldContainVersionInfo()
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
            var versionInfo = FileVersionInfo.GetVersionInfo(result.Location);
            var fileVersion = versionInfo.FileVersion;
            Assert.NotNull(fileVersion);
        }
        
        [Fact]
        public void CanSetAssemblyContextFactory()
        {
            var myContext = new AssemblyContextForUnitTesting();
            var generator = new CodeToAssemblyGenerator(true, null, null, () => myContext);

            var code = @"public class MyClass
                   {
                       public void RunThings()
                       {
                           var y = 0;
                           var a = 1;
           
                           a = y + 10;
                       }
                   }";

            generator.GenerateAssembly(code);
            Assert.True(myContext.HasLoaded);
        }
        
        
        public class AssemblyContextForUnitTesting : CustomAssemblyLoadContext
        {
            public bool HasLoaded { get; set; }

            public AssemblyContextForUnitTesting()
            {
            }

            public AssemblyContextForUnitTesting(List<Assembly> assemblies) : base(assemblies)
            {
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                HasLoaded = true;

                return base.Load(assemblyName);
            }
        }

        [Collection(nameof(NotThreadSafeResourceCollection))]
        public class DefaultContextTests : IDisposable
        {
            private readonly ITestOutputHelper _testOutputHelper;
            private CodeToAssemblyGenerator _generator;
            private AssemblyContextForUnitTesting _myContext;

            public DefaultContextTests(ITestOutputHelper testOutputHelper)
            {
                _myContext = new AssemblyContextForUnitTesting();
                CodeToAssemblyGenerator.DefaultAssemblyLoadContextFactory = () => _myContext;
                _testOutputHelper = testOutputHelper;
                _generator = new CodeToAssemblyGenerator();
            }

            [Fact]
            public void CanSetDefaultAssemblyContextFactory()
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

                _generator.GenerateAssembly(code);
                Assert.True(_myContext.HasLoaded);
            }

            public void Dispose()
            {
                CodeToAssemblyGenerator.DefaultAssemblyLoadContextFactory = () => new CustomAssemblyLoadContext();
            }
        }
    }
}
