// STS2Weaver — 把 Harmony 风格的 prefix/postfix 钩子静态织入 sts2.dll (iOS NativeAOT 无 JIT,不能运行时打补丁)
// 用法: STS2Weaver <sts2.dll> <hooks.dll> <manifest.json> <output.dll>
//
// manifest.json: { "patches": [ { "targetType", "targetMethod", "kind": "prefix"|"postfix",
//                                 "hookType", "hookMethod" } ] }
//
// 语义(与 HarmonyLib 对齐,仅实现本项目补丁用到的子集):
//   prefix 返回 void  → 原方法开头插入调用
//   prefix 返回 bool  → 开头插入调用; 返回 false 则跳过原方法体直接 return
//                       (返回值方法: 返回 __result 局部变量,未绑定 __result 时返回 default)
//   postfix           → 每个 return 点插入调用(经单出口改写)
//   钩子参数绑定: __instance → this; __result / ref __result → 返回值局部变量;
//                 其余按参数名匹配原方法参数(支持 ref);所有绑定做类型兼容检查
//                 (值类型严格一致/引用类型按继承链/object 放行/ref 严格一致),
//                 不匹配即报错拒绝出产物——游戏更新改了参数类型会响亮失败,
//                 而不是织出运行时崩溃的 dll。
// 不支持: transpiler / finalizer / __state / ___field 注入 / 泛型目标 / 重载目标 — 遇到即报错退出。

using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

// --gen 模式: 从 mod 程序集提取 [HarmonyPatch] 生成织入清单(不织入)
// 用法: STS2Weaver --gen <mod.dll> <game-sts2.dll> <out-manifest.json>
//   - 扫描 mod 里所有 [HarmonyPatch] 类,解析目标(含 MethodType/argumentTypes/TargetMethods 运行时反射目标)
//   - 把钩子方法(Prefix/Postfix/Finalizer)与其声明类型翻成 public(跨程序集 call 需要)
//   - 写回 mod.dll + 输出 manifest(含 ModManager.Initialize → WatcherBootstrap.ModManagerInitializePostfix 引导条目)
if (args.Length == 4 && args[0] == "--gen")
{
    return GenHarmonyManifest(args[1], args[2], args[3]);
}

if (args.Length != 4)
{
    Console.Error.WriteLine("usage: STS2Weaver <sts2.dll> <hooks.dll> <manifest.json> <output.dll>");
    return 2;
}

string targetPath = args[0], hooksPath = args[1], manifestPath = args[2], outputPath = args[3];

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(targetPath)));
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(hooksPath)));
// 核心库搜索目录:类型兼容检查需要解析 System.* 类型,否则全部 fail-open
resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));

var readParams = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = false };
var targetAsm = AssemblyDefinition.ReadAssembly(targetPath, readParams);
var hooksAsm = AssemblyDefinition.ReadAssembly(hooksPath, readParams);
var module = targetAsm.MainModule;

var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("manifest 解析失败");

// stripReadonly: 去掉指定静态字段的 initonly 标记(否则 .NET8 运行时反射 SetValue 会抛 FieldAccessException)
foreach (var s in manifest.StripReadonly ?? new())
{
    var t = module.GetType(s.Type) ?? throw new InvalidOperationException($"stripReadonly 类型不存在: {s.Type}");
    foreach (var fname in s.Fields)
    {
        var f = t.Fields.FirstOrDefault(x => x.Name == fname)
            ?? throw new InvalidOperationException($"stripReadonly 字段不存在: {s.Type}.{fname}");
        f.IsInitOnly = false;
        Console.WriteLine($"[OK] strip-ro {s.Type}.{fname}");
    }
}

// patchConst: 替换指定类型(含嵌套状态机)方法体内的 int 字面量 — 用于 const 内联后无法反射改的值(如 AutoSlayer maxFloor=49)
foreach (var pc in manifest.PatchConsts ?? new())
{
    var t = module.GetType(pc.Type) ?? throw new InvalidOperationException($"patchConst 类型不存在: {pc.Type}");
    int count = 0;
    var allTypes = new List<TypeDefinition> { t };
    allTypes.AddRange(t.NestedTypes);
    foreach (var td in allTypes)
        foreach (var m in td.Methods.Where(x => x.HasBody))
        {
            if (pc.MethodContains != null && !td.Name.Contains(pc.MethodContains) && !m.Name.Contains(pc.MethodContains)) continue;
            foreach (var ins in m.Body.Instructions)
            {
                bool hit = (ins.OpCode == OpCodes.Ldc_I4_S && (sbyte)ins.Operand == pc.FromInt)
                        || (ins.OpCode == OpCodes.Ldc_I4 && (int)ins.Operand == pc.FromInt);
                if (!hit) continue;
                ins.OpCode = OpCodes.Ldc_I4; ins.Operand = pc.ToInt; count++;
                Console.WriteLine($"[OK] patch-const {td.Name}.{m.Name}: {pc.FromInt}→{pc.ToInt}");
            }
        }
    if (count == 0) throw new InvalidOperationException($"patchConst 没有命中任何 {pc.FromInt}: {pc.Type} ({pc.MethodContains})");
}

