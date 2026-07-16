namespace TodoApp.Logic;

/// <summary>
/// タスク1件。Id はセッション内で安定した一意 ID(GUIDELINE 3.4)。
/// </summary>
public sealed record TodoItem(int Id, string Title, bool Completed);
