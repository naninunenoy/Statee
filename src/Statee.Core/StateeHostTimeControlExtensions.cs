using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Statee.Core;

/// <summary>
/// TimeControl を標準コマンド(freeze / unfreeze / step / wait)として StateeHost に登録する。
/// step / wait は指定した進行が完了してから応答を返す(AI の決定論的操作の要)。
/// </summary>
public static class StateeHostTimeControlExtensions
{
    private static readonly TimeSpan DefaultStepTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(10);

    public static void RegisterTimeControl(
        this StateeHost host,
        TimeControl timeControl,
        TimeSpan? stepTimeout = null,
        TimeSpan? waitTimeout = null
    )
    {
        host.RegisterCommand(
            "freeze",
            _ =>
            {
                timeControl.Freeze();
                return new { timeControl.IsFrozen };
            }
        );
        host.RegisterCommand(
            "unfreeze",
            _ =>
            {
                timeControl.Unfreeze();
                return new { timeControl.IsFrozen };
            }
        );
        host.RegisterCommand(
            "step",
            args => HandleStep(timeControl, args, stepTimeout ?? DefaultStepTimeout)
        );
        host.RegisterCommand(
            "wait",
            args => HandleWait(host, timeControl, args, waitTimeout ?? DefaultWaitTimeout)
        );
    }

    private static object HandleStep(TimeControl timeControl, CommandArgs args, TimeSpan timeout)
    {
        var frames = args.GetInt("frames", 1);
        timeControl.Step(frames);
        if (!timeControl.WaitForStep(timeout))
        {
            throw new TimeoutException(
                $"step が {timeout.TotalSeconds} 秒以内に完了しない (ゲームループが動いているか確認する)"
            );
        }

        return new { timeControl.IsFrozen, Frames = frames };
    }

    /// <summary>
    /// State のフィールドが条件を満たすまで進めて待つ(GUIDELINE.md §7-1 の条件待機)。
    /// 凍結中は 1 フレームずつ step し(決定論)、実行中はフレーム進行のたびに再評価する。
    /// </summary>
    private static object HandleWait(
        StateeHost host,
        TimeControl timeControl,
        CommandArgs args,
        TimeSpan defaultTimeout
    )
    {
        var path = args.GetString("path") ?? throw new ArgumentException("path 引数が必要");
        var field = args.GetString("field") ?? throw new ArgumentException("field 引数が必要");
        var op = args.GetString("op") ?? "eq";
        var expected = args.GetString("value") ?? throw new ArgumentException("value 引数が必要");
        var timeout = TimeSpan.FromMilliseconds(
            args.GetInt("timeoutMs", (int)defaultTimeout.TotalMilliseconds)
        );

        var deadline = Stopwatch.StartNew();
        var startFrame = timeControl.FrameCount;
        while (true)
        {
            var current = ReadField(host.CaptureState(path), path, field);
            if (Satisfies(current, op, expected))
            {
                return new
                {
                    Satisfied = true,
                    Frames = timeControl.FrameCount - startFrame,
                    Value = Convert.ToString(current, CultureInfo.InvariantCulture),
                };
            }

            var remaining = timeout - deadline.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    $"wait がタイムアウト: {path}.{field} {op} {expected} (現在値: {current})"
                );
            }

            var observed = timeControl.FrameCount;
            if (timeControl.IsFrozen)
            {
                timeControl.Step(1);
                if (!timeControl.WaitForStep(remaining))
                {
                    throw new TimeoutException(
                        $"wait がタイムアウト: {path}.{field} {op} {expected} (現在値: {current}。"
                            + "step が進まない場合はゲームループが動いているか確認する)"
                    );
                }
            }
            else if (!timeControl.WaitForNextFrame(observed, remaining))
            {
                throw new TimeoutException(
                    $"wait がタイムアウト: {path}.{field} {op} {expected} (現在値: {current})"
                );
            }
        }
    }

    private static object? ReadField(object state, string path, string field)
    {
        var property = state
            .GetType()
            .GetProperty(
                field,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
            );
        if (property is null)
        {
            throw new ArgumentException($"State '{path}' にフィールド '{field}' がない");
        }

        return property.GetValue(state);
    }

    private static bool Satisfies(object? current, string op, string expected)
    {
        // 数値として比較できるなら数値で、できなければ文字列(順序比較は数値のみ)
        var currentText = Convert.ToString(current, CultureInfo.InvariantCulture) ?? "";
        var currentIsNumber = double.TryParse(
            currentText,
            CultureInfo.InvariantCulture,
            out var currentNumber
        );
        var expectedIsNumber = double.TryParse(
            expected,
            CultureInfo.InvariantCulture,
            out var expectedNumber
        );
        var numeric = currentIsNumber && expectedIsNumber;
        return op switch
        {
            "eq" when numeric => currentNumber == expectedNumber,
            "eq" => string.Equals(currentText, expected, StringComparison.OrdinalIgnoreCase),
            "ne" when numeric => currentNumber != expectedNumber,
            "ne" => !string.Equals(currentText, expected, StringComparison.OrdinalIgnoreCase),
            "gt" when numeric => currentNumber > expectedNumber,
            "ge" when numeric => currentNumber >= expectedNumber,
            "lt" when numeric => currentNumber < expectedNumber,
            "le" when numeric => currentNumber <= expectedNumber,
            "gt" or "ge" or "lt" or "le" => throw new ArgumentException(
                $"演算子 '{op}' は数値フィールドにのみ使える (現在値: {currentText})"
            ),
            _ => throw new ArgumentException($"未知の演算子: {op} (eq/ne/gt/ge/lt/le)"),
        };
    }
}
