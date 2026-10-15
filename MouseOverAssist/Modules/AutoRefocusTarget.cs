using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace MouseOverAssist.Modules;

public class AutoRefocusTarget : PluginModule
{
    public override bool ShouldEnable => MouseOverAssist.Config.EnableAutoRefocusTarget;

    protected override bool Validate() => Game.SetFocusTargetByObjectIDHook is { Address: not 0 };
    protected override void Enable() => DalamudApi.Framework.Update += Update;
    protected override void Disable() => DalamudApi.Framework.Update -= Update;

    private static void Update(IFramework framework)
    {
        if (!DalamudApi.Condition[ConditionFlag.BoundByDuty] || DalamudApi.TargetManager.FocusTarget != null) return;
        Game.RefocusTarget();
    }
}