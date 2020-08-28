using System.Collections.Generic;
using System.Linq;
using SharpYaml.Model;

namespace GTASDK.Generator
{
    public sealed class StaticMethodParsing
    {
        private readonly TypeCache _typeCache;

        public StaticMethodParsing(TypeCache typeCache)
        {
            _typeCache = typeCache;
        }

        public StaticMethod ParseMethod(string containingTypeName, (string returnType, string name, string[] arguments, uint offset) signature)
        {
            return new StaticMethod(_typeCache, containingTypeName, signature.returnType, signature.name, signature.arguments, signature.offset);
        }
    }

    public class StaticMethod : InstanceMethod
    {
        public StaticMethod(TypeCache typeCache, string containingType, string returnType, string name, IEnumerable<string> arguments, uint offset)
            : base(typeCache, containingType, returnType, name, arguments, offset)
        {
        }

        public override string Emit()
        {
            var condensedArguments = SerializeArguments().ToArray();
            var callArguments = SerializeArgumentPassing().ToArray();

            var originalSignature = Arguments.Select(e => $"{e.Type.OriginalName} {e.Name}");

            return $@"
                // Method: {Name}({string.Join(", ", originalSignature)})

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate {ReturnType.CsharpName} {DelegateName}({string.Join(", ", condensedArguments)});
                private static readonly {DelegateName} Call_{DelegateName} = Memory.CallFunction<{DelegateName}>(0x{Offset:X});

                public {ReturnType.CsharpName} {Name}({string.Join(", ", condensedArguments)})
                {{
                    {(ReturnType.CsharpName != "void" ? "return " : "")}Call_{DelegateName}({string.Join(", ", callArguments)});
                }}
            ";
        }

        public override string EmitHook()
        {
            return $@"
                public static LocalHook {Name}({DelegateName} functionDelegate) => Memory.Hook((IntPtr)0x{Offset:X}, functionDelegate);
            ";
        }
    }
}
