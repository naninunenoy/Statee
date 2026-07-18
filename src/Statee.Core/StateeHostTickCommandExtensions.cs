namespace Statee.Core;

/// <summary>
/// リアルタイムゲームの「freeze + tick」プレイ経路(D-048/D-049)の定型を登録する。
/// ゲームごとに違うのは「引数 → 入力型の写像」と「1 tick 進める処理」だけなので、
/// frames のクランプ・ループ・TimeControl.OnFrame 呼び出しをここに吸収する(D-072)。
/// </summary>
public static class StateeHostTickCommandExtensions
{
    /// <summary>
    /// 「入力を指定して N tick 進める」コマンドをメインスレッドコマンドとして登録する。
    /// </summary>
    /// <param name="timeControl">tick ごとに OnFrame を通知する対象。</param>
    /// <param name="parseInput">コマンド引数から 1 tick 分の入力を組み立てる(呼び出しごとに 1 回)。</param>
    /// <param name="step">入力で 1 tick 進める処理。frames 回呼ばれる。</param>
    /// <param name="result">進めた後の応答ペイロードを作る(TOON 化できる形を返す)。</param>
    /// <param name="maxFramesPerCall">1 回で進められる上限(暴走防止)。</param>
    /// <param name="commandName">コマンド名(既定 "tick")。</param>
    public static void RegisterTickCommand<TInput>(
        this StateeHost host,
        TimeControl timeControl,
        Func<CommandArgs, TInput> parseInput,
        Action<TInput> step,
        Func<object> result,
        int maxFramesPerCall = 3600,
        string commandName = "tick"
    )
    {
        throw new NotImplementedException();
    }
}
