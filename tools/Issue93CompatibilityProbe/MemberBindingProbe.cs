using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

internal static class MemberBindingProbe
{
    // Resolves actual CLR tokens; does not instantiate a model or launch Godot.
    internal static void Check(Assembly mod, string output)
    {
        using var stream = File.OpenRead(mod.Location);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var records = new List<object>();
        foreach (var handle in reader.TypeReferences)
        {
            var type = reader.GetTypeReference(handle);
            EntityHandle scope = type.ResolutionScope;
            while (scope.Kind == HandleKind.TypeReference)
                scope = reader.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
            if (scope.Kind != HandleKind.AssemblyReference || reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name) != "sts2") continue;
            var resolved = mod.ManifestModule.ResolveType(MetadataTokens.GetToken(handle));
            records.Add(new { kind = "type", target = resolved.FullName });
        }
        foreach (var handle in reader.MemberReferences)
        {
            var reference = reader.GetMemberReference(handle);
            if (!ReferenceInventory.IsGameMember(reader, reference.Parent)) continue;
            MemberInfo? member;
            try { member = mod.ManifestModule.ResolveMember(MetadataTokens.GetToken(handle)); }
            catch (Exception ex) { throw new InvalidOperationException($"Unresolved CLR member {reader.GetString(reference.Name)}: {ex.Message}", ex); }
            if (member?.DeclaringType?.Assembly.GetName().Name != "sts2") continue;
            records.Add(new { kind = member.MemberType.ToString(), owner = member.DeclaringType.FullName,
                name = member.Name, signature = member.ToString() });
        }
        File.WriteAllText(output, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"issue#248 actual CLR reference token bindings PASS: {records.Count}; no game/model initialization");
    }
}
