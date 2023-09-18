using Extism.Sdk.Native;
using Shouldly;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using Xunit;

namespace Extism.Sdk.Tests;

public class BasicTests
{
    [Theory]
    [InlineData("code.wasm", "count_vowels", true)]
    [InlineData("code.wasm", "i_dont_exist", false)]
    public void FunctionExists(string fileName, string functionName, bool expected)
    {
        using var plugin = Helpers.LoadPlugin(fileName);

        var actual = plugin.FunctionExists(functionName);
        actual.ShouldBe(expected);
    }

    [Fact]
    public void CountHelloWorldVowels()
    {
        using var plugin = Helpers.LoadPlugin("code.wasm");

        var response = plugin.CallFunction("count_vowels", Encoding.UTF8.GetBytes("Hello World"));
        Encoding.UTF8.GetString(response).ShouldBe("{\"count\": 3}");
    }

    [Fact]
    public void CountVowelsHostFunctions()
    {
        var userData = Marshal.StringToHGlobalAnsi("Hello again!");

        using var helloWorld = new HostFunction(
            "hello_world",
            "env",
            new[] { ExtismValType.I64 },
            new[] { ExtismValType.I64 },
            userData,
            HelloWorld);

        using var plugin = Helpers.LoadPlugin("code-functions.wasm", config: null, helloWorld);

        var response = plugin.CallFunction("count_vowels", Encoding.UTF8.GetBytes("Hello World"));
        Encoding.UTF8.GetString(response).ShouldBe("{\"count\": 3}");

        void HelloWorld(CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs, nint data)
        {
            Console.WriteLine("Hello from .NET!");

            var text = Marshal.PtrToStringAnsi(data);
            Console.WriteLine(text);

            var input = plugin.ReadString(new nint(inputs[0].v.i64));
            Console.WriteLine($"Input: {input}");

            var output = new string(input); // clone the string
            outputs[0].v.i64 = plugin.WriteString(output);
        }
    }
}
