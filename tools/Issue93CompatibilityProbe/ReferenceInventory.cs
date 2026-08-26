using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Text.Json;

internal static class ReferenceInventory
{
    private const string GameAssemblyName = "sts2";

    public static void Write(string assemblyPath)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        MetadataReader reader = peReader.GetMetadataReader();
        var provider = new TypeNameProvider(reader);

        var types = new SortedSet<string>(StringComparer.Ordinal);
        foreach (TypeReferenceHandle handle in reader.TypeReferences)
        {
            if (provider.GetAssemblyName(handle) == GameAssemblyName)
                types.Add(provider.GetTypeFromReference(reader, handle, 0));
        }

        var members = new SortedSet<MemberRecord>(MemberRecordComparer.Instance);
        foreach (MemberReferenceHandle handle in reader.MemberReferences)
        {
            MemberReference member = reader.GetMemberReference(handle);
            string? owner = provider.GetMemberOwner(member.Parent);
            if (owner is null || !provider.IsGameType(member.Parent))
                continue;

            string name = reader.GetString(member.Name);
            string signature;
            try
            {
                BlobReader blob = reader.GetBlobReader(member.Signature);
                SignatureHeader header = blob.ReadSignatureHeader();
                signature = header.Kind switch
                {
                    SignatureKind.Field => member.DecodeFieldSignature(provider, null),
                    SignatureKind.Method => FormatMethod(member.DecodeMethodSignature(provider, null)),
                    _ => Convert.ToHexString(reader.GetBlobBytes(member.Signature)),
                };
            }
            catch (Exception)
            {
                signature = Convert.ToHexString(reader.GetBlobBytes(member.Signature));
            }

            members.Add(new MemberRecord(
                owner,
                name,
                signature,
                member.Parent.Kind == HandleKind.TypeSpecification
                    ? "type-specification"
                    : "type-reference"));
        }