// redirectCalls: 把指定命名空间下所有对某方法的调用改指到钩子(签名须一致) — 如 AutoSlay 的 Task.Delay → 1ms 版
foreach (var rc in manifest.RedirectCalls ?? new())
{
    var hookTypeDef = hooksAsm.MainModule.GetType(rc.HookType)
        ?? throw new InvalidOperationException($"redirect 钩子类型不存在: {rc.HookType}");
    var hook = hookTypeDef.Methods.SingleOrDefault(m => m.Name == rc.HookMethod && m.IsStatic && m.IsPublic)
        ?? throw new InvalidOperationException($"redirect 钩子方法不存在: {rc.HookMethod}");
    var hookRef = module.ImportReference(hook);
    int n = 0;
    foreach (var td in module.GetTypes())   // 含嵌套类型(async 状态机)
    {
        if (!td.FullName.StartsWith(rc.NamespacePrefix)) continue;
        foreach (var m in td.Methods.Where(x => x.HasBody))
            foreach (var ins in m.Body.Instructions)
            {
                if (ins.OpCode != OpCodes.Call && ins.OpCode != OpCodes.Callvirt) continue;
                if (ins.Operand is not MethodReference mr) continue;
                bool match;
                if (rc.GenericArg != null)
                {
                    // 泛型实例调用(如 Rng.NextItem<NEventOptionButton>): 按元方法+泛型实参匹配。
                    // 实例方法→静态钩子: 栈型一致(this 变第一个参数), 调用方自查签名对齐。
                    match = mr is GenericInstanceMethod gim
                        && gim.ElementMethod.Name == rc.TargetMethod
                        && gim.ElementMethod.DeclaringType.FullName == rc.TargetType
                        && gim.GenericArguments.Count == 1
                        && gim.GenericArguments[0].FullName == rc.GenericArg;
                }
                else
                {
                    // 参数类型逐个比对: 避免误伤重载(如 Delay(TimeSpan,ct) vs Delay(int,ct))
                    match = mr.DeclaringType.FullName == rc.TargetType && mr.Name == rc.TargetMethod
                        && mr.Parameters.Count == hook.Parameters.Count
                        && mr.Parameters.Select((pp, i) => pp.ParameterType.FullName == hook.Parameters[i].ParameterType.FullName).All(x => x);
                }
                if (!match) continue;
                ins.Operand = hookRef;
                ins.OpCode = OpCodes.Call;   // 静态钩子必须用 call(callvirt 对静态方法是非法 IL)
                n++;
            }
    }
    Console.WriteLine($"[OK] redirect {rc.TargetType}.{rc.TargetMethod}({string.Join(",", hook.Parameters.Select(x => x.ParameterType.Name))}) → {rc.HookMethod} x{n} (范围: {rc.NamespacePrefix})");
    if (n == 0) throw new InvalidOperationException($"redirectCalls 没有命中任何调用: {rc.TargetType}.{rc.TargetMethod}");
}

int woven = 0, failed = 0;
foreach (var p in manifest.Patches)
{
    try
    {
        WeaveOne(p);
        woven++;
        Console.WriteLine($"[OK] {p.Kind,-7} {p.TargetType}.{p.TargetMethod} <= {p.HookType}.{p.HookMethod}");
    }
    catch (Exception e)
    {
        failed++;
        Console.Error.WriteLine($"[FAIL] {p.Kind} {p.TargetType}.{p.TargetMethod}: {e.Message}");
    }
}

if (failed > 0)
{
    Console.Error.WriteLine($"织入失败 {failed} 处,拒绝写出产物 (要么全对要么不出货)");
    return 1;
}

targetAsm.Write(outputPath);
Console.WriteLine($"完成: {woven} 处织入 → {outputPath}");
return 0;

