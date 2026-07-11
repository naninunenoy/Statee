namespace ShootingGame.Logic;

/// <summary>イベントログの1件。Sequence は起動からの通し番号(リングバッファで消えても不変)。</summary>
public readonly record struct EventLogEntry(int Sequence, int Tick, string Name, string Detail);
