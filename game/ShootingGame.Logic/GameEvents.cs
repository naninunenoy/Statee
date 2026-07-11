using VitalRouter;

namespace ShootingGame.Logic;

// ゲーム内イベント(D-048)。多対多の Publish/Subscribe で流し、
// EventLog(interceptor)が全件を記録して State 公開の源になる。
// 命名は過去分詞(GUIDELINE 5)。

/// <summary>敵が出現した。</summary>
public readonly record struct EnemySpawned(int EnemyId, EnemyKind Kind) : ICommand;

/// <summary>敵が撃破された。</summary>
public readonly record struct EnemyDestroyed(int EnemyId, EnemyKind Kind) : ICommand;

/// <summary>自機が被弾した。</summary>
public readonly record struct PlayerHit(int LivesRemaining) : ICommand;

/// <summary>残機が尽きてゲームオーバーになった。</summary>
public readonly record struct GameEnded(int Score) : ICommand;

/// <summary>ウェーブが始まった(1 始まり)。</summary>
public readonly record struct WaveStarted(int Wave) : ICommand;

/// <summary>ウェーブの敵が全ていなくなった(撃破または画面外へ退場)。</summary>
public readonly record struct WaveCleared(int Wave) : ICommand;