void WeaveOne(PatchEntry p)
{
    var targetType = module.GetType(p.TargetType)
        ?? throw new InvalidOperationException($"目标类型不存在: {p.TargetType}");
    var candidates = targetType.Methods.Where(m => m.Name == p.TargetMethod).ToList();
    if (candidates.Count == 0) throw new InvalidOperationException($"目标方法不存在: {p.TargetMethod}");
    if (candidates.Count > 1 && p.TargetParams != null)
    {
        // 按参数类型名消歧(匹配简单名或 FullName)
        candidates = candidates.Where(m =>
            m.Parameters.Count == p.TargetParams.Count &&
            m.Parameters.Select((pr, i) => pr.ParameterType.Name == p.TargetParams[i] || pr.ParameterType.FullName == p.TargetParams[i]).All(x => x)
        ).ToList();
    }
    if (candidates.Count > 1) throw new InvalidOperationException($"目标方法有重载({candidates.Count}个),需 targetParams 消歧: {p.TargetMethod}");
    var target = candidates[0];
    if (target.HasGenericParameters) throw new InvalidOperationException("不支持泛型目标方法");
    if (!target.HasBody) throw new InvalidOperationException("目标方法无方法体");

    var hookTypeDef = hooksAsm.MainModule.GetType(p.HookType)
        ?? throw new InvalidOperationException($"钩子类型不存在: {p.HookType}");
    var hook = hookTypeDef.Methods.SingleOrDefault(m => m.Name == p.HookMethod && m.IsStatic && m.IsPublic)
        ?? throw new InvalidOperationException($"钩子方法不存在或非 public static: {p.HookMethod}");

    bool isPrefix = p.Kind.Equals("prefix", StringComparison.OrdinalIgnoreCase);
    bool isPostfix = p.Kind.Equals("postfix", StringComparison.OrdinalIgnoreCase);
    bool isFinalizer = p.Kind.Equals("finalizer", StringComparison.OrdinalIgnoreCase);
    if (!isPrefix && !isPostfix && !isFinalizer) throw new InvalidOperationException($"未知 kind: {p.Kind}");

    var hookReturnsBool = hook.ReturnType.MetadataType == MetadataType.Boolean;
    var hookReturnsVoid = hook.ReturnType.MetadataType == MetadataType.Void;
    if (isPrefix && !hookReturnsBool && !hookReturnsVoid)
        throw new InvalidOperationException("prefix 钩子必须返回 bool 或 void");
    if (isPostfix && !hookReturnsVoid)
        throw new InvalidOperationException("postfix 钩子必须返回 void");
    if (isFinalizer && !hookReturnsVoid && hook.ReturnType.FullName != "System.Exception")
        throw new InvalidOperationException("finalizer 钩子必须返回 void 或 Exception");

    if (isFinalizer) { WeaveFinalizer(p, target, hook); return; }

    var body = target.Body;
    body.InitLocals = true;
    body.SimplifyMacros();
    var il = body.GetILProcessor();
    var returnsValue = target.ReturnType.MetadataType != MetadataType.Void;

    // __result 局部变量(按需创建)
    VariableDefinition resultVar = null!;
    VariableDefinition EnsureResultVar()
    {
        if (!returnsValue) throw new InvalidOperationException("目标方法返回 void,钩子不能绑定 __result");
        resultVar ??= AddLocal(body, target.ReturnType);
        return resultVar;
    }

    // 生成钩子实参加载指令
    // ref __result 桥接: 钩子声明基类型(如 Texture2D)而目标返回派生类型(CompressedTexture2D)
    // — 游戏更新改了返回类型但 mod 没跟上; Harmony 在 PC 容忍此绑定。桥接局部变量 + castclass 复制回写,
    // 钩子写入非派生实例时 castclass 抛 InvalidCastException(与 Harmony 未检查行为同等级)。
    List<Instruction> resultCopyBacks = new();

    List<Instruction> BuildArgLoads(bool inPostfixEpilogue)
    {
        var loads = new List<Instruction>();
        resultCopyBacks.Clear();
        foreach (var hp in hook.Parameters)
        {
            var name = hp.Name;
            if (name == "__instance")
            {
                if (target.IsStatic) throw new InvalidOperationException("静态目标方法无 __instance");
                // 类型检查:钩子声明的实例类型必须是目标类型的基类/接口(或 object)
                if (!IsAssignableFrom(hp.ParameterType, target.DeclaringType))
                    throw new InvalidOperationException(
                        $"__instance 类型不兼容: 钩子声明 {hp.ParameterType.FullName}，目标类型 {target.DeclaringType.FullName}(须为其基类/接口或 object)");
                loads.Add(il.Create(OpCodes.Ldarg_0));
            }
            else if (name == "__result")
            {
                var v = EnsureResultVar();
                if (hp.ParameterType.IsByReference)
                {
                    var elem = ((ByReferenceType)hp.ParameterType).ElementType;
                    if (!SameType(elem, target.ReturnType))
                    {
                        // 桥接: 类型须与返回类型同链(互为继承), 且返回类型为引用类型(castclass 只对引用类型合法)
                        if (target.ReturnType.IsValueType
                            || (!IsAssignableFrom(elem, target.ReturnType) && !IsAssignableFrom(target.ReturnType, elem)))
                            throw new InvalidOperationException(
                                $"ref __result 元素类型须与返回类型一致或同继承链: 钩子 {elem.FullName} vs 目标 {target.ReturnType.FullName}");
                        var bridge = AddLocal(body, module.ImportReference(elem));
                        loads.Add(il.Create(OpCodes.Ldloca, bridge));
                        resultCopyBacks.Add(il.Create(OpCodes.Ldloc, bridge));
                        resultCopyBacks.Add(il.Create(OpCodes.Castclass, target.ReturnType));
                        resultCopyBacks.Add(il.Create(OpCodes.Stloc, v));
                    }
                    else
                    {
                        // ref __result 会写回返回值,元素类型必须与返回类型完全一致
                        loads.Add(il.Create(OpCodes.Ldloca, v));
                    }
                }
                else
                {
                    if (!inPostfixEpilogue) throw new InvalidOperationException("prefix 的 __result 必须是 ref");
                    if (!IsAssignableFrom(hp.ParameterType, target.ReturnType))
                        throw new InvalidOperationException(
                            $"__result 类型不兼容: 钩子 {hp.ParameterType.FullName} vs 目标返回 {target.ReturnType.FullName}(须为后者的基类/接口或 object)");
                    loads.Add(il.Create(OpCodes.Ldloc, v));
                }
            }
            else
            {
                var op = target.Parameters.FirstOrDefault(x => x.Name == name)
                    ?? throw new InvalidOperationException($"钩子参数 '{name}' 在目标方法中找不到同名参数");
                // 类型检查:按名绑定只匹配名字,游戏更新改了参数类型会产出运行时崩溃的 dll——这里拦下。
                //   hook ref + 目标非 ref:合法(Harmony 写回语义,Ldarga 取参数槽地址),元素类型须一致
                //   hook ref + 目标 ref:合法(Ldarg 推托管指针),元素类型须一致
                //   hook 非 ref + 目标 ref:非法(会把托管指针喂给非 ref 形参),拒绝
                //   普通参数:hook 类型须是目标类型的基类/接口(或 object)。值类型只认完全一致(不发 box)。
                if (hp.ParameterType.IsByReference || op.ParameterType.IsByReference)
                {
                    if (op.ParameterType.IsByReference && !hp.ParameterType.IsByReference)
                        throw new InvalidOperationException(
                            $"钩子参数 '{name}' 不能以非 ref 形式绑定目标 ref 参数: 目标 {op.ParameterType.FullName}");
                    var hookElem = hp.ParameterType.IsByReference
                        ? ((ByReferenceType)hp.ParameterType).ElementType : hp.ParameterType;
                    var tgtElem = op.ParameterType.IsByReference
                        ? ((ByReferenceType)op.ParameterType).ElementType : op.ParameterType;
                    if (!SameType(hookElem, tgtElem))
                        throw new InvalidOperationException(
                            $"钩子参数 '{name}' 的 ref 元素类型须与目标一致: 钩子 {hookElem.FullName} vs 目标 {tgtElem.FullName}");
                }
                else if (!IsAssignableFrom(hp.ParameterType, op.ParameterType))
                {
                    throw new InvalidOperationException(
                        $"钩子参数 '{name}' 类型不兼容: 钩子 {hp.ParameterType.FullName} vs 目标 {op.ParameterType.FullName}(须为目标的基类/接口或 object)");
                }
                loads.Add(hp.ParameterType.IsByReference && !op.ParameterType.IsByReference
                    ? il.Create(OpCodes.Ldarga, op)
                    : il.Create(OpCodes.Ldarg, op));
            }
        }
        return loads;
    }

    var hookRef = module.ImportReference(hook);

    if (isPrefix)
    {
        var first = body.Instructions[0];
        var seq = BuildArgLoads(inPostfixEpilogue: false);
        seq.Add(il.Create(OpCodes.Call, hookRef));
        seq.AddRange(resultCopyBacks);
        if (hookReturnsBool)
        {
            seq.Add(il.Create(OpCodes.Brtrue, first)); // true → 继续原方法
            if (returnsValue) seq.Add(il.Create(OpCodes.Ldloc, resultVar ?? EnsureResultVar()));
            seq.Add(il.Create(OpCodes.Ret));
        }
        foreach (var ins in seq) il.InsertBefore(first, ins);
        // 注意: 原有跳转若指向 first 会绕过 prefix — 语义正确(prefix 只在入口执行一次)
    }
    else // postfix: 单出口改写
    {
        var rets = body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList();
        if (body.HasExceptionHandlers)
        {
            foreach (var h in body.ExceptionHandlers)
                foreach (var r in rets)
                    if (InRange(body, r, h.TryStart, h.TryEnd) || InRange(body, r, h.HandlerStart, h.HandlerEnd))
                        throw new InvalidOperationException("postfix 目标的 return 位于 try/catch 内,当前不支持 — 需要 leave 语义,报出来再处理");
        }
        // 尾部构建 epilogue: [stloc result] [args] call hook [ldloc result] ret
        var epi = new List<Instruction>();
        if (returnsValue) { EnsureResultVar(); epi.Add(il.Create(OpCodes.Stloc, resultVar)); }
        epi.AddRange(BuildArgLoads(inPostfixEpilogue: true));
        epi.Add(il.Create(OpCodes.Call, hookRef));
        epi.AddRange(resultCopyBacks);
        if (returnsValue) epi.Add(il.Create(OpCodes.Ldloc, resultVar));
        epi.Add(il.Create(OpCodes.Ret));
        var epiHead = epi[0];
        foreach (var ins in epi) body.Instructions.Add(ins);
        foreach (var r in rets) { r.OpCode = OpCodes.Br; r.Operand = epiHead; }
    }

    body.OptimizeMacros();
}

