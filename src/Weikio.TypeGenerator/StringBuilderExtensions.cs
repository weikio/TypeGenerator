// ReSharper disable once CheckNamespace

namespace System.Text
{
    public static class StringBuilderExtensions
    {
        public static void UsingNamespace(this StringBuilder sb, string ns)
        {
            sb.AppendLine($"using {ns};");
        }

        public static void WriteLine(this StringBuilder sb, string text)
        {
            sb.AppendLine(text);
        }

        public static void Write(this StringBuilder sb, string text)
        {
            sb.Append(text);
        }

        public static void Namespace(this StringBuilder sb, string ns)
        {
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
        }

        public static void StartBlock(this StringBuilder sb)
        {
            sb.AppendLine("{");
        }

        public static void FinishBlock(this StringBuilder sb)
        {
            sb.AppendLine("}");
        }

        public static void StartClass(this StringBuilder sb, string className)
        {
            sb.AppendLine($"public class {className}");
            sb.AppendLine("{");
        }

        public static void UsingBlock(this StringBuilder writer, string declaration, Action<StringBuilder> inner)
        {
            writer.Write("using (" + declaration + ")");
            writer.Write("{");
            inner(writer);
            writer.FinishBlock();
        }
    }
}
