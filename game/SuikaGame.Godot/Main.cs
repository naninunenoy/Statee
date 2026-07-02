using System.Runtime.InteropServices;
using Godot;

namespace SuikaGame;

/// <summary>
/// フェーズ0 検証用エントリポイント。
/// .NET 10 ランタイム上で動作していることと C# 14 の言語機能を確認し、即終了する。
/// </summary>
public partial class Main : Node
{
    /// <summary>C# 14 の field キーワードが通ることの確認用。</summary>
    private int Answer => field == 0 ? 42 : field;

    public override void _Ready()
    {
        GD.Print($"FrameworkDescription: {RuntimeInformation.FrameworkDescription}");
        GD.Print($"Environment.Version: {System.Environment.Version}");
        GD.Print($"CSharp14FieldKeyword: {Answer}");
        GetTree().Quit();
    }
}
