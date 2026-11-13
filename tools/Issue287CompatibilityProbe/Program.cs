using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HarmonyLib;
using Mono.Cecil;

namespace Issue287CompatibilityProbe;

internal sealed record Finding(string Category, string Id, string Status, string Target,
    string Source, string Detail, object? Evidence = null);

internal sealed class Audit
{
    public const string OfficialSha = "A6CC282C52B96AD2E01D5692B1144B33AA8E3BA4E969582F648ECB13B2F751D7";
    public const string HarmonySha = "EF1898322C9F5C86DC1B0758B272A9C440823B4A41CA9A0B82A3AA6B3D206387";
    public const BindingFlags All = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public required string Root { get; init; }
    public required string Dependencies { get; init; }
    public required string ModPath { get; init; }
    public required string RunOutput { get; init; }
    public required Assembly Game { get; init; }
    public required Assembly Mod { get; init; }
    public required ModuleDefinition Metadata { get; init; }
    public List<Finding> Findings { get; } = new();
    public List<object> Inputs { get; } = new();
    public Dictionary<string, string> InitialHashes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<(MethodBase Target, MethodInfo Hook)>> Bindings { get; } = new();
    public List<(MethodBase Target, MethodInfo Hook, string Kind)> ExpectedPatches { get; } = new();
    private static string TypeIdentity(Type type) => type.IsByRef ? TypeIdentity(type.GetElementType()!) + "&"
        : type.IsArray ? TypeIdentity(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]"
        : type.IsGenericType ? type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(TypeIdentity)) + ">"
        : type.FullName ?? type.Name;
    public static object MethodIdentity(MethodBase method) => new
    {
        assembly = method.Module.Assembly.GetName().Name,
        declaringType = method.DeclaringType?.FullName,
        name = method.Name,
        returnType = method is MethodInfo info ? TypeIdentity(info.ReturnType) : "System.Void",
        parameterTypes = method.GetParameters().Select(p => TypeIdentity(p.ParameterType)).ToArray(),
        parameterNames = method.GetParameters().Select(p => p.Name).ToArray(),
        genericArity = method.IsGenericMethod ? method.GetGenericArguments().Length : 0,
        canonicalSignature = CanonicalSignature(method)
    };
    public static string CanonicalSignature(MethodBase method) =>
        (method is MethodInfo info ? TypeIdentity(info.ReturnType) : "System.Void") + " "
        + TypeIdentity(method.DeclaringType!) + "::" + method.Name
        + (method.IsGenericMethod ? "``" + method.GetGenericArguments().Length : "")
        + "(" + string.Join(",", method.GetParameters().Select(p => TypeIdentity(p.ParameterType))) + ")";
    private static object RuntimeIdentity(MethodBase method) => new { semantic = MethodIdentity(method),
        moduleMvid = method.Module.ModuleVersionId, metadataToken = method.MetadataToken,
        metadataTokenHex = "0x" + method.MetadataToken.ToString("X8") };
    public string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');
    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    public void Add(string category, string id, string status, string target, string source, string detail, object? evidence = null)
        => Findings.Add(new(category, id, status, target, source, detail, evidence));
    public void Input(string path, string role)
    {
        path = Path.GetFullPath(path);
        string sha = Hash(path);
        InitialHashes[path] = sha;
        Inputs.Add(new { role, path, sha256 = sha, bytes = new FileInfo(path).Length });
    }
    public IEnumerable<TypeDefinition> Types => Flatten(Metadata.Types);
    private static IEnumerable<TypeDefinition> Flatten(IEnumerable<TypeDefinition> types)
        => types.SelectMany(t => new[] { t }.Concat(Flatten(t.NestedTypes)));
    public MethodDefinition? Definition(MethodBase method)
        => Types.SelectMany(t => t.Methods).FirstOrDefault(m => m.MetadataToken.ToInt32() == method.MetadataToken);
    public string Location(MethodDefinition? method, int offset = int.MaxValue)
    {
        if (method == null) return "<no-source>";
        var points = method.DebugInformation.SequencePoints.Where(p => !p.IsHidden).ToArray();
        if (points.Length == 0)
        {
            var state = method.CustomAttributes.FirstOrDefault(c => c.AttributeType.FullName is
                "System.Runtime.CompilerServices.IteratorStateMachineAttribute" or "System.Runtime.CompilerServices.AsyncStateMachineAttribute");
            if (state?.ConstructorArguments.FirstOrDefault().Value is TypeReference reference)
                points = Types.FirstOrDefault(t => t.FullName == reference.FullName)?.Methods.Where(m => m.Name == "MoveNext")
                    .SelectMany(m => m.DebugInformation.SequencePoints).Where(p => !p.IsHidden).ToArray() ?? points;
        }
        var point = points.LastOrDefault(p => p.Offset <= offset) ?? points.FirstOrDefault();
        if (point == null && method.DeclaringType.DeclaringType != null)
            point = method.DeclaringType.DeclaringType.Methods.SelectMany(m => m.DebugInformation.SequencePoints).FirstOrDefault(p => !p.IsHidden);
        if (point == null) return $"<metadata:{method.FullName}>";
        string path = point.Document.Url;
        int src = path.Replace('\\', '/').IndexOf("/shin-getter-mod-godot/", StringComparison.OrdinalIgnoreCase);
        if (src >= 0) path = Path.Combine(Root, path[(src + 1)..]);
        return Relative(path) + ":" + point.StartLine;
    }
    public string Location(MethodBase method) => Location(Definition(method), 0);
    public static string Signature(MethodBase m) => $"{m.DeclaringType?.FullName}::{m}";
    public static Exception Unwrap(Exception ex) => ex is TargetInvocationException { InnerException: not null } t ? Unwrap(t.InnerException!) : ex;
    public void Guard(string category, string id, string target, string source, Action action)
    {
        try { action(); }
        catch (Exception ex) { ex = Unwrap(ex); Add(category, id, "FAIL", target, source, ex.GetType().Name + ": " + ex.Message); }
    }
    private Finding Stable(Finding f)
    {
        string id = f.Id;
        if (f.Category is "type-ref" or "member-ref" or "override" or "non-game-override") id = f.Target.Replace('/', '+');
        if (f.Category is "harmony-target" or "harmony-parameters" or "transpiler" or "transpiler-native-layout")
            id = f.Id[..f.Id.LastIndexOf("->", StringComparison.Ordinal)] + "->" + f.Target;
        var token = Regex.Match(id, @"^\[Method:0x([0-9A-Fa-f]+)\]");
        if (token.Success)
        {
            int rid = Convert.ToInt32(token.Groups[1].Value, 16);
            id = Metadata.LookupToken(0x06000000 | rid) + id[token.Length..];
        }
        JsonNode? evidence = JsonSerializer.SerializeToNode(f.Evidence);
        void Strip(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                foreach (string key in new[] { "metadataToken", "metadataTokenHex", "moduleMvid" }) obj.Remove(key);
                foreach (var child in obj.ToArray()) Strip(child.Value);
            }
            else if (node is JsonArray array) foreach (var child in array) Strip(child);
        }
        Strip(evidence);
        return f with { Id = id, Evidence = evidence };
    }
    public bool Write(bool checkOnly)
    {
        foreach (var (path, before) in InitialHashes.ToArray())
            if (Hash(path) != before) Add("snapshot", Relative(path), "FAIL", path, Relative(path), "Input changed during audit; rerun on the final build.");
        string jsonPath = Path.Combine(Root, ".github", "issue-287-dynamic-audit.json");
        string mdPath = Path.ChangeExtension(jsonPath, ".md");
        var stable = Findings.Select(Stable).OrderBy(f => f.Category, StringComparer.Ordinal).ThenBy(f => f.Id, StringComparer.Ordinal).ThenBy(f => f.Source, StringComparer.Ordinal).ToArray();
        var counts = stable.GroupBy(f => f.Category).Select(g => new { category = g.Key, total = g.Count(),
            pass = g.Count(f => f.Status == "PASS"), fail = g.Count(f => f.Status == "FAIL"),
            unresolved = g.Count(f => f.Status == "UNRESOLVED"), scopedOut = g.Count(f => f.Status == "SCOPED_OUT") }).ToArray();
        var failures = stable.Where(f => f.Status == "FAIL").ToArray();
        var unresolved = stable.Where(f => f.Status == "UNRESOLVED").ToArray();
        var unresolvedDeclarations = stable.Where(f => f.Category == "harmony-class" && f.Status != "PASS").ToArray();
        string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        string[] Sorted(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToArray();
        var graph = new SortedDictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["gameTypeReferences"] = Sorted(stable.Where(f => f.Category == "type-ref").Select(f => f.Target.Replace('/', '+'))),
            ["gameMemberReferences"] = Sorted(stable.Where(f => f.Category == "member-ref").Select(f => f.Target.Replace('/', '+'))),
            ["gameOverrides"] = Sorted(stable.Where(f => f.Category == "override").Select(f => f.Target + "->" + JsonSerializer.Serialize(f.Evidence))),
            ["harmonyCallbackEdges"] = Sorted(ExpectedPatches.Select(p => p.Kind + "|" + CanonicalSignature(p.Target) + "|" + CanonicalSignature(p.Hook))),
            ["dynamicBindings"] = Sorted(stable.Where(f => f.Category is "dynamic" or "reflection-signature" or "history-boundary").Select(f => f.Category + "|" + f.Target + "|" + f.Status))
        };
        var semanticFingerprint = new { algorithm = "SHA-256 over canonical sorted binding graph; excludes product SHA/MVID/tokens/time/SourceRevisionId",
            aggregateSha256 = Digest(JsonSerializer.Serialize(graph)),
            groups = graph.Select(g => new { category = g.Key, count = g.Value.Length, sha256 = Digest(JsonSerializer.Serialize(g.Value)) }).ToArray() };
        var expectedHarmonyRegistry = new
        {
            schema = "issue287-expected-harmony-registry-v2",
            interpretation = "DECLARATION CONTRACT ONLY. Match canonical signatures and owner, NOT product MVID/token/SHA. Actual engineering identities are in the external run report. Not proof of installed hooks.",
            owner = "Artanis.ShinGetterMod",
            status = unresolvedDeclarations.Length > 0 ? "INCOMPLETE_DECLARATIONS" : "ALL_DECLARED_TARGETS_RESOLVED",
            gameSha256 = OfficialSha, semanticSha256 = semanticFingerprint.aggregateSha256,
            originalCount = ExpectedPatches.Select(p => p.Target).Distinct().Count(),
            callbackEdges = ExpectedPatches.Count,
            offlineParameterFailures = Findings.Count(f => f.Category == "harmony-parameters" && f.Status == "FAIL"),
            offlineTranspilerFailures = Findings.Count(f => f.Category == "transpiler" && f.Status == "FAIL"),
            unresolvedDeclarations,
            expected = ExpectedPatches.GroupBy(p => p.Target).OrderBy(g => Signature(g.Key)).Select(g => new
            {
                original = MethodIdentity(g.Key),
                prefix = g.Where(p => p.Kind == "Prefix").OrderBy(p => CanonicalSignature(p.Hook), StringComparer.Ordinal).Select(p => new { callback = MethodIdentity(p.Hook), source = Location(p.Hook) }).ToArray(),
                postfix = g.Where(p => p.Kind == "Postfix").OrderBy(p => CanonicalSignature(p.Hook), StringComparer.Ordinal).Select(p => new { callback = MethodIdentity(p.Hook), source = Location(p.Hook) }).ToArray(),
                transpiler = g.Where(p => p.Kind == "Transpiler").OrderBy(p => CanonicalSignature(p.Hook), StringComparer.Ordinal).Select(p => new { callback = MethodIdentity(p.Hook), source = Location(p.Hook) }).ToArray(),
                finalizer = g.Where(p => p.Kind == "Finalizer").OrderBy(p => CanonicalSignature(p.Hook), StringComparer.Ordinal).Select(p => new { callback = MethodIdentity(p.Hook), source = Location(p.Hook) }).ToArray()
            }).ToArray(),
            exclusions = new[] { "St2bs optional adapter: not loaded, not installed, outside ShinGetter-only native registry contract." }
        };
        var doc = new { schema = "issue287-official113-semantic-audit-v2",
            verdict = failures.Length > 0 ? "FAIL" : unresolved.Length > 0 ? "PARTIAL_WITH_UNRESOLVED" : "OFFLINE_GATES_PASS_ONLY",
            scope = "ShinGetterMod only. No game launch, no Entry.Init, no Patch/PatchAll, no gameplay, no shared deployment.",
            pins = new { officialGameSha256 = OfficialSha, harmonySha256 = HarmonySha },
            semanticFingerprint, normalizedBindingGraph = graph,
            counts, failures = failures.Length, unresolved = unresolved.Length, expectedHarmonyRegistry, findings = stable,
            attribution = "Grid ReflectedType mismatch was introduced temporarily by this adaptation's derived-type lookup, not an original 111 mod defect or official 113 break. The preserved external first-failure evidence must not be reclassified." };
        var options = new JsonSerializerOptions { WriteIndented = true };
        bool matches = true;
        string checkResult = "NOT_REQUESTED";
        if (checkOnly)
        {
            matches = File.Exists(jsonPath);
            if (matches)
            {
                using var baseline = JsonDocument.Parse(File.ReadAllText(jsonPath));
                matches = baseline.RootElement.TryGetProperty("semanticFingerprint", out var fingerprint)
                    && fingerprint.GetProperty("aggregateSha256").GetString() == semanticFingerprint.aggregateSha256;
            }
            checkResult = matches ? "SEMANTIC_MATCH" : "SEMANTIC_MISMATCH_OR_BASELINE_MISSING";
        }
        string runDirectory = Path.Combine(RunOutput, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ"));
        Directory.CreateDirectory(runDirectory);
        string runPath = Path.Combine(runDirectory, "run.json");
        File.WriteAllText(runPath, JsonSerializer.Serialize(new { schema = "issue287-engineering-run-v2", generatedUtc = DateTime.UtcNow,
            doc.verdict, doc.failures, doc.unresolved, checkResult, semanticFingerprint, inputs = Inputs,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            actualModuleIdentities = new { product = new { sha256 = InitialHashes[ModPath], moduleMvid = Mod.ManifestModule.ModuleVersionId },
                game = new { sha256 = InitialHashes[Game.Location], moduleMvid = Game.ManifestModule.ModuleVersionId } },
            actualRegistryIdentities = ExpectedPatches.Select(p => new { original = RuntimeIdentity(p.Target), callback = RuntimeIdentity(p.Hook), kind = p.Kind }).ToArray(), findings = Findings }, options), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(runDirectory, "semantic-audit.json"), JsonSerializer.Serialize(doc, options), new UTF8Encoding(false));
        if (!checkOnly)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(doc, options), new UTF8Encoding(false));
        }
        var md = new StringBuilder("# Issue 287: Official 113 Dynamic and Binary Audit\n\n");
        md.AppendLine($"Verdict: **{doc.verdict}**. Failures: {failures.Length}; unresolved: {unresolved.Length}.");
        md.AppendLine("\nOffline evidence only. `Entry.Init`, native `PatchClassProcessor.Patch` / `PatchAll`, Godot callbacks and gameplay were NOT executed. No other mod was loaded.\n");
        md.AppendLine("## Stable Identity\n");
        md.AppendLine($"- Official 113 DLL SHA-256 (strict pin): `{OfficialSha}`\n- Actual Harmony SHA-256 (strict pin): `{HarmonySha}`\n- Normalized binding graph SHA-256: `{semanticFingerprint.aggregateSha256}`");
        md.AppendLine("\nProduct DLL SHA/MVID/tokens, PDB SHA, timestamps, absolute input paths and raw per-run evidence are external engineering facts, not future semantic equality gates. Run reports are written under `E:/Work/outputs/issue113-beta-20261113/dynamic-probe-run/` by default. `--check` compares only the normalized binding graph and never updates this baseline.");
        md.AppendLine("\nGrid attribution: the temporary derived-type reflection lookup was introduced during this adaptation. Its ReflectedType mismatch was NOT a pre-existing 111 baseline defect or an official 113 break. The external first-FAIL snapshot is preserved separately.");
        md.AppendLine("\n## Counts\n\n| Category | Total | PASS | FAIL | Unresolved | Scoped out |\n|---|---:|---:|---:|---:|---:|");
        foreach (var c in counts) md.AppendLine($"| {c.category} | {c.total} | {c.pass} | {c.fail} | {c.unresolved} | {c.scopedOut} |");
        md.AppendLine("\n## Findings and Boundaries\n");
        foreach (var f in failures.Concat(unresolved)) md.AppendLine($"- **{f.Status}** `{f.Category}/{f.Id}` at `{f.Source}`: {f.Target}: {f.Detail.Replace('\n', ' ')}");
        md.AppendLine("\n## Reproduce\n\n```powershell\n" +
            "dotnet run --project tools/Issue287CompatibilityProbe -- --root . --dependencies 'E:\\SteamLibrary\\steamapps\\common\\Slay the Spire 2\\data_sts2_windows_x86_64'\n```\n");
        md.AppendLine("Use the audited compiled product DLL/PDB; this command does not rebuild the product. A changed source/PDB checksum is a failed gate, not a new accepted source version. Dependencies are read-only. See the JSON for every reference, override, patch hook, dynamic call site, source checksum, negative control and IL evidence.");
        md.AppendLine($"\n## Expected Runtime Registry Contract\n\nJSON `expectedHarmonyRegistry`: {expectedHarmonyRegistry.status}; {expectedHarmonyRegistry.originalCount} unique original methods / {expectedHarmonyRegistry.callbackEdges} production callback edges. Includes `ShinGetterMerchantWelcomePatch`. Compare canonical signatures and callback lists to actual native `Harmony.GetPatchInfo` under owner `Artanis.ShinGetterMod`, not rebuild-dependent product MVID/tokens. This is not an observed installed registry.");
        md.AppendLine("\n## Dynamic Call-Site Inventory\n\n| ID | Status | Source | Target | Conclusion |\n|---|---|---|---|---|");
        foreach (var f in Findings.Where(f => f.Category is "dynamic" or "dynamic-attribute" or "reflection-signature" or "dynamic-manual-patch"))
            md.AppendLine($"| {f.Id} | {f.Status} | `{f.Source}` | `{f.Target.Replace("|", "\\|")}` | {f.Detail.Replace("|", "\\|").Replace('\n', ' ')} |");
        md.AppendLine("\n## Added Files\n\n- `tools/Issue287CompatibilityProbe/Issue287CompatibilityProbe.csproj`\n- `tools/Issue287CompatibilityProbe/Program.cs`\n- `tools/Issue287CompatibilityProbe/BinaryAudit.cs`\n- `tools/Issue287CompatibilityProbe/DynamicAudit.cs`\n- `tools/Issue287CompatibilityProbe/HarmonyAudit.cs`\n- `tools/Issue287CompatibilityProbe/README.md`\n- `.github/issue-287-dynamic-audit.json`\n- `.github/issue-287-dynamic-audit.md`");
        if (!checkOnly) File.WriteAllText(mdPath, md.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(runDirectory, "run.md"), md + "\n## Engineering Inputs\n\n" + string.Join("\n", InitialHashes.Select(p => $"- `{p.Key}` SHA-256 `{p.Value}`")), new UTF8Encoding(false));
        Console.WriteLine(JsonSerializer.Serialize(new { doc.verdict, doc.failures, doc.unresolved, checkResult, semanticFingerprint, counts, jsonPath, mdPath, runPath }, options));
        return matches;
    }
}

