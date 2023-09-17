using Extism.Sdk.Native;

using System.Reflection;
using System.Text;

using Xunit;

namespace Extism.Sdk.Tests;

public class ManifestTests
{
    [Fact]
    public void LoadPluginFromByteArray()
    {
        var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var wasm = File.ReadAllBytes(Path.Combine(binDirectory, "code.wasm"));

        var manifest = new Manifest(new ByteArrayWasmSource(wasm, "main"));

        using var plugin = new Plugin(manifest, Array.Empty<HostFunction>(), withWasi: true);

        var response = plugin.CallFunction("count_vowels", Encoding.UTF8.GetBytes("Hello World"));
        Assert.Equal("{\"count\": 3}", Encoding.UTF8.GetString(response));
    }

    [Fact]
    public void LoadPluginFromPath()
    {
        var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var manifest = new Manifest(new PathWasmSource(Path.Combine(binDirectory, "code.wasm"), "main"));

        using var plugin = new Plugin(manifest, Array.Empty<HostFunction>(), withWasi: true);

        var response = plugin.CallFunction("count_vowels", Encoding.UTF8.GetBytes("Hello World"));
        Assert.Equal("{\"count\": 3}", Encoding.UTF8.GetString(response));
    }

    [Fact]
    public void LoadPluginFromUri()
    {
        var source = new UrlWasmSource("https://raw.githubusercontent.com/extism/extism/main/wasm/code.wasm")
        {
            Method = HttpMethod.GET,
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Basic <credentials>" }
            }
        };

        var manifest = new Manifest(source);

        using var plugin = new Plugin(manifest, Array.Empty<HostFunction>(), withWasi: true);

        var response = plugin.CallFunction("count_vowels", Encoding.UTF8.GetBytes("Hello World"));
        Assert.Equal("{\"count\": 3}", Encoding.UTF8.GetString(response));
    }

    [Theory]
    [InlineData("hello", "{\"config\": \"hello\"}")]
    [InlineData("", "{\"config\": \"<unset by host>\"}")]
    public void CanSetPluginConfig(string thing, string expected)
    {
        var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var manifest = new Manifest(new PathWasmSource(Path.Combine(binDirectory, "config.wasm"), "main"));

        if (!string.IsNullOrEmpty(thing))
        {
            manifest.Config["thing"] = thing;
        }

        using var plugin = new Plugin(manifest, Array.Empty<HostFunction>(), withWasi: true);
        var response = plugin.CallFunction("run_test", Array.Empty<byte>());
        var text = Encoding.UTF8.GetString(response);

        Assert.Equal(expected, text);
    }
}
