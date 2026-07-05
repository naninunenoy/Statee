namespace SuikaGame.Logic;

/// <summary>ゲームルールの調整値(docs/adr/D-024.md)。</summary>
public sealed record SuikaConfig
{
    /// <summary>溢れ状態がこの秒数連続したらゲームオーバー。</summary>
    public double OverflowGraceSeconds { get; init; } = 1.0;

    /// <summary>次に落ちるフルーツの候補になる種類数(先頭からこの数まで)。</summary>
    public int DroppableKindCount { get; init; } = 5;
}
