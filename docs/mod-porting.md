# Mod 移植：静态织入观者（Watcher）mod

本文档记录把 Steam Workshop 订阅的 **Watcher** mod（观者角色，作者 Boninall）移植到 iOS 的技术路径。
原理同样适用于移植其他"带托管代码"的 STS2 mod。

> ⚠️ 与游戏本体一样：本仓库不含 mod 的资产与代码。使用者需自备 Steam 正版游戏 + 自己在
> Workshop 订阅的 mod 文件（`~/Library/Application Support/Steam/steamapps/workshop/content/2868840/3747526116/`）。

## 为什么不能像 PC 一样"扔进 Mods 文件夹"

PC 上游戏的 mod 管线（`MegaCrit.Sts2.Core.Modding.ModManager`）：

```
扫描 Mods/Workshop 目录 → 读 <mod>.json → AssemblyLoadContext.LoadFromAssemblyPath(dll)
+ ProjectSettings.LoadResourcePack(pck) → 调 [ModInitializerAttribute] 类 → new Harmony(id).PatchAll()
```

iOS NativeAOT 三项全禁：**运行时加载 IL 程序集**、**Harmony/MonoMod 运行时打补丁（需要 JIT）**、
**Reflection.Emit（AccessTools.FieldRefAccess 等）**。因此 mod 的托管代码必须**构建期静态织入**。

## 移植流水线（build-ios.sh 步骤 1b，已内建）

```
┌─ 反编译/重编译 ─────────────────────────────────────────────┐
│ src/STS2WatcherMod/                                         │
│   Decompiled/Watcher.decompiled.cs   ← ilspycmd 反编译产物   │
│   （内含 WatcherFieldAccess 辅助类 ← 替代 FieldRefAccess）   │
│   WatcherBootstrap.ModManagerInitializePostfix()  ← iOS 引导│
│   （构建: dotnet build → STS2WatcherMod.dll）                │
└─────────────────────────────────────────────────────────────┘
        │  STS2Weaver --gen <mod.dll> <sts2.dll> <manifest.json>
        ▼
   把 76 个 [HarmonyPatch] 类转成织入清单条目（prefix/postfix/finalizer）
   + 钩子方法/类型翻 public（跨程序集 call 需要）+ 写回 mod dll
        │  STS2Weaver <已织入 sts2.dll> <mod.dll> <manifest> <最终 dll>
        ▼
   与仓库自带补丁同一条织入路径 → NativeAOT 一起编译
        │  push-mod.sh（devicectl 拷贝）
        ▼
   Documents/Watcher.pck —— 启动时 ModManager.Initialize 织入钩子挂载
```

## 关键技术点

### 1. Harmony 补丁静态化

weaver 的 prefix/postfix 语义与 Harmony 对齐（`__instance` / `ref __result` / 按名参数绑定 / bool
prefix 跳过原方法）。`--gen` 模式解析 `[HarmonyPatch]` 的四种目标形态：

| 形态 | 解析方式 |
|---|---|
| `[HarmonyPatch(typeof(T), "M")]` | 直接 |
| `[HarmonyPatch(typeof(T), "M", MethodType)]` | Getter→`get_M` / Setter→`set_M` / … |
| `[HarmonyPatch(typeof(T), "M", Type[] argTypes)]` | 生成 `targetParams` 重载消歧 |
| `[HarmonyPatch()]` + `TargetMethod(s)()` 运行时反射 | 构建期静态解析：`AccessTools.TypeByName("X")+Method(t,"Y")` 模式与 `WatcherHookCompat.FindHookMethods("Name")`（游戏 `Core.Hooks.Hook` 类的公开静态 Task 方法）模式 |

### 2. finalizer 语义

Harmony Finalizer（原方法抛异常时执行、返回非 null `Exception` 可替换异常）由 weaver 的
`kind: "finalizer"` 实现：整个原方法体包 `try`，`catch(Exception)` 里调钩子后 `rethrow`，
原 `ret` 改写为 `leave`。替换异常走 `throw` 新异常分支（null 时 `rethrow` 原异常）。
**注意坑**：catch handler 里对 GC 引用的 `dup`+条件分支会让 RyuJIT/ILC 编译失败
（`InvalidProgramException`/`Code generation failed`，且桌面 JIT 与 NativeAOT 表现一致）——
织入产物可用桌面 CoreCLR 的 `RuntimeHelpers.PrepareMethod` 快速验证（秒级），不必等 30 分钟 ILC。

### 3. ref __result 基类型桥接

游戏更新把 `get_CharacterSelectIcon` 返回类型从 `Texture2D` 改成了 `CompressedTexture2D`，
而 mod 钩子声明 `ref Texture2D __result`。Harmony 在 PC 容忍此绑定；weaver 用桥接局部变量 +
`castclass` 复制回写对齐（钩子写入非派生实例时抛 `InvalidCastException`，与 Harmony 未检查行为同级）。

### 4. 反射的两种命运

- **FieldInfo 型**（`GetField(...).GetValue/SetValue`、`AccessTools.Field`）——游戏主程序集与
  `STS2WatcherMod` 整库 `TrimmerRootAssembly` root，反射元数据全保留，**直接可用**。
- **Reflection.Emit 型**（`AccessTools.FieldRefAccess` 生成动态方法委托）——NativeAOT 必炸。
  已在源码层替换为 `WatcherFieldAccess.Field()` 缓存 FieldInfo 版本（9 处，均非热路径）。

### 5. 修改点清单（相对反编译原始产物）

- 171 处 Roslyn 内部类型 `<>z__ReadOnlyArray/List` 包装器还原为普通数组；
- 3 个反编译失败的迭代器按原始 IL 重写为 `yield`（`FlattenDamageResults` 等）；
- `WatcherBootstrap.Init` 去掉 MonoMod 预载/Harmony/`GetTypes()` 反射循环，保留订阅与模型注册；
  新增 `ModManagerInitializePostfix()`：挂载 `user://Watcher.pck` + 调 `Init()`；
- 删掉 Android 专属回退（`SceneTreeListener`、`InjectDarvVioletLotus`——iOS 无跳过补丁，不需要回退）；
- 2 个 `[HarmonyPatch()]` + `TargetMethod()` 反射目标（MegaSpine）由 `--gen` 构建期解析。

## 更新 mod / 移植其他 mod

1. **mod 更新**：Steam 更新订阅文件后，重跑反编译→修复→`dotnet build` 即可；`--gen` 与织入会自动
   适应新签名（目标方法不存在/签名不符时织入**响亮失败**，不会产出坏 dll）。
2. **移植其他 mod**：照搬 `src/STS2WatcherMod/` 目录结构。需要额外注意：
   - 依赖其他 mod 的（如引用 Prophet）→ 要么连依赖一起移植，要么跳过相关补丁；
   - 用了 Transpiler / ReversePatch 的 → weaver 不支持，需手工改写；
   - `FieldRefAccess` / `PropertyGetter` / `MethodDelegate` 等 emit 型 AccessTools → 换 FieldInfo 或手写织入。

## 局限

- mod 只进单机局（联机对局里观者不可用——`WatcherMultiplayerLoad*` 补丁只管背景，PC 同理）；
- 存档里的观者内容与 PC 互通（类型名未变），但旧版 mod 的旧卡牌 id 依赖
  `WatcherLegacyCardIdMigrationPatch`（已织入）迁移；
- `Hook.AfterCardRetained` 在当前游戏版本不存在，`WatcherAfterCardRetainedCompatPatch` 的
  `Prepare()` 返回 false —— `--gen` 解析出 0 目标自然跳过，与 PC 运行时行为一致。
