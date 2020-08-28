using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpYaml.Model;
using SharpYaml.Serialization;

namespace GTASDK.Generator
{
    public sealed class InstanceMethodParsing
    {
        private readonly TypeCache _typeCache;

        public InstanceMethodParsing(TypeCache typeCache)
        {
            _typeCache = typeCache;
        }

        public Method ParseMethod(string containingTypeName, YamlSequence sequence)
        {
            switch (((YamlValue)sequence[0]).Value)
            {
                case "virtual" when sequence.Count != 5:
                    throw new ArgumentException("Virtual method definitions must have 5 elements");
                case "virtual":
                {
                    var (_, returnType, name, arguments, index) = sequence.ToObjectX<(Modifier modifier, string returnType, string name, string[] arguments, uint index)>();
                    return new VirtualMethod(_typeCache, containingTypeName, returnType, name, arguments, index);
                }
                case "partial" when sequence.Count != 5:
                    throw new ArgumentException("Partial method definitions must have 5 elements");
                case "partial":
                {
                    var (_, returnType, name, arguments, offset) = sequence.ToObjectX<(Modifier modifier, string returnType, string name, string[] arguments, uint offset)>();
                    return new PartialMethod(_typeCache, containingTypeName, returnType, name, arguments, offset);
                }
                default:
                {
                    if (sequence.Count != 4)
                        throw new ArgumentException("Method definitions must be either 4 elements or 5 elements with a valid modifier (virtual/partial)");

                    var (returnType, name, arguments, offset) = sequence.ToObjectX<(string returnType, string name, string[] arguments, uint offset)>();
                    return new InstanceMethod(_typeCache, containingTypeName, returnType, name, arguments, offset);
                }
            }
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
        public string Name { get; }
        public CompositeType ReturnType { get; }
        public IReadOnlyList<ParserArgument> Arguments { get; }
        public Visibility Visibility { get; protected set; } = Visibility.@public;

        protected Method(TypeCache typeCache, string returnType, string name, IEnumerable<string> arguments)
        {
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

        public abstract string Emit();
        public abstract string EmitHook();
    }

    public class InstanceMethod : Method
    {
        public string ContainingType { get; }
        public uint Offset { get; }
        protected string DelegateName => $"{ContainingType}__{Name}";

        public InstanceMethod(TypeCache typeCache, string containingType, string returnType, string name, IEnumerable<string> arguments, uint offset)
            : base(typeCache, returnType, name, arguments)
        {
            Offset = offset;
            ContainingType = containingType;
        }

        protected IEnumerable<string> SerializeArguments()
        {
            foreach (var argument in Arguments)
            {
                if (argument.Type.TryGet(out var type))
                    if (argument.Type.IsRef)
                        yield return type.ArgumentTemplate.Argument($"ref {argument.Type.CsharpName}", $"{argument.Name}");
                    else
                        yield return type.ArgumentTemplate.Argument(argument.Type.CsharpName, argument.Name);
                else if (argument.Type.IsPointer)
                    yield return Types.Pointer.ArgumentTemplate.Argument("IntPtr", argument.Name);
                else
                    throw new ArgumentException($"Did not find valid type mapping for argument {argument.Type.OriginalName} {argument.Name}", nameof(argument));
            }
        }

        protected IEnumerable<string> SerializeDelegateArguments() // ATM the only special case: Pointers are IntPtr on delegates, real type on other methods.
        {
            foreach (var argument in Arguments)
            {
                if (argument.Type.IsPointer)
                    yield return Types.Pointer.ArgumentTemplate.Argument("IntPtr", argument.Name);
                else if (argument.Type.TryGet(out var type))
                    if (argument.Type.IsRef)
                        yield return type.ArgumentTemplate.Argument($"ref {argument.Type.CsharpName}", $"{argument.Name}");
                    else
                        yield return type.ArgumentTemplate.Argument(argument.Type.CsharpName, argument.Name);
                else
                    throw new ArgumentException($"Did not find valid type mapping for argument {argument.Type.OriginalName} {argument.Name}", nameof(argument));
            }
        }

        protected IEnumerable<string> SerializeArgumentPassing()
        {
            foreach (var argument in Arguments)
            {
                if (argument.Type.TryGet(out var type))
                    if (argument.Type.IsRef)
                        yield return type.ArgumentTemplate.Call($"{argument.Type.CsharpName}", $"ref {argument.Name}");
                    else
                        yield return type.ArgumentTemplate.Call(argument.Type.CsharpName, argument.Name);
                else if (argument.Type.IsPointer)
                    yield return Types.Pointer.ArgumentTemplate.Call("IntPtr", argument.Name);
                else
                    throw new ArgumentException($"Did not find valid type mapping for argument {argument.Type.OriginalName} {argument.Name}", nameof(argument));
            }
        }

        public override string Emit()
        {
            var condensedArguments = SerializeArguments();

            var delegateArguments = SerializeDelegateArguments().Prepend($"{ContainingType} thisArg");

            var callArguments = SerializeArgumentPassing().Prepend("this");

            var originalSignature = Arguments.Select(e => $"{e.Type.OriginalName} {e.Name}");

            return $@"
                // Method: {Name}({string.Join(", ", originalSignature)})

                [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
                public delegate {ReturnType.CsharpName} {DelegateName}({string.Join(", ", delegateArguments)});
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

    public sealed class VirtualMethod : InstanceMethod
    {
        public VirtualMethod(TypeCache typeCache, string containingType, string returnType, string name, IEnumerable<string> arguments, uint index)
            : base(typeCache, containingType, returnType, name, arguments, index)
        {
        }

        public override string Emit()
        {
            var condensedArguments = SerializeArguments();

            var delegateArguments = SerializeDelegateArguments().Prepend($"{ContainingType} thisArg");

            var callArguments = SerializeArgumentPassing().Prepend("this");

            var originalSignature = Arguments.Select(e => $"{e.Type.OriginalName} {e.Name}");

            return $@"
                // VTable method: {Name}({string.Join(", ", originalSignature)})

                [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
                public delegate {ReturnType.CsharpName} {DelegateName}({string.Join(", ", delegateArguments)});

                public {ReturnType.CsharpName} {Name}({string.Join(", ", condensedArguments)})
                {{
                    {(ReturnType.CsharpName != "void" ? "return " : "")}Memory.CallVirtualFunction<{DelegateName}>(_vtable.ToInt32(), {Offset})({string.Join(", ", callArguments)});
                }}
            ";
        }

        public override string EmitHook()
        {
            return "";
        }
    }

    public class PartialMethod : InstanceMethod
    {
        public PartialMethod(TypeCache typeCache, string containingType, string returnType, string name, IEnumerable<string> arguments, uint offset)
            : base(typeCache, containingType, returnType, name, arguments, offset)
        {
        }

        public override string Emit()
        {
            var condensedArguments = SerializeArguments().ToArray();

            var callArguments = SerializeArgumentPassing().Append($"0x{Offset:X}").ToArray();

            var originalSignature = Arguments.Select(e => $"{e.Type.OriginalName} {e.Name}");

            return $@"
                // Partial method: {Name}({string.Join(", ", originalSignature)})

                public {ReturnType.CsharpName} {Name}({string.Join(", ", condensedArguments)})
                {{
                    {(ReturnType.CsharpName != "void" ? "return " : "")}{Name}Impl({string.Join(", ", callArguments)});
                }}
            ";
        }
        public override string EmitHook()
        {
            return "";
        }
    }
}
