using System;
using System.Collections.Generic;

using Il2Cpp;

using MelonLoader;

using UnityEngine;

namespace UncookedFoodPickable;

/// <summary>
/// 补全 "GEAR_Uncooked*"（生面饼 / 生馅饼 / 生炖菜 / 生粥…）物品缺失的碰撞体与层。
///
/// 原版这些 prefab 只有 MonoBehaviour + Transform：<b>没有 Collider、没有 Rigidbody、layer = 0(Default)</b>，
/// 而对应的熟食（GEAR_Cooked*）是 layer 17(Gear) + BoxCollider + Rigidbody。
/// 玩家丢下后，交互射线（只打交互层上的碰撞体）永远命不中，于是"看得见、捡不起来"。
/// 这里在实例化 / 掉落 / 读档三个时机把它们补成与熟食一致的形态。
/// </summary>
internal static class UncookedFoodFixer
{
	/// <summary>TagManager 第 17 层 = "Gear"，原版所有可拾取物品都在这一层。</summary>
	internal const int GearLayer = 17;

	private const float TickSeconds = 2f;

	private static readonly List<GearItem> Tracked = new List<GearItem>();
	private static readonly HashSet<int> TrackedIds = new HashSet<int>();

	private static float nextTick;
	private static int fixedCount;
	private static bool loggedFirst;

	// ---------------------------------------------------------------- 判定

	private static string PrefabName(GearItem gi)
	{
		try
		{
			if (gi.gameObject != null)
			{
				string n = gi.gameObject.name;
				if (!string.IsNullOrEmpty(n))
				{
					return n;
				}
			}
		}
		catch
		{
			// 对象已销毁
		}

		try
		{
			var data = gi.GearItemData;
			if (data != null)
			{
				string n = data.name;
				if (!string.IsNullOrEmpty(n))
				{
					return n;
				}
			}
		}
		catch
		{
		}

		return null;
	}

	internal static bool IsTarget(GearItem gi)
	{
		string name = PrefabName(gi);
		if (string.IsNullOrEmpty(name))
		{
			return false;
		}

		return name.IndexOf("Uncooked", StringComparison.Ordinal) >= 0;
	}

	// ---------------------------------------------------------------- 修复

	/// <param name="reason">awake / drop / deserialize / scan</param>
	internal static void Fix(GearItem gi, string reason)
	{
		if (gi == null)
		{
			return;
		}

		GameObject go;
		try
		{
			go = gi.gameObject;
		}
		catch
		{
			return;
		}

		if (go == null || !IsTarget(gi))
		{
			return;
		}

		bool changed = false;

		// 1) 层：交互射线只认交互层上的碰撞体
		if (go.layer != GearLayer)
		{
			SetLayerRecursive(go.transform, GearLayer);
			changed = true;
		}

		// 2) 碰撞体：这些 prefab 一个都没有
		//    背包 / 容器 / 火堆烹饪槽里的物品不该有可命中的碰撞体，交给游戏自己开关
		bool parked = IsParked(gi);
		bool added = EnsureCollider(go, parked);
		if (added)
		{
			changed = true;
		}

		// 3) 交互开关：即使有碰撞体，NonInteractive 也会让提示消失
		try
		{
			if (gi.NonInteractive)
			{
				gi.NonInteractive = false;
				changed = true;
			}

			gi.SetInteractive(true, false);
		}
		catch (Exception ex)
		{
			UncookedFoodPickableMod.Log?.Warning($"[UncookedFoodPickable] SetInteractive 失败（{PrefabName(gi)}）：{ex.Message}");
		}

		// 4) GUID：缺了会导致"点得到、捡不走"（存档里曾出现整批无 GUID 的地面物品）
		try
		{
			if (gi.m_ObjectGuid == null)
			{
				gi.ForceGUIDSetup();
				changed = true;
			}
		}
		catch
		{
		}

		// 掉落时补的碰撞体要重新挂上物理，否则物体会停在半空/穿地
		if (added && reason == "drop")
		{
			try
			{
				gi.EnablePhysics();
			}
			catch
			{
			}
		}

		Track(gi);

		if (changed)
		{
			fixedCount++;
			if (!loggedFirst)
			{
				loggedFirst = true;
				UncookedFoodPickableMod.Log?.Msg(
					$"[UncookedFoodPickable] 首次修复 {PrefabName(gi)}：layer={go.layer}"
					+ $"，碰撞体={CountColliders(go)}，renderer={CountRenderers(go)}，原因={reason}");
			}
			else if (reason != "scan")
			{
				UncookedFoodPickableMod.Log?.Msg($"[UncookedFoodPickable] 修复 {PrefabName(gi)}（{reason}）");
			}
		}
	}

