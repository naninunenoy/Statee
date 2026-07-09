namespace RogueGame.Logic;

/// <summary>1フロア分の状態。地形(不変)と Entity(可変)を持ち、離脱後も保持される。</summary>
public sealed class Floor
{
    private readonly List<Enemy> enemies;

    public Floor(DungeonMap map, IEnumerable<Enemy> enemies)
    {
        Map = map;
        this.enemies = [.. enemies];
    }

    /// <summary>フロアの地形。</summary>
    public DungeonMap Map { get; }

    /// <summary>生存している敵。</summary>
    public IReadOnlyList<Enemy> Enemies => enemies;

    /// <summary>倒された敵を取り除く。</summary>
    internal void RemoveEnemy(Enemy enemy) => enemies.Remove(enemy);
}
