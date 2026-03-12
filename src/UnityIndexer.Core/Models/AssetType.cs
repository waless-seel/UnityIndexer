namespace UnityIndexer.Core.Models;

/// <summary>Unity アセットの種別</summary>
public enum AssetType
{
    Unknown,
    Script,       // .cs
    Prefab,       // .prefab
    Scene,        // .unity
    Material,     // .mat
    Texture,      // .png/.jpg/.tga 等
    Audio,        // .wav/.mp3/.ogg 等
    Animation,    // .anim
    AnimatorController, // .controller
    ScriptableObject,   // .asset
    Shader,       // .shader/.hlsl/.cginc
    AssemblyDefinition, // .asmdef
    Font,         // .ttf/.otf
    Meta,         // .meta
    Other,
}
