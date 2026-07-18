namespace MessBreak.Logic;

/// <summary>ステージのテキスト形式(<see cref="StageMap"/>)のパース失敗。</summary>
public sealed class StageMapException(string message) : Exception(message);
