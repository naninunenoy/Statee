using System.Collections.Generic;
using Statee.Core;

namespace RogueGame;

/// <summary>
/// ダンジョンの State 公開(D-022)。CaptureState はソケットスレッドで走るため(D-019)、
/// メインスレッドがターンごとに差し替える不変スナップショットを読むだけにする。
/// FoW(視界)は描画層の演出であり、検証用の State は全情報を公開する。
/// </summary>
[StateeState("game/rogue")]
public partial class RogueState
{
    /// <summary>フロア上の敵1体。Id はフレームを跨いで安定(GUIDELINE 3.4)。</summary>
    public sealed record EnemyEntry(int Id, int X, int Y, int Hp);

    /// <summary>フロア上のアイテム1つ。</summary>
    public sealed record ItemEntry(int Id, string Kind, int X, int Y);

    private sealed record Snapshot(
        int Floor,
        int PlayerX,
        int PlayerY,
        int Hp,
        int Attack,
        IReadOnlyList<string> Inventory,
        bool HasGem,
        bool IsCleared,
        bool IsGameOver,
        IReadOnlyList<string> Map,
        IReadOnlyList<EnemyEntry> Enemies,
        IReadOnlyList<ItemEntry> Items
    );

    private volatile Snapshot _current = new(0, 0, 0, 0, 0, [], false, false, false, [], [], []);

    [StateeField]
    public int Floor => _current.Floor;

    [StateeField]
    public int PlayerX => _current.PlayerX;

    [StateeField]
    public int PlayerY => _current.PlayerY;

    [StateeField]
    public int Hp => _current.Hp;

    [StateeField]
    public int Attack => _current.Attack;

    [StateeField]
    public IReadOnlyList<string> Inventory => _current.Inventory;

    [StateeField]
    public bool HasGem => _current.HasGem;

    [StateeField]
    public bool IsCleared => _current.IsCleared;

    [StateeField]
    public bool IsGameOver => _current.IsGameOver;

    /// <summary>地形を1行1文字列で公開する(# = 壁, . = 床, &lt; = 上り階段, &gt; = 下り階段)。</summary>
    [StateeField]
    public IReadOnlyList<string> Map => _current.Map;

    [StateeField]
    public IReadOnlyList<EnemyEntry> Enemies => _current.Enemies;

    [StateeField]
    public IReadOnlyList<ItemEntry> Items => _current.Items;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(
        int floor,
        int playerX,
        int playerY,
        int hp,
        int attack,
        IReadOnlyList<string> inventory,
        bool hasGem,
        bool isCleared,
        bool isGameOver,
        IReadOnlyList<string> map,
        IReadOnlyList<EnemyEntry> enemies,
        IReadOnlyList<ItemEntry> items
    )
    {
        _current = new Snapshot(
            floor,
            playerX,
            playerY,
            hp,
            attack,
            inventory,
            hasGem,
            isCleared,
            isGameOver,
            map,
            enemies,
            items
        );
    }
}
