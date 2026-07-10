using System;
using Godot;

namespace Statee.Godot;

/// <summary>
/// キー入力 → アクションの配線1件(D-039)。この表を _UnhandledInput の処理
/// (<see cref="KeyBindingTable.TryHandle"/>)と game/input State
/// (<see cref="KeyBindingTable.CreateInputStateProvider"/>)の両方の情報源にすることで、
/// 実装と公開情報が乖離しない。
/// </summary>
/// <param name="Key">物理キー。</param>
/// <param name="Publishes">発行するアクションの名前(State に公開される作用)。</param>
/// <param name="Explain">人間・エージェント向けの説明。</param>
/// <param name="Publish">実際の作用。</param>
/// <param name="ActiveIn">有効な場面の名前(State に公開)。場面の概念が無いゲームは null。</param>
/// <param name="IsActive">現在有効かの判定。null なら常に有効。</param>
public sealed record KeyBinding(
    Key Key,
    string Publishes,
    string Explain,
    Action Publish,
    string? ActiveIn = null,
    Func<bool>? IsActive = null
);
