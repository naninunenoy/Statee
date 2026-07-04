namespace Statee.Cli;

/// <summary>
/// 環境変数 STATEE_TRACE にファイルパスが設定されていれば、ワイヤ入出力を追記する(docs/MEMO.md D-021)。
/// 先頭の ~ はユーザープロファイルに展開する。トレースはベストエフォートで、失敗しても動作に影響させない。
/// </summary>
internal static class WireTrace
{
    public static void Write(string direction, string line)
    {
        var path = Environment.GetEnvironmentVariable("STATEE_TRACE");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (path.StartsWith('~'))
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];
        }

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(
                path,
                $"{DateTimeOffset.Now:yyyy-MM-dd'T'HH:mm:ss.fffzzz} {direction} {line}{Environment.NewLine}"
            );
        }
        catch (IOException)
        {
            // トレース先に書けなくても本来の入出力は継続する
        }
    }
}
