using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Weikio.TypeGenerator.Delegates;
using Xunit;
using Xunit.Abstractions;

namespace Weikio.TypeGenerator.Tests
{
    public class DelegateToTypeTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public DelegateToTypeTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void CanConvertDelegateToType()
        {
            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }));
        }
        
        [Fact]
        public void GeneratedTypeWorks()
        {
            var converter = new DelegateToTypeConverter();

            var type = converter.CreateType(new Func<int, bool>(i =>
            {
                System.Console.WriteLine($"Hello from delegate. I is {i}");

                return true;
            }));

            dynamic instance = Activator.CreateInstance(type);
            var result = instance.Run(25);
        }

        [Fact]
        public void ByDefaultNoProperties()
        {
            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }));

            var type = result;
            Assert.Empty(type.GetProperties());
        }

        [Fact]
        public void ByDefaultRunMethod()
        {
            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }));

            var type = result;
            var methodInfos = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Assert.Single(methodInfos);
            Assert.Equal("Run", methodInfos.Single().Name);
        }

        [Fact]
        public void ByDefaultGeneratedNamespace()
        {
            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }));

            var type = result;
            Assert.Equal("GeneratedNamespace", type.Namespace);
        }

        [Fact]
        public void CanConfigureNamespace()
        {
            var converter = new DelegateToTypeConverter();

            var type = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }), new DelegatePluginCatalogOptions() { NamespaceName = "HelloThereNs" });

            Assert.Equal("HelloThereNs", type.Namespace);
        }

        [Fact]
        public void CanConfigureNamespaceUsinGenerator()
        {
            var converter = new DelegateToTypeConverter();

            var type = converter.CreateType(new Func<int, Task<bool>>(async i =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    _testOutputHelper.WriteLine("Hello from test");

                    return true;
                }), new DelegatePluginCatalogOptions() { NamespaceNameGenerator = options => "GeneratorNS" }
            );

            Assert.Equal("GeneratorNS", type.Namespace);
        }

        [Fact]
        public void ByDefaultGeneratedTypeName()
        {
            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }));

            Assert.Equal("GeneratedType", result.Name);
        }

        [Fact]
        public void CanConfigureTypename()
        {
            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }), new DelegatePluginCatalogOptions() { TypeName = "HelloThereType" });

            Assert.Equal("HelloThereType", result.Name);
        }

        [Fact]
        public void CanConfigureTypenameUsinGenerator()
        {
            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }), new DelegatePluginCatalogOptions() { TypeNameGenerator = options => "GeneratorTypeName" });

            Assert.Equal("GeneratorTypeName", result.Name);
        }

        [Fact]
        public void CanConfigureGeneratedMethodName()
        {
            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }), new DelegatePluginCatalogOptions() { MethodName = "HelloMethod" });

            var method = result.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Single();

            Assert.Equal("HelloMethod", method.Name);
        }

        [Fact]
        public void CanConfigureGeneratedMethodNameUsingGenerator()
        {
            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }), new DelegatePluginCatalogOptions() { MethodNameGenerator = catalogOptions => "MethodGeneratorName" });

            var method = result.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Single();

            Assert.Equal("MethodGeneratorName", method.Name);
        }

        [Fact]
        public void ByDefaultNoConstructorParameters()
        {
            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }));

            Assert.Single(result.GetConstructors());

            foreach (var constructorInfo in result.GetConstructors())
            {
                Assert.Empty(constructorInfo.GetParameters());
            }
        }

        [Fact]
        public void CanConvertParameterToProperty()
        {
            var rules = new List<ConversionRule>()
            {
                new ConversionRule(info => info.ParameterType == typeof(int), nfo => new ParameterConversion() { ToPublicProperty = true }),
            };

            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }), new DelegatePluginCatalogOptions() { ConversionRules = rules });

            Assert.Single(result.GetProperties());
        }

        [Fact]
        public void CanConvertParameterToConstructorParameter()
        {
            var rules = new List<ConversionRule>()
            {
                new ConversionRule(info => info.ParameterType == typeof(int), nfo => new ParameterConversion() { ToConstructor = true })
            };

            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, Task<bool>>(async i =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }), new DelegatePluginCatalogOptions() { ConversionRules = rules });

            Assert.Single(result.GetConstructors());
            Assert.Single(result.GetConstructors().Single().GetParameters());
        }

        [Fact]
        public void CanConvertMultipleParametersToConstructorAndPropertyParameters()
        {
            var rules = new List<ConversionRule>()
            {
                new ConversionRule(info => info.ParameterType == typeof(int), nfo => new ParameterConversion() { ToConstructor = true }),
                new ConversionRule(info => info.ParameterType == typeof(string), nfo => new ParameterConversion() { ToPublicProperty = true }),
                new ConversionRule(info => info.ParameterType == typeof(bool), nfo => new ParameterConversion() { ToPublicProperty = true }),
                new ConversionRule(info => info.ParameterType == typeof(decimal), nfo => new ParameterConversion() { ToConstructor = true }),
            };

            var converter = new DelegateToTypeConverter();

            var result = converter.CreateType(new Func<int, string, bool, decimal, char, bool>((i, s, arg3, arg4, c) =>
            {
                _testOutputHelper.WriteLine("Hello from test");

                return true;
            }), new DelegatePluginCatalogOptions() { ConversionRules = rules });

            Assert.Single(result.GetConstructors());
            Assert.Equal(2, result.GetConstructors().Single().GetParameters().Length);

            Assert.Equal(2, result.GetProperties().Length);

            dynamic obj = Activator.CreateInstance(result, new object[] { 30, new decimal(22) });
            obj.S = "hello";
            obj.Arg3 = true;

            var res = obj.Run('g');
            Assert.True(res);
        }
    }
}
