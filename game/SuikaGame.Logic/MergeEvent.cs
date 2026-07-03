namespace SuikaGame.Logic;

/// <summary>
/// 合体の発生通知。Godot 層はこれを購読して物理ボディの削除・生成を行う(docs/MEMO.md D-024)。
/// スイカ同士の消滅では <see cref="Created"/> / <see cref="CreatedKind"/> が null になる。
/// </summary>
public readonly record struct MergeEvent(
    FruitId RemovedA,
    FruitId RemovedB,
    FruitId? Created,
    FruitKind? CreatedKind
);
