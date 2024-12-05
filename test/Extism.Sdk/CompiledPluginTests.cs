using Shouldly;

using System.Runtime.InteropServices;
using System.Text;

using Xunit;

using static Extism.Sdk.Tests.BasicTests;

namespace Extism.Sdk.Tests;

public class CompiledPluginTests
{
    [Fact]
    public void CountVowels()
    {
        using var compiledPlugin = Helpers.CompilePlugin("code.wasm");

        for (var i = 0; i < 3; i++)
        {
            using var plugin = compiledPlugin.Instantiate();

            var response = plugin.Call<CountVowelsResponse>("count_vowels", "Hello World");

            response.ShouldNotBeNull();
            response.Count.ShouldBe(3);
        }
    }

    [Fact]
    public void CountVowelsHostFunctions()
    {
        var userData = "Hello again!";
        using var helloWorld = HostFunction.FromMethod<long, long>("hello_world", userData, HelloWorld);

        using var compiledPlugin = Helpers.CompilePlugin("code-functions.wasm", null, helloWorld);
        for (int i = 0; i < 3; i++)
        {
            using var plugin = compiledPlugin.Instantiate();

            var response = plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello World"));
            Encoding.UTF8.GetString(response).ShouldBe("{\"count\": 3}");
        }

        long HelloWorld(CurrentPlugin plugin, long ptr)
        {
            Console.WriteLine("Hello from .NET!");

            var text = plugin.GetUserData<string>();
            Console.WriteLine(text);

            var input = plugin.ReadString(ptr);
            Console.WriteLine($"Input: {input}");

            return plugin.WriteString(new string(input)); // clone the string
        }
    }

}
