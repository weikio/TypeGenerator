using System;
using System.Collections.Generic;

namespace Weikio.TypeGenerator.Delegates
{
    public class DelegateToTypeWrapperOptions
    {
        public List<ParameterConversionRule> ConversionRules { get; set; } = new List<ParameterConversionRule>();
        public string MethodName { get; set; } = "Run";
        public string TypeName { get; set; } = "GeneratedType";
        public string NamespaceName { get; set; } = "GeneratedNamespace";
        public Func<DelegateToTypeWrapperOptions, string> MethodNameGenerator { get; set; } = options => options.MethodName;
        public Func<DelegateToTypeWrapperOptions, string> TypeNameGenerator { get; set; } = options => options.TypeName;
        public Func<DelegateToTypeWrapperOptions, string> NamespaceNameGenerator { get; set; } = options => options.NamespaceName;
    }
}