internal static class Program
{
    public static int Main(string[] args)
    {
        string Option(string key, string fallback) { int i = Array.IndexOf(args, key); return i < 0 ? fallback : args[i + 1]; }
        string root = Path.GetFullPath(Option("--root", Environment.CurrentDirectory));
        string deps = Path.GetFullPath(Option("--dependencies", @"E:\SteamLibrary\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64"));
        string lib = Path.Combine(root, "shin-getter-mod-godot", "lib");
        string gamePath = Path.Combine(lib, "sts2.dll");
        string modPath = Path.GetFullPath(Option("--mod", Path.Combine(root, "shin-getter-mod-godot", "build", "ShinGetterMod.dll")));
        if (Audit.Hash(gamePath) != Audit.OfficialSha) throw new InvalidOperationException("Official 113 SHA gate failed.");
        if (Audit.Hash(typeof(Harmony).Assembly.Location) != Audit.HarmonySha) throw new InvalidOperationException("Actual 0Harmony SHA gate failed.");
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            if (name.Name == "sts2") return AssemblyLoadContext.Default.LoadFromAssemblyPath(gamePath);
            string p = Path.Combine(deps, name.Name + ".dll");
            return File.Exists(p) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(p) : null;
        };
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(lib);
        resolver.AddSearchDirectory(deps);
        resolver.AddSearchDirectory(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory());
        using var metadata = ModuleDefinition.ReadModule(modPath, new ReaderParameters { ReadSymbols = true, InMemory = true, AssemblyResolver = resolver });
        var audit = new Audit { Root = root, Dependencies = deps, ModPath = modPath, Metadata = metadata,
            RunOutput = Path.GetFullPath(Option("--run-output", @"E:\Work\outputs\issue113-beta-20261113\dynamic-probe-run")),
            Game = AssemblyLoadContext.Default.LoadFromAssemblyPath(gamePath), Mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(modPath) };
        audit.Input(gamePath, "official113");
        audit.Input(modPath, "compiled-product");
        audit.Input(Path.ChangeExtension(modPath, ".pdb"), "compiled-product-symbols");
        audit.Input(typeof(Harmony).Assembly.Location, "executed-harmony");
        audit.Input(typeof(ModuleDefinition).Assembly.Location, "metadata-reader");
        audit.Input(typeof(Program).Assembly.Location, "executed-audit-probe");
        audit.Input(Path.Combine(deps, "GodotSharp.dll"), "read-only-game-dependency");
        if (File.Exists(Path.Combine(deps, "sts2.dll"))) audit.Input(Path.Combine(deps, "sts2.dll"), "read-only-steam-official-comparison");
        foreach (var path in Directory.GetFiles(Path.Combine(root, "tools", "Issue287CompatibilityProbe"), "*", SearchOption.TopDirectoryOnly))
            audit.Input(path, "audit-probe-source");
        audit.Guard("gate", "binary-audit", "all game binary edges", "", () => BinaryAudit.Run(audit));
        audit.Guard("gate", "harmony-audit", "all actual Harmony declarations", "", () => HarmonyAudit.Run(audit));
        audit.Guard("gate", "dynamic-audit", "all source dynamic sites", "", () => DynamicAudit.Run(audit));
        audit.Add("runtime", "Entry.Init", "UNRESOLVED", "ShinGetterMod.Entry.Init", "shin-getter-mod-godot/src/Entry.cs:16", "NOT RUN: authorization excludes launching the game and native initialization; offline results do not certify initialization or PatchAll.");
        audit.Add("runtime", "native-patch-installation", "UNRESOLVED", "PatchClassProcessor.Patch / PatchAll", "shin-getter-mod-godot/src/Entry.cs:28", "NOT RUN. No production Harmony wrapper emitted or detour installed; expectedHarmonyRegistry is a declaration contract for independent native observation.");
        foreach (var assembly in AssemblyLoadContext.Default.Assemblies.Where(assembly => !assembly.IsDynamic && assembly.Location.StartsWith(deps + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            if (!audit.InitialHashes.ContainsKey(assembly.Location)) audit.Input(assembly.Location, "actually-loaded-read-only-game-dependency");
        audit.Add("environment", "managed-game-module-initializer", "SCOPED_OUT", "SentryGodotInitializer", "<official-game-assembly>",
            "Loading/reflection of official game metadata in CLR emits Sentry GDExtension not loaded ... skipping. This managed module-initializer warning is not a game launch, mod Entry.Init, native engine acceptance or PatchAll result.");
        bool semanticMatches = audit.Write(args.Contains("--check", StringComparer.Ordinal));
        return !semanticMatches ? 2 : audit.Findings.Any(f => f.Status == "FAIL") ? 1 : 0;
    }
}
