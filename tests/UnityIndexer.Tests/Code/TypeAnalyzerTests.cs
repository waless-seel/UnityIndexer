using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityIndexer.Analyzer.Code;
using CoreTypeKind = UnityIndexer.Core.Models.TypeKind;

namespace UnityIndexer.Tests.Code;

public class TypeAnalyzerTests
{
    private const string ProjectRoot = "/project";
    private const string ScriptRelPath = "Assets/Scripts/PlayerController.cs";
    private const string TestGuid = "aabb1122334455667788";

    /// <summary>MonoBehaviour を継承したクラスが正しく検出される</summary>
    [Fact]
    public void AnalyzeCompilation_MonoBehaviourClass_Detected()
    {
        // Arrange: MonoBehaviour を定義してから継承するクラスを作成
        var src = """
            namespace UnityEngine {
                public class MonoBehaviour { }
                public class SerializeFieldAttribute : System.Attribute { }
            }
            namespace MyGame {
                public class PlayerController : UnityEngine.MonoBehaviour {
                    public float Speed;
                    [UnityEngine.SerializeField] private int _hp;
                }
            }
            """;

        var compilation = CreateCompilation(src, ProjectRoot + "/" + ScriptRelPath);
        var pathToGuid = new Dictionary<string, string> { [ScriptRelPath] = TestGuid };

        // Act
        var results = TypeAnalyzer.AnalyzeCompilation(compilation, pathToGuid, ProjectRoot);

        // Assert
        var playerController = results.FirstOrDefault(r => r.ClassName == "PlayerController");
        Assert.NotNull(playerController);
        Assert.Equal(TestGuid, playerController.AssetGuid);
        Assert.True(playerController.IsMonoBehaviour);
        Assert.False(playerController.IsScriptableObject);
        Assert.Equal("MyGame", playerController.Namespace);
        Assert.Equal(CoreTypeKind.Class, playerController.Kind);

        // Speed は public なのでシリアライズ対象
        var speedField = playerController.SerializedFields.FirstOrDefault(f => f.Name == "Speed");
        Assert.NotNull(speedField);
        Assert.True(speedField.IsPublic);

        // _hp は [SerializeField] 付きなのでシリアライズ対象
        var hpField = playerController.SerializedFields.FirstOrDefault(f => f.Name == "_hp");
        Assert.NotNull(hpField);
        Assert.False(hpField.IsPublic);
        Assert.True(hpField.HasSerializeFieldAttribute);
    }

    /// <summary>ScriptableObject を継承したクラスが正しく検出される</summary>
    [Fact]
    public void AnalyzeCompilation_ScriptableObjectClass_Detected()
    {
        var src = """
            namespace UnityEngine {
                public class ScriptableObject { }
            }
            namespace MyGame {
                public class GameSettings : UnityEngine.ScriptableObject {
                    public int MaxPlayers;
                }
            }
            """;

        var soRelPath = "Assets/Scripts/GameSettings.cs";
        var soGuid = "ccdd99887766554433";
        var compilation = CreateCompilation(src, ProjectRoot + "/" + soRelPath);
        var pathToGuid = new Dictionary<string, string> { [soRelPath] = soGuid };

        var results = TypeAnalyzer.AnalyzeCompilation(compilation, pathToGuid, ProjectRoot);

        var gameSettings = results.FirstOrDefault(r => r.ClassName == "GameSettings");
        Assert.NotNull(gameSettings);
        Assert.Equal(soGuid, gameSettings.AssetGuid);
        Assert.True(gameSettings.IsScriptableObject);
        Assert.False(gameSettings.IsMonoBehaviour);
    }

    /// <summary>通常クラスは MonoBehaviour でも ScriptableObject でもない</summary>
    [Fact]
    public void AnalyzeCompilation_NormalClass_NotMonoBehaviourNorScriptableObject()
    {
        var src = """
            namespace MyGame {
                public class UtilityHelper {
                    public static int Add(int a, int b) => a + b;
                }
            }
            """;

        var utilRelPath = "Assets/Scripts/UtilityHelper.cs";
        var utilGuid = "eeff11223344556677";
        var compilation = CreateCompilation(src, ProjectRoot + "/" + utilRelPath);
        var pathToGuid = new Dictionary<string, string> { [utilRelPath] = utilGuid };

        var results = TypeAnalyzer.AnalyzeCompilation(compilation, pathToGuid, ProjectRoot);

        var helper = results.FirstOrDefault(r => r.ClassName == "UtilityHelper");
        Assert.NotNull(helper);
        Assert.False(helper.IsMonoBehaviour);
        Assert.False(helper.IsScriptableObject);
        Assert.Empty(helper.SerializedFields);
    }

    // -------------------------------------------------------
    // ヘルパー
    // -------------------------------------------------------

    private static CSharpCompilation CreateCompilation(string source, string filePath)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: filePath);
        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees: [tree],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
