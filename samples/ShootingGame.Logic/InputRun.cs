namespace ShootingGame.Logic;

/// <summary>
/// 同一 InputState が連続した区間(ランレングス圧縮)。入力ログの State 公開と
/// 「入力を指定して N Tick 進める」再生(tick コマンド)の単位に対応する。
/// </summary>
public readonly record struct InputRun(int Ticks, InputState Input);
