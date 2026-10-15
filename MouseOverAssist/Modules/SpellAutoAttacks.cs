using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace MouseOverAssist.Modules;

public class SpellAutoAttacks : PluginModule
{
    public override bool ShouldEnable => MouseOverAssist.Config.EnableSpellAutoAttacks && !MouseOverAssist.Config.EnableSpellAutoAttacksOutOfCombat;

    protected override bool Validate() => Game.spellAutoAttackPatch.IsValid;
    protected override void Enable() => DalamudApi.Framework.Update += Update;
    protected override void Disable() => DalamudApi.Framework.Update -= Update;

    private static void Update(IFramework framework)
    {
        if (MouseOverAssist.Config.EnableSpellAutoAttacks)
        {
            if (Game.spellAutoAttackPatch.IsEnabled != DalamudApi.Condition[ConditionFlag.InCombat])
                Game.spellAutoAttackPatch.Toggle();
        }
        else
        {
            Game.spellAutoAttackPatch.Disable();
        }
    }
}