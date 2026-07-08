using Arch.Core;
using R3;

namespace SuikaGame.Logic;

/// <summary>
/// スイカゲームの規則エンジン(docs/adr/D-024.md)。物理は持たず、Godot 層からの
/// 接触・溢れ報告と Tick(D-023: 外部駆動・壁時計禁止)で状態が進む。
/// フルーツは Arch の Entity として管理する。
/// </summary>
public sealed class SuikaLogic : IDisposable
{
    /// <summary>場に出ているフルーツ。</summary>
    private struct FruitComponent
    {
        public FruitId Id;
        public FruitKind Kind;
    }

    /// <summary>ゲームオーバーラインを超えているフルーツに付く。経過秒を蓄積する。</summary>
    private struct OverflowComponent
    {
        public double Seconds;
    }

    private static readonly QueryDescription FruitQuery =
        new QueryDescription().WithAll<FruitComponent>();
    private static readonly QueryDescription OverflowQuery = new QueryDescription().WithAll<
        FruitComponent,
        OverflowComponent
    >();

    private readonly World _world = World.Create();
    private readonly Dictionary<FruitId, Entity> _entities = [];
    private readonly Random _random;
    private readonly SuikaConfig _config;
    private readonly ReactiveProperty<int> _score = new(0);
    private readonly ReactiveProperty<bool> _isGameOver = new(false);
    private readonly Subject<MergeEvent> _merges = new();
    private int _nextId;
    private FruitKind _next;

    public SuikaLogic(int seed, SuikaConfig? config = null)
    {
        _config = config ?? new SuikaConfig();
        _random = new Random(seed);
        _next = Draw();
    }

    /// <summary>現在のスコア。</summary>
    public ReadOnlyReactiveProperty<int> Score => _score;

    /// <summary>ゲームオーバーか。true になった後は盤面が凍結する。</summary>
    public ReadOnlyReactiveProperty<bool> IsGameOver => _isGameOver;

    /// <summary>合体の発生通知。Godot 層が物理ボディの削除・生成に使う。</summary>
    public Observable<MergeEvent> Merges => _merges;

    /// <summary>場に出ているフルーツの一覧。</summary>
    public IReadOnlyList<FruitSnapshot> Fruits
    {
        get
        {
            var fruits = new List<FruitSnapshot>(_entities.Count);
            _world.Query(
                in FruitQuery,
                (ref FruitComponent fruit) => fruits.Add(new FruitSnapshot(fruit.Id, fruit.Kind))
            );
            return fruits;
        }
    }

    /// <summary>次に落ちるフルーツの種類(消費しない)。</summary>
    public FruitKind PeekNext() => _next;

    /// <summary>次のフルーツを場に出し、抽選キューを進める。</summary>
    public FruitId SpawnNext()
    {
        var id = Spawn(_next);
        _next = Draw();
        return id;
    }

    /// <summary>種類を指定してフルーツを場に出す(初期配置・リプレイ・テスト用)。</summary>
    public FruitId Spawn(FruitKind kind)
    {
        var id = new FruitId(_nextId++);
        var entity = _world.Create(new FruitComponent { Id = id, Kind = kind });
        _entities[id] = entity;
        return id;
    }

    /// <summary>Godot 層からの衝突報告。同種なら合体する。未知の ID は無視する。</summary>
    public void ReportContact(FruitId a, FruitId b)
    {
        if (
            _isGameOver.Value
            || a == b
            || !_entities.TryGetValue(a, out var entityA)
            || !_entities.TryGetValue(b, out var entityB)
        )
        {
            return;
        }

        var kind = _world.Get<FruitComponent>(entityA).Kind;
        if (kind != _world.Get<FruitComponent>(entityB).Kind)
        {
            return;
        }

        Remove(a, entityA);
        Remove(b, entityB);
        if (kind == FruitKind.Watermelon)
        {
            _score.Value += MergeScore(FruitKind.Watermelon + 1);
            _merges.OnNext(new MergeEvent(a, b, Created: null, CreatedKind: null));
            return;
        }

        var mergedKind = kind + 1;
        var created = Spawn(mergedKind);
        _score.Value += MergeScore(mergedKind);
        _merges.OnNext(new MergeEvent(a, b, created, mergedKind));
    }

    /// <summary>Godot 層からの溢れ報告(ゲームオーバーラインを超えているか)。</summary>
    public void SetOverflowing(FruitId id, bool overflowing)
    {
        if (_isGameOver.Value || !_entities.TryGetValue(id, out var entity))
        {
            return;
        }

        if (overflowing && !_world.Has<OverflowComponent>(entity))
        {
            _world.Add(entity, new OverflowComponent());
        }
        else if (!overflowing && _world.Has<OverflowComponent>(entity))
        {
            _world.Remove<OverflowComponent>(entity);
        }
    }

    /// <summary>時間を進める(D-023)。溢れの猶予時間の計測に使う。</summary>
    public void Tick(double delta)
    {
        if (_isGameOver.Value)
        {
            return;
        }

        var gameOver = false;
        _world.Query(
            in OverflowQuery,
            (ref OverflowComponent overflow) =>
            {
                overflow.Seconds += delta;
                gameOver |= overflow.Seconds >= _config.OverflowGraceSeconds;
            }
        );
        if (gameOver)
        {
            _isGameOver.Value = true;
        }
    }

    /// <summary>盤面をやり直す。フルーツ全消去・スコア 0・ゲームオーバー解除。
    /// 乱数系列と ID 採番は継続する(決定論と追跡可能性の維持)。</summary>
    public void Reset() { }

    public void Dispose()
    {
        _merges.Dispose();
        _score.Dispose();
        _isGameOver.Dispose();
        World.Destroy(_world);
    }

    private FruitKind Draw() => (FruitKind)_random.Next(_config.DroppableKindCount);

    private void Remove(FruitId id, Entity entity)
    {
        _world.Destroy(entity);
        _entities.Remove(id);
    }

    /// <summary>合体スコア(D-024)。結果種の三角数。スイカ消滅は Watermelon+1 扱いで 66。</summary>
    private static int MergeScore(FruitKind resultKind)
    {
        var index = (int)resultKind;
        return index * (index + 1) / 2;
    }
}
