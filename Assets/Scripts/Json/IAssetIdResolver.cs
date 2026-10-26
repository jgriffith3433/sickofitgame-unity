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

    /// <summary>
    /// A resolver whose assets come and go (a player build loads art and sound on demand: Core/Addressables GameAssets).
    /// For an id it <see cref="Defers"/>, the reader keeps the field empty without an error and hands the resolver a setter
    /// (<see cref="Bind"/>), which it calls with the asset whenever it is loaded and with null when it is released.
    /// </summary>
    public interface IDeferredAssetResolver : IAssetIdResolver
    {
        /// <summary>True when <paramref name="id"/> is known but may not be loaded now: read it through <see cref="Bind"/>.</summary>
        bool Defers(string id, Type fieldType);

        /// <summary>Keeps <paramref name="assign"/> for <paramref name="id"/> and calls it now if the asset is loaded.</summary>
        void Bind(string id, Type fieldType, Action<UnityEngine.Object> assign);
    }
}
