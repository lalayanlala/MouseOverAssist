using MouseOverAssist;
var scenarios = new (bool enabled,uint job,uint current,bool expected,string name)[] {
    (true,24,24,true,"白魔道士ルールを白魔道士で使用"),
    (true,24,28,false,"白魔道士ルールを学者に適用しない"),
    (true,0,40,true,"全ジョブルールを賢者で使用"),
    (false,0,24,false,"OFFの全ジョブルール"),
    (false,24,24,false,"OFFのジョブ別ルール"),
    (true,40,24,false,"賢者ルールを白魔道士に適用しない")
};
foreach(var s in scenarios)
    if(RulePolicy.Applies(s.enabled,s.job,s.current)!=s.expected) throw new Exception(s.name);
Console.WriteLine("6 job-scope behavior checks passed.");


var pickerModes = new[]
{
    // shared, supports current, current checkbox, shared checkbox, expected
    (false, true,  true,  true,  true,  "両方ON: 現在ジョブ専用技"),
    (true,  true,  true,  true,  true,  "両方ON: 現在ジョブで使用可能な共通・ロール技"),
    (true,  false, true,  true,  false, "両方ON: 現在ジョブで使用できない共通・ロール技は除外"),
    (false, false, true,  true,  false, "両方ON: 他ジョブ専用技は除外"),
    (false, true,  true,  false, true,  "現在ジョブのみ: 現在ジョブ専用技"),
    (true,  true,  true,  false, false, "現在ジョブのみ: 共通・ロール技は除外"),
    (false, false, true,  false, false, "現在ジョブのみ: 他ジョブ専用技は除外"),
    (true,  true,  false, true,  true,  "共通・ロール技のみ: ロール技"),
    (false, true,  false, true,  false, "共通・ロール技のみ: 現在ジョブ専用技は除外"),
    (false, false, false, false, true,  "チェック無し: 他ジョブ専用技も表示"),
    (true,  false, false, false, true,  "チェック無し: 共通・ロール技も表示")
};
foreach (var t in pickerModes)
    if (RulePolicy.ActionPickerApplies(t.Item1, t.Item2, t.Item3, t.Item4) != t.Item5)
        throw new Exception(t.Item6);
Console.WriteLine("11 action-picker checkbox mode checks passed.");

var sharedRoleKinds = new[]
{
    // isRoleAction, battleJobCount, hasExplicitClassJob, expected
    (true,  8, false, true,  "戦闘ロール技は共通・ロール技として扱う"),
    (true,  0, false, false, "ギャザクラのロール技は除外"),
    (false, 8, false, true,  "複数戦闘ジョブ共通技は共通技として扱う"),
    (false, 1, true,  false, "ジョブ専用技は共通技にしない"),
    (false, 0, false, false, "戦闘ジョブ非対応の共有データは除外")
};
foreach (var t in sharedRoleKinds)
    if (RulePolicy.IsBattleSharedOrRole(t.Item1, t.Item2, t.Item3) != t.Item4)
        throw new Exception(t.Item5);
Console.WriteLine("5 battle-only shared/role classification checks passed.");


var searchCases = new[]
{
    ("暗", "ブラックナイト", "7393", "70", new[] { "暗黒騎士" }, true, "ジョブ名の部分一致"),
    ("暗黒騎士", "ブラックナイト", "7393", "70", new[] { "暗黒騎士" }, true, "ジョブ名の完全一致"),
    ("ブラ", "ブラックナイト", "7393", "70", new[] { "暗黒騎士" }, true, "技名検索"),
    ("7393", "ブラックナイト", "7393", "70", new[] { "暗黒騎士" }, true, "ID検索"),
    ("70", "ブラックナイト", "7393", "70", new[] { "暗黒騎士" }, true, "習得Lv検索"),
    ("白", "ブラックナイト", "7393", "70", new[] { "暗黒騎士" }, false, "別ジョブ名は不一致")
};
foreach (var t in searchCases)
    if (RulePolicy.ActionSearchMatches(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5) != t.Item6)
        throw new Exception(t.Item7);
Console.WriteLine("6 action-search job-name checks passed.");

