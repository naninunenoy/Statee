using R3;

namespace SuikaGame.Logic;

/// <summary>
/// 画面遷移の状態機械。タイトル → プレイの遷移だけを規則として持ち、
/// 実際のシーン差し替え・プロセス終了は Godot 層が Phase を購読して行う。
/// </summary>
public sealed class GameFlow : IDisposable
{
    /// <summary>現在のフェーズ。初期状態はタイトル。</summary>
    public ReadOnlyReactiveProperty<GamePhase> Phase => throw new NotImplementedException();

    /// <summary>ゲームを開始する。タイトルにいるときだけ Playing へ遷移し true を返す。</summary>
    public bool StartGame() => throw new NotImplementedException();

    public void Dispose() => throw new NotImplementedException();
}