static VariableDefinition AddLocal(MethodBody body, TypeReference type)
{
    var v = new VariableDefinition(type);
    body.Variables.Add(v);
    return v;
}

// finalizer 语义: 原方法抛异常时调用钩子(绑定 __exception), 然后原样重抛。
// 静态实现: 整个原方法体包 try, catch(Exception) 里 [存异常][加载参数][调钩子][重抛];
// 原 ret 全部改写为 leave → 出口(IL 不允许 ret 位于 try 内)。
void WeaveFinalizer(PatchEntry p, MethodDefinition target, MethodDefinition hook)
{
    var body = target.Body;
    body.InitLocals = true;
    body.SimplifyMacros();
    var il = body.GetILProcessor();
    var returnsValue = target.ReturnType.MetadataType != MetadataType.Void;

    VariableDefinition resultVar = null!;
    VariableDefinition EnsureResultVar()
    {
        if (!returnsValue) throw new InvalidOperationException("目标方法返回 void,钩子不能绑定 __result");
        resultVar ??= AddLocal(body, target.ReturnType);
        return resultVar;
    }

    var exType = module.ImportReference(typeof(Exception));
    var exVar = AddLocal(body, exType);

    List<Instruction> BuildFinalizerArgLoads()
    {
        var loads = new List<Instruction>();
        foreach (var hp in hook.Parameters)
        {
            var name = hp.Name;
            if (name == "__exception")
            {
                if (hp.ParameterType.FullName != "System.Exception")
                    throw new InvalidOperationException($"finalizer __exception 参数类型须为 System.Exception: {hp.ParameterType.FullName}");
                loads.Add(il.Create(OpCodes.Ldloc, exVar));
            }
            else if (name == "__instance")
            {
                if (target.IsStatic) throw new InvalidOperationException("静态目标方法无 __instance");
                if (!IsAssignableFrom(hp.ParameterType, target.DeclaringType))
                    throw new InvalidOperationException(
                        $"__instance 类型不兼容: 钩子 {hp.ParameterType.FullName}，目标类型 {target.DeclaringType.FullName}(须为其基类/接口或 object)");
                loads.Add(il.Create(OpCodes.Ldarg_0));
            }
            else if (name == "__result")
            {
                var v = EnsureResultVar();
                if (!hp.ParameterType.IsByReference)
                    throw new InvalidOperationException("finalizer 的 __result 必须是 ref");
                var elem = ((ByReferenceType)hp.ParameterType).ElementType;
                if (!SameType(elem, target.ReturnType))
                    throw new InvalidOperationException(
                        $"ref __result 元素类型须与返回类型一致: 钩子 {elem.FullName} vs 目标 {target.ReturnType.FullName}");
                loads.Add(il.Create(OpCodes.Ldloca, v));
            }
            else
            {
                var op = target.Parameters.FirstOrDefault(x => x.Name == name)
                    ?? throw new InvalidOperationException($"钩子参数 '{name}' 在目标方法中找不到同名参数");
                if (hp.ParameterType.IsByReference || op.ParameterType.IsByReference)
                {
                    if (op.ParameterType.IsByReference && !hp.ParameterType.IsByReference)
                        throw new InvalidOperationException($"钩子参数 '{name}' 不能以非 ref 形式绑定目标 ref 参数");
                    var hookElem = hp.ParameterType.IsByReference ? ((ByReferenceType)hp.ParameterType).ElementType : hp.ParameterType;
                    var tgtElem = op.ParameterType.IsByReference ? ((ByReferenceType)op.ParameterType).ElementType : op.ParameterType;
                    if (!SameType(hookElem, tgtElem))
                        throw new InvalidOperationException($"钩子参数 '{name}' 的 ref 元素类型须与目标一致");
                }
                else if (!IsAssignableFrom(hp.ParameterType, op.ParameterType))
                    throw new InvalidOperationException(
                        $"钩子参数 '{name}' 类型不兼容: 钩子 {hp.ParameterType.FullName} vs 目标 {op.ParameterType.FullName}");
                loads.Add(hp.ParameterType.IsByReference && !op.ParameterType.IsByReference
                    ? il.Create(OpCodes.Ldarga, op) : il.Create(OpCodes.Ldarg, op));
            }
        }
        return loads;
    }

    var hookRef = RehomeHookReference(module.ImportReference(hook), module);

    // 出口: [ldloc resultVar] ret — 所有原 ret 改为 leave 到这里
    Instruction epiHead, epiRet;
    if (returnsValue)
    {
        epiHead = il.Create(OpCodes.Ldloc, EnsureResultVar());
        body.Instructions.Add(epiHead);
        epiRet = il.Create(OpCodes.Ret);
        body.Instructions.Add(epiRet);
    }
    else
    {
        epiHead = epiRet = il.Create(OpCodes.Ret);
        body.Instructions.Add(epiHead);
    }

    // handler: stloc ex; 参数加载; call hook; [替换异常]; rethrow (位于出口之后, HandlerEnd=null 到方法末尾)
    var hs = il.Create(OpCodes.Stloc, exVar);
    body.Instructions.Add(hs);
    foreach (var ins in BuildFinalizerArgLoads()) body.Instructions.Add(ins);
    body.Instructions.Add(il.Create(OpCodes.Call, hookRef));
    var hookReturnsException = hook.ReturnType.FullName == "System.Exception";
    if (hookReturnsException)
    {
        // Harmony 替换语义: hook 返回非 null Exception → throw 新异常; 返回 null → rethrow 原异常。
        // 注意: 不能用 dup(GC 引用在 catch handler 里 dup+分支会触发 RyuJIT/ILC 编译失败,实测)。
        var ldNew = il.Create(OpCodes.Ldloc, exVar);
        body.Instructions.Add(il.Create(OpCodes.Stloc, exVar));
        body.Instructions.Add(ldNew);
        var br = il.Create(OpCodes.Brtrue, ldNew);   // 目标稍后修正为 throw 前加载
        body.Instructions.Add(br);
        body.Instructions.Add(il.Create(OpCodes.Rethrow));
        var ldThrow = il.Create(OpCodes.Ldloc, exVar);
        body.Instructions.Add(ldThrow);
        body.Instructions.Add(il.Create(OpCodes.Throw));
        br.Operand = ldThrow;
    }
    else
    {
        body.Instructions.Add(il.Create(OpCodes.Rethrow));
    }

    body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
    {
        TryStart = body.Instructions[0],
        TryEnd = epiHead,
        HandlerStart = hs,
        HandlerEnd = null,
        CatchType = exType
    });

    foreach (var r in body.Instructions.Where(i => i.OpCode == OpCodes.Ret && i != epiHead && i != epiRet).ToList())
    {
        if (returnsValue) il.InsertBefore(r, il.Create(OpCodes.Stloc, resultVar));
        r.OpCode = OpCodes.Leave; r.Operand = epiHead;
    }
    body.OptimizeMacros();
    Console.WriteLine($"[OK] finalizer 语义已包裹: try[{body.Instructions[0].Offset:x4},{epiHead.Offset:x4}) catch → {p.HookType}.{p.HookMethod}");
}

