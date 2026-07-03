namespace SuikaGame.Logic;

/// <summary>場に出ているフルーツのスナップショット(位置は Godot 物理が所有するため持たない)。</summary>
public readonly record struct FruitSnapshot(FruitId Id, FruitKind Kind);
