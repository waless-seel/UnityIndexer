using UnityIndexer.Core.Models;

namespace UnityIndexer.Core.Utilities;

/// <summary>ファイル拡張子から AssetType を判定するユーティリティ</summary>
public static class AssetTypeDetector
{
    private static readonly Dictionary<string, AssetType> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"]         = AssetType.Script,
        [".prefab"]     = AssetType.Prefab,
        [".unity"]      = AssetType.Scene,
        [".mat"]        = AssetType.Material,
        [".shader"]     = AssetType.Shader,
        [".hlsl"]       = AssetType.Shader,
        [".cginc"]      = AssetType.Shader,
        [".anim"]       = AssetType.Animation,
        [".controller"] = AssetType.AnimatorController,
        [".asset"]      = AssetType.ScriptableObject,
        [".asmdef"]     = AssetType.AssemblyDefinition,
        [".meta"]       = AssetType.Meta,
        [".png"]        = AssetType.Texture,
        [".jpg"]        = AssetType.Texture,
        [".jpeg"]       = AssetType.Texture,
        [".tga"]        = AssetType.Texture,
        [".psd"]        = AssetType.Texture,
        [".exr"]        = AssetType.Texture,
        [".hdr"]        = AssetType.Texture,
        [".wav"]        = AssetType.Audio,
        [".mp3"]        = AssetType.Audio,
        [".ogg"]        = AssetType.Audio,
        [".aif"]        = AssetType.Audio,
        [".aiff"]       = AssetType.Audio,
        [".ttf"]        = AssetType.Font,
        [".otf"]        = AssetType.Font,
    };

    public static AssetType Detect(string path)
    {
        var ext = Path.GetExtension(path);
        return ExtensionMap.TryGetValue(ext, out var type) ? type : AssetType.Other;
    }
}
