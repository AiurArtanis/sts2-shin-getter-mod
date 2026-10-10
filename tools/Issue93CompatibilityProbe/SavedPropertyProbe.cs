using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class SavedPropertyProbe
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal static void Check(Assembly mod, Assembly game)
    {
        Type cache = game.GetType("MegaCrit.Sts2.Core.Multiplayer.Serialization.ModelIdSerializationCache", true)!;
        MethodInfo cacheType = cache.GetMethod("CacheSavedPropertiesForTypeDebug", All)!;
        var map = (IDictionary)cache.GetField("_savedPropertyCache", All)!.GetValue(null)!;
        var ids = (IDictionary)cache.GetField("_propertyNameToNetIdMap", All)!.GetValue(null)!;
        Type saved = game.GetType("MegaCrit.Sts2.Core.Saves.Runs.SavedProperties", true)!;
        MethodInfo from = saved.GetMethod("FromInternal", All)!;
        MethodInfo fill = saved.GetMethod("FillInternal", All)!;
        Type model = game.GetType("MegaCrit.Sts2.Core.Models.AbstractModel", true)!;
        FieldInfo mutable = model.GetField("<IsMutable>k__BackingField", All)!;
        var initialized = cache.GetField("_initialized", All)!;
        bool oldInitialized = (bool)initialized.GetValue(null)!;
        int properties = 0, roundtrips = 0;
        // Isolated managed metadata fixture only. It does not execute native Init
        // or claim packet width, game-load or save-account acceptance.
        try
        {
            foreach (Type type in mod.GetTypes().Where(t => model.IsAssignableFrom(t) && !t.IsAbstract))
            {
                var values = type.GetProperties(All).Where(p => p.CustomAttributes.Any(a => a.AttributeType.FullName == "MegaCrit.Sts2.Core.Saves.Runs.SavedPropertyAttribute")).ToArray();
                if (values.Length == 0) continue;
                cacheType.Invoke(null, new object[] { type });
                var cached = (IList?)map[type] ?? throw new InvalidOperationException("Beta native property discovery omitted " + type.Name);
                foreach (var property in values)
                {
                    if (!cached.Cast<PropertyInfo>().Any(p => p.Name == property.Name) || !ids.Contains(property.Name))
                        throw new InvalidOperationException("Missing Beta saved-property cache/ID: " + type.Name + "." + property.Name);
                    properties++;
                }
            }
            // Native FromInternal requires an initialized cache. Only this process
            // enables its already-populated debug cache; normal native Init is still
            // checked from source and later by main-task isolated initialization.
            initialized.SetValue(null, true);
            foreach (string name in new[] { "ShinGetterMod.Models.Relics.SGR_GetterFurnace", "ShinGetterMod.Models.Relics.SGR_EmperorsFragment" })
            {
                Type type = mod.GetType(name, true)!;
                object source = RuntimeHelpers.GetUninitializedObject(type);
                object restored = RuntimeHelpers.GetUninitializedObject(type);
                mutable.SetValue(source, true);
                mutable.SetValue(restored, true);
                var values = type.GetProperties(All).Where(p => p.CustomAttributes.Any(a => a.AttributeType.FullName == "MegaCrit.Sts2.Core.Saves.Runs.SavedPropertyAttribute")).ToArray();
                foreach (var p in values)
                    p.SetValue(source, p.PropertyType == typeof(bool) ? true : p.PropertyType == typeof(int) ? 7 : throw new InvalidOperationException("Review new save type " + p.PropertyType));
                object snapshot = from.Invoke(null, new object?[] { source, null }) ?? throw new InvalidOperationException("No native saved-property snapshot");
                fill.Invoke(snapshot, new[] { restored });
                foreach (var p in values)
                {
                    if (!Equals(p.GetValue(source), p.GetValue(restored))) throw new InvalidOperationException("Native field round-trip mismatch: " + p.Name);
                    roundtrips++;
                }
            }
        }
        finally { initialized.SetValue(null, oldInitialized); }
        Console.WriteLine($"issue#248 native save metadata fixture PASS: {properties} discovered properties/{roundtrips} actual model property roundtrips; NOT native initialization/packet/gameplay proof");
    }
}