        Assembly modAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(
            Path.GetFullPath(assemblyPath));
        var overrides = new SortedSet<OverrideRecord>(OverrideRecordComparer.Instance);
        foreach (Type type in modAssembly.GetTypes())
        {
            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Instance
                | BindingFlags.DeclaredOnly))
            {
                if (!method.IsVirtual)
                    continue;
                MethodInfo baseMethod = method.GetBaseDefinition();
                if (baseMethod == method
                    || baseMethod.DeclaringType?.Assembly.GetName().Name != GameAssemblyName)
                {
                    continue;
                }
                overrides.Add(new OverrideRecord(
                    type.FullName ?? type.Name,
                    method.Name,
                    baseMethod.DeclaringType.FullName ?? baseMethod.DeclaringType.Name,
                    baseMethod.Name,
                    FormatReflectionMethod(baseMethod)));
            }
        }

        var document = new
        {
            assembly = Path.GetFullPath(assemblyPath),
            assembly_sha256 = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assemblyPath))),
            game_type_reference_count = types.Count,
            game_member_reference_count = members.Count,
            direct_game_member_reference_count = members.Count(member => member.owner_kind == "type-reference"),
            generic_game_member_reference_count = members.Count(member => member.owner_kind == "type-specification"),
            game_type_references = types,
            game_member_references = members,
            game_override_count = overrides.Count,
            game_overrides = overrides,
        };
        Console.WriteLine(JsonSerializer.Serialize(document));
    }

    private static string FormatMethod(MethodSignature<string> signature)
    {
        string generic = signature.GenericParameterCount == 0
            ? string.Empty
            : $"<{signature.GenericParameterCount}>";
        return $"{signature.ReturnType} {generic}({string.Join(", ", signature.ParameterTypes)})";
    }

    private static string FormatReflectionMethod(MethodInfo method) =>
        $"{method.ReturnType.FullName ?? method.ReturnType.Name} ("
        + string.Join(", ", method.GetParameters().Select(parameter =>
            parameter.ParameterType.FullName ?? parameter.ParameterType.Name))
        + ")";

    private sealed record MemberRecord(
        string owner,
        string name,
        string signature,
        string owner_kind);

    private sealed record OverrideRecord(
        string mod_type,
        string mod_method,
        string base_owner,
        string base_method,
        string base_signature);

    private sealed class MemberRecordComparer : IComparer<MemberRecord>
    {
        public static readonly MemberRecordComparer Instance = new();

        public int Compare(MemberRecord? left, MemberRecord? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;
            int owner = StringComparer.Ordinal.Compare(left.owner, right.owner);
            if (owner != 0)
                return owner;
            int name = StringComparer.Ordinal.Compare(left.name, right.name);
            if (name != 0)
                return name;
            int signature = StringComparer.Ordinal.Compare(left.signature, right.signature);
            return signature != 0
                ? signature
                : StringComparer.Ordinal.Compare(left.owner_kind, right.owner_kind);
        }
    }

    private sealed class OverrideRecordComparer : IComparer<OverrideRecord>
    {
        public static readonly OverrideRecordComparer Instance = new();

        public int Compare(OverrideRecord? left, OverrideRecord? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;
            return StringComparer.Ordinal.Compare(
                $"{left.base_owner}\0{left.base_method}\0{left.base_signature}\0{left.mod_type}\0{left.mod_method}",
                $"{right.base_owner}\0{right.base_method}\0{right.base_signature}\0{right.mod_type}\0{right.mod_method}");
        }
    }

    private sealed class TypeNameProvider : ISignatureTypeProvider<string, object?>
    {
        private readonly MetadataReader _reader;

        public TypeNameProvider(MetadataReader reader)
        {
            _reader = reader;
        }

        public string? GetAssemblyName(TypeReferenceHandle handle)
        {
            TypeReference type = _reader.GetTypeReference(handle);
            EntityHandle scope = type.ResolutionScope;
            while (scope.Kind == HandleKind.TypeReference)
            {
                type = _reader.GetTypeReference((TypeReferenceHandle)scope);
                scope = type.ResolutionScope;
            }

            return scope.Kind == HandleKind.AssemblyReference
                ? _reader.GetString(_reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name)
                : null;
        }

        public bool IsGameType(EntityHandle handle)
        {
            return handle.Kind switch
            {
                HandleKind.TypeReference =>
                    GetAssemblyName((TypeReferenceHandle)handle) == GameAssemblyName,
                HandleKind.TypeSpecification =>
                    GetMemberOwner(handle)?.StartsWith("MegaCrit.Sts2.", StringComparison.Ordinal) == true,
                _ => false,
            };
        }

        public string? GetMemberOwner(EntityHandle handle)
        {
            return handle.Kind switch
            {
                HandleKind.TypeReference =>
                    GetTypeFromReference(_reader, (TypeReferenceHandle)handle, 0),
                HandleKind.TypeSpecification =>
                    GetTypeFromSpecification(_reader, null, (TypeSpecificationHandle)handle, 0),
                _ => null,
            };
        }

        public string GetArrayType(string elementType, ArrayShape shape) =>
            $"{elementType}[{new string(',', Math.Max(shape.Rank - 1, 0))}]";

        public string GetByReferenceType(string elementType) => $"{elementType}&";

        public string GetFunctionPointerType(MethodSignature<string> signature) =>
            $"fnptr{FormatMethod(signature)}";

        public string GetGenericInstantiation(
            string genericType,
            ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(", ", typeArguments)}>";

        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";

        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) =>
            $"{unmodifiedType} {(isRequired ? "modreq" : "modopt")}({modifier})";

        public string GetPinnedType(string elementType) => $"{elementType} pinned";

        public string GetPointerType(string elementType) => $"{elementType}*";

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

        public string GetSZArrayType(string elementType) => $"{elementType}[]";

        public string GetTypeFromDefinition(
            MetadataReader reader,
            TypeDefinitionHandle handle,
            byte rawTypeKind)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            string name = reader.GetString(type.Name);
            string ns = reader.GetString(type.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        public string GetTypeFromReference(
            MetadataReader reader,
            TypeReferenceHandle handle,
            byte rawTypeKind)
        {
            TypeReference type = reader.GetTypeReference(handle);
            string name = reader.GetString(type.Name);
            string ns = reader.GetString(type.Namespace);
            if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                string parent = GetTypeFromReference(
                    reader,
                    (TypeReferenceHandle)type.ResolutionScope,
                    rawTypeKind);
                return $"{parent}+{name}";
            }
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        public string GetTypeFromSpecification(
            MetadataReader reader,
            object? genericContext,
            TypeSpecificationHandle handle,
            byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
    }
}
