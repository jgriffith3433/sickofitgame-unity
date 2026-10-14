using System;

namespace SickOfIt.Json
{
    /// <summary>
    /// Maps Unity asset references to the string ids written in config JSON, and back.
    /// The editor resolves ids through the AssetDatabase; a runtime resolver can use a registry.
    /// </summary>
    public interface IAssetIdResolver
    {
        /// <summary>The id to write for <paramref name="asset"/>, or null with <paramref name="error"/> set.</summary>
        string GetId(UnityEngine.Object asset, Type fieldType, out string error);

        /// <summary>The asset for <paramref name="id"/> that fits <paramref name="fieldType"/>, or null with <paramref name="error"/> set.</summary>
        UnityEngine.Object Resolve(string id, Type fieldType, out string error);

        /// <summary>A human-readable description of what a broken reference used to point at.</summary>
        string DescribeMissing(UnityEngine.Object missing);
    }
}
