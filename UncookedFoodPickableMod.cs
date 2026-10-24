using System;

using HarmonyLib;

using Il2Cpp;

using MelonLoader;

using UnityEngine;

[assembly: MelonInfo(typeof(UncookedFoodPickable.UncookedFoodPickableMod), "Uncooked Food Pickable", "1.0.0", "Hemalurgic")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace UncookedFoodPickable;

/// <summary>
/// 让 GEAR_Uncooked*（生面饼 / 生馅饼 / 生炖菜 / 生粥…）丢到地上后可以正常拾取。
/// 原因：这些 prefab 只有 MonoBehaviour + Transform，没有 Collider、没有 Rigidbody、layer 是 Default，
/// 交互射线打不到；熟食版本则是 layer 17(Gear) + BoxCollider + Rigidbody。
/// </summary>
public class UncookedFoodPickableMod : MelonMod
{
	internal static MelonLogger.Instance Log;

	private static bool loggedError;

	public override void OnInitializeMelon()
	{
		Log = LoggerInstance;
		Log.Msg("Uncooked Food Pickable 已加载：未烹饪食物（GEAR_Uncooked*）掉在地上可以捡了。");
	}

	public override void OnSceneWasLoaded(int buildIndex, string sceneName)
	{
		// 兜底：万一三个 Harmony 钩子在当前运行环境里都没挂上，进场景后自己扫一遍地上的生食
		UncookedFoodFixer.RequestSceneSweep(sceneName);
	}

	public override void OnUpdate()
	{
		try
		{
			UncookedFoodFixer.Tick();
			UncookedFoodFixer.TickSceneSweep();
		}
		catch (Exception ex)
		{
			LogOnce(ex);
		}
	}

	/// <summary>同一种异常只打一次，避免每帧刷屏。</summary>
	internal static void LogOnce(Exception ex)
	{
		if (loggedError)
		{
			return;
		}

		loggedError = true;
		Log?.Warning("[UncookedFoodPickable] " + ex);
	}
}
