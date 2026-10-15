using System;
using System.Collections.Generic;
using System.Linq;

namespace MouseOverAssist;

public enum RuleDiagnosticSeverity
{
    Warning,
    Error
}

public sealed class RuleDiagnostic
{
    public RuleDiagnostic(RuleDiagnosticSeverity severity, string message)
    {
        Severity = severity;
        Message = message;
    }

    public RuleDiagnosticSeverity Severity { get; }
    public string Message { get; }
}

/// <summary>
/// Action Stack の設定だけから安全に判定できるミスを検出します。
/// ゲームデータに依存する ID 存在確認は UI 側で追加します。
/// </summary>
public static class RuleDiagnostics
{
    public static List<RuleDiagnostic> GetForStack(IReadOnlyList<Configuration.ActionStack> stacks, int index)
    {
        var result = new List<RuleDiagnostic>();
        if (index < 0 || index >= stacks.Count) return result;

        var stack = stacks[index];
        if (string.IsNullOrWhiteSpace(stack.Name))
            result.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Warning, "ルール名が空です。動作には影響しませんが、後で見分けにくくなります。"));

        if (stack.Actions.Count == 0)
            result.Add(new RuleDiagnostic(
                stack.Enabled ? RuleDiagnosticSeverity.Error : RuleDiagnosticSeverity.Warning,
                stack.Enabled
                    ? "反応するアクションがありません。このルールは実行されません。"
                    : "無効中のルールですが、反応するアクションが未設定です。"));

        if (stack.Items.Count == 0)
            result.Add(new RuleDiagnostic(
                stack.Enabled ? RuleDiagnosticSeverity.Error : RuleDiagnosticSeverity.Warning,
                stack.Enabled
                    ? "対象 / 使用するアクションがありません。このルールは成功しません。"
                    : "無効中のルールですが、対象 / 使用するアクションが未設定です。"));

        if (stack.Actions.GroupBy(ActionKey).Any(x => x.Count() > 1))
            result.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Warning, "同じ『反応するアクション』が重複しています。"));

        if (stack.Items.GroupBy(x => (x.ID, x.TargetID)).Any(x => x.Count() > 1))
            result.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Warning, "同じ対象 / 使用アクションの候補が重複しています。"));

        if (stack.ExcludedActions.GroupBy(x => x).Any(x => x.Count() > 1))
            result.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Warning, "同じ除外アクションが重複しています。"));

        if (stack.Actions.Count > 1 && stack.Actions.Any(x => x.ID == 0))
            result.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Warning, "『すべてのスキル』があるため、同じルール内の他の反応アクションは不要です。"));

        if ((stack.ModifierKeys & ~0xFu) != 0)
            result.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Warning, "修飾キー設定に未対応の値が含まれています。Shift / Ctrl / Alt / 完全一致を設定し直してください。"));

        if (!string.IsNullOrWhiteSpace(stack.Name) &&
            stacks.Where((_, i) => i != index).Any(x => string.Equals(x.Name?.Trim(), stack.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            result.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Warning, "同じ名前のルールがあります。動作には影響しませんが、見分けやすい名前を付けるのがおすすめです。"));

        if (stack.Enabled && stack.Actions.Count > 0)
        {
            for (var i = 0; i < index; i++)
            {
                var earlier = stacks[i];
                if (!earlier.Enabled || earlier.Actions.Count == 0) continue;
                if (!JobScopeCovers(earlier.JobId, stack.JobId)) continue;
                if (!ModifierScopeCovers(earlier.ModifierKeys, stack.ModifierKeys)) continue;
                if (!TriggerSetCovers(earlier.Actions, stack.Actions)) continue;

                var earlierName = string.IsNullOrWhiteSpace(earlier.Name) ? $"ルール {i + 1}" : earlier.Name;
                result.Add(new RuleDiagnostic(
                    RuleDiagnosticSeverity.Warning,
                    $"上の『{earlierName}』が同じ条件を先に受け取るため、このルールまで判定が届かない可能性があります。"));
                break;
            }
        }

        return result;
    }

    public static (int Errors, int Warnings) CountAll(IReadOnlyList<Configuration.ActionStack> stacks)
    {
        var errors = 0;
        var warnings = 0;
        for (var i = 0; i < stacks.Count; i++)
        {
            foreach (var diagnostic in GetForStack(stacks, i))
            {
                if (diagnostic.Severity == RuleDiagnosticSeverity.Error) errors++;
                else warnings++;
            }
        }
        return (errors, warnings);
    }

    private static bool JobScopeCovers(uint earlierJob, uint laterJob)
        => earlierJob == 0 || earlierJob == laterJob;

    private static bool ModifierScopeCovers(uint earlier, uint later)
    {
        var earlierRequired = earlier & 0x7u;
        var laterRequired = later & 0x7u;
        var earlierExact = (earlier & 0x8u) != 0;
        var laterExact = (later & 0x8u) != 0;

        // 完全一致のルールは、同じ完全一致条件だけを完全に覆います。
        if (earlierExact) return laterExact && earlierRequired == laterRequired;

        // 非完全一致は「指定キーを含めば一致」なので、必要キーが少ないほど範囲が広いです。
        return (earlierRequired & laterRequired) == earlierRequired;
    }

    private static bool TriggerSetCovers(IReadOnlyList<Configuration.Action> earlier, IReadOnlyList<Configuration.Action> later)
    {
        if (earlier.Any(x => x.ID == 0)) return true;
        if (later.Any(x => x.ID == 0)) return false;

        foreach (var action in later)
        {
            if (!earlier.Any(x => ActionCovers(x, action))) return false;
        }
        return true;
    }

    private static bool ActionCovers(Configuration.Action earlier, Configuration.Action later)
    {
        if (earlier.ID == 0) return true;
        if (earlier.ID is 1 or 2) return earlier.ID == later.ID;
        return earlier.ID == later.ID && earlier.UseAdjustedID == later.UseAdjustedID;
    }

    private static string ActionKey(Configuration.Action action)
        => $"{action.ID}:{action.UseAdjustedID}";
}
