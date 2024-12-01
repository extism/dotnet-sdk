using Extism.Sdk.Native;

using Shouldly;

using System.Runtime.InteropServices;
using System.Text;

using Xunit;

namespace Extism.Sdk.Tests;

public class BasicTests
{
    [Fact]
    public void Alloc()
    {
        using var plugin = Helpers.LoadPlugin("alloc.wasm");
        _ = plugin.Call("run_test", Array.Empty<byte>());
    }

    [Fact]
    public void GetId()
    {
        using var plugin = Helpers.LoadPlugin("alloc.wasm");
        var id = plugin.Id;
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public void Fail()
    {
        using var plugin = Helpers.LoadPlugin("fail.wasm");

        Should.Throw<ExtismException>(() => plugin.Call("run_test", Array.Empty<byte>()));
    }


    [Theory]
    [InlineData("abc", 1)]
    [InlineData("", 2)]
    public void Exit(string code, int expected)
    {
        using var plugin = Helpers.LoadPlugin("exit.wasm", m =>
        {
            m.Config["code"] = code;
        });

        var exception = Should.Throw<ExtismException>(() => plugin.Call("_start", Array.Empty<byte>()));

        exception.Message.ShouldContain(expected.ToString());
        exception.Message.ShouldContain("WASI exit code");
    }

    [Fact]
    public void Timeout()
    {
        using var plugin = Helpers.LoadPlugin("sleep.wasm", m =>
        {
            m.Timeout = TimeSpan.FromMilliseconds(50);
            m.Config["duration"] = "3"; // sleep for 3 seconds
        });

        Should.Throw<ExtismException>(() => plugin.Call("run_test", Array.Empty<byte>()))
            .Message.ShouldContain("timeout");
    }

    [Fact]
    public void Cancel()
    {
        using var plugin = Helpers.LoadPlugin("sleep.wasm", m =>
        {
            m.Config["duration"] = "1"; // sleep for 1 seconds
        });

        for (var i = 0; i < 3; i++)
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(50));

            Should.Throw<ExtismException>(() => plugin.Call("run_test", Array.Empty<byte>(), cts.Token))
               .Message.ShouldContain("timeout");

            Should.Throw<OperationCanceledException>(() => plugin.Call("run_test", Array.Empty<byte>(), cts.Token));
        }

