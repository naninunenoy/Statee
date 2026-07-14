namespace Declaree;

/// <summary>
/// 差分適用(reconcile)の判定。全破棄・全再構築(D-035)は Slider のドラッグを
/// 破壊し LineEdit の入力を消すことが TodoApp で証明されたため、
/// 「その場でプロパティ更新できるか」をノード単位で判定する(D-061)。
/// 子の個数・構造の照合は呼び出し側(レンダラ)の責任。
/// </summary>
public static class UiDiff
{
    /// <summary>
    /// 描画済み Control を破棄せずに <paramref name="next"/> の内容へ更新できるか。
    /// 型・Name・イベント ID(クロージャに固定されるもの)がすべて一致するときだけ true。
    /// </summary>
    public static bool CanPatch(UiNode current, UiNode next) => false;
}
