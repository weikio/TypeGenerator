using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Weikio.TypeGenerator.Types;
using Xunit;
using Xunit.Abstractions;

namespace Weikio.TypeGenerator.Tests
{
    public class TypeWrapperTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TypeWrapperTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void CanWrapType()
        {
            var wrapper = new TypeToTypeWrapper();

            var result = wrapper.CreateType(typeof(TestClass));

            dynamic instance = Activator.CreateInstance(result);
            instance.Run();

            var res = instance.HelloWorld(20, true);
            var res2 = instance.HelloWorld(15, false);

            Assert.Equal("20", res);
            Assert.Equal("Hello 15", res2);
        }

        [Fact]
        public void IncludesAllDeclaredPublicMethodsByDefault()
        {
            var wrapper = new TypeToTypeWrapper();

            var originalType = typeof(TestClass);
            var result = wrapper.CreateType(originalType);

            // Properties are also skipped by default 
            var typeMethods = originalType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(x => x.IsSpecialName == false).ToArray();
            var generatedMethods = result.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            Assert.Equal(typeMethods.Length, generatedMethods.Length);
        }

        [Fact]
        public void CanIncludeSpecificMethods()
        {
            var wrapper = new TypeToTypeWrapper();

            var options = new TypeToTypeWrapperOptions() { IncludedMethods = new List<string>() { "HelloWorld" } };

            var originalType = typeof(TestClass);
            var result = wrapper.CreateType(originalType, options);

            var allMethods = result.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Single(allMethods);
        }

        [Fact]
        public void CanIncludeSpecificMethodsWithWildCard()
        {
            var wrapper = new TypeToTypeWrapper();

            var options = new TypeToTypeWrapperOptions() { IncludedMethods = new List<string>() { "Hello*" } };

            var originalType = typeof(TestClass);
            var result = wrapper.CreateType(originalType, options);

            var allMethods = result.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Single(allMethods);
        }

        [Fact]
        public void CanIncludeSpecificMethodsWithMultipleWildCards()
        {
            var wrapper = new TypeToTypeWrapper();

            var options = new TypeToTypeWrapperOptions() { IncludedMethods = new List<string>() { "*llo*", "Ru*" } };
            var result = wrapper.CreateType(typeof(TestClass), options);

            var allMethods = result.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Equal(2, allMethods.Length);
        }

        [Fact]
        public void CanRenameMethod()
        {
            var wrapper = new TypeToTypeWrapper();

            var options = new TypeToTypeWrapperOptions()
            {
                IncludedMethods = new List<string>() { "Run" }, MethodNameGenerator = (wrapperOptions, type, mInfo) => "CustomMethodName"
            };

            var result = wrapper.CreateType(typeof(TestClass), options);

            dynamic instance = Activator.CreateInstance(result);
            instance.CustomMethodName();
        }

        [Fact]
        public void ByDefaultEachInstanceIsTransient()
        {
            var wrapper = new TypeToTypeWrapper();
            var result = wrapper.CreateType(typeof(TestClass));

            dynamic instance = Activator.CreateInstance(result);
            dynamic instance2 = Activator.CreateInstance(result);

            instance.AddCount();
            instance.AddCount();

            instance2.AddCount();
            instance2.AddCount();
            instance2.AddCount();

            var instanceCount = instance.GetCount();
            Assert.Equal(2, instanceCount);

            var instance2Count = instance2.GetCount();
            Assert.Equal(3, instance2Count);
        }

        [Fact]
        public void CanCreateSingleton()
        {
            var inst = new TestClass();
            var options = new TypeToTypeWrapperOptions() { Factory = (wrapperOptions, type) => inst };

            var wrapper = new TypeToTypeWrapper();
            var result = wrapper.CreateType(typeof(TestClass), options);

            dynamic instance = Activator.CreateInstance(result);
            dynamic instance2 = Activator.CreateInstance(result);

            instance.AddCount();
            instance.AddCount();

            instance2.AddCount();
            instance2.AddCount();
            instance2.AddCount();

            var instanceCount = instance.GetCount();
            Assert.Equal(5, instanceCount);

            var instance2Count = instance2.GetCount();
            Assert.Equal(5, instance2Count);
        }

        [Fact]
        public void CanRunCodeBeforeMethodExecution()
        {
            var options = new TypeToTypeWrapperOptions()
            {
                OnBeforeMethod = (wrapperOptions, type, inst, mi) =>
                {
                    if (mi.Name == "Run")
                    {
                        ((TestClass) inst).AddCount(6);
                    }
                }
            };

            var wrapper = new TypeToTypeWrapper();
            var result = wrapper.CreateType(typeof(TestClass), options);

            dynamic instance = Activator.CreateInstance(result);
            instance.Run();
            instance.Run();

            var instanceCount = instance.GetCount();
            Assert.Equal(12, instanceCount);
        }

        [Fact]
        public void CanRunCodeAfterMethodExecution()
        {
            var options = new TypeToTypeWrapperOptions()
            {
                OnBeforeMethod = (wrapperOptions, type, inst, mi) =>
                {
                    ((TestClass) inst).AddCount(8);
                }
            };

            var wrapper = new TypeToTypeWrapper();
            var result = wrapper.CreateType(typeof(TestClass), options);

            dynamic instance = Activator.CreateInstance(result);
            instance.Run();
            instance.Run();

            var instanceCount = instance.GetCount();
            Assert.Equal(24, instanceCount);
        }

        [Fact]
        public void CanRunCodeOnConstructor()
        {
            var options = new TypeToTypeWrapperOptions()
            {
                OnConstructor = (wrapperOptions, type, inst) =>
                {
                    ((TestClass) inst).AddCount(15);
                }
            };

            var wrapper = new TypeToTypeWrapper();
            var result = wrapper.CreateType(typeof(TestClass), options);

            dynamic instance = Activator.CreateInstance(result);
            var instanceCount = instance.GetCount();

            Assert.Equal(15, instanceCount);
        }

        [Fact]
        public void CanAddCustomCode()
        {
            var customCode = @"public void RunThings()
                       {
                           var y = 0;
                           var a = 1;
           
                           a = y + 10;
                       }";

            var options = new TypeToTypeWrapperOptions() { CustomCodeGenerator = (wrapperOptions, type) => customCode };

            var wrapper = new TypeToTypeWrapper();
            var result = wrapper.CreateType(typeof(TestClass), options);

            dynamic instance = Activator.CreateInstance(result);
            instance.RunThings();
        }
        
        [Fact]
        public void CanWrapTypeWithConstructor()
        {
            var wrapper = new TypeToTypeWrapper();
            var result = wrapper.CreateType(typeof(TestClassWithConstructor), new TypeToTypeWrapperOptions()
            {
                Factory = (options, type) => new TestClassWithConstructor("Hello there")
            });

            dynamic instance = Activator.CreateInstance(result);
            instance.Get();
        }
    }
}
