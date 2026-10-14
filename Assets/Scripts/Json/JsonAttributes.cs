using System;

namespace SickOfIt.Json
{
    /// <summary>
    /// Marks an abstract base class whose concrete subclasses are picked in JSON by a "type" field:
    /// { "type": "chase", "speed": 2 }. Each subclass declares its name with <see cref="JsonTypeAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class JsonPolymorphicAttribute : Attribute
    {
    }

    /// <summary>The "type" name of a concrete subclass of a <see cref="JsonPolymorphicAttribute"/> base.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class JsonTypeAttribute : Attribute
    {
        public readonly string Name;
        public JsonTypeAttribute(string name) { Name = name; }
    }

    /// <summary>
    /// A string field that holds a JSON value. In config files it is written inline as JSON, not as a
    /// quoted string; in the asset it is stored as compact JSON text and parsed later (at runtime).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RawJsonAttribute : Attribute
    {
    }

    /// <summary>Documentation for a content type or field, used by the generated gameplay reference.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Enum)]
    public sealed class DocAttribute : Attribute
    {
        public readonly string Text;
        public DocAttribute(string text) { Text = text; }
    }
}
