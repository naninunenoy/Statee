namespace Statee.Cli;

/// <summary>StateeClient の失敗(エラー応答・接続失敗・プロトコル不整合)。</summary>
public sealed class StateeClientException(string message, bool isErrorResponse = false)
    : Exception(message)
{
    /// <summary>ターゲットがエラー応答を返した(true)か、接続・プロトコルの問題(false)か。</summary>
    public bool IsErrorResponse => isErrorResponse;
}