	private static void SetLayerRecursive(Transform t, int layer)
	{
		if (t == null)
		{
			return;
		}

		t.gameObject.layer = layer;
		int childCount = t.childCount;
		for (int i = 0; i < childCount; i++)
		{
			SetLayerRecursive(t.GetChild(i), layer);
		}
	}

	/// <summary>背包、容器、火堆烹饪槽里的物品不需要（也不该有）可命中的碰撞体。</summary>
	private static bool IsParked(GearItem gi)
	{
		try
		{
			if (gi.m_InPlayerInventory || gi.m_InsideContainer || gi.m_IsInSatchel)
			{
				return true;
			}

			if (gi.IsAttachedToPlacePoint())
			{
				return true;
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool EnsureCollider(GameObject go, bool parked)
	{
		try
		{
			Collider[] existing = go.GetComponentsInChildren<Collider>(true);
			bool rootHasOne = false;
			bool restored = false;
			if (existing != null && existing.Length > 0)
			{
				for (int i = 0; i < existing.Length; i++)
				{
					Collider c = existing[i];
					if (c == null)
					{
						continue;
					}

					if (c.gameObject == go)
					{
						rootHasOne = true;

						// 从背包/容器里回到世界：把我们之前关掉的碰撞体开回来
						if (!parked && !c.enabled)
						{
							c.enabled = true;
							restored = true;
						}
					}

					if (c.gameObject.layer != GearLayer)
					{
						c.gameObject.layer = GearLayer;
					}
				}
			}

			if (restored)
			{
				return true;
			}

			if (rootHasOne)
			{
				return false;
			}

			BoxCollider box = go.AddComponent<BoxCollider>();
			if (box == null)
			{
				return false;
			}

			Vector3 center;
			Vector3 size;
			if (!ComputeLocalBounds(go, out center, out size))
			{
				center = new Vector3(0f, 0.04f, 0f);
				size = new Vector3(0.12f, 0.08f, 0.12f);
			}

			box.center = center;
			box.size = size;
			box.isTrigger = false;
			box.enabled = !parked;
			box.gameObject.layer = GearLayer;
			return true;
		}
		catch (Exception ex)
		{
			UncookedFoodPickableMod.Log?.Warning("[UncookedFoodPickable] 添加碰撞体失败：" + ex.Message);
			return false;
		}
	}

	/// <summary>用模型渲染器的世界包围盒反推本地 BoxCollider 的中心/尺寸。</summary>
	private static bool ComputeLocalBounds(GameObject go, out Vector3 center, out Vector3 size)
	{
		center = Vector3.zero;
		size = Vector3.zero;

		Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
		if (renderers == null || renderers.Length == 0)
		{
			return false;
		}

		Bounds bounds = default;
		bool started = false;
		for (int i = 0; i < renderers.Length; i++)
		{
			Renderer r = renderers[i];
			if (r == null)
			{
				continue;
			}

			if (!started)
			{
				bounds = r.bounds;
				started = true;
			}
			else
			{
				bounds.Encapsulate(r.bounds);
			}
		}

		if (!started)
		{
			return false;
		}

		Transform t = go.transform;
		Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

		for (int i = 0; i < 8; i++)
		{
			Vector3 corner = new Vector3(
				(i & 1) == 0 ? bounds.min.x : bounds.max.x,
				(i & 2) == 0 ? bounds.min.y : bounds.max.y,
				(i & 4) == 0 ? bounds.min.z : bounds.max.z);
			Vector3 local = t.InverseTransformPoint(corner);
			min = Vector3.Min(min, local);
			max = Vector3.Max(max, local);
		}

		center = (min + max) * 0.5f;
		size = max - min;

		// 夹紧：模型可能带 LOD/特效子物体，包围盒不可信时会做出巨型碰撞体
		size.x = Mathf.Clamp(size.x, 0.02f, 0.6f);
		size.y = Mathf.Clamp(size.y, 0.02f, 0.6f);
		size.z = Mathf.Clamp(size.z, 0.02f, 0.6f);
		return size.x * size.y * size.z > 1e-5f;
	}

	private static int CountColliders(GameObject go)
	{
		try
		{
			Collider[] cols = go.GetComponentsInChildren<Collider>(true);
			return cols == null ? 0 : cols.Length;
		}
		catch
		{
			return -1;
		}
	}

	private static int CountRenderers(GameObject go)
	{
		try
		{
			Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
			return rs == null ? 0 : rs.Length;
		}
		catch
		{
			return -1;
		}
	}

	// ---------------------------------------------------------------- 跟踪与巡视

	private static void Track(GearItem gi)
	{
		int id;
		try
		{
			id = gi.GetInstanceID();
		}
		catch
		{
			return;
		}

		if (TrackedIds.Add(id))
		{
			Tracked.Add(gi);
		}
	}

	internal static void Tick()
	{
		if (Time.unscaledTime < nextTick)
		{
			return;
		}

		nextTick = Time.unscaledTime + TickSeconds;

		for (int i = Tracked.Count - 1; i >= 0; i--)
		{
			GearItem gi = Tracked[i];
			try
			{
				if (gi == null || gi.gameObject == null)
				{
					TrackedIds.Remove(gi.GetInstanceID());
					Tracked.RemoveAt(i);
					continue;
				}

				// 背包 / 容器 / 烹饪槽里的不参与世界交互，等它回到地上再说
				if (IsParked(gi))
				{
					continue;
				}

				Collider[] cols = gi.gameObject.GetComponentsInChildren<Collider>(true);
				bool usable = false;
				if (cols != null)
				{
					for (int k = 0; k < cols.Length; k++)
					{
						Collider c = cols[k];
						if (c != null && c.gameObject == gi.gameObject && c.enabled)
						{
							usable = true;
							break;
						}
					}
				}

				if (!usable)
				{
					Fix(gi, "scan");
				}
			}
			catch
			{
				try
				{
					TrackedIds.Remove(gi.GetInstanceID());
				}
				catch
				{
				}

				Tracked.RemoveAt(i);
			}
		}
	}

	internal static int FixedCount => fixedCount;

	// ---------------------------------------------------------------- 进场景兜底扫描

	private static float sweepAt = -1f;
	private static string sweepScene;
	private static bool warnedEmptySweep;

	private static void TryFixOne(GearItem gi)
	{
		if (gi == null)
		{
			return;
		}

		try
		{
			Fix(gi, "scan");
		}
		catch
		{
		}
	}

	/// <summary>进场景后延迟几秒做一次全量扫描：钩子万一失效也能把地上的生食补齐。</summary>
	internal static void RequestSceneSweep(string sceneName)
	{
		sweepScene = sceneName;
		sweepAt = Time.unscaledTime + 4f;
	}

	internal static void TickSceneSweep()
	{
		if (sweepAt < 0f || Time.unscaledTime < sweepAt)
		{
			return;
		}

		sweepAt = -1f;

		try
		{
			int total = 0;
			int before = fixedCount;

			// GearManager.m_Gear 是场景内所有 GearItem 的权威列表（地上的 + 背包里的）
			var list = GearManager.m_Gear;
			if (list != null && list.Count > 0)
			{
				total = list.Count;
				for (int i = 0; i < total; i++)
				{
					TryFixOne(list[i]);
				}
			}
			else
			{
				GearItem[] all = UnityEngine.Object.FindObjectsOfType<GearItem>();
				if (all == null || all.Length == 0)
				{
					if (!warnedEmptySweep)
					{
						warnedEmptySweep = true;
						UncookedFoodPickableMod.Log?.Msg("[UncookedFoodPickable] 场景扫描没拿到物品列表（交给钩子处理）。");
					}

					return;
				}

				total = all.Length;
				for (int i = 0; i < total; i++)
				{
					TryFixOne(all[i]);
				}
			}

			int delta = fixedCount - before;
			if (delta > 0)
			{
				UncookedFoodPickableMod.Log?.Msg($"[UncookedFoodPickable] 进入 {sweepScene} 后补修 {delta} 个生食（场景内物品共 {total}）。");
			}
		}
		catch (Exception ex)
		{
			UncookedFoodPickableMod.Log?.Warning("[UncookedFoodPickable] 场景扫描失败：" + ex.Message);
		}
	}
}
