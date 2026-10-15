using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;

namespace MouseOverAssist;

public class MouseOverAssist(IDalamudPluginInterface pluginInterface) : DalamudPlugin<Configuration>(pluginInterface), IDalamudPlugin
{
    public static Dictionary<uint, Lumina.Excel.Sheets.Action> actionSheet;
    public static Dictionary<uint, Lumina.Excel.Sheets.Action> mountActionsSheet;

    protected override void Initialize()
    {
        Game.Initialize();
        PronounManager.Initialize();

        actionSheet = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().Where(i => i.ClassJobCategory.RowId > 0 && i.ActionCategory.RowId <= 4 && i.RowId > 8).ToDictionary(i => i.RowId, i => i);
        mountActionsSheet = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().Where(i => i.ActionCategory.RowId == 12).ToDictionary(i => i.RowId, i => i);
        if (actionSheet == null || mountActionsSheet == null)
            throw new ApplicationException("Action sheet failed to load!");
    }

    protected override void ToggleConfig() => PluginUI.IsVisible ^= true;

    [PluginCommand("/moa", HelpMessage = "MouseOverAssist の設定画面を開きます。")]
    private void ToggleConfig(string command, string argument) => ToggleConfig();

    [PluginCommand("/macroqueue", "/mqueue", HelpMessage = "実行中のマクロの先行入力を切り替えます。onで有効、offで無効。")]
    private void OnMacroQueue(string command, string argument)
    {
        if (!Common.IsMacroRunning)
        {
            DalamudApi.PrintError("マクロの実行中に使用してください。");
            return;
        }

        switch (argument)
        {
            case "on":
                Game.queueACCommandPatch.Enable();
                break;
            case "off":
                Game.queueACCommandPatch.Disable();
                break;
            case "":
                if (!Config.EnableMacroQueue) // Bug, users could use two /macroqueue and would expect the second to disable it, but scenario is very unlikely
                    Game.queueACCommandPatch.Toggle();
                break;
            default:
                DalamudApi.PrintError("/macroqueue on または off を指定してください。");
                break;
        }
    }

    protected override void Update()
    {
        if (Config.EnableMacroQueue)
        {
            if (!Game.queueACCommandPatch.IsEnabled && !Common.IsMacroRunning)
                Game.queueACCommandPatch.Enable();
        }
        else
        {
            if (Game.queueACCommandPatch.IsEnabled && !Common.IsMacroRunning)
                Game.queueACCommandPatch.Disable();
        }
    }

    protected override void Draw() => PluginUI.Draw();

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        Game.Dispose();
    }
}