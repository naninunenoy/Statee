namespace Statee.Core;

/// <summary>
/// ソケットスレッドから届いたコマンドをメインスレッドで実行するためのディスパッチャ。
/// ゲーム側がメインループ(毎フレーム)から <see cref="Pump"/> を呼び、
/// <see cref="Run"/> は処理の完了までブロックして結果を返す。
/// メインスレッド自身から <see cref="Run"/> を呼ぶとデッドロックするため、
/// ソケットスレッド専用とする。
/// </summary>
public sealed class MainThreadDispatcher(TimeSpan? timeout = null)
{
    /// <summary>処理をキューに積み、メインスレッドでの実行完了を待って結果を返す。</summary>
    public object? Run(Func<object?> action) => throw new NotImplementedException();

    /// <summary>キューに溜まった処理をすべて実行する。メインスレッドから毎フレーム呼ぶ。</summary>
    public void Pump() => throw new NotImplementedException();
}
