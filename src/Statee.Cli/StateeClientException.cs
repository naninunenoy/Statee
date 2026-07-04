namespace Statee.Cli;

/// <summary>StateeClient の失敗(エラー応答・接続失敗・プロトコル不整合)。</summary>
public sealed class StateeClientException(string message) : Exception(message);
