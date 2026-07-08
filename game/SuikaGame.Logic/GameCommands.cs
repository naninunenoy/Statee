using VitalRouter;

namespace SuikaGame.Logic;

/// <summary>ゲームを開始せよ(タイトル → プレイ)。UI や外部コマンドがこれを発行する。</summary>
public readonly record struct StartGameCommand : ICommand;

/// <summary>ゲームを終了せよ。プロセスの終了は Godot 層が ExitRequests 購読で行う。</summary>
public readonly record struct ExitGameCommand : ICommand;

/// <summary>やり直せ(ポーズ → プレイ再開)。盤面・スコアのリセットは RestartRequests 購読で行う。</summary>
public readonly record struct RestartGameCommand : ICommand;

/// <summary>ポーズを解除して続けよ(ポーズ → プレイ再開。盤面はそのまま)。</summary>
public readonly record struct ResumeGameCommand : ICommand;
