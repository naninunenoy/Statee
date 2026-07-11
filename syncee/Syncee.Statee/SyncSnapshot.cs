namespace Syncee.Statee;

/// <summary>
/// 同期状態の観測用スナップショット(D-050)。接続クライアント数・確定ログ長・
/// 最終確定コマンドを AI/人間が State として読めるようにする。
/// </summary>
public sealed record SyncSnapshot(int ConnectedClients, long CommittedCount, string? LastCommand);
