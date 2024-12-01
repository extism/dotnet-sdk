using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;

using Extism.Sdk;

using System.Reflection;

var summary = BenchmarkRunner.Run<CompiledPluginBenchmarks>();

public class CompiledPluginBenchmarks
{
    private const int N = 1000;
    private const string _input = "Hello, World!";
    private const string _function = "count_vowels";
    private readonly Manifest _manifest;

    public CompiledPluginBenchmarks()
    {
        var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        _manifest = new Manifest(new PathWasmSource(Path.Combine(binDirectory, "wasm", "code.wasm"), "main"));
    }

    [Benchmark]
    public void CompiledPluginInstantiate()
    {
        using var compiledPlugin = new CompiledPlugin(_manifest, [], withWasi: true);

        for (var i = 0; i < N; i++)
        {
            using var plugin = compiledPlugin.Instantiate();
            var response = plugin.Call(_function, _input);
        }
    }

    [Benchmark]
    public void PluginInstantiate()
    {
        for (var i = 0; i < N; i++)
        {
            using var plugin = new Plugin(_manifest, [], withWasi: true);
            var response = plugin.Call(_function, _input);
        }
    }
}

public class CountVowelsResponse
{
    public int Count { get; set; }
    public int Total { get; set; }
    public string? Vowels { get; set; }
}