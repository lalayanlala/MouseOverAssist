using System;
using System.Collections.Generic;
using System.Linq;
using ActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Hypostasis.Game.Structures;

namespace MouseOverAssist;

public static unsafe class ActionStackManager
{
    public delegate void PreUseActionEventDelegate(ActionManager* actionManager, ref uint actionType, ref uint actionID, ref ulong targetObjectID, ref uint param, ref uint useType, ref int pvp);
    public static event PreUseActionEventDelegate PreUseAction;
    public delegate void PreActionStackDelegate(ActionManager* actionManager, ref uint actionType, ref uint actionID, ref uint adjustedActionID, ref ulong targetObjectID, ref uint param, uint useType, ref int pvp, out bool? ret);
    public static event PreActionStackDelegate PreActionStack;
    public delegate void PostActionStackDelegate(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref ulong targetObjectID, uint param, uint useType, int pvp);
    public static event PostActionStackDelegate PostActionStack;
    public delegate void PostUseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ulong targetObjectID, uint param, uint useType, int pvp, bool ret);
    public static event PostUseActionDelegate PostUseAction;

    private static ulong queuedGroundTargetObjectID = 0;
    private static ActionDecisionSnapshot lastDecision = ActionDecisionSnapshot.Empty;

    public static ActionDecisionSnapshot LastDecision => lastDecision;

    public static void ClearDecision() => lastDecision = ActionDecisionSnapshot.Empty;

    public static Bool OnUseAction(ActionManager* actionManager, uint actionType, uint actionID, ulong targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
    {
        try
        {
            if (DalamudApi.ObjectTable.LocalPlayer == null) return 0;

            var inputActionID = actionID;
            var tryStack = useType == 0;
            if (useType == 100)
            {
                useType = 0;
                DalamudApi.LogDebug("UseAction called from a macro using /macroqueue");
            }

            PreUseAction?.Invoke(actionManager, ref actionType, ref actionID, ref targetObjectID, ref param, ref useType, ref pvp);

            var adjustedActionID = actionType == 1 ? actionManager->CS.GetAdjustedActionId(actionID) : actionID;

            DalamudApi.LogDebug($"UseAction called {actionType}, {actionID} -> {adjustedActionID}, {targetObjectID:X}, {param}, {useType}, {pvp}");

            bool? ret = null;
            PreActionStack?.Invoke(actionManager, ref actionType, ref actionID, ref adjustedActionID, ref targetObjectID, ref param, useType, ref pvp, out ret);
            if (ret.HasValue)
                return ret.Value;

            var succeeded = false;
            // 判定トレースは設定画面を開いている時だけ収集します。
            // 通常プレイ中のアクション実行ごとに文字列や List を生成しないための負荷対策です。
            var monitorThisInput = PluginUI.IsVisible && tryStack && actionType == 1;
            var trace = monitorThisInput ? new List<string>() : null;
            var decisionResult = string.Empty;
            var decisionStack = string.Empty;

            if (PluginModuleManager.GetModule<Modules.ActionStacks>().IsValid && MouseOverAssist.Config.EnableTargetRules && tryStack && actionType == 1 && MouseOverAssist.actionSheet.TryGetValue(adjustedActionID, out var a))
            {
                DalamudApi.LogDebug("Checking stacks");

                var modifierKeys = GetModifierKeys();
                var stackIndex = 0;
                foreach (var stack in MouseOverAssist.Config.ActionStacks)
                {
                    stackIndex++;
                    var stackName = string.IsNullOrWhiteSpace(stack.Name) ? $"ルール {stackIndex}" : stack.Name;
                    var currentJob = DalamudApi.ObjectTable.LocalPlayer.ClassJob.RowId;
                    if (!RulePolicy.Applies(stack.Enabled, stack.JobId, currentJob))
                    {
                        if (monitorThisInput)
                            trace.Add(!stack.Enabled
                                ? $"{stackIndex}. {stackName}: ルールが無効"
                                : $"{stackIndex}. {stackName}: ジョブ条件が不一致（現在 {currentJob} / 指定 {stack.JobId}）");
                        continue;
                    }
                    if (stack.ExcludedActions.Contains(actionID) || stack.ExcludedActions.Contains(adjustedActionID))
                    {
                        if (monitorThisInput) trace.Add($"{stackIndex}. {stackName}: このアクションは除外設定");
                        continue;
                    }

                    var exactMatch = (stack.ModifierKeys & 8) != 0;
                    if (exactMatch ? stack.ModifierKeys != modifierKeys : (stack.ModifierKeys & modifierKeys) != stack.ModifierKeys)
                    {
                        if (monitorThisInput) trace.Add($"{stackIndex}. {stackName}: 修飾キー条件が不一致");
                        continue;
                    }

                    if (!stack.Actions.Any(action
                            => action.ID == 0
                               || action.ID == 1 && a.CanTargetHostile
                               || action.ID == 2 && (a.CanTargetAlliance || a.CanTargetParty)
                               || (action.UseAdjustedID ? actionManager->CS.GetAdjustedActionId(action.ID) : action.ID) == adjustedActionID))
                    {
                        if (monitorThisInput) trace.Add($"{stackIndex}. {stackName}: 反応するアクション条件が不一致");
                        continue;
                    }

                    if (monitorThisInput)
                    {
                        trace.Add($"{stackIndex}. {stackName}: 条件一致");
                        decisionStack = stackName;
                    }

                    if (!CheckActionStack(adjustedActionID, stack, trace, out var newAction, out var newTarget))
                    {
                        if (stack.BlockOriginal)
                        {
                            DalamudApi.LogDebug("Stack failed, blocking original");
                            if (monitorThisInput)
                            {
                                decisionResult = "候補がすべて失敗したため、元アクションを停止";
                                SetDecision(inputActionID, adjustedActionID, 0, decisionStack, decisionResult, trace);
                            }
                            return 0;
                        }

                        if (monitorThisInput) decisionResult = "候補がすべて失敗したため、元アクションを使用";
                        break;
                    }

                    DalamudApi.LogDebug($"Stack succeeded {adjustedActionID} -> {newAction}, {targetObjectID:X} -> {newTarget:X}");

                    actionID = newAction;
                    adjustedActionID = newAction;
                    targetObjectID = newTarget;
                    succeeded = true;
                    if (monitorThisInput) decisionResult = $"候補を採用: {ActionLabel(newAction)}";
                    break;
                }

                if (monitorThisInput && string.IsNullOrEmpty(decisionResult))
                {
                    decisionResult = "一致するルールなし → 元アクションを使用";
                    if (trace.Count == 0)
                        trace.Add("有効なジョブ・修飾キー・反応アクション条件に一致するルールがありませんでした。");
                }
            }
            else if (monitorThisInput)
            {
                if (!MouseOverAssist.Config.EnableTargetRules)
                    decisionResult = "アクションスタックが無効 → 元アクションを使用";
                else if (!PluginModuleManager.GetModule<Modules.ActionStacks>().IsValid)
                    decisionResult = "Action Stack モジュールが利用不可 → 元アクションを使用";
                else
                    decisionResult = "対象外のアクション → 元アクションを使用";
            }

            if (monitorThisInput)
                SetDecision(inputActionID, actionType == 1 ? actionManager->CS.GetAdjustedActionId(inputActionID) : inputActionID, adjustedActionID, decisionStack, decisionResult, trace);

            PostActionStack?.Invoke(actionManager, actionType, actionID, adjustedActionID, ref targetObjectID, param, useType, pvp);

            ret = Game.UseActionHook.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

            PostUseAction?.Invoke(actionManager, actionType, actionID, adjustedActionID, targetObjectID, param, useType, pvp, ret!.Value);

            if (succeeded && MouseOverAssist.actionSheet[adjustedActionID].TargetArea)
            {
                actionManager->queuedGroundTargetObjectID = targetObjectID;
                queuedGroundTargetObjectID = targetObjectID;
            }
            else if (useType == 1 && queuedGroundTargetObjectID != 0)
            {
                actionManager->queuedGroundTargetObjectID = queuedGroundTargetObjectID;
                queuedGroundTargetObjectID = 0;
            }
            else
            {
                queuedGroundTargetObjectID = 0;
            }

            if (MouseOverAssist.Config.EnableInstantGroundTarget && !succeeded && queuedGroundTargetObjectID == 0)
                SetInstantGroundTarget(actionType, useType);

            return ret!.Value;
        }
        catch (Exception e)
        {
            DalamudApi.LogError($"Failed to modify action\n{e}");
            return 0;
        }
    }

    private static uint GetModifierKeys()
    {
        var keys = 8u;
        if (DalamudApi.KeyState[16]) // Shift
            keys |= 1;
        if (DalamudApi.KeyState[17]) // Ctrl
            keys |= 2;
        if (DalamudApi.KeyState[18]) // Alt
            keys |= 4;
        return keys;
    }

    private static bool CheckActionStack(uint id, Configuration.ActionStack stack, List<string> trace, out uint action, out ulong target)
    {
        action = 0;
        target = Game.InvalidObjectID;

        var useRange = stack.CheckRange;
        var useCooldown = stack.CheckCooldown;
        for (var i = 0; i < stack.Items.Count; i++)
        {
            var item = stack.Items[i];
            var newID = item.ID != 0 ? Common.ActionManager->CS.GetAdjustedActionId(item.ID) : id;
            var newTarget = PronounManager.GetGameObjectFromID(item.TargetID);

            if (newTarget == null)
            {
                if (trace != null) TraceCandidate(trace, i, item.TargetID, newID, "対象が見つからない");
                continue;
            }
            if (!CanUseAction(newID, newTarget))
            {
                if (trace != null) TraceCandidate(trace, i, item.TargetID, newID, "現在は使用不可");
                continue;
            }
            if (useRange && Game.IsActionOutOfRange(newID, newTarget))
            {
                if (trace != null) TraceCandidate(trace, i, item.TargetID, newID, "射程外");
                continue;
            }
            if (useCooldown && !Common.ActionManager->CanQueueAction(1, newID))
            {
                if (trace != null) TraceCandidate(trace, i, item.TargetID, newID, "リキャスト / 先行入力待ち");
                continue;
            }

            if (trace != null) TraceCandidate(trace, i, item.TargetID, newID, "採用");
            action = newID;
            target = Game.GetObjectID(newTarget);
            return true;
        }

        return false;
    }

    private static bool CanUseAction(uint id, GameObject* target)
        => ActionManager.CanUseActionOnGameObject(id, target) && Common.ActionManager->CS.GetActionStatus(ActionType.Action, id, target->GetGameObjectId(), false, false) == 0;

    private static void SetDecision(uint inputActionID, uint adjustedActionID, uint outputActionID, string stackName, string result, List<string> trace)
    {
        lastDecision = new ActionDecisionSnapshot
        {
            TimestampUtc = DateTime.UtcNow,
            InputActionID = inputActionID,
            AdjustedActionID = adjustedActionID,
            OutputActionID = outputActionID,
            StackName = stackName,
            Result = string.IsNullOrEmpty(result) ? "元アクションを使用" : result,
            Steps = trace?.Take(24).ToArray() ?? []
        };
    }

    private static void TraceCandidate(List<string> trace, int index, uint targetID, uint actionID, string result)
        => trace.Add($"  候補 {index + 1}: {PronounManager.GetPronounName(targetID)} / {ActionLabel(actionID)} → {result}");

    private static string ActionLabel(uint id)
    {
        var action = DalamudApi.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(id);
        var name = action?.Name.ToString();
        return string.IsNullOrWhiteSpace(name) ? $"ID {id}" : $"{name} [{id}]";
    }

    private static void SetInstantGroundTarget(uint actionType, uint useType)
    {
        if ((MouseOverAssist.Config.EnableBlockMiscInstantGroundTargets && actionType == 11) || useType == 2 && actionType == 1 || actionType == 15) return;

        DalamudApi.LogDebug($"Making ground target instant {actionType}, {useType}");

        Common.ActionManager->activateGroundTarget = 1;
    }
}
