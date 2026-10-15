using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MouseOverAssist;

public class Configuration : PluginConfiguration, IPluginConfiguration
{
    public class Action
    {
        public uint ID = 0;
        public bool UseAdjustedID = false;
    }

    public class ActionStackItem
    {
        public uint ID = 0;
        public uint TargetID = 10_000;
    }

    public class ActionStack
    {
        public string Name = string.Empty;
        public bool Enabled = true;
        public uint JobId = 0;
        public List<Action> Actions = [];
        public List<uint> ExcludedActions = [];
        public List<ActionStackItem> Items = [];
        public uint ModifierKeys = 0u;
        public bool BlockOriginal = false;
        public bool CheckRange = false;
        public bool CheckCooldown = false;
    }

    public class StackSerializer : DefaultSerializationBinder
    {
        private static readonly Type actionStackType = typeof(ActionStack);
        private static readonly Type actionStackItemType = typeof(ActionStackItem);
        private static readonly Type actionType = typeof(Action);
        private const string actionStackShortName = "s";
        private const string actionStackItemShortName = "i";
        private const string actionShortName = "a";
        private static readonly Dictionary<string, Type> types = new()
        {
            [actionStackType.FullName!] = actionStackType,
            [actionStackShortName] = actionStackType,
            ["ReAction.Configuration+ActionStack"] = actionStackType,
            [actionStackItemType.FullName!] = actionStackItemType,
            [actionStackItemShortName] = actionStackItemType,
            ["ReAction.Configuration+ActionStackItem"] = actionStackItemType,
            [actionType.FullName!] = actionType,
            [actionShortName] = actionType,
            ["ReAction.Configuration+Action"] = actionType
        };
        private static readonly Dictionary<Type, string> typeNames = new()
        {
            [actionStackType] = actionStackShortName,
            [actionStackItemType] = actionStackItemShortName,
            [actionType] = actionShortName
        };

        public override Type BindToType(string assemblyName, string typeName)
            => types.TryGetValue(typeName, out var t) ? t : throw new JsonSerializationException("未対応の型です。");

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = null;
            if (typeNames.TryGetValue(serializedType, out var name))
                typeName = name;
            else
                base.BindToName(serializedType, out assemblyName, out typeName);
        }
    }

    public int Version { get; set; } = 2;
    public bool EnableTargetRules = true;
    // V1 の設定は保持し、最初の起動時にルールへ移行します。
    public bool Enabled { get; set; } = true;
    public Dictionary<uint, LegacyJob> Jobs { get; set; }
    public class LegacyJob { public bool Enabled = true; public int Mode = 0; public Dictionary<uint, bool> Actions = []; }

    public List<ActionStack> ActionStacks = [];
    public bool EnableEnhancedAutoFaceTarget = false;
    public bool EnableAutoDismount = false;
    public bool EnableGroundTargetQueuing = false;
    public bool EnableInstantGroundTarget = false;
    public bool EnableBlockMiscInstantGroundTargets = false;
    public bool EnableAutoCastCancel = false;
    public bool EnableAutoTarget = false;
    public bool EnableAutoChangeTarget = false;
    public bool EnableSpellAutoAttacks = false;
    public bool EnableSpellAutoAttacksOutOfCombat = false;
    public bool EnableCameraRelativeDashes = false;
    public bool EnableNormalBackwardDashes = false;
    public bool EnableQueuingMore = false;
    public bool EnableFrameAlignment = false;
    public bool EnableAutoRefocusTarget = false;
    public bool EnableMacroQueue = false;
    public bool EnableFractionality = false;
    public bool EnablePlayerNamesInCommands = false;
    public bool EnableQueueAdjustments = false;
    public bool EnableRequeuing = false;
    public bool EnableSlidecastQueuing = false;
    public bool EnableGCDAdjustedQueueThreshold = false;
    public float QueueThreshold = 0.5f;
    public float QueueLockThreshold = 0.5f;
    public float QueueActionLockout = 0f;
    public bool EnableTurboHotbars = false;
    public int TurboHotbarInterval = 400;
    public int InitialTurboHotbarInterval = 0;
    public bool EnableTurboHotbarsOutOfCombat = false;
    public HashSet<uint> TurboHotbarBlacklist = [];
    public bool EnableCameraRelativeDirectionals = false;
    public bool EnableUnassignableActions = false;
    public uint AutoFocusTargetID = 0;
    public bool EnableAutoFocusTargetOutOfCombat = false;

    public bool EnableDecomboLiturgy = false;
    public bool EnableDecomboEarthlyStar = false;
    public bool EnableDecomboMinorArcana = false;
    public bool EnableDecomboFireInRed = false;
    public bool EnableDecomboFire2InRed = false;
    public bool EnableDecomboBlizzardInCyan = false;
    public bool EnableDecomboBlizzard2InCyan = false;

    public override void Initialize()
    {
        if (Jobs == null) return;
        EnableTargetRules = Enabled;
        foreach (var (job, settings) in Jobs)
        {
            var stack = new ActionStack { Name = $"V1 から移行（ジョブ {job}）", JobId = job, Enabled = settings.Enabled };
            stack.Actions.Add(new Action { ID = 2 });
            stack.ExcludedActions = settings.Actions.Where(x => !x.Value).Select(x => x.Key).ToList();
            if (settings.Mode != 2) stack.Items.Add(new ActionStackItem { TargetID = 10002 });
            if (settings.Mode != 1) stack.Items.Add(new ActionStackItem { TargetID = 10003 });
            ActionStacks.Add(stack);
        }
        Jobs = null;
        Version = 2;
    }

    private static readonly StackSerializer serializer = new ();

    private const string exportPrefix = "MA_";

    public static string ExportActionStack(ActionStack stack)
        => Util.CompressString(JsonConvert.SerializeObject(stack, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            SerializationBinder = serializer
        }), exportPrefix);

    public static ActionStack ImportActionStack(string import)
        => JsonConvert.DeserializeObject<ActionStack>(Util.DecompressString(import, import.StartsWith("RE_", StringComparison.Ordinal) ? "RE_" : exportPrefix), new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            SerializationBinder = serializer
        });
}