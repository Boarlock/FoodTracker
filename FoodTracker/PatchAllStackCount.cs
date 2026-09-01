using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

[HarmonyPatch]
public static class PatchThingStackCountWrites
{
    private static readonly FieldInfo StackCountField =
        AccessTools.Field(typeof(Thing), "stackCount");

    public static IEnumerable<MethodBase> TargetMethods()
    {
        if (StackCountField == null)
        {
            Log.Error("[FoodTracker] Could not find Thing.stackCount field.");
            yield break;
        }

        foreach (Type type in typeof(Thing).Assembly.GetTypes())
        {
            MethodInfo[] methods;

            try
            {
                methods = type.GetMethods(AccessTools.all);
            }
            catch
            {
                continue;
            }

            foreach (MethodInfo method in methods)
            {
                if (method.IsAbstract)
                    continue;

                if (method.GetMethodBody() == null)
                    continue;

                // Skip SpawnSetup entirely.
                if (method.DeclaringType == typeof(Thing) &&
                    method.Name == "SpawnSetup")
                {
                    continue;
                }

                IEnumerable<CodeInstruction> instructions;

                try
                {
                    instructions = PatchProcessor.GetCurrentInstructions(method);
                }
                catch
                {
                    continue;
                }

                if (instructions.Any(ins =>
                    ins.opcode == OpCodes.Stfld &&
                    ins.operand is FieldInfo field &&
                    field == StackCountField))
                {
                    yield return method;
                }
            }
        }
    }

    public static void Prefix(
        MethodBase __originalMethod,
        Thing __instance)
    {
        if (__instance == null)
            return;

        Log.Message(
            $"[FoodTracker][STACKCOUNT WRITE] " +
            $"BEFORE " +
            $"{__originalMethod.DeclaringType?.FullName}.{__originalMethod.Name} " +
            $"stack={__instance.stackCount}");
    }

    public static void Postfix(
        MethodBase __originalMethod,
        Thing __instance)
    {
        if (__instance == null)
            return;

        Log.Message(
            $"[FoodTracker][STACKCOUNT WRITE] " +
            $"AFTER " +
            $"{__originalMethod.DeclaringType?.FullName}.{__originalMethod.Name} " +
            $"stack={__instance.stackCount}");
    }
}