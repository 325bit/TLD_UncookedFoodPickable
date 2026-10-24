# Uncooked Food Pickable

让《The Long Dark》TFTFT DLC 里的**未烹饪食物**（`GEAR_Uncooked*`）
丢到地上后能正常瞄准、拾取。

*A MelonLoader mod that makes uncooked food items (`GEAR_Uncooked*`) interactable after being dropped on the ground.*

![version](https://img.shields.io/badge/version-1.0.0-informational)
![game](https://img.shields.io/badge/The%20Long%20Dark-2.55-success)
![loader](https://img.shields.io/badge/MelonLoader-0.6%2B-blueviolet)
![license](https://img.shields.io/badge/license-MIT-blue)

---

## 安装

**前置条件**

- 《The Long Dark》Steam / Epic 版，游戏版本 **2.55**（本 mod 针对 2.55 编译）
- [MelonLoader](https://melonwiki.xyz/) **0.6.0 或更高**（开发与实测环境为 0.7.3）
- 《Tales from the Far Territory》DLC（没装 DLC 也能加载，但游戏里不会出现这些物品，mod 无实际作用）

**步骤**

1. 确认游戏目录下已有 `MelonLoader/` 文件夹（没有就先装 MelonLoader 并启动一次游戏）
2. 把 `UncookedFoodPickable.dll` 复制到 `TheLongDark/Mods/`
3. 重启游戏

默认安装路径参考：

```
F:\SteamLibrary\steamapps\common\TheLongDark\Mods\UncookedFoodPickable.dll
```

本 mod **无其他 mod 依赖**，不需要 ModSettings 之类的配置框架。

## 卸载

删除 `TheLongDark/Mods/UncookedFoodPickable.dll` 即可。

注意：修复只存在于运行时。卸载后，那些还留在地上的生食会回到"捡不起来"的原始状态；
已经捡回背包或放进容器的物品不受影响。

## 兼容性

- 与修改背包 / 堆叠 / 烹饪的 mod 无已知冲突（本 mod 不碰这些系统）
- 会主动把物品放到 layer `17`（Gear），与原版所有可拾取物品一致；极端情况下，如果有 mod 靠"物品留在
  Default 层"做判断，可能互相影响——目前未发现此类 mod
- 只对名字含 `Uncooked` 的 `GearItem` 生效，不插队到其他物品的交互链上