var old=Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>("""
{"Version":1,"Enabled":false,"Jobs":{"24":{"Enabled":true,"Mode":0,"Actions":{"120":false,"135":true}},"28":{"Enabled":false,"Mode":2,"Actions":{}}}}
""");
old.Initialize();
if(old.EnableTargetRules||old.ActionStacks.Count!=2||old.Jobs!=null)throw new Exception("V1 enabled state or migration count");
var white=old.ActionStacks.Single(x=>x.JobId==24);
if(!white.Enabled||!white.ExcludedActions.SequenceEqual(new uint[]{120})||!white.Items.Select(x=>x.TargetID).SequenceEqual(new uint[]{10002,10003}))throw new Exception("V1 WHM settings lost");
var scholar=old.ActionStacks.Single(x=>x.JobId==28);
if(scholar.Enabled||!scholar.Items.Select(x=>x.TargetID).SequenceEqual(new uint[]{10003}))throw new Exception("V1 model-only disabled SCH settings lost");
old.Initialize();if(old.ActionStacks.Count!=2)throw new Exception("Migration repeated");
var exported=Configuration.ExportActionStack(white);var roundTrip=Configuration.ImportActionStack(exported);
if(!exported.StartsWith("MA_")||roundTrip.JobId!=24||roundTrip.ExcludedActions.Single()!=120)throw new Exception("Sharing lost job/disabled skills");
var legacy=Util.CompressString("""{"$type":"s","Name":"Legacy","Actions":[{"$type":"a","ID":2}],"Items":[{"$type":"i","TargetID":10002}]}""","RE_");
if(Configuration.ImportActionStack(legacy).Items.Single().TargetID!=10002)throw new Exception("ReAction sharing incompatible");
try { Configuration.ImportActionStack(Util.CompressString("""{"$type":"System.IO.FileInfo, System.Private.CoreLib"}""","MA_"));throw new Exception("Unexpected type accepted"); }
catch(Newtonsoft.Json.JsonSerializationException) {}
Console.WriteLine("V1 migration, idempotency, sharing round trip, ReAction import and type allowlist passed.");

var diagnosticsStacks = new List<Configuration.ActionStack>
{
    new()
    {
        Name = "上のルール",
        JobId = 24,
        ModifierKeys = 0,
        Actions = [new Configuration.Action { ID = 0 }],
        Items = [new Configuration.ActionStackItem { TargetID = 10002 }]
    },
    new()
    {
        Name = "下のルール",
        JobId = 24,
        ModifierKeys = 0,
        Actions = [new Configuration.Action { ID = 120 }],
        Items = [new Configuration.ActionStackItem { TargetID = 10003 }]
    },
    new()
    {
        Name = "空ルール",
        JobId = 28,
        Actions = [],
        Items = []
    }
};
var shadowed = RuleDiagnostics.GetForStack(diagnosticsStacks, 1);
if (!shadowed.Any(x => x.Message.Contains("判定が届かない"))) throw new Exception("Shadowed rule warning missing");
var empty = RuleDiagnostics.GetForStack(diagnosticsStacks, 2);
if (empty.Count(x => x.Severity == RuleDiagnosticSeverity.Error) != 2) throw new Exception("Empty rule diagnostics missing");

var redundant = new Configuration.ActionStack
{
    Name = "重複チェック",
    Actions = [new Configuration.Action { ID = 0 }, new Configuration.Action { ID = 120 }],
    Items = [new Configuration.ActionStackItem { TargetID = 10002 }]
};
var redundantWarnings = RuleDiagnostics.GetForStack(new List<Configuration.ActionStack> { redundant }, 0);
if (!redundantWarnings.Any(x => x.Message.Contains("すべてのスキル"))) throw new Exception("Wildcard redundancy warning missing");


var modifierShadow = new List<Configuration.ActionStack>
{
    new()
    {
        Name = "修飾なし上位",
        ModifierKeys = 0,
        Actions = [new Configuration.Action { ID = 120 }],
        Items = [new Configuration.ActionStackItem { TargetID = 10002 }]
    },
    new()
    {
        Name = "Shift下位",
        ModifierKeys = 1,
        Actions = [new Configuration.Action { ID = 120 }],
        Items = [new Configuration.ActionStackItem { TargetID = 10003 }]
    }
};
if (!RuleDiagnostics.GetForStack(modifierShadow, 1).Any(x => x.Message.Contains("判定が届かない")))
    throw new Exception("Modifier shadow warning missing");

var disabledIncomplete = new Configuration.ActionStack { Name = "無効ルール", Enabled = false, Actions = [], Items = [] };
var disabledDiagnostics = RuleDiagnostics.GetForStack(new List<Configuration.ActionStack> { disabledIncomplete }, 0);
if (disabledDiagnostics.Any(x => x.Severity == RuleDiagnosticSeverity.Error)) throw new Exception("Disabled incomplete rule should not be an error");
Console.WriteLine("Rule diagnostics checks passed.");
