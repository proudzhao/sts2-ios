using Smoke;

namespace Smoke.Hooks;

public static class Hooks
{
    // Add(int,int) 的 prefix:两个参数精确匹配,返回 bool → 合法(正例)
    public static bool AddPrefix(int a, int b) => true;

    // Greet(string) 的 postfix:__instance 精确匹配,参数按名匹配 → 合法(正例)
    public static void GreetPostfix(Calculator __instance, string name) { }

    // Shout(string) 的 postfix:ref __result 元素 string == 返回类型 string → 合法(正例)
    public static void ShoutPostfix(ref string __result) { }

    // 负例 1:Mul(int,int) 的 prefix,参数 x 同名但类型错误(string vs int)
    // → 应被类型兼容检查拒绝,织入器拒绝出产物
    public static bool BadTypePrefix(string x, int y) => true;

    // 负例 2:Shout 的 prefix 用非 ref 的 __result → 应报 "prefix 的 __result 必须是 ref"
    public static bool BadResultPrefix(string __result) => true;
}
