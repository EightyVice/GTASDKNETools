using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTASDK.Generator
{
    public sealed class MethodParsing
    {
        private readonly TypeCache _typeCache;

        public MethodParsing(TypeCache typeCache)
        {
            _typeCache = typeCache;
        }
    }

    public sealed class ParserArgument
    {
        public CompositeType Type { get; }
        public string Name { get; }
        public string TypeMapsTo => Type.CsharpName;

        public ParserArgument(CompositeType type, string name)
        {
            Type = type;
            Name = name;
        }
    }

    public abstract class Method
    {
        public virtual string Name { get; protected set; }
        public virtual CompositeType ReturnType { get; protected set; }
        public virtual IReadOnlyList<ParserArgument> Arguments { get; protected set; }
        public virtual Visibility Visibility { get; protected set; } = Visibility.@public;

        public abstract string Emit();
    }

    public sealed class InstanceMethod : Method
    {
        public string ContainingType { get; }
        public uint Offset { get; }

        public InstanceMethod(TypeCache typeCache, string containingType, string returnType, string name, string[] arguments, uint offset)
        {
            Offset = offset;
            ContainingType = containingType;
            Name = name;
            ReturnType = new CompositeType(typeCache, returnType);
            Arguments = arguments.Select(arg =>
            {
                var separator = arg.LastIndexOf(' ');
                var argType = arg.Substring(0, separator);
                var argName = arg.Substring(separator + 1);
                return new ParserArgument(new CompositeType(typeCache, argType), argName);
            }).ToArray();
        }

        public override string Emit()
        {
            var condensedArguments = string.Join(", ", Arguments.Select(e => $"{e.TypeMapsTo} {e.Name}"));
            var delegateName = $"{ContainingType}__{Name}";
            return $@"
                // Method: {Name}

                [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
                private delegate {ReturnType.CsharpName} {delegateName}({ContainingType} thisArg, {condensedArguments});
                private static readonly {delegateName} Call_{delegateName} = Memory.CallFunction<{delegateName}>({Offset});

                public static partial class Hook
                {{
                    public static LocalHook {Name}({delegateName} functionDelegate) => Memory.Hook((IntPtr){Offset}, functionDelegate);
                }}

                public {ReturnType.CsharpName} {Name}({condensedArguments})
                {{
                    {(ReturnType.CsharpName != "void" ? "return " : "")}Call_{delegateName}({Arguments.Select(e => e.Name)});
                }}
            ";
        }
    }

}
