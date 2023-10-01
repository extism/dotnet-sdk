using Extism.Sdk.Native;
using Shouldly;
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
        var wasm = File.ReadAllBytes(Path.Combine(binDirectory, "wasm", "code.wasm"));

        var manifest = new Manifest(new ByteArrayWasmSource(wasm, "main"));

        using var plugin = new Plugin(manifest, Array.Empty<HostFunction>(), withWasi: true);

        var response = plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello World"));
        Encoding.UTF8.GetString(response).ShouldBe("{\"count\": 3}");
    }

    [Fact]
    public void LoadPluginFromPath()
    {
        var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var manifest = new Manifest(new PathWasmSource(Path.Combine(binDirectory, "wasm", "code.wasm"), "main"));

        using var plugin = new Plugin(manifest, Array.Empty<HostFunction>(), withWasi: true);

        var response = plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello World"));
        Encoding.UTF8.GetString(response).ShouldBe("{\"count\": 3}");
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

        var response = plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello World"));
        Encoding.UTF8.GetString(response).ShouldBe("{\"count\": 3}");
    }

    [Theory]
    [InlineData("hello", "{\"config\": \"hello\"}")]
    [InlineData("", "{\"config\": \"<unset by host>\"}")]
    public void CanSetPluginConfig(string thing, string expected)
    {
        using var plugin = Helpers.LoadPlugin("config.wasm", m =>
        {
            if (!string.IsNullOrEmpty(thing))
            {
                m.Config["thing"] = thing;
            }
        });

        var response = plugin.Call("run_test", Array.Empty<byte>());
        var actual = Encoding.UTF8.GetString(response);

        actual.ShouldBe(expected);
    }

    [Fact]
    public void CanMakeHttpCalls_WhenAllowed()
    {
        using var plugin = Helpers.LoadPlugin("http.wasm", m =>
        {
            m.AllowedHosts.Add("jsonplaceholder.*.com");
        });

        var expected =
        """
        {
          "userId": 1,
          "id": 1,
          "title": "delectus aut autem",
          "completed": false
        }
        """;

        var response = plugin.Call("run_test", Array.Empty<byte>());
        var actual = Encoding.UTF8.GetString(response);
        actual.ShouldBe(expected, StringCompareShould.IgnoreLineEndings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("google*")]
    public void CantMakeHttpCalls_WhenDenied(string allowedHost)
    {
        using var plugin = Helpers.LoadPlugin("http.wasm", m =>
        {
            if (!string.IsNullOrEmpty(allowedHost))
            {
                m.AllowedHosts.Add(allowedHost);
            }
        });

        Should.Throw<ExtismException>(() => plugin.Call("run_test", Array.Empty<byte>()));
    }
}
