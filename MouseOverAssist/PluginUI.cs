using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Lumina.Excel.Sheets;

namespace MouseOverAssist;

// MouseOverAssist 独自の日本語設定画面。設定は変更時に保存します。
public static class PluginUI
{
    public static bool IsVisible { get; set; }
    private static int selected = -1, bar = 1, slot = 1;
    private static uint placedAction, blacklistAction;
    private static string actionSearch = "", placementActionSearch = "", settingsSearch = "", message = "";
    private static bool actionCurrentJobOnly = true, actionIncludeSharedRole = false, actionPvPOnly = false;
    private static bool placementCurrentJobOnly = true, placementIncludeSharedRole = false, placementPvPOnly = false;
    private static int draggingStack = -1;
    private static int draggingTrigger = -1;
    private static int draggingCandidate = -1;
    private static Configuration C => MouseOverAssist.Config;
    private static Version PluginVersion => typeof(PluginUI).Assembly.GetName().Version ?? new Version(0, 0);
    private static string DisplayVersion => $"{PluginVersion.Major}.{PluginVersion.Minor}";
    private static string SourceZipUrl => $"https://github.com/lalayanlala/MouseOverAssist/releases/download/v{PluginVersion}/MouseOverAssist_V2_Source.zip";

    public static void Draw()
    {
        if (!IsVisible) return;
        ImGui.SetNextWindowSize(new Vector2(1220, 820), ImGuiCond.FirstUseEver);
        var open = IsVisible;
        if (ImGui.Begin("MouseOverAssist 設定###MouseOverAssist", ref open))
        {
            ImGui.TextColored(new Vector4(0.4f, 0.85f, 0.8f, 1), $"MouseOverAssist  V{DisplayVersion}");
            ImGui.SameLine();
            ImGui.TextDisabled("  設定は自動保存されます");

            if (ImGui.BeginTabBar("MouseOverAssistTabs"))
            {
                if (ImGui.BeginTabItem("アクションスタック"))
                {
                    Rules();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("その他の設定"))
                {
                    OtherSettingsTabs();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("対象一覧"))
                {
                    Targets();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("ヘルプ"))
                {
                    Home();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }

            if (message.Length > 0)
            {
                ImGui.Separator();
                ImGui.TextWrapped(message);
            }
        }
        ImGui.End();
        IsVisible = open;
    }

    private static void Title(string title, string description)
    {
        ImGui.TextColored(new Vector4(.4f, .85f, .8f, 1), title);
        ImGui.TextWrapped(description);
        ImGui.Spacing();
    }

    private static void Tooltip(string text)
    {
        if (!ImGui.IsItemHovered()) return;
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(420);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static void Home()
    {
        Title("使い方", "ReAction の Action Stack と同じ考え方で、左でスタックを選び、右で『反応するアクション』と『対象 / 使用アクション』を設定します。");
        ImGui.TextWrapped("基本は ①スタックを作る → ②反応するアクションを追加 → ③対象を追加、の3手順です。右下の対象は上から順に判定され、最初に使えるものが選ばれます。");
        ImGui.Spacing();
        if (ImGui.Button("マウスオーバー用スタックを作成")) AddPreset();
        ImGui.Separator();
        ImGui.TextWrapped("・左下の設定は、そのスタック全体に対する条件です。\n・『同じアクション』を選ぶと、押した元のアクションを対象だけ変更して実行します。\n・Shift / Ctrl / Alt は必要なときだけ使えばOKです。\n・迷った場合は『射程外なら失敗』をON、『元アクションを止める』『リキャスト中なら失敗』はOFFから試すのがおすすめです。");
        ImGui.Spacing();
        foreach (var module in PluginModuleManager.PluginModules.Where(x => !x.IsValid))
            ImGui.TextWrapped($"利用できないモジュール：{module.GetType().Name}（詳細は /xllog）");
        ImGui.Separator();
        ImGui.TextWrapped("ReAction / Hypostasis を基にした派生版です。出典・ライセンスは配布ソースに同梱しています。");
        if (ImGui.Button("この版のソースURLをコピー"))
        {
            ImGui.SetClipboardText(SourceZipUrl);
            message = "公開中のこの版に対応するソースZIPのURLをコピーしました。";
        }
    }

    private static void AddPreset()
    {
        var job = DalamudApi.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        C.ActionStacks.Add(new Configuration.ActionStack
        {
            Name = "マウスオーバー",
            JobId = job,
            Actions = [new Configuration.Action { ID = 2 }],
            Items =
            [
                new Configuration.ActionStackItem { TargetID = 10002 },
                new Configuration.ActionStackItem { TargetID = 10003 }
            ],
            CheckRange = true
        });
        selected = C.ActionStacks.Count - 1;
        C.Save();
    }

    private static void OtherSettingsTabs()
    {
        if (ImGui.BeginTabBar("OtherSettingsTabs"))
        {
            if (ImGui.BeginTabItem("操作補助"))
            {
                Settings("操作");
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("先行入力"))
            {
                Settings("先行");
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("マクロ・詳細"))
            {
                Settings("詳細");
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private static void Rules()
    {
        if (ImGui.Checkbox("アクションスタックを有効にする", ref C.EnableTargetRules)) C.Save();
        Tooltip("OFFにすると、ここで作ったスタックをすべて一時的に無効化します。");
        ImGui.SameLine();
        ImGui.TextDisabled("左：ルール一覧と条件 / 右：反応アクション・候補・リアルタイム判定");

        DrawStackToolbar();
        DrawRuleHealthSummary();
        ImGui.Separator();

        var avail = ImGui.GetContentRegionAvail();
        var leftWidth = Math.Min(360f, Math.Max(300f, avail.X * 0.31f));
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        ImGui.BeginChild("StackLeftColumn", new Vector2(leftWidth, avail.Y), false);
        var listHeight = Math.Max(210f, ImGui.GetContentRegionAvail().Y * 0.43f);
        ImGui.TextUnformatted("ルール一覧");
        ImGui.SameLine();
        ImGui.TextDisabled("上ほど優先 / 行をドラッグして並び替え");
        if (draggingStack >= 0 && draggingStack < C.ActionStacks.Count)
            ImGui.TextColored(new Vector4(.45f, .8f, 1f, 1), $"移動中: {C.ActionStacks[draggingStack].Name} → 移動先の行で離してください");
        ImGui.BeginChild("StackList", new Vector2(0, listHeight), true);
        for (var i = 0; i < C.ActionStacks.Count; i++)
        {
            var stack = C.ActionStacks[i];
            var diagnostics = GetDiagnostics(i);
            var errors = diagnostics.Count(x => x.Severity == RuleDiagnosticSeverity.Error);
            var warnings = diagnostics.Count - errors;
            var status = errors > 0 ? "[X]" : warnings > 0 ? "[!]" : "[OK]";
            var enabled = stack.Enabled ? "●" : "○";
            var stackName = string.IsNullOrWhiteSpace(stack.Name) ? $"ルール {i + 1}" : stack.Name;
            var label = $"{status} {enabled} {stackName}##stack{i}";

            if (ImGui.Selectable(label, selected == i)) selected = i;
            if (HandleStackDrag(i)) break;
            ImGui.TextDisabled($"   {JobName(stack.JobId)} | {TriggerSummary(stack)} → {stack.Items.Count}候補");
            if (i != C.ActionStacks.Count - 1) ImGui.Separator();
        }
        if (draggingStack >= 0 && ImGui.IsMouseReleased(ImGuiMouseButton.Left)) draggingStack = -1;
        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.TextUnformatted("ルール設定");
        ImGui.BeginChild("StackSettings", new Vector2(0, 0), true);
        if (selected >= 0 && selected < C.ActionStacks.Count)
            DrawStackSettings(C.ActionStacks[selected], selected);
        else
            ImGui.TextWrapped("左上の一覧から編集するルールを選ぶか、上の「＋ 新規」で作成してください。");
        ImGui.EndChild();
        ImGui.EndChild();

        ImGui.SameLine(0, spacing);
        ImGui.BeginChild("StackRightColumn", new Vector2(0, avail.Y), false);
        if (selected >= 0 && selected < C.ActionStacks.Count)
        {
            var stack = C.ActionStacks[selected];
            var rightHeight = ImGui.GetContentRegionAvail().Y;
            var triggerHeight = Math.Max(170f, rightHeight * 0.27f);
            var monitorHeight = Math.Max(150f, rightHeight * 0.25f);
            var targetHeight = Math.Max(190f, rightHeight - triggerHeight - monitorHeight - spacing * 2f);

            DrawTriggerActionEditor(stack, triggerHeight);
            ImGui.Spacing();
            DrawTargetActionEditor(stack, targetHeight);
            ImGui.Spacing();
            DrawDecisionMonitor(monitorHeight);
        }
        else
        {
            ImGui.BeginChild("NoStackSelected", new Vector2(0, 0), true);
            ImGui.TextWrapped("ルールを選ぶと、ここに『反応するアクション』『対象 / 使用するアクション』『リアルタイム判定』が表示されます。");
            ImGui.EndChild();
        }
        ImGui.EndChild();
    }

    private static void DrawStackToolbar()
    {
        if (ImGui.Button("＋ 新規"))
        {
            C.ActionStacks.Add(new Configuration.ActionStack { Name = "新しいスタック" });
            selected = C.ActionStacks.Count - 1;
            C.Save();
        }
        Tooltip("空のアクションスタックを追加します。");

        ImGui.SameLine();
        if (ImGui.Button("＋ マウスオーバー")) AddPreset();
        Tooltip("味方アクションを UIマウスオーバー → キャラ本体マウスオーバー の順で試すスタックを作成します。");

        var has = selected >= 0 && selected < C.ActionStacks.Count;
        ImGui.SameLine();
        if (ImGui.Button("コピー") && has)
        {
            ImGui.SetClipboardText(Configuration.ExportActionStack(C.ActionStacks[selected]));
            message = "選択中のスタックを共有文字列としてクリップボードへコピーしました。";
        }
        Tooltip("選択中のスタックを共有文字列としてコピーします。ReAction形式の読み込みにも対応しています。");

        ImGui.SameLine();
        if (ImGui.Button("貼り付け")) ImportFromClipboard();
        Tooltip("クリップボードにある MouseOverAssist / ReAction の共有文字列を読み込みます。");

        ImGui.SameLine();
        if (ImGui.Button("↑") && has && selected > 0)
        {
            (C.ActionStacks[selected - 1], C.ActionStacks[selected]) = (C.ActionStacks[selected], C.ActionStacks[selected - 1]);
            selected--;
            C.Save();
        }
        Tooltip("選択中のスタックの優先順位を上げます。");

        ImGui.SameLine();
        if (ImGui.Button("↓") && has && selected < C.ActionStacks.Count - 1)
        {
            (C.ActionStacks[selected + 1], C.ActionStacks[selected]) = (C.ActionStacks[selected], C.ActionStacks[selected + 1]);
            selected++;
            C.Save();
        }
        Tooltip("選択中のスタックの優先順位を下げます。");

        ImGui.SameLine();
        if (ImGui.Button("複製") && has)
        {
            var copy = Configuration.ImportActionStack(Configuration.ExportActionStack(C.ActionStacks[selected]));
            copy.Name += "（コピー）";
            C.ActionStacks.Insert(selected + 1, copy);
            selected++;
            C.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("削除…") && has) ImGui.OpenPopup("スタック削除確認");
        if (ImGui.BeginPopup("スタック削除確認"))
        {
            ImGui.TextWrapped("選択中のスタックを削除しますか？");
            if (ImGui.Button("削除する") && selected >= 0 && selected < C.ActionStacks.Count)
            {
                C.ActionStacks.RemoveAt(selected);
                selected = Math.Min(selected, C.ActionStacks.Count - 1);
                C.Save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("キャンセル")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    private static void DrawStackSettings(Configuration.ActionStack stack, int stackIndex)
    {
        var save = false;
        ImGui.SetNextItemWidth(-1);
        save |= ImGui.InputText("##StackName", ref stack.Name, 128);
        Tooltip("ルール名。自分が分かりやすい名前でOKです。");

        save |= ImGui.Checkbox("このルールを使う", ref stack.Enabled);

        ImGui.TextDisabled("ジョブ");
        ImGui.SetNextItemWidth(-1);
        LimitNextComboPopup(380f, 420f);
        if (ImGui.BeginCombo("##Job", JobName(stack.JobId)))
        {
            if (ImGui.Selectable("すべてのジョブ", stack.JobId == 0)) { stack.JobId = 0; save = true; }
            foreach (var j in DalamudApi.DataManager.GetExcelSheet<ClassJob>(ClientLanguage.Japanese).Where(x => x.RowId > 0 && x.Name.ToString().Length > 0))
            {
                if (ImGui.Selectable($"{j.Name}##job{j.RowId}", stack.JobId == j.RowId))
                {
                    stack.JobId = j.RowId;
                    save = true;
                }
            }
            ImGui.EndCombo();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("修飾キー");
        foreach (var (label, bit) in new[] { ("Shift", 1u), ("Ctrl", 2u), ("Alt", 4u) })
        {
            var on = (stack.ModifierKeys & bit) != 0;
            if (ImGui.Checkbox(label + $"##mod{bit}", ref on))
            {
                stack.ModifierKeys = on ? stack.ModifierKeys | bit : stack.ModifierKeys & ~bit;
                save = true;
            }
            if (bit != 4u) ImGui.SameLine();
        }
        var exact = (stack.ModifierKeys & 8u) != 0;
        if (ImGui.Checkbox("完全一致", ref exact))
        {
            stack.ModifierKeys = exact ? stack.ModifierKeys | 8u : stack.ModifierKeys & ~8u;
            save = true;
        }
        Tooltip("ONなら選んだ修飾キーだけが押されている時に一致します。OFFなら、選んだキーを含んでいれば他の修飾キーが押されていても一致します。");

        ImGui.Separator();
        save |= ImGui.Checkbox("元アクションを止める", ref stack.BlockOriginal);
        Tooltip("右側の候補がすべて失敗した時、元々押したアクションも発動させません。通常はOFF推奨です。");
        save |= ImGui.Checkbox("射程外なら失敗", ref stack.CheckRange);
        Tooltip("対象が使用アクションの射程外なら、その候補を飛ばして次を試します。");
        save |= ImGui.Checkbox("リキャスト中なら失敗", ref stack.CheckCooldown);
        Tooltip("そのアクションが先行入力できない状態なら、その候補を飛ばして次を試します。");

        if (stack.ExcludedActions.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextWrapped("V1からの除外設定：" + string.Join("、", stack.ExcludedActions.Select(ActionName)));
            if (ImGui.Button("除外設定を解除"))
            {
                stack.ExcludedActions.Clear();
                save = true;
            }
        }

        ImGui.Separator();
        DrawDiagnosticsPanel(stackIndex);

        if (save) C.Save();
    }

    private static void DrawTriggerActionEditor(Configuration.ActionStack stack, float height)
    {
        ImGui.BeginChild("TriggerActions", new Vector2(0, height), true);
        ImGui.TextUnformatted("反応するアクション");
        ImGui.SameLine();
        ImGui.TextDisabled("この一覧のどれかを押した時にスタックを実行");
        ImGui.Separator();

        var save = false;
        for (var i = 0; i < stack.Actions.Count; i++)
        {
            ImGui.PushID(10000 + i);
            var action = stack.Actions[i];
            var width = ImGui.GetContentRegionAvail().X;

            var rowStart = ImGui.GetCursorScreenPos();
            var rowTop = rowStart.Y;
            var rowLeft = rowStart.X;
            var rowRight = rowLeft + width;
            ImGui.Button("↕##DragTrigger");
            var dragStarted = ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left);
            Tooltip("この取っ手をドラッグして、反応アクションの順番を変更できます。行の上で離すと移動します。");
            ImGui.SameLine();
            if (ImGui.Button("↑") && i > 0)
            {
                (stack.Actions[i - 1], stack.Actions[i]) = (action, stack.Actions[i - 1]);
                save = true;
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.Button("↓") && i < stack.Actions.Count - 1)
            {
                (stack.Actions[i + 1], stack.Actions[i]) = (action, stack.Actions[i + 1]);
                save = true;
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(Math.Max(180f, width - 325f));
            save |= ActionCombo("##TriggerAction", ref action.ID, true);
            ImGui.SameLine();
            save |= ImGui.Checkbox("変化後も一致", ref action.UseAdjustedID);
            Tooltip("レベルアップ・特性・コンボ・他プラグインなどで別アクションに変化した場合も同じアクションとして判定します。");
            ImGui.SameLine();
            var remove = ImGui.Button("×");
            var rowBottom = ImGui.GetItemRectMax().Y;
            if (HandleListDrag(stack.Actions, i, ref draggingTrigger, dragStarted, rowLeft, rowTop, rowRight, rowBottom))
            {
                save = true;
                ImGui.PopID();
                break;
            }
            if (remove)
            {
                stack.Actions.RemoveAt(i);
                save = true;
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        if (draggingTrigger >= 0 && ImGui.IsMouseReleased(ImGuiMouseButton.Left)) draggingTrigger = -1;
        if (ImGui.Button("＋ アクションを追加"))
        {
            stack.Actions.Add(new Configuration.Action());
            save = true;
        }
        if (save) C.Save();
        ImGui.EndChild();
    }

    private static void DrawTargetActionEditor(Configuration.ActionStack stack, float height)
    {
        ImGui.BeginChild("TargetActions", new Vector2(0, height), true);
        ImGui.TextUnformatted("対象 / 使用するアクション");
        ImGui.SameLine();
        ImGui.TextDisabled("上から順に試し、最初に使える行を実行");
        ImGui.Separator();

        var save = false;
        if (stack.Items.Count > 0)
        {
            var available = ImGui.GetContentRegionAvail().X;
            ImGui.TextDisabled("        対象");
            ImGui.SameLine(Math.Max(100f, available * 0.48f));
            ImGui.TextDisabled("使用するアクション");
        }

        for (var i = 0; i < stack.Items.Count; i++)
        {
            ImGui.PushID(20000 + i);
            var item = stack.Items[i];
            var width = ImGui.GetContentRegionAvail().X;
            var buttonSpace = 140f;
            var comboWidth = Math.Max(150f, (width - buttonSpace) / 2f);

            var rowStart = ImGui.GetCursorScreenPos();
            var rowTop = rowStart.Y;
            var rowLeft = rowStart.X;
            var rowRight = rowLeft + width;
            ImGui.Button("↕##DragCandidate");
            var dragStarted = ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left);
            Tooltip("この取っ手をドラッグして、候補の優先順位を変更できます。行の上で離すと移動します。");
            ImGui.SameLine();
            if (ImGui.Button("↑") && i > 0)
            {
                (stack.Items[i - 1], stack.Items[i]) = (item, stack.Items[i - 1]);
                save = true;
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.Button("↓") && i < stack.Items.Count - 1)
            {
                (stack.Items[i + 1], stack.Items[i]) = (item, stack.Items[i + 1]);
                save = true;
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(comboWidth);
            save |= TargetCombo("##Target", ref item.TargetID);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(comboWidth);
            save |= ActionCombo("##OverrideAction", ref item.ID, false);
            ImGui.SameLine();
            var remove = ImGui.Button("×");
            var rowBottom = ImGui.GetItemRectMax().Y;
            if (HandleListDrag(stack.Items, i, ref draggingCandidate, dragStarted, rowLeft, rowTop, rowRight, rowBottom))
            {
                save = true;
                ImGui.PopID();
                break;
            }
            if (remove)
            {
                stack.Items.RemoveAt(i);
                save = true;
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        if (draggingCandidate >= 0 && ImGui.IsMouseReleased(ImGuiMouseButton.Left)) draggingCandidate = -1;
        if (ImGui.Button("＋ 対象を追加"))
        {
            stack.Items.Add(new Configuration.ActionStackItem());
            save = true;
        }
        ImGui.SameLine();
        ImGui.TextDisabled("「同じアクション」なら対象だけを変更します");

        if (save) C.Save();
        ImGui.EndChild();
    }

    private static void DrawRuleHealthSummary()
    {
        var entries = new List<(int StackIndex, RuleDiagnostic Diagnostic)>();
        var errors = 0;
        var warnings = 0;
        for (var i = 0; i < C.ActionStacks.Count; i++)
        {
            foreach (var diagnostic in GetDiagnostics(i))
            {
                entries.Add((i, diagnostic));
                if (diagnostic.Severity == RuleDiagnosticSeverity.Error) errors++;
                else warnings++;
            }
        }

        ImGui.SameLine();
        if (errors == 0 && warnings == 0)
        {
            ImGui.TextColored(new Vector4(.45f, .9f, .5f, 1), "設定チェック: 問題なし");
            return;
        }

        if (errors > 0)
            ImGui.TextColored(new Vector4(1f, .45f, .4f, 1), $"設定チェック: エラー {errors} / 注意 {warnings}");
        else
            ImGui.TextColored(new Vector4(1f, .8f, .35f, 1), $"設定チェック: 注意 {warnings}");

        Tooltip("『内容を見る』を押すと、どのルールの何が問題なのか確認できます。");
        ImGui.SameLine();
        if (ImGui.SmallButton("内容を見る##RuleHealthDetails"))
            ImGui.OpenPopup("設定チェック詳細");

        LimitNextComboPopup(660f, 520f);
        if (!ImGui.BeginPopup("設定チェック詳細")) return;

        ImGui.TextUnformatted("設定チェックの内容");
        ImGui.TextDisabled("ルールごとに、エラー / 注意の理由を表示します。");
        ImGui.Separator();

        foreach (var group in entries.GroupBy(x => x.StackIndex))
        {
            var stack = C.ActionStacks[group.Key];
            var stackName = string.IsNullOrWhiteSpace(stack.Name) ? $"ルール {group.Key + 1}" : stack.Name;
            ImGui.TextUnformatted($"{group.Key + 1}. {stackName}");

            foreach (var entry in group)
            {
                var isError = entry.Diagnostic.Severity == RuleDiagnosticSeverity.Error;
                var color = isError
                    ? new Vector4(1f, .55f, .5f, 1)
                    : new Vector4(1f, .82f, .45f, 1);
                ImGui.TextColoredWrapped(color, $"{(isError ? "[エラー]" : "[注意]")} {entry.Diagnostic.Message}");
            }

            if (ImGui.SmallButton($"このルールを開く##RuleHealthOpen{group.Key}"))
            {
                selected = group.Key;
                ImGui.CloseCurrentPopup();
                break;
            }
            ImGui.Separator();
        }

        ImGui.EndPopup();
    }

    private static List<RuleDiagnostic> GetDiagnostics(int stackIndex)
    {
        var diagnostics = RuleDiagnostics.GetForStack(C.ActionStacks, stackIndex);
        if (stackIndex < 0 || stackIndex >= C.ActionStacks.Count) return diagnostics;

        var stack = C.ActionStacks[stackIndex];
        if (stack.JobId != 0 && DalamudApi.DataManager.GetExcelSheet<ClassJob>(ClientLanguage.Japanese).GetRowOrDefault(stack.JobId) is null)
            diagnostics.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Error, $"ジョブ ID {stack.JobId} がゲームデータに見つかりません。"));

        foreach (var action in stack.Actions)
        {
            if (action.ID <= 2) continue;
            if (!ActionExists(action.ID))
                diagnostics.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Error, $"反応アクション ID {action.ID} は現在のアクションスタックで扱えません。"));
        }

        foreach (var item in stack.Items)
        {
            if (item.ID != 0 && !ActionExists(item.ID))
                diagnostics.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Error, $"使用アクション ID {item.ID} は現在のアクションスタックで扱えません。"));
            if (!PronounManager.OrderedIDs.Contains(item.TargetID))
                diagnostics.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Warning, $"対象 ID {item.TargetID} は現在の対象一覧にありません。"));
        }

        foreach (var excluded in stack.ExcludedActions)
        {
            if (!ActionExists(excluded))
                diagnostics.Add(new RuleDiagnostic(RuleDiagnosticSeverity.Warning, $"除外アクション ID {excluded} は現在のアクションスタック対象にありません。"));
        }
        return diagnostics;
    }

    private static void DrawDiagnosticsPanel(int stackIndex)
    {
        var diagnostics = GetDiagnostics(stackIndex);
        if (diagnostics.Count == 0)
        {
            ImGui.TextColored(new Vector4(.45f, .9f, .5f, 1), "[OK] 設定チェック: 問題は見つかりませんでした");
            return;
        }

        var errors = diagnostics.Count(x => x.Severity == RuleDiagnosticSeverity.Error);
        var warnings = diagnostics.Count - errors;
        if (errors > 0)
            ImGui.TextColored(new Vector4(1f, .45f, .4f, 1), $"[X] 設定チェック: エラー {errors} / 注意 {warnings}");
        else
            ImGui.TextColored(new Vector4(1f, .8f, .35f, 1), $"[!] 設定チェック: 注意 {warnings}");

        foreach (var diagnostic in diagnostics)
        {
            var color = diagnostic.Severity == RuleDiagnosticSeverity.Error
                ? new Vector4(1f, .55f, .5f, 1)
                : new Vector4(1f, .82f, .45f, 1);
            ImGui.TextColoredWrapped(color, $"・{diagnostic.Message}");
        }
    }

    private static string TriggerSummary(Configuration.ActionStack stack)
    {
        if (stack.Actions.Count == 0) return "反応アクション未設定";
        var names = stack.Actions.Take(2).Select(x => TriggerActionName(x.ID)).ToArray();
        var suffix = stack.Actions.Count > 2 ? $" +{stack.Actions.Count - 2}" : string.Empty;
        return string.Join(" / ", names) + suffix;
    }

    private static string TriggerActionName(uint id)
        => id switch
        {
            0 => "全スキル",
            1 => "敵対象スキル",
            2 => "味方対象スキル",
            _ => ActionName(id)
        };

    private static bool ActionExists(uint id)
        => MouseOverAssist.actionSheet.ContainsKey(id);

    private static bool HandleStackDrag(int rowIndex)
    {
        var rectMin = ImGui.GetItemRectMin();
        var rectMax = ImGui.GetItemRectMax();
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            draggingStack = rowIndex;
            selected = rowIndex;
        }

        var mouse = ImGui.GetMousePos();
        var mouseOverRow = mouse.X >= rectMin.X && mouse.X <= rectMax.X && mouse.Y >= rectMin.Y && mouse.Y <= rectMax.Y;
        if (draggingStack >= 0 && draggingStack != rowIndex && mouseOverRow)
        {
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                Move(C.ActionStacks, draggingStack, rowIndex);
                selected = rowIndex;
                draggingStack = -1;
                C.Save();
                message = "ルールの優先順位を変更しました。";
                return true;
            }

            ImGui.SetTooltip("ここへ移動");
        }

        return false;
    }

    private static void Move<T>(List<T> list, int from, int to)
    {
        if (from == to || from < 0 || from >= list.Count || to < 0 || to >= list.Count) return;
        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
    }

    private static bool HandleListDrag<T>(List<T> list, int rowIndex, ref int draggingIndex, bool dragStarted, float rowLeft, float rowTop, float rowRight, float rowBottom)
    {
        if (dragStarted) draggingIndex = rowIndex;

        var mouse = ImGui.GetMousePos();
        var mouseOverRow = mouse.X >= rowLeft && mouse.X <= rowRight && mouse.Y >= rowTop && mouse.Y <= rowBottom;
        if (draggingIndex >= 0 && draggingIndex != rowIndex && mouseOverRow)
        {
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                Move(list, draggingIndex, rowIndex);
                draggingIndex = -1;
                return true;
            }

            ImGui.SetTooltip("ここへ移動");
        }

        return false;
    }

    private static void DrawDecisionMonitor(float height)
    {
        ImGui.BeginChild("DecisionMonitor", new Vector2(0, height), true);
        ImGui.TextUnformatted("リアルタイム判定");
        ImGui.SameLine();
        ImGui.TextDisabled("設定画面を開いている間の最後の判定理由を表示");
        ImGui.SameLine();
        if (ImGui.SmallButton("クリア")) ActionStackManager.ClearDecision();
        ImGui.Separator();

        var snapshot = ActionStackManager.LastDecision;
        if (snapshot.TimestampUtc == DateTime.MinValue)
        {
            ImGui.TextWrapped(snapshot.Result);
            ImGui.EndChild();
            return;
        }

        var age = DateTime.UtcNow - snapshot.TimestampUtc;
        var ageText = age.TotalSeconds < 1 ? "たった今" : age.TotalSeconds < 60 ? $"{Math.Floor(age.TotalSeconds)}秒前" : $"{Math.Floor(age.TotalMinutes)}分前";
        ImGui.TextWrapped($"入力: {ActionName(snapshot.InputActionID)} [{snapshot.InputActionID}]    {ageText}");
        if (snapshot.AdjustedActionID != snapshot.InputActionID)
            ImGui.TextWrapped($"変化後: {ActionName(snapshot.AdjustedActionID)} [{snapshot.AdjustedActionID}]");
        if (snapshot.OutputActionID != 0 && snapshot.OutputActionID != snapshot.AdjustedActionID)
            ImGui.TextWrapped($"実行: {ActionName(snapshot.OutputActionID)} [{snapshot.OutputActionID}]");
        if (!string.IsNullOrWhiteSpace(snapshot.StackName))
            ImGui.TextWrapped($"判定ルール: {snapshot.StackName}");

        var resultColor = snapshot.Result.Contains("採用", StringComparison.Ordinal) || snapshot.Result.Contains("元アクション", StringComparison.Ordinal)
            ? new Vector4(.55f, .9f, .6f, 1)
            : new Vector4(1f, .75f, .4f, 1);
        ImGui.TextColoredWrapped(resultColor, $"結果: {snapshot.Result}");
        foreach (var step in snapshot.Steps)
            ImGui.TextWrapped(step);
        ImGui.EndChild();
    }

    private static void ImportFromClipboard()
    {
        try
        {
            var text = ImGui.GetClipboardText()?.Trim() ?? string.Empty;
            var stack = Configuration.ImportActionStack(text);
            ValidateImportedStack(stack);
            C.ActionStacks.Add(stack);
            selected = C.ActionStacks.Count - 1;
            C.Save();
            message = "クリップボードからスタックを読み込みました。ジョブ指定を確認してください。";
        }
        catch
        {
            message = "読み込めませんでした。クリップボードに MouseOverAssist または ReAction の共有文字列があるか確認してください。";
        }
    }

    private static void ValidateImportedStack(Configuration.ActionStack stack)
    {
        if (stack == null || stack.Actions == null || stack.Items == null || stack.ExcludedActions == null ||
            stack.Actions.Count > 256 || stack.Items.Count > 256 || stack.Actions.Any(x => x == null) || stack.Items.Any(x => x == null))
            throw new InvalidOperationException();
    }
    private static string JobName(uint id) => id==0?"すべてのジョブ":DalamudApi.DataManager.GetExcelSheet<ClassJob>(ClientLanguage.Japanese).GetRowOrDefault(id)?.Name.ToString()??$"ジョブ {id}";
    private static string ActionName(uint id) => DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>(ClientLanguage.Japanese).GetRowOrDefault(id)?.Name.ToString()??$"ID {id}";
    private static void LimitNextComboPopup(float maxWidth, float maxHeight)
    {
        var display = ImGui.GetIO().DisplaySize;
        var width = MathF.Min(maxWidth, MathF.Max(260f, display.X - 80f));
        var height = MathF.Min(maxHeight, MathF.Max(220f, display.Y - 120f));
        ImGui.SetNextWindowSizeConstraints(Vector2.Zero, new Vector2(width, height));
    }

    private static bool ActionCombo(string label, ref uint id, bool trigger)
    {
        var preview = id == 0
            ? (trigger ? "すべてのスキル" : "押したスキルのまま")
            : trigger && id == 1
                ? "敵を対象にするスキル"
                : trigger && id == 2
                    ? "味方を対象にするスキル"
                    : $"{ActionName(id)}  [ID {id}]";

        LimitNextComboPopup(680f, 470f);
        if (!ImGui.BeginCombo(label, preview)) return false;

        var changed = false;
        ImGui.TextDisabled(actionPvPOnly
            ? "プレイヤーが使用できるPvPアクションだけを表示します。技名 / ID / 習得Lv / ジョブ名で検索できます。"
            : "プレイヤーが使用できるPvEアクションだけを表示します。技名 / ID / 習得Lv / ジョブ名で検索できます。");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("検索（技名・ID・ジョブ）##ActionSearch", ref actionSearch, 128);

        var currentJob = DalamudApi.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        var currentOnly = actionCurrentJobOnly;
        if (ImGui.Checkbox("現在ジョブのみ", ref currentOnly))
            actionCurrentJobOnly = currentOnly;
        Tooltip("現在ジョブのジョブ専用技を表示します。『共通・ロール技』もONなら、現在ジョブで使用可能な共通・ロール技も表示します。両方OFFなら有効な全アクションを表示します。");

        ImGui.SameLine();
        var includeSharedRole = actionIncludeSharedRole;
        if (ImGui.Checkbox("共通・ロール技", ref includeSharedRole))
            actionIncludeSharedRole = includeSharedRole;
        Tooltip("共通・ロール技を表示します。これだけONなら共通・ロール技のみ、現在ジョブもONなら現在ジョブ専用技＋現在ジョブで使用可能な共通・ロール技、両方OFFなら有効な全アクションを表示します。");

        ImGui.SameLine();
        var pvpOnly = actionPvPOnly;
        if (ImGui.Checkbox("PvPの技のみ", ref pvpOnly))
            actionPvPOnly = pvpOnly;
        Tooltip("ONならPvEアクションを隠して、現在のPvPアクションだけを表示します。OFFではPvEアクションだけを表示します。");

        ImGui.TextDisabled($"現在ジョブ: {JobName(currentJob)} (ID {currentJob}) / 表示: {ActionPickerModeLabel(actionCurrentJobOnly, actionIncludeSharedRole)} / {(actionPvPOnly ? "PvP" : "PvE")}");
        ImGui.Separator();
        if (ImGui.Selectable(trigger ? "すべてのスキル" : "押したスキルのまま", id == 0))
        {
            id = 0;
            changed = true;
        }
        if (trigger)
        {
            if (ImGui.Selectable("敵を対象にするスキル", id == 1)) { id = 1; changed = true; }
            if (ImGui.Selectable("味方を対象にするスキル", id == 2)) { id = 2; changed = true; }
        }

        ImGui.Separator();
        ImGui.BeginChild("ActionPickerList", new Vector2(0, 270));
        var shown = 0;
        const int maxShown = 250;
        var actions = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>(ClientLanguage.Japanese)
            .Where(a => IsSelectablePlayerAction(a, actionPvPOnly))
            .OrderBy(a => ActionSortGroup(a, currentJob))
            .ThenBy(a => a.ClassJobLevel)
            .ThenBy(a => a.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.RowId);

        foreach (var a in actions)
        {
            var name = a.Name.ToString();
            if (!ActionMatchesSearch(a, actionSearch))
                continue;

            if (!PassesActionPickerJobFilter(a, currentJob, actionCurrentJobOnly, actionIncludeSharedRole))
                continue;

            if (shown++ >= maxShown) continue;

            ImGui.PushID((int)a.RowId);
            DrawActionIcon(a.Icon);
            var owner = ActionOwnerLabel(a);
            if (ImGui.Selectable($"{name}    ID {a.RowId}    Lv{a.ClassJobLevel}    {owner}", id == a.RowId))
            {
                id = a.RowId;
                changed = true;
            }
            ImGui.PopID();
        }
        if (shown == 0)
            ImGui.TextDisabled("条件に一致するアクションがありません。『共通・ロール技』や検索条件を確認してください。");
        else if (shown > maxShown)
            ImGui.TextDisabled($"候補が多いため先頭 {maxShown} 件を表示しています。検索で絞り込んでください。");
        ImGui.EndChild();
        if (changed) ImGui.CloseCurrentPopup();
        ImGui.EndCombo();

        if (changed) actionSearch = string.Empty;
        return changed;
    }

    private static bool IsSelectablePlayerAction(Lumina.Excel.Sheets.Action action, bool pvpOnly)
    {
        // V2.3.3 で実際に動いていた基本条件を基準にする。
        // ここで ClassJob の有効性を先に検査すると、正規のロールアクションまで
        // 0xFFFFFFFF 等の特殊値を理由に落ちるため、共有/ロール判定を先に行う。
        if (action.RowId <= 8
            || action.Name.ToString().Length == 0
            || !action.IsPlayerAction
            || action.IsPvP != pvpOnly
            || action.ClassJobCategory.RowId == 0
            || action.ActionCategory.RowId < 2
            || action.ActionCategory.RowId > 4)
            return false;

        // V2.3.3 で表示できていた共通・ロール技は最優先で保持する。
        // これが V2.3.4 以降の回帰修正の要点。
        if (IsSharedOrRoleAction(action))
            return true;

        // ジョブ専用技だけは、旧PvP/NPC/内部用の残存データを除外するため
        // ClassJob が実在する戦闘ジョブかを確認する。
        if (action.ClassJob.RowId != 0)
        {
            var ownerJob = DalamudApi.DataManager.GetExcelSheet<ClassJob>(ClientLanguage.Japanese).GetRowOrDefault(action.ClassJob.RowId);
            if (ownerJob is not { } owner || !IsBattleJobId(owner.RowId))
                return false;
        }

        return true;
    }

    // ClassJobCategory の互換判定は、生成プロパティ名ではなくシート上の実データを読む。
    // ClassJobCategory は RowOffset + 4 が ADV、その後は ClassJob RowId と同じ順で
    // GLA=+5, ... AST(33)=+37 のように並ぶ。API/生成コード差に影響されない。
    private static readonly uint[] BattleJobIds =
    [
        1, 2, 3, 4, 5, 6, 7,
        19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
        31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43
    ];

    private static bool IsBattleJobId(uint jobId) => BattleJobIds.Contains(jobId);

    private static bool CategorySupportsJob(ClassJobCategory category, uint jobId)
    {
        if (jobId > 43) return false;
        return category.ExcelPage.ReadBool(category.RowOffset + 4 + jobId);
    }


    private static bool ActionMatchesSearch(Lumina.Excel.Sheets.Action action, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;

        var jobNames = new List<string>();
        if (action.ClassJobCategory.RowId != 0)
        {
            var category = DalamudApi.DataManager.GetExcelSheet<ClassJobCategory>(ClientLanguage.Japanese).GetRowOrDefault(action.ClassJobCategory.RowId);
            if (category is { } c)
            {
                foreach (var jobId in BattleJobIds)
                {
                    if (CategorySupportsJob(c, jobId))
                        jobNames.Add(JobName(jobId));
                }
            }
        }

        return RulePolicy.ActionSearchMatches(
            search,
            action.Name.ToString(),
            action.RowId.ToString(),
            action.ClassJobLevel.ToString(),
            jobNames.ToArray());
    }

    private static bool PassesActionPickerJobFilter(
        Lumina.Excel.Sheets.Action action, uint currentJob, bool currentJobOnly, bool includeSharedRole)
    {
        var sharedOrRole = IsSharedOrRoleAction(action);
        var supportsCurrentJob = currentJob != 0 && ActionSupportsJob(action, currentJob);

        // 2つのチェックは「表示したい集合」を独立して選ぶ。
        // ON+ON  : 現在ジョブ専用技 + 現在ジョブで使用可能な共通・ロール技
        // ON+OFF : 現在ジョブ専用技のみ
        // OFF+ON : 共通・ロール技のみ
        // OFF+OFF: 有効な全アクション
        return RulePolicy.ActionPickerApplies(
            sharedOrRole,
            supportsCurrentJob,
            currentJobOnly,
            includeSharedRole);
    }

    private static string ActionPickerModeLabel(bool currentJobOnly, bool includeSharedRole)
        => (currentJobOnly, includeSharedRole) switch
        {
            (true, true) => "現在ジョブ + 使用可能な共通・ロール技",
            (true, false) => "現在ジョブのみ",
            (false, true) => "共通・ロール技のみ",
            _ => "すべて"
        };

    private static bool IsSharedOrRoleAction(Lumina.Excel.Sheets.Action action)
    {
        if (action.ClassJobCategory.RowId == 0) return false;

        var category = DalamudApi.DataManager.GetExcelSheet<ClassJobCategory>(ClientLanguage.Japanese).GetRowOrDefault(action.ClassJobCategory.RowId);
        if (category is not { } c) return false;

        // 「共通・ロール技」は戦闘ジョブ用だけを対象にする。
        // ギャザラー/クラフターにも IsRoleAction=true のアクションが存在するため、
        // IsRoleAction だけで判定すると採集/製作アクションまで混ざってしまう。
        var battleJobCount = 0;
        foreach (var jobId in BattleJobIds)
        {
            if (CategorySupportsJob(c, jobId))
                battleJobCount++;
        }

        return RulePolicy.IsBattleSharedOrRole(
            action.IsRoleAction,
            battleJobCount,
            action.ClassJob.RowId != 0);
    }

    private static bool ActionSupportsJob(Lumina.Excel.Sheets.Action action, uint jobId)
    {
        if (jobId == 0 || action.ClassJobCategory.RowId == 0) return false;
        var category = DalamudApi.DataManager.GetExcelSheet<ClassJobCategory>(ClientLanguage.Japanese).GetRowOrDefault(action.ClassJobCategory.RowId);
        return category is { } c && CategorySupportsJob(c, jobId);
    }

    private static int ActionSortGroup(Lumina.Excel.Sheets.Action action, uint currentJob)
    {
        if (currentJob > 0 && ActionSupportsJob(action, currentJob))
            return IsSharedOrRoleAction(action) ? 1 : 0;
        return IsSharedOrRoleAction(action) ? 3 : 2;
    }

    private static string ActionOwnerLabel(Lumina.Excel.Sheets.Action action)
    {
        if (IsSharedOrRoleAction(action)) return "ロール";
        if (action.ClassJob.RowId > 0) return JobName(action.ClassJob.RowId);

        var category = DalamudApi.DataManager.GetExcelSheet<ClassJobCategory>(ClientLanguage.Japanese).GetRowOrDefault(action.ClassJobCategory.RowId);
        var categoryName = category?.Name.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(categoryName)) return categoryName;
        return IsSharedOrRoleAction(action) ? "共通" : "ジョブ派生";
    }

    private static void DrawActionIcon(uint iconID)
    {
        if (iconID == 0) return;
        var lookup = new GameIconLookup { IconId = iconID };
        if (!DalamudApi.TextureProvider.TryGetFromGameIcon(lookup, out var texture) || texture == null) return;
        if (!texture.TryGetWrap(out var wrap, out _) || wrap == null) return;
        ImGui.Image(wrap.Handle, new Vector2(24, 24));
        ImGui.SameLine();
    }

    private static void PlacementActionPicker(ref uint id)
    {
        var selectedText = id == 0 ? "未選択" : $"{ActionName(id)}  [ID {id}]";
        ImGui.TextWrapped($"配置するスキル：{selectedText}");

        if (ImGui.Button("スキルを選ぶ…"))
        {
            placementActionSearch = string.Empty;
            ImGui.OpenPopup("配置するスキルを選択");
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("ベル起爆##QuickBell")) id = 28509;
        ImGui.SameLine();
        if (ImGui.SmallButton("星起爆##QuickStar")) id = 8324;
        var display = ImGui.GetIO().DisplaySize;
        var popupWidth = MathF.Min(620f, MathF.Max(420f, display.X - 160f));
        var popupHeight = MathF.Min(500f, MathF.Max(320f, display.Y - 180f));
        ImGui.SetNextWindowSize(new Vector2(popupWidth, popupHeight), ImGuiCond.Appearing);
        ImGui.SetNextWindowSizeConstraints(new Vector2(360f, 280f), new Vector2(popupWidth, popupHeight));
        if (!ImGui.BeginPopup("配置するスキルを選択")) return;

        ImGui.TextUnformatted("配置するスキルを選択");
        ImGui.TextDisabled("この一覧は設定ウィンドウとは別の固定サイズポップアップで開きます。");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("検索（技名・ID・ジョブ）##PlacementActionSearch", ref placementActionSearch, 128);

        var currentJob = DalamudApi.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        var currentOnly = placementCurrentJobOnly;
        if (ImGui.Checkbox("現在ジョブのみ##PlacementCurrentJob", ref currentOnly))
            placementCurrentJobOnly = currentOnly;
        Tooltip("現在ジョブのジョブ専用技を表示します。『共通・ロール技』もONなら、現在ジョブで使用可能な共通・ロール技も表示します。両方OFFなら有効な全アクションを表示します。");

        ImGui.SameLine();
        var includeSharedRole = placementIncludeSharedRole;
        if (ImGui.Checkbox("共通・ロール技##PlacementSharedRole", ref includeSharedRole))
            placementIncludeSharedRole = includeSharedRole;
        Tooltip("共通・ロール技を表示します。これだけONなら共通・ロール技のみ、現在ジョブもONなら現在ジョブ専用技＋現在ジョブで使用可能な共通・ロール技、両方OFFなら有効な全アクションを表示します。");

        ImGui.SameLine();
        var pvpOnly = placementPvPOnly;
        if (ImGui.Checkbox("PvPの技のみ##PlacementPvPOnly", ref pvpOnly))
            placementPvPOnly = pvpOnly;
        Tooltip("ONならPvEアクションを隠して、現在のPvPアクションだけを表示します。OFFではPvEアクションだけを表示します。");

        ImGui.TextDisabled($"現在ジョブ: {JobName(currentJob)} (ID {currentJob}) / 表示: {ActionPickerModeLabel(placementCurrentJobOnly, placementIncludeSharedRole)} / {(placementPvPOnly ? "PvP" : "PvE")}");
        ImGui.Separator();
        var listHeight = MathF.Max(150f, popupHeight - 175f);
        ImGui.BeginChild("PlacementActionList", new Vector2(0, listHeight), true);
        var shown = 0;
        const int maxShown = 300;
        var actions = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>(ClientLanguage.Japanese)
            .Where(a => IsSelectablePlayerAction(a, placementPvPOnly))
            .OrderBy(a => ActionSortGroup(a, currentJob))
            .ThenBy(a => a.ClassJobLevel)
            .ThenBy(a => a.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.RowId);

        foreach (var a in actions)
        {
            var name = a.Name.ToString();
            if (!ActionMatchesSearch(a, placementActionSearch))
                continue;

            if (!PassesActionPickerJobFilter(a, currentJob, placementCurrentJobOnly, placementIncludeSharedRole))
                continue;
            if (shown++ >= maxShown) continue;

            ImGui.PushID((int)a.RowId);
            DrawActionIcon(a.Icon);
            var owner = ActionOwnerLabel(a);
            if (ImGui.Selectable($"{name}    ID {a.RowId}    Lv{a.ClassJobLevel}    {owner}", id == a.RowId))
            {
                id = a.RowId;
                placementActionSearch = string.Empty;
                ImGui.CloseCurrentPopup();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }
        if (shown == 0)
            ImGui.TextDisabled("条件に一致するアクションがありません。『共通・ロール技』や検索条件を確認してください。");
        else if (shown > maxShown)
            ImGui.TextDisabled($"候補が多いため先頭 {maxShown} 件を表示しています。検索で絞り込んでください。");
        ImGui.EndChild();
        ImGui.EndPopup();
    }

    private static bool TargetCombo(string label,ref uint id)
    {
        LimitNextComboPopup(380f, 360f);
        if(!ImGui.BeginCombo(label,PronounManager.GetPronounName(id)))return false;
        var changed=false;
        foreach(var target in PronounManager.OrderedIDs)
            if(ImGui.Selectable(PronounManager.GetPronounName(target),id==target)){id=target;changed=true;}
        if(changed)ImGui.CloseCurrentPopup();
        ImGui.EndCombo();return changed;
    }
    private static void Settings(string group)
    {
        Title(group=="操作"?"操作補助":group=="先行"?"先行入力":"マクロ・詳細", "必要な機能だけをONにできます。各項目の下に動作を説明しています。");
        ImGui.InputText("設定を検索",ref settingsSearch,128);var save=false;
        foreach(var option in Options.Where(x=>x.Group==group))
        {
            if(settingsSearch.Length>0&&!option.Label.Contains(settingsSearch,StringComparison.OrdinalIgnoreCase)&&!option.Help.Contains(settingsSearch,StringComparison.OrdinalIgnoreCase))continue;
            var field=typeof(Configuration).GetField(option.Field);var value=(bool)field.GetValue(C);
            if(ImGui.Checkbox(option.Label+"##"+option.Field,ref value)){field.SetValue(C,value);save=true;}
            ImGui.PushTextWrapPos(0);ImGui.TextDisabled(option.Help);ImGui.PopTextWrapPos();ImGui.Spacing();
        }
        if(group=="操作")
        {
            save|=ImGui.SliderInt("長押しの繰り返し間隔（ms）",ref C.TurboHotbarInterval,50,1000);
            save|=ImGui.SliderInt("長押し開始までの待機（ms）",ref C.InitialTurboHotbarInterval,0,1000);
            ImGui.TextWrapped("長押しから除外するスキル（ホットバーに置いた元のスキル）");
            ActionCombo("除外スキル",ref blacklistAction,false);
            if(ImGui.Button("除外に追加")&&blacklistAction>0){C.TurboHotbarBlacklist.Add(blacklistAction);save=true;}
            foreach(var id in C.TurboHotbarBlacklist.ToArray())if(ImGui.Button($"解除：{ActionName(id)}##black{id}")){C.TurboHotbarBlacklist.Remove(id);save=true;}
            var focus=C.AutoFocusTargetID!=0;
            if(ImGui.Checkbox("フォーカスターゲットを自動設定",ref focus)){C.AutoFocusTargetID=focus?10000u:0u;save=true;}
            if(focus)save|=TargetCombo("自動設定する対象",ref C.AutoFocusTargetID);
        }
        if(group=="先行")
        {
            save|=ImGui.SliderFloat("先行入力を受け付ける残り時間（秒）",ref C.QueueThreshold,.1f,2.5f);
            save|=ImGui.SliderFloat("入力の差し替えを止める残り時間（秒）",ref C.QueueLockThreshold,0,2.5f);
            save|=ImGui.SliderFloat("同じスキルの連続入力を止める時間（秒）",ref C.QueueActionLockout,0,2.5f);
            ImGui.TextWrapped("初期値は受付0.5秒・差し替え停止0.5秒・連続入力停止0秒です。調整は『先行入力の詳細調整』がONの時に適用されます。");
        }
        if(group=="詳細")
        {
            ImGui.Separator();ImGui.Text("スキルをホットバーに配置");
            ImGui.TextWrapped("置き換え先のスロットを確認してください。派生スキルは、ベル起爆：28509 / アーサリースター起爆：8324 です。");
            ImGui.SliderInt("ホットバー番号",ref bar,1,18);ImGui.SliderInt("スロット番号",ref slot,1,bar<=10?12:16);
            PlacementActionPicker(ref placedAction);
            if(ImGui.Button("この位置に配置…")&&placedAction>0)ImGui.OpenPopup("配置の確認");
            if(ImGui.BeginPopup("配置の確認"))
            {
                ImGui.TextWrapped($"ホットバー{bar}の{slot}番を「{ActionName(placedAction)}」に置き換えます。");
                if(ImGui.Button("置き換える")){Game.SetHotbarSlot(bar-1,slot-1,1,placedAction);message="ホットバーに配置しました。";ImGui.CloseCurrentPopup();}
                ImGui.EndPopup();
            }
        }
        if(save){ApplyPatches();C.Save();}
    }
    private static void ApplyPatches()
    {
        Set(Game.queueGroundTargetsPatch,C.EnableGroundTargetQueuing);
        Set(Game.spellAutoAttackPatch,C.EnableSpellAutoAttacks&&C.EnableSpellAutoAttacksOutOfCombat);
        Set(Game.waitSyntaxDecimalPatch,C.EnableFractionality);Set(Game.waitCommandDecimalPatch,C.EnableFractionality);
        Set(Game.allowUnassignableActionsPatch,C.EnableUnassignableActions);
    }
    private static void Set(AsmPatch patch,bool enabled){if(!patch.IsValid)return;if(enabled)patch.Enable();else patch.Disable();}
    private static void Targets()
    {
        Title("対象一覧", "対象ルールで選べる対象です。マクロ用の記号はこのまま使用できます。");
        foreach(var (placeholder,target) in PronounManager.CustomPlaceholders)ImGui.TextWrapped($"{target.Name}  —  {placeholder}");
        ImGui.Separator();ImGui.TextWrapped("HP優先の対象は、生存していてHPが減ったパーティメンバーから選びます。ロール・ジョブ指定もパーティ内から選びます。");
    }
    private record Option(string Group,string Field,string Label,string Help);
    private static readonly Option[] Options = [
        new("操作","EnableTurboHotbars","ホットバーのキーを長押しで繰り返す","キーボードのみ対応。文字マクロも繰り返されるため除外設定を使えます。"),
        new("操作","EnableTurboHotbarsOutOfCombat","長押しを非戦闘中にも使う","戦闘していない時にもキーの繰り返しを有効にします。"),
        new("操作","EnableInstantGroundTarget","地面設置をカーソル位置で即確定","対象ルールが位置を指定していない時に使います。"),
        new("操作","EnableBlockMiscInstantGroundTargets","ペット配置などは即確定から除外","通常の地面設置スキル以外は、手動で位置を決めます。"),
        new("操作","EnableEnhancedAutoFaceTarget","回復などでは自動で向きを変えない","対象に向く必要がないスキルで向きを維持します。"),
        new("操作","EnableCameraRelativeDirectionals","方向指定スキルをカメラの向きに合わせる","パッセージ・オブ・アームズなどの方向を変更します。"),
        new("操作","EnableCameraRelativeDashes","移動スキルをカメラの向きに合わせる","アン・アヴァンなどの移動方向を変更します。"),
        new("操作","EnableNormalBackwardDashes","後方への移動は通常の向きを維持","イルーシブジャンプなどをカメラ方向への変更から外します。"),
        new("操作","EnableAutoDismount","スキルを押したらマウントから降りる","スキルの使用処理より先に降ります。"),
        new("操作","EnableAutoCastCancel","対象が倒れたら詠唱を中断","倒れた対象への詠唱を自動で止めます。"),
        new("操作","EnableAutoTarget","対象未選択なら近くの敵を選ぶ","敵を必要とするスキルを押した際に選択します。"),
        new("操作","EnableAutoChangeTarget","攻撃できない対象なら近くの敵へ変更","自動ターゲットがONの時に使います。"),
        new("操作","EnableAutoFocusTargetOutOfCombat","フォーカス自動設定を非戦闘中にも使う","下の自動設定する対象に従います。"),
        new("操作","EnableAutoRefocusTarget","見失ったフォーカス対象を再設定","コンテンツ内で同じ対象を再び探します。"),
        new("操作","EnableSpellAutoAttacks","魔法でもオートアタックを開始","ウェポンスキルと同様に攻撃を開始します。"),
        new("操作","EnableSpellAutoAttacksOutOfCombat","魔法からのAA開始を非戦闘中にも使う","回復などをきっかけに敵への攻撃が始まる場合があります。"),
        new("先行","EnableGroundTargetQueuing","地面設置スキルを先行入力","受付中に押した地面設置を使用可能になった時に実行します。"),
        new("先行","EnableQueuingMore","アイテム・LBも先行入力","通常は先行入力できない種類にも受付を広げます。"),
        new("先行","EnableMacroQueue","マクロの /ac を常に先行入力","個別に切り替える場合は /macroqueue on または off を使います。"),
        new("先行","EnableQueueAdjustments","先行入力の詳細調整（試験的）","下の秒数設定や差し替えなどを適用します。"),
        new("先行","EnableGCDAdjustedQueueThreshold","受付時間を現在のGCDに合わせる","指定した秒数をGCDの長さに応じて調整します。"),
        new("先行","EnableRequeuing","先行入力済みのスキルを差し替える","差し替え停止の残り時間に達するまで別の入力を受け付けます。"),
        new("先行","EnableSlidecastQueuing","詠唱の終盤にも先行入力","詠唱の最後の約0.5秒で入力を受け付けます。"),
        new("詳細","EnableFractionality","マクロの待機に小数を使う","/wait 0.5 や <wait.0.5> に対応し、60秒の上限を外します。"),
        new("詳細","EnablePlayerNamesInCommands","対象の名前とワールド名を指定できるようにする","対象を使うコマンドで First Last@World 形式を使います。"),
        new("詳細","EnableUnassignableActions","通常指定できない派生スキルを /ac で使用","起爆などの派生スキルを直接指定できるようにします。"),
        new("詳細","EnableFrameAlignment","フレームとGCD・硬直の終了をそろえる","終了時に小さな描画の引っかかりが発生する場合があります。"),
        new("詳細","EnableDecomboLiturgy","リタージー・オブ・ベルと起爆を別ボタンにする","起爆を別スロットへ配置して使います。"),
        new("詳細","EnableDecomboEarthlyStar","アーサリースターと起爆を別ボタンにする","起爆（ステラデトネーション / ID 8324）を別スロットへ配置して使います。"),
        new("詳細","EnableDecomboFireInRed","ファイア系の単体コンボを別ボタンにする","続くスキルもそれぞれホットバーに配置します。"),
        new("詳細","EnableDecomboFire2InRed","ファイラ系の範囲コンボを別ボタンにする","続くスキルもそれぞれホットバーに配置します。"),
        new("詳細","EnableDecomboBlizzardInCyan","ブリザド系の単体コンボを別ボタンにする","続くスキルもそれぞれホットバーに配置します。"),
        new("詳細","EnableDecomboBlizzard2InCyan","ブリザラ系の範囲コンボを別ボタンにする","続くスキルもそれぞれホットバーに配置します。"),
    ];
}
