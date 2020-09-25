namespace Weikio.TypeGenerator
{
    public class ParameterConversion
    {
        public bool ToConstructor { get; set; }
        public bool ToPublicProperty { get; set; }
        public string Name { get; set; }
    }

    public class MethodConversion
    {
        public string MethodName { get; set; }
    }
}