        // We should be able to call the plugin normally after a cancellation
        plugin.Call("run_test", Array.Empty<byte>());
    }

    [Fact]
    public void FileSystem()
    {
        using var plugin = Helpers.LoadPlugin("fs.wasm", m =>
        {
            m.AllowedPaths.Add("data", "/mnt");
        });

        var output = plugin.Call("_start", Array.Empty<byte>());
        var text = Encoding.UTF8.GetString(output);

        text.ShouldBe("hello world!");
    }

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

        var response = plugin.Call("count_vowels", "Hello World");
        response.ShouldContain("\"count\":3");
    }

    [Fact]
    public void CountVowelsJson()
    {
        using var plugin = Helpers.LoadPlugin("code.wasm");

        var response = plugin.Call<CountVowelsResponse>("count_vowels", "Hello World");

        response.ShouldNotBeNull();
        response.Count.ShouldBe(3);
    }

    [Fact]
    public void CountVowelsHostFunctionsBackCompat()
    {
        for (int i = 0; i < 100; i++)
        {
            var userData = Marshal.StringToHGlobalAnsi("Hello again!");

            using var helloWorld = new HostFunction(
                "hello_world",
                new[] { ExtismValType.PTR },
                new[] { ExtismValType.PTR },
                userData,
                HelloWorld);

            using var plugin = Helpers.LoadPlugin("code-functions.wasm", config: null, helloWorld);

            var response = plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello World"));
            Encoding.UTF8.GetString(response).ShouldBe("{\"count\": 3}");
        }

        void HelloWorld(CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs)
        {
            Console.WriteLine("Hello from .NET!");


#pragma warning disable CS0618 // Type or member is obsolete
            var text = Marshal.PtrToStringAnsi(plugin.UserData);
#pragma warning restore CS0618 // Type or member is obsolete
            Console.WriteLine(text);

            var context = plugin.GetHostContext<Dictionary<string, object>>();
            if (context is null || !context.ContainsKey("answer"))
            {
                throw new InvalidOperationException("Context not found");
            }

            Assert.Equal(42, context["answer"]);

            var input = plugin.ReadString(new nint(inputs[0].v.ptr));
            Console.WriteLine($"Input: {input}");

            var output = new string(input); // clone the string
            outputs[0].v.ptr = plugin.WriteString(output);
        }
    }

    [Fact]
    public void CountVowelsHostFunctions()
    {
        for (int i = 0; i < 100; i++)
        {
            var userData = "Hello again!";

            using var helloWorld = new HostFunction(
                "hello_world",
                new[] { ExtismValType.PTR },
                new[] { ExtismValType.PTR },
                userData,
                HelloWorld);

            using var plugin = Helpers.LoadPlugin("code-functions.wasm", config: null, helloWorld);

            var dict = new Dictionary<string, object>
            {
                { "answer", 42 }
            };

            var response = plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello World"), dict);
            Encoding.UTF8.GetString(response).ShouldBe("{\"count\": 3}");
        }

        void HelloWorld(CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs)
        {
            Console.WriteLine("Hello from .NET!");

            var text = plugin.GetUserData<string>();
            Assert.Equal("Hello again!", text);

            var context = plugin.GetHostContext<Dictionary<string, object>>();
            if (context is null || !context.ContainsKey("answer"))
            {
                throw new InvalidOperationException("Context not found");
            }

            Assert.Equal(42, context["answer"]);

            var input = plugin.ReadString(new nint(inputs[0].v.ptr));
            Console.WriteLine($"Input: {input}");

            var output = new string(input); // clone the string
            outputs[0].v.ptr = plugin.WriteString(output);
        }
    }

    [Fact]
    public void HostFunctionsWithMemory()
    {
        var userData = Marshal.StringToHGlobalAnsi("Hello again!");

        using var helloWorld = HostFunction.FromMethod("to_upper", IntPtr.Zero, (CurrentPlugin plugin, long offset) =>
        {
            var input = plugin.ReadString(offset);
            var output = input.ToUpperInvariant();
            Console.WriteLine($"Result: {output}"); ;
            plugin.FreeBlock(offset);

            return plugin.WriteString(output);
        }).WithNamespace("host");

        using var plugin = Helpers.LoadPlugin("host_memory.wasm", config: null, helloWorld);

        var response = plugin.Call("run_test", Encoding.UTF8.GetBytes("Frodo"));
        Encoding.UTF8.GetString(response).ShouldBe("HELLO FRODO!");
    }

    [Fact]
    public void FuelLimit()
    {
        using var plugin = Helpers.LoadPlugin("loop.wasm", options: new PluginIntializationOptions
        {
            FuelLimit = 1000,
            WithWasi = true
        });

        Should.Throw<ExtismException>(() => plugin.Call("loop_forever", Array.Empty<byte>()))
            .Message.ShouldContain("fuel");
    }

    //[Fact]
    // flakey
    internal void FileLog()
    {
        var tempFile = Path.GetTempFileName();
        Plugin.ConfigureFileLogging(tempFile, LogLevel.Warn);
        using (var plugin = Helpers.LoadPlugin("log.wasm"))
        {
            plugin.Call("run_test", Array.Empty<byte>());
        }

        // HACK: tempFile gets locked by the Extism runtime 
        var tempFile2 = Path.GetTempFileName();
        File.Copy(tempFile, tempFile2, true);

        var content = File.ReadAllText(tempFile2);
        content.ShouldContain("warn");
        content.ShouldContain("error");
        content.ShouldNotContain("info");
        content.ShouldNotContain("debug");
        content.ShouldNotContain("trace");
    }


    // [Fact]
    // Interferes with FileLog
    internal void CustomLog()
    {
        var builder = new StringBuilder();

        Plugin.ConfigureCustomLogging(LogLevel.Warn);
        using (var plugin = Helpers.LoadPlugin("log.wasm"))
        {
            plugin.Call("run_test", Array.Empty<byte>());
        }

        Plugin.DrainCustomLogs(line => builder.AppendLine(line));

        var content = builder.ToString();
        content.ShouldContain("warn");
        content.ShouldContain("error");
        content.ShouldNotContain("info");
        content.ShouldNotContain("debug");
        content.ShouldNotContain("trace");
    }

    [Fact]
    public void F64Return()
    {
        using var plugin = Helpers.LoadPlugin("float.wasm", config: null, HostFunctions());

        var response = plugin.Call("addf64", Array.Empty<byte>());
        var result = BitConverter.ToDouble(response);
        result.ShouldBe(101.5);
    }

    [Fact]
    public void F32Return()
    {
        using var plugin = Helpers.LoadPlugin("float.wasm", config: null, HostFunctions());

        var response = plugin.Call("addf32", Array.Empty<byte>());
        var result = BitConverter.ToSingle(response);
        result.ShouldBe(101.5f);
    }

    private HostFunction[] HostFunctions()
    {
        var functions = new HostFunction[]
        {
            HostFunction.FromMethod<double>("getf64", IntPtr.Zero, (CurrentPlugin plugin) =>
            {
                return 100.5;
            }),

            HostFunction.FromMethod<float>("getf32", IntPtr.Zero, (CurrentPlugin plugin) =>
            {
                return 100.5f;
            }),
        };

        foreach (var function in functions)
        {
            function.SetNamespace("example");
        }

        return functions;
    }

    public class CountVowelsResponse
    {
        public int Count { get; set; }
        public int Total { get; set; }
        public string? Vowels { get; set; }
    }
}
