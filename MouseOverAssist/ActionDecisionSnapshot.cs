using System;

namespace MouseOverAssist;

public sealed class ActionDecisionSnapshot
{
    public static readonly ActionDecisionSnapshot Empty = new()
    {
        TimestampUtc = DateTime.MinValue,
        Result = "まだ判定履歴がありません。アクションを押すとここに表示されます。",
        Steps = []
    };

    public DateTime TimestampUtc { get; init; }
    public uint InputActionID { get; init; }
    public uint AdjustedActionID { get; init; }
    public uint OutputActionID { get; init; }
    public string StackName { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string[] Steps { get; init; } = [];
}
