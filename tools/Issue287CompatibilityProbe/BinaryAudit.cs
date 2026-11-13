using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Issue287CompatibilityProbe;

internal static class BinaryAudit
{
    internal static bool GameType(TypeReference type)
    {
        while (type is TypeSpecification specification) type = specification.ElementType;
        while (type.DeclaringType != null) type = type.DeclaringType;
        return type.Scope.Name == "sts2";
    }

    internal static void Run(Audit a)
    {
        var methods = a.Types.SelectMany(t => t.Methods).ToArray();
        using var pdbStream = File.OpenRead(Path.ChangeExtension(a.ModPath, ".pdb"));
        using var pdbProvider = System.Reflection.Metadata.MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var pdb = pdbProvider.GetMetadataReader();
        foreach (var handle in pdb.Documents)
        {
            var document = pdb.GetDocument(handle);
            string path = pdb.GetString(document.Name);
            int index = path.Replace('\\', '/').IndexOf("/shin-getter-mod-godot/", StringComparison.OrdinalIgnoreCase);
            if (index >= 0) path = Path.Combine(a.Root, path[(index + 1)..]);
            string relative = a.Relative(path);
            if (!relative.StartsWith("shin-getter-mod-godot/src/", StringComparison.Ordinal)) continue;
            if (!File.Exists(path)) { a.Add("source-checksum", relative, "FAIL", path, relative, "PDB source missing."); continue; }
            a.Input(path, "pdb-verified-source");
            byte[] bytes = File.ReadAllBytes(path);
            Guid algorithm = pdb.GetGuid(document.HashAlgorithm);
            byte[] expected = pdb.GetBlobBytes(document.Hash);
            byte[] actual = algorithm.ToString() switch
            {
                "8829d00f-11b8-4213-878b-770e8597ac16" => SHA256.HashData(bytes),
                "ff1816ec-aa5e-4d10-87f7-6f4963833460" => SHA1.HashData(bytes),
                _ => Array.Empty<byte>()
            };
            bool equal = actual.Length > 0 && actual.SequenceEqual(expected);
            a.Add("source-checksum", relative, equal ? "PASS" : "FAIL", relative, relative,
                equal ? "Current source bytes equal compiled PDB document checksum." : "STALE BUILD: current source differs from product PDB; rebuild by product owner before final audit.",
                new { algorithm, pdbHash = Convert.ToHexString(expected), actualHash = Convert.ToHexString(actual) });
        }
        foreach (var path in Directory.GetFiles(Path.Combine(a.Root, "shin-getter-mod-godot", "src"), "*.cs", SearchOption.AllDirectories))
            if (!a.InitialHashes.ContainsKey(path)) a.Add("source-checksum", a.Relative(path), "FAIL", a.Relative(path), a.Relative(path), "Current source file is absent from compiled PDB documents.");

        var uses = methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions
            .Where(i => i.Operand is MemberReference).Select(i => (Method: m, Instruction: i, Member: (MemberReference)i.Operand)))
            .GroupBy(x => x.Member.MetadataToken.ToInt32()).ToDictionary(g => g.Key, g => g.Select(x => a.Location(x.Method, x.Instruction.Offset)).Distinct().ToArray());
        string[] Sources(MemberReference member) => uses.GetValueOrDefault(member.MetadataToken.ToInt32()) ?? Array.Empty<string>();
        foreach (var type in a.Metadata.GetTypeReferences().Where(GameType))
            a.Guard("type-ref", type.MetadataToken.ToString(), type.FullName, Sources(type).FirstOrDefault() ?? "<metadata-only>", () =>
            {
                var resolved = type.Resolve() ?? throw new TypeLoadException(type.FullName);
                var clr = a.Mod.ManifestModule.ResolveType(type.MetadataToken.ToInt32());
                if (clr.Assembly != a.Game || resolved.Module.Assembly.Name.Name != "sts2") throw new TypeLoadException("Wrong assembly binding.");
                a.Add("type-ref", type.MetadataToken.ToString(), "PASS", type.FullName, Sources(type).FirstOrDefault() ?? "<metadata-only>", "Cecil and actual CLR ResolveType bind official sts2.", new { resolved = clr.FullName, sources = Sources(type) });
            });
        foreach (var member in a.Metadata.GetMemberReferences().Where(m => GameType(m.DeclaringType)))
            a.Guard("member-ref", member.MetadataToken.ToString(), member.FullName, Sources(member).FirstOrDefault() ?? "<metadata-only>", () =>
            {
                MemberReference? definition = member switch { MethodReference m => m.Resolve(), FieldReference f => f.Resolve(), _ => null };
                if (definition == null) throw new MissingMemberException(member.FullName);
                var clr = a.Mod.ManifestModule.ResolveMember(member.MetadataToken.ToInt32()) ?? throw new MissingMemberException(member.FullName);
                if (clr.DeclaringType?.Assembly != a.Game) throw new MissingMemberException("Resolved outside official sts2.");
                if (member is MethodReference mr && definition is MethodDefinition md && (mr.HasThis != md.HasThis || mr.GenericParameters.Count != md.GenericParameters.Count))
                    throw new MissingMethodException("Calling convention or generic arity changed.");
                a.Add("member-ref", member.MetadataToken.ToString(), "PASS", member.FullName, Sources(member).FirstOrDefault() ?? "<metadata-only>",
                    "Cecil signature resolution + actual CLR ResolveMember succeeded; not invocation proof.", new { resolved = clr.ToString(), declaringType = clr.DeclaringType.FullName, sources = Sources(member) });
            });

