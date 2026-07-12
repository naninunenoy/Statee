namespace Declaree;

/// <summary>
/// 子を同じ領域に重ねるコンテナ(後の子が手前)。モーダル(<see cref="Overlay"/>)を
/// 通常 UI の上に被せる用途で使う。各子は親いっぱいに広がる。
/// </summary>
public record Stack(params UiNode[] Children) : UiNode;