// AccessTools.Method 语义: 沿继承链找第一个同名方法(任意可见性)
static MethodDefinition? FindMethodByName(TypeDefinition type, string name)
{
    for (var t = type; t != null; t = t.BaseType?.Resolve())
    {
        var m = t.Methods.FirstOrDefault(x => x.Name == name);
        if (m != null) return m;
    }
    return null;
}

// Cecil 导入钩子签名时的坑: mod 程序集里指向游戏类型的 TypeRef 作用域是"外部 sts2 程序集",
// 导入进游戏模块后就成了自程序集引用(AssemblyRef 指向自己)——桌面 RyuJIT 与 NativeAOT ILC 都拒绝。
// 修复: 把签名里所有指向本程序集(与目标模块同名)的类型引用重定位为目标模块自己的类型定义。
static TypeReference RehomeToTarget(TypeReference t, ModuleDefinition target)
{
    switch (t)
    {
        case ByReferenceType br:
            return new ByReferenceType(RehomeToTarget(br.ElementType, target));
        case PointerType pt:
            return new PointerType(RehomeToTarget(pt.ElementType, target));
        case ArrayType at:
            return new ArrayType(RehomeToTarget(at.ElementType, target), at.Rank);
        case GenericInstanceType gi:
            var g2 = new GenericInstanceType(RehomeToTarget(gi.ElementType, target));
            foreach (var a in gi.GenericArguments) g2.GenericArguments.Add(RehomeToTarget(a, target));
            return g2;
        default:
            if (t.Scope is AssemblyNameReference anr && anr.Name == target.Assembly.Name.Name)
            {
                var found = target.GetType(t.FullName);
                if (found != null) return found;
            }
            return t;
    }
}

