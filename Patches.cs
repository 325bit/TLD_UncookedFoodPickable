using System;

using HarmonyLib;

using Il2Cpp;

using MelonLoader;

using UnityEngine;

namespace UncookedFoodPickable;

/// <summary>
/// 三个时机补齐碰撞体：实例化完成（Awake）、玩家丢到地上（Drop）、读档恢复（Deserialize）。
/// 地上的旧物品读档时也会走 Awake + Deserialize，所以历史遗留的"捡不起来"一并修好。
/// </summary>
[HarmonyPatch(typeof(GearItem), nameof(GearItem.Awake))]
internal static class GearItemAwakePatch
{
	private static void Postfix(GearItem __instance)
	{
		try
		{
			UncookedFoodFixer.Fix(__instance, "awake");
		}
		catch (Exception ex)
		{
			UncookedFoodPickableMod.LogOnce(ex);
		}
	}
}

[HarmonyPatch(typeof(GearItem), nameof(GearItem.Drop))]
internal static class GearItemDropPatch
{
	private static void Postfix(GearItem __result)
	{
		try
		{
			UncookedFoodFixer.Fix(__result, "drop");
		}
		catch (Exception ex)
		{
			UncookedFoodPickableMod.LogOnce(ex);
		}
	}
}

[HarmonyPatch(typeof(GearItem), nameof(GearItem.Deserialize))]
internal static class GearItemDeserializePatch
{
	private static void Postfix(GearItem __instance)
	{
		try
		{
			UncookedFoodFixer.Fix(__instance, "deserialize");
		}
		catch (Exception ex)
		{
			UncookedFoodPickableMod.LogOnce(ex);
		}
	}
}
