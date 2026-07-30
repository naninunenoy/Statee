namespace TpsGame.Logic;

/// <summary>1 Tick で起きた出来事。Godot 層が演出へ翻訳する。</summary>
public readonly record struct GameEvent(string Name, string Detail);
