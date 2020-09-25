using System;
using System.Collections.Generic;

namespace Weikio.TypeGenerator
{
    public class DelegatePluginCatalogOptions
    {
        public List<ConversionRule> ConversionRules { get; set; } = new List<ConversionRule>();
        public string MethodName { get; set; } = "Run";
        public string TypeName { get; set; } = "GeneratedType";
        public string NamespaceName { get; set; } = "GeneratedNamespace";
        public Func<DelegatePluginCatalogOptions, string> MethodNameGenerator { get; set; } = options => options.MethodName;
        public Func<DelegatePluginCatalogOptions, string> TypeNameGenerator { get; set; } = options => options.TypeName;
        public Func<DelegatePluginCatalogOptions, string> NamespaceNameGenerator { get; set; } = options => options.NamespaceName;
        public List<string> Tags { get; set; } = new List<string>();
    }
}
