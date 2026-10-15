namespace MouseOverAssist;

public static class RulePolicy
{
    public static bool Applies(bool enabled, uint job, uint currentJob) => enabled && (job == 0 || job == currentJob);

    // Action picker checkbox semantics:
    // current + shared => current job-specific OR shared/role usable by current job
    // current only     => current job-specific only
    // shared only      => shared/role only
    // neither          => all valid actions
    public static bool ActionPickerApplies(
        bool isSharedOrRole,
        bool supportsCurrentJob,
        bool currentJobOnly,
        bool includeSharedRole)
    {
        if (currentJobOnly && includeSharedRole)
            return supportsCurrentJob;
        if (currentJobOnly)
            return !isSharedOrRole && supportsCurrentJob;
        if (includeSharedRole)
            return isSharedOrRole;
        return true;
    }

    // 「共通・ロール技」一覧は戦闘ジョブ向けだけを対象にする。
    // DoH/DoL にも IsRoleAction=true のアクションがあるため、
    // battleJobCount == 0 のものは共通・ロール技として扱わない。
    public static bool ActionSearchMatches(
        string query,
        string actionName,
        string actionId,
        string level,
        string[] jobNames)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        if (actionName?.Contains(query, System.StringComparison.OrdinalIgnoreCase) == true) return true;
        if (actionId?.Contains(query, System.StringComparison.OrdinalIgnoreCase) == true) return true;
        if (level?.Contains(query, System.StringComparison.OrdinalIgnoreCase) == true) return true;

        if (jobNames != null)
        {
            foreach (var jobName in jobNames)
                if (jobName?.Contains(query, System.StringComparison.OrdinalIgnoreCase) == true)
                    return true;
        }

        return false;
    }

    public static bool IsBattleSharedOrRole(
        bool isRoleAction,
        int battleJobCount,
        bool hasExplicitClassJob)
    {
        if (battleJobCount <= 0)
            return false;
        if (isRoleAction)
            return true;
        return !hasExplicitClassJob && battleJobCount >= 3;
    }
}
