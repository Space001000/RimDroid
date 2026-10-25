// WaylithClr.LaxPrivateFieldNames for mods on stock .NET (2026-10-25).
//
// Mods written for RimWorld 1.6 (Mono, .NET Framework class library) reach private fields of the
// framework by their Mono names: XmlElement.name, List<T>._items is "_items" in both but
// Dictionary's "buckets" became "_buckets", and so on (.NET Core prefixed most private fields
// with '_'). Ludeon's runtime finds the field anyway; stock .NET does not, and Humanoid Alien Races
// failed in its static constructor (FieldRefAccess<IXmlSchemaInfo>(typeof(XmlElement), "name")),
// which broke pawn and world generation.
//
// Mods look fields up through Harmony's AccessTools almost always, so when 0Harmony loads, Harmony
// itself gets a postfix on AccessTools.Field and AccessTools.DeclaredField: a framework type's field
// that is not found by the asked name is looked up again by its .NET name ("_name" for "name",
// "_name" for "m_name", "name" for "_name"). Game and mod types are left alone, their names did not
// change. Everything is done through reflection: Harmony 2.x and 3.x both have these members.
using System;
using System.Collections.Generic;
using System.Reflection;

public static class RimDroidLaxFieldNames
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                             | BindingFlags.Static | BindingFlags.DeclaredOnly;
    static readonly HashSet<string> logged = new HashSet<string>();
    static bool patched;
    static Action<int, string> log;

    internal static void Install(Assembly harmony, Action<int, string> logger)
    {
        if (patched) return;
        log = logger;
        try
        {
            Type accessTools = harmony.GetType("HarmonyLib.AccessTools");
            Type harmonyType = harmony.GetType("HarmonyLib.Harmony");
            Type harmonyMethod = harmony.GetType("HarmonyLib.HarmonyMethod");
            if (accessTools == null || harmonyType == null || harmonyMethod == null) { log(5, "lax field names: Harmony types not found"); return; }
            object instance = Activator.CreateInstance(harmonyType, "rimdroid.laxfieldnames");
            MethodInfo patch = null;
            foreach (MethodInfo m in harmonyType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                ParameterInfo[] p = m.GetParameters();
                if (m.Name == "Patch" && p.Length >= 3 && p[0].ParameterType == typeof(MethodBase)) { patch = m; break; }
            }
            if (patch == null) { log(5, "lax field names: Harmony.Patch not found"); return; }
            object postfix = Activator.CreateInstance(harmonyMethod, typeof(RimDroidLaxFieldNames).GetMethod(nameof(Postfix)));
            int count = 0;
            foreach (string name in new[] { "Field", "DeclaredField" })
            {
                MethodInfo original = accessTools.GetMethod(name, new[] { typeof(Type), typeof(string) });
                if (original == null) continue;
                object[] args = new object[patch.GetParameters().Length];
                args[0] = original;
                args[2] = postfix;   // (original, prefix, postfix, ...)
                patch.Invoke(instance, args);
                count++;
            }
            patched = count > 0;
            log(4, "lax field names: " + count + " AccessTools lookups patched (Harmony " + harmony.GetName().Version + ")");
        }
        catch (Exception e)
        {
            log(5, "lax field names: cannot patch Harmony: " + (e.InnerException ?? e).Message);
        }
    }

    // Postfix on AccessTools.Field(Type type, string name) / DeclaredField: names must match.
    public static void Postfix(Type type, string name, ref FieldInfo __result)
    {
        if (__result != null || type == null || string.IsNullOrEmpty(name) || !IsFramework(type)) return;
        foreach (string alt in Alternatives(name))
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(alt, All);
                if (f == null) continue;
                __result = f;
                string key = type.FullName + "." + name;
                lock (logged)
                    if (logged.Add(key)) log?.Invoke(4, "lax field names: " + key + " found as " + alt);
                return;
            }
        }
    }

    static IEnumerable<string> Alternatives(string name)
    {
        if (name.StartsWith("m_", StringComparison.Ordinal)) yield return "_" + name.Substring(2);
        else if (name.StartsWith("_", StringComparison.Ordinal)) { yield return name.Substring(1); yield return "m" + name; }
        else { yield return "_" + name; yield return "m_" + name; }
    }

    static bool IsFramework(Type type)
    {
        string a = type.Assembly.GetName().Name ?? "";
        return a.StartsWith("System", StringComparison.Ordinal) || a == "mscorlib" || a == "netstandard";
    }
}
