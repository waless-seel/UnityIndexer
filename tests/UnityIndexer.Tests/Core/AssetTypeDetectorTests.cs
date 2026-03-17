using UnityIndexer.Core.Models;
using UnityIndexer.Core.Utilities;

namespace UnityIndexer.Tests.Core;

public class AssetTypeDetectorTests
{
    [Theory]
    [InlineData("Player.cs",            AssetType.Script)]
    [InlineData("Main.unity",           AssetType.Scene)]
    [InlineData("Enemy.prefab",         AssetType.Prefab)]
    [InlineData("icon.png",             AssetType.Texture)]
    [InlineData("icon.PNG",             AssetType.Texture)]
    [InlineData("sprite.jpg",           AssetType.Texture)]
    [InlineData("sprite.TGA",           AssetType.Texture)]
    [InlineData("bgm.wav",              AssetType.Audio)]
    [InlineData("se.mp3",              AssetType.Audio)]
    [InlineData("bgm.ogg",             AssetType.Audio)]
    [InlineData("Game.asmdef",         AssetType.AssemblyDefinition)]
    [InlineData("Lit.shader",          AssetType.Shader)]
    [InlineData("Common.hlsl",         AssetType.Shader)]
    [InlineData("Utils.cginc",         AssetType.Shader)]
    [InlineData("Attack.anim",         AssetType.Animation)]
    [InlineData("Player.controller",   AssetType.AnimatorController)]
    [InlineData("Settings.asset",      AssetType.ScriptableObject)]
    [InlineData("icon.meta",           AssetType.Meta)]
    [InlineData("font.ttf",            AssetType.Font)]
    [InlineData("font.otf",            AssetType.Font)]
    [InlineData("Material.mat",        AssetType.Material)]
    [InlineData("unknown.xyz",         AssetType.Other)]
    [InlineData("no_extension",        AssetType.Other)]
    public void Detect_Extension_ReturnsExpectedType(string filename, AssetType expected)
    {
        var result = AssetTypeDetector.Detect(filename);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Detect_FullPath_UsesExtension()
    {
        var result = AssetTypeDetector.Detect("Assets/Scripts/Player.cs");
        Assert.Equal(AssetType.Script, result);
    }

    [Fact]
    public void Detect_UppercaseExtension_IsCaseInsensitive()
    {
        Assert.Equal(AssetType.Script, AssetTypeDetector.Detect("Player.CS"));
        Assert.Equal(AssetType.Prefab, AssetTypeDetector.Detect("Enemy.PREFAB"));
        Assert.Equal(AssetType.Scene,  AssetTypeDetector.Detect("Main.UNITY"));
    }
}
