using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpYaml.Model;
using SharpYaml.Serialization;

namespace GTASDK.Generator
{
    public sealed class MethodParsing
    {
        private readonly TypeCache _typeCache;

        public MethodParsing(TypeCache typeCache)
        {
            _typeCache = typeCache;
        }

        public Method ParseMethod(string containingTypeName, YamlSequence sequence)
        {
            if (sequence.Count == 5)
            {
                var (modifier, returnType, name, arguments, offset) = sequence.ToObjectX<(Modifier modifier, string returnType, string name, string[] arguments, uint offset)>();
                if (modifier != Modifier.Virtual)
                {
                    throw new ArgumentException($"Invalid modifier {modifier}, method definitions with 5 elements must be virtual");
                }
            }
            else if (sequence.Count == 4)
            {
                switch (((YamlValue)sequence[0]).Value)
                {
                    case "virtual":
                        throw new ArgumentException("Virtual method definitions must have 5 members");
                    case "partial":
                    {
                        var (modifier, returnType, name, arguments) = sequence.ToObjectX<(Modifier modifier, string returnType, string name, string[] arguments)>();
                        break;
                    }
                    default:
                    {
                        var (returnType, name, arguments, offset) = sequence.ToObjectX<(string returnType, string name, string[] arguments, uint offset)>();
                        return new InstanceMethod(_typeCache, containingTypeName, returnType, name, arguments, offset);
                    }
                }
            }

            return null;
        }
    }

    public enum Modifier
    {
        [YamlMember("virtual")]
        Virtual,
        [YamlMember("partial")]
        Partial,
        None
    }

    public sealed class ParserArgument
    {
        public CompositeType Type { get; }
        public string Name { get; }

        public ParserArgument(CompositeType type, string name)
        {
            Type = type;
            Name = name;
        }
    }

    public abstract class Method : IFixedEmittableMember
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

        public InstanceMethod(TypeCache typeCache, string containingType, string returnType, string name, IEnumerable<string> arguments, uint offset)
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
            var condensedArguments = Arguments.Select(argument =>
            {
                if (argument.Type.TryGet(out var type))
                    if (argument.Type.IsRef)
                        return type.ArgumentTemplate.Argument($"ref {argument.Type.CsharpName}", $"{argument.Name}");
                    else
                        return type.ArgumentTemplate.Argument(argument.Type.CsharpName, argument.Name);
                if (argument.Type.IsPointer)
                    return Types.Pointer.ArgumentTemplate.Argument("IntPtr", argument.Name);

                throw new ArgumentException($"Did not find valid type mapping for argument {argument.Type.OriginalName} {argument.Name}", nameof(argument));
            }).ToArray();

            var condensedArgumentsWithThisArg = condensedArguments.Prepend($"{ContainingType} thisArg");

            var callArguments = Arguments.Select(argument =>
            {
                if (argument.Type.TryGet(out var type))
                    if (argument.Type.IsRef)
                        return type.ArgumentTemplate.Call($"{argument.Type.CsharpName}", $"ref {argument.Name}");
                    else
                        return type.ArgumentTemplate.Call(argument.Type.CsharpName, argument.Name);
                if (argument.Type.IsPointer)
                    return Types.Pointer.ArgumentTemplate.Call("IntPtr", argument.Name);

                throw new ArgumentException($"Did not find valid type mapping for argument {argument.Type.OriginalName} {argument.Name}", nameof(argument));
            }).Prepend("this").ToArray();

            var originalSignature = Arguments.Select(e => $"{e.Type.OriginalName} {e.Name}");

            var delegateName = $"{ContainingType}__{Name}";

            return $@"
                // Method: {Name}({string.Join(", ", originalSignature)})

                [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
                public delegate {ReturnType.CsharpName} {delegateName}({string.Join(", ", condensedArgumentsWithThisArg)});
                private static readonly {delegateName} Call_{delegateName} = Memory.CallFunction<{delegateName}>({Offset});

                public static partial class Hook
                {{
                    public static LocalHook {Name}({delegateName} functionDelegate) => Memory.Hook((IntPtr){Offset}, functionDelegate);
                }}

                public {ReturnType.CsharpName} {Name}({string.Join(", ", condensedArguments)})
                {{
                    {(ReturnType.CsharpName != "void" ? "return " : "")}Call_{delegateName}({string.Join(", ", callArguments)});
                }}
            ";
        }
    }

}