static MethodReference RehomeHookReference(MethodReference hookRef, ModuleDefinition target)
{
    var rehomed = new MethodReference(hookRef.Name, RehomeToTarget(hookRef.ReturnType, target), hookRef.DeclaringType)
    {
        HasThis = hookRef.HasThis,
        CallingConvention = hookRef.CallingConvention,
        ExplicitThis = hookRef.ExplicitThis
    };
    foreach (var p in hookRef.Parameters)
        rehomed.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, RehomeToTarget(p.ParameterType, target)));
    return rehomed;
}

static bool InRange(MethodBody body, Instruction ins, Instruction start, Instruction end)
{
    if (start == null) return false;
    int i = body.Instructions.IndexOf(ins), s = body.Instructions.IndexOf(start);
    int e = end == null ? body.Instructions.Count : body.Instructions.IndexOf(end);
    return i >= s && i < e;
}

// ---- 钩子/目标类型兼容性(按名绑定不查类型的兜底防线) ----

static bool IsObjectType(TypeReference t) => t.FullName == "System.Object";

static TypeReference Element(TypeReference t) => t.IsByReference ? ((ByReferenceType)t).ElementType : t;

// 两侧(去掉 byref 后)类型全名一致 — ref 绑定要求严格一致(写回语义)
static bool SameType(TypeReference a, TypeReference b) => Element(a).FullName == Element(b).FullName;

// 钩子形参类型能否承接目标侧的值(加载方向 target → hook):
//   object 钩子形参恒放行(现有 manifest 用 object 收任意值,由钩子自己负责不碰;
//   若目标实为值类型会织出需 box 的 IL——当前 manifest 无此组合,加 resolver 前不改此宽松)
//   同类直通;类/接口按继承链判定;值类型只认完全一致(织入器不发 box)
static bool IsAssignableFrom(TypeReference hookType, TypeReference targetType)
{
    hookType = Element(hookType);
    targetType = Element(targetType);
    if (hookType.FullName == targetType.FullName) return true;
    if (IsObjectType(hookType)) return true;
    if (hookType.FullName == "System.ValueType") return true;
    TypeDefinition ht = null!, tt = null!;
    try { ht = hookType.Resolve(); tt = targetType.Resolve(); }
    catch (AssemblyResolutionException) { }   // Resolve 失败抛异常(非返回 null):放宽,运行时兜底
    if (ht == null || tt == null) return true; // 解析不出(泛型/外部程序集)放宽,运行时兜底
    try
    {
        if (tt.IsValueType) return false;      // 值类型目标:只认完全一致(不 box)
        for (var t = tt; t != null; t = t.BaseType?.Resolve())
            if (t.FullName == ht.FullName) return true;
        for (var t = tt; t != null; t = t.BaseType?.Resolve())
            foreach (var ifc in t.Interfaces)
                if (ifc.InterfaceType.FullName == ht.FullName) return true;
    }
    catch (AssemblyResolutionException) { return true; }   // 链上类型解析不了:放宽
    return false;
}