        Type[] runtimeTypes;
        try { runtimeTypes = a.Mod.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            foreach (var error in ex.LoaderExceptions) a.Add("type-load", Guid.NewGuid().ToString(), "FAIL", "mod.GetTypes", "<binary>", error?.ToString() ?? "unknown");
            runtimeTypes = ex.Types.OfType<Type>().ToArray();
        }
        foreach (var type in runtimeTypes)
        foreach (var method in type.GetMethods(Audit.All | BindingFlags.DeclaredOnly).Where(m => m.IsVirtual))
        {
            var definition = a.Definition(method);
            bool reuseSlot = (method.Attributes & System.Reflection.MethodAttributes.NewSlot) == 0;
            var explicitGame = definition?.Overrides.Where(o => GameType(o.DeclaringType)).ToArray() ?? Array.Empty<MethodReference>();
            if (!reuseSlot && explicitGame.Length == 0) continue;
            var baseMethod = method.GetBaseDefinition();
            bool gameHierarchy = false;
            for (Type? b = type.BaseType; b != null; b = b.BaseType) gameHierarchy |= b.Assembly == a.Game;
            if (baseMethod.DeclaringType?.Assembly != a.Game && explicitGame.Length == 0 && !gameHierarchy) continue;
            string category = baseMethod.DeclaringType?.Assembly == a.Game || explicitGame.Length > 0 || baseMethod == method ? "override" : "non-game-override";
            a.Guard(category, type.FullName + "::" + method.MetadataToken, Audit.Signature(method), a.Location(method), () =>
            {
                if (reuseSlot && baseMethod == method) throw new MissingMethodException("Reused virtual slot no longer resolves to a base method.");
                foreach (var edge in explicitGame) if (edge.Resolve() == null) throw new MissingMethodException(edge.FullName);
                if (baseMethod.IsFinal) throw new TypeLoadException("Cannot override final base method.");
                a.Add(category, type.FullName + "::" + method.MetadataToken, "PASS", Audit.Signature(method), a.Location(method),
                    "CLR virtual-slot binding resolved; explicit MethodImpl edges checked with Cecil.", new { baseTarget = Audit.Signature(baseMethod), explicitEdges = explicitGame.Select(e => e.FullName).ToArray() });
            });
        }
        // Controls use the same official module and exact Cecil resolver as production references.
        var card = a.Metadata.GetTypeReferences().First(t => t.FullName == "MegaCrit.Sts2.Core.Models.CardModel");
        var missing = new MethodReference("__Issue287DefinitelyMissing", a.Metadata.TypeSystem.Void, card) { HasThis = true };
        var wrong = new MethodReference("SpendResources", a.Metadata.TypeSystem.Void, card) { HasThis = true };
        wrong.Parameters.Add(new ParameterDefinition(a.Metadata.TypeSystem.Int32));
        foreach (var (id, probe) in new[] { ("cecil-missing-member", missing), ("cecil-wrong-signature", wrong) })
        {
            bool rejected;
            try { rejected = probe.Resolve() == null; } catch (ResolutionException) { rejected = true; }
            a.Add("negative-control", id, rejected ? "PASS" : "FAIL", probe.FullName, "<probe>", rejected ? "Deliberately invalid reference rejected." : "FALSE POSITIVE: invalid reference accepted.");
        }
    }
}
