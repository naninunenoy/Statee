namespace Declaree;

/// <summary>
/// モーダルの背景。半透明の幕が全面を覆い、背面の UI へのマウス入力を遮断する。
/// 子(ダイアログ本体)は幕の上に親いっぱいで載る(中央寄せしたい場合は子を Center にする)。
/// </summary>
public record Overlay(UiNode Child) : UiNode;