int GenHarmonyManifest(string modPath, string gamePath, string outPath)
{
    var resolver = new DefaultAssemblyResolver();
    resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(modPath)));
    resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(gamePath)));
    resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
    // 读 HarmonyPatch 特性签名里的 HarmonyLib 枚举(MethodType)需要解析 0Harmony — 游戏安装目录里有
    var gameDataDir = Environment.GetEnvironmentVariable("STS2_GAME_DATA_DIR");
    if (gameDataDir != null) resolver.AddSearchDirectory(gameDataDir);
    var modAsm = AssemblyDefinition.ReadAssembly(modPath, new ReaderParameters { AssemblyResolver = resolver, ReadWrite = true });
    var gameAsm = AssemblyDefinition.ReadAssembly(gamePath, new ReaderParameters { AssemblyResolver = resolver });
    var gameModule = gameAsm.MainModule;
    var modModule = modAsm.MainModule;

    var entries = new List<PatchEntry>();

    foreach (var t in modModule.GetTypes().OrderBy(x => x.FullName))
    {
        var hp = t.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "HarmonyPatch");
        if (hp == null) continue;

        int classPriority = 400;
        var pr = t.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "HarmonyPriority");
        if (pr != null && pr.ConstructorArguments.Count > 0)
            classPriority = Convert.ToInt32(pr.ConstructorArguments[0].Value);

        var targets = new List<(string TypeFullName, string MethodName, List<string>? TargetParams)>();
        if (hp.ConstructorArguments.Count > 0)
        {
            var arg0 = hp.ConstructorArguments[0].Value;
            string typeName = arg0 is TypeReference tr ? tr.FullName : (string)arg0!;
            if (gameModule.GetType(typeName) == null)
            {
                Console.Error.WriteLine($"[GEN][WARN] 目标类型不在游戏程序集,跳过: {typeName} ({t.FullName})");
                continue;
            }
            string? methodName = hp.ConstructorArguments.Count > 1 ? (string?)hp.ConstructorArguments[1].Value : null;
            List<string>? targetParams = null;
            int methodType = 0;
            if (hp.ConstructorArguments.Count > 2)
            {
                if (hp.ConstructorArguments[2].Value is CustomAttributeArgument[] arr)
                    targetParams = arr.Select(x => ((TypeReference)x.Value!).FullName).ToList();
                else
                    methodType = Convert.ToInt32(hp.ConstructorArguments[2].Value);
            }
            methodName = methodType switch
            {
                0 => methodName,           // Normal
                1 => "get_" + methodName,  // Getter
                2 => "set_" + methodName,  // Setter
                3 => ".ctor",              // Constructor
                4 => ".cctor",             // StaticConstructor
                6 => methodName,           // Async
                _ => throw new InvalidOperationException($"未知 MethodType {methodType} in {t.FullName}")
            };
            if (methodName == null) throw new InvalidOperationException($"[GEN] {t.FullName} 无目标方法名");
            targets.Add((typeName, methodName, targetParams));
        }
        else
        {
            // [HarmonyPatch()] 空特性类: 目标由 TargetMethod()/TargetMethods() 运行时反射给出,在构建期静态解析。
            // 支持两种 mod 实际用到的模式:
            //   A. AccessTools.TypeByName("X") + AccessTools.Method(t, "Y") → 单个目标 (X 类型全名, Y 方法名)
            //   B. WatcherHookCompat.FindHookMethods("Name") → 游戏 Hook 类的公开静态同名 Task 方法 (多个目标);
            //      钩子名可能是 TargetMethods 方法体内的 ldstr,也可能是类的 const string 字段
            var tm = t.Methods.FirstOrDefault(m => (m.Name == "TargetMethods" || m.Name == "TargetMethod") && m.IsStatic);
            var strings = new List<string>();
            if (tm is { HasBody: true })
                foreach (var ins in tm.Body.Instructions)
                    if (ins.OpCode == OpCodes.Ldstr && ins.Operand is string s)
                        strings.Add(s);
            // 兜底: const string 字段(如 private const string HookName = "...")
            if (strings.Count == 0)
            {
                var constField = t.Fields.FirstOrDefault(f => f.IsLiteral && f.InitialValue.Length > 0);
                if (constField != null && constField.Constant is string cs)
                    strings.Add(cs);
            }
            if (strings.Count == 0)
            {
                Console.Error.WriteLine($"[GEN][WARN] TargetMethod(s) 取不到字符串常量,跳过: {t.FullName}");
                continue;
            }
            if (strings.Count >= 2 && gameModule.GetType(strings[0]) != null)
            {
                // 模式 A: 类型全名 + 方法名
                var typeDef = gameModule.GetType(strings[0])!;
                var mm = FindMethodByName(typeDef, strings[1])
                    ?? throw new InvalidOperationException($"[GEN] 模式A 找不到方法 {strings[0]}.{strings[1]} ({t.FullName})");
                targets.Add((typeDef.FullName, mm.Name, null));
                Console.WriteLine($"[GEN] {t.FullName} → {typeDef.FullName}.{mm.Name} (TargetMethod 静态解析)");
            }
            else
            {
                // 模式 B: Hook 钩子名
                string hookName = strings[0];
                var hookType = gameModule.GetType("MegaCrit.Sts2.Core.Hooks.Hook")
                    ?? throw new InvalidOperationException("游戏里找不到 MegaCrit.Sts2.Core.Hooks.Hook");
                var matches = hookType.Methods.Where(m => m.IsStatic && m.IsPublic && m.Name == hookName
                    && m.ReturnType.FullName.StartsWith("System.Threading.Tasks.Task")).ToList();
                foreach (var m in matches)
                    targets.Add((hookType.FullName, m.Name, null));
                Console.WriteLine($"[GEN] {t.FullName} → Hook.{hookName} x{matches.Count}");
            }
        }

        foreach (var m in t.Methods.Where(m => m.IsStatic))
        {
            string? kind = m.Name switch { "Prefix" => "prefix", "Postfix" => "postfix", "Finalizer" => "finalizer", _ => null };
            kind ??= m.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyPrefix") ? "prefix" : null;
            kind ??= m.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyPostfix") ? "postfix" : null;
            kind ??= m.CustomAttributes.Any(a => a.AttributeType.Name == "HarmonyFinalizer") ? "finalizer" : null;
            if (kind == null) continue;

            int priority = classPriority;
            var mp = m.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "HarmonyPriority");
            if (mp != null && mp.ConstructorArguments.Count > 0)
                priority = Convert.ToInt32(mp.ConstructorArguments[0].Value);

            foreach (var (typeFullName, methodName, targetParams) in targets)
                entries.Add(new PatchEntry(typeFullName, methodName, kind, t.FullName, m.Name, targetParams, priority));

            // 跨程序集 call 需要 public: 翻转钩子方法与声明类型,写回 mod.dll
            if (!m.IsPublic) { m.IsPublic = true; Console.WriteLine($"[GEN] flip method public: {t.FullName}.{m.Name}"); }
            if (!t.IsPublic) { t.IsPublic = true; Console.WriteLine($"[GEN] flip type public: {t.FullName}"); }
        }
    }

    // 引导条目: ModManager.Initialize 完成后挂载 pck 并初始化
    entries.Add(new PatchEntry("MegaCrit.Sts2.Core.Modding.ModManager", "Initialize", "postfix",
        "WatcherMod.WatcherBootstrap", "ModManagerInitializePostfix", null, 0));

    // 排序: 同目标同 kind 按 Harmony 语义 — prefix 高优先级先跑(weaver 前插,低优先级先插入即高优先级在前);
    // postfix 低优先级先跑(插入序即运行序)。统一升序即可。
    entries = entries
        .OrderBy(e => e.TargetType).ThenBy(e => e.TargetMethod)
        .ThenBy(e => e.Kind == "prefix" ? 0 : e.Kind == "finalizer" ? 1 : 2)
        .ThenBy(e => e.Priority)
        .ToList();

    File.WriteAllText(outPath, JsonSerializer.Serialize(new Manifest(entries),
        new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    modAsm.Write();
    Console.WriteLine($"完成: {entries.Count} 条目 → {outPath} (mod dll 已写回 public 翻转)");
    return 0;
}

record Manifest(List<PatchEntry> Patches, List<StripEntry>? StripReadonly = null, List<ConstEntry>? PatchConsts = null, List<RedirectEntry>? RedirectCalls = null);
// GenericArg: 可选 — 匹配泛型实例调用(如 NextItem<NEventOptionButton>), 按泛型实参全名过滤
record RedirectEntry(string NamespacePrefix, string TargetType, string TargetMethod, string HookType, string HookMethod, string? GenericArg = null);
record StripEntry(string Type, List<string> Fields);
// MethodContains: 匹配方法名或嵌套类型名(async 状态机如 <PlayRunAsync>d__N)
record ConstEntry(string Type, int FromInt, int ToInt, string? MethodContains = null);
// TargetParams: 可选, 重载消歧用 — 按参数类型名(FullName 或简单名)匹配。null=方法名唯一时直接用。
// Priority: Harmony 优先级, 同目标同 kind 排序用(prefix 高优先级先跑, postfix 低优先级先跑)
record PatchEntry(string TargetType, string TargetMethod, string Kind, string HookType, string HookMethod, List<string>? TargetParams = null, int Priority = 400);

// --gen: 从 mod 程序集提取 [HarmonyPatch] 生成织入清单。
// 1) 解析目标: typeof/字符串 + MethodType(Normal/Getter/Setter/…) + argumentTypes(→targetParams);
//    [HarmonyPatch()] 空特性类走 TargetMethods() 运行时反射 — 按 mod 的 FindHookMethods 约定
//    (游戏 MegaCrit.Sts2.Core.Hooks.Hook 类的公开静态同名 Task 方法) 在构建期静态解析。
// 2) 钩子方法(Prefix/Postfix/Finalizer)与声明类型翻成 public — 跨程序集 call 需要。
// 3) 写回 mod.dll, 输出 manifest(含 ModManager.Initialize → WatcherBootstrap.ModManagerInitializePostfix 引导条目)。
