namespace Smoke;

// 织入目标:STS2Weaver 功能冒烟测试的"游戏程序集"替身
public class Calculator
{
    public int Add(int a, int b) => a + b;
    public string Greet(string name) => "hi " + name;
    public static string Shout(string text) => text.ToUpper();
    public static int Mul(int x, int y) => x * y;
}
