using R3;

namespace SuikaGame.Logic;

/// <summary>
/// スイカゲームの規則エンジン(docs/MEMO.md D-024)。物理は持たず、Godot 層からの
/// 接触・溢れ報告と Tick(D-023: 外部駆動・壁時計禁止)で状態が進む。
/// </summary>
public sealed class SuikaLogic : IDisposable
{
    public SuikaLogic(int seed, SuikaConfig? config = null) => throw new NotImplementedException();

    /// <summary>現在のスコア。</summary>
    public ReadOnlyReactiveProperty<int> Score => throw new NotImplementedException();

    /// <summary>ゲームオーバーか。true になった後は盤面が凍結する。</summary>
    public ReadOnlyReactiveProperty<bool> IsGameOver => throw new NotImplementedException();

    /// <summary>合体の発生通知。Godot 層が物理ボディの削除・生成に使う。</summary>
    public Observable<MergeEvent> Merges => throw new NotImplementedException();

    /// <summary>場に出ているフルーツの一覧。</summary>
    public IReadOnlyList<FruitSnapshot> Fruits => throw new NotImplementedException();

    /// <summary>次に落ちるフルーツの種類(消費しない)。</summary>
    public FruitKind PeekNext() => throw new NotImplementedException();

    /// <summary>次のフルーツを場に出し、抽選キューを進める。</summary>
    public FruitId SpawnNext() => throw new NotImplementedException();

    /// <summary>種類を指定してフルーツを場に出す(初期配置・リプレイ・テスト用)。</summary>
    public FruitId Spawn(FruitKind kind) => throw new NotImplementedException();

    /// <summary>Godot 層からの衝突報告。同種なら合体する。未知の ID は無視する。</summary>
    public void ReportContact(FruitId a, FruitId b) => throw new NotImplementedException();

    /// <summary>Godot 層からの溢れ報告(ゲームオーバーラインを超えているか)。</summary>
    public void SetOverflowing(FruitId id, bool overflowing) => throw new NotImplementedException();

    /// <summary>時間を進める(D-023)。溢れの猶予時間の計測に使う。</summary>
    public void Tick(double delta) => throw new NotImplementedException();

    public void Dispose() => throw new NotImplementedException();
}
