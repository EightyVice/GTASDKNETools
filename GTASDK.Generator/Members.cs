using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpYaml.Serialization;

namespace GTASDK.Generator
{
    /// <summary>
    /// The visibility level for a generated member in C#.
    /// </summary>
    public enum Visibility
    {
        [YamlMember("private")]
        @private,
        [YamlMember("internal")]
        @internal,
        [YamlMember("public")]
        @public
    }

    /// <summary>
    /// Represents a member of a given generated C# class. This type does not contain code emit metadata, that is handled as
    /// <see cref="IFixedEmittableMember"/> and <see cref="IOffsetEmittableMember"/>.
    /// </summary>
    public interface IMember
    {
        /// <summary>
        /// The visibility of this member in C#.
        /// </summary>
        Visibility Visibility { get; }
    }

    /// <summary>
    /// Represents a type whose properly constructed instances can be emitted into C# code, used as part of a generated class.
    /// This interface differs from <see cref="IOffsetEmittableMember"/> in that these instances do not rely on positional offset
    /// in a given class for their output values.
    /// </summary>
    public interface IFixedEmittableMember : IMember
    {
        /// <summary>
        /// Gets the emitted string representation for this code member.
        /// </summary>
        /// <returns>The generated C# code, to be used as part of a given class.</returns>
        string Emit();
    }

    /// <summary>
    /// Represents a type whose properly constructed instances can be emitted into C# code, used as part of a generated class.
    /// This interface differs from <see cref="IFixedEmittableMember"/> in that these instances' <see cref="Emit"/> methods take
    /// a byte offset parameter, and they contain a byte offset property <see cref="Size"/>, which is the caller should then add
    /// to the offset accumulator.
    /// </summary>
    public interface IOffsetEmittableMember : IMember
    {
        /// <summary>
        /// The size of this emittable code member, in bytes.
        /// </summary>
        uint Size { get; }

        /// <summary>
        /// Gets the emitted string representation for this code member.
        /// </summary>
        /// <param name="offset">The offset at which this member is placed in the containing type, in bytes.</param>
        /// <returns>The generated C# code, to be used as part of a given class.</returns>
        string Emit(uint offset);
    }
}
