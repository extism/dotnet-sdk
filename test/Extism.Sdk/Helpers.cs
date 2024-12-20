using System.Reflection;

namespace Extism.Sdk.Tests;

public static class Helpers
{
    public static Plugin LoadPlugin(string name, PluginIntializationOptions options, Action<Manifest>? config = null, params HostFunction[] hostFunctions)
    {
        var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var manifest = new Manifest(new PathWasmSource(Path.Combine(binDirectory, "wasm", name), "main"));

        if (config is not null)
        {
            config(manifest);
        }

        return new Plugin(manifest, hostFunctions, options);
    }

    public static Plugin LoadPlugin(string name, Action<Manifest>? config = null, params HostFunction[] hostFunctions)
    {
        return LoadPlugin(name, new PluginIntializationOptions
        {
            WithWasi = true,
        }, config, hostFunctions);
    }

    public static CompiledPlugin CompilePlugin(string name, Action<Manifest>? config = null, params HostFunction[] hostFunctions)
    {
        var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var manifest = new Manifest(new PathWasmSource(Path.Combine(binDirectory, "wasm", name), "main"));
        if (config is not null)
        {
            config(manifest);
        }

        return new CompiledPlugin(manifest, hostFunctions, withWasi: true);
    }
}