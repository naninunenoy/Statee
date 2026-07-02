namespace Statee.Core;

/// <summary>コマンドの処理。戻り値のオブジェクトは TOON にエンコードされ応答の payload になる。</summary>
public delegate object? CommandHandler(CommandArgs args);
