using Extism.Sdk;
using Extism.Sdk.Native;

using System.Runtime.InteropServices;
using System.Text;

var kvStore = new Dictionary<string, byte[]>();

Console.WriteLine($"Version: {Plugin.ExtismVersion()}");

var userData = Marshal.StringToHGlobalAnsi("Hello again!");

var manifest = new Manifest(new UrlWasmSource("https://github.com/extism/plugins/releases/latest/download/count_vowels_kvstore.wasm"));

var functions = new[]
{
    HostFunction.FromMethod("kv_read", "env", IntPtr.Zero, (CurrentPlugin plugin, long keyOffset) =>
    {
        var key = plugin.ReadString(keyOffset);
        if (!kvStore.TryGetValue(key, out var value))
        {
            value = new byte[] { 0, 0, 0, 0 };
        }

        Console.WriteLine($"Read {BitConverter.ToUInt32(value)} from key={key}");
        return plugin.WriteBytes(value);
    }),

    HostFunction.FromMethod("kv_write", "env", IntPtr.Zero, (CurrentPlugin plugin, long keyOffset, long valueOffset) =>
    {
        var key = plugin.ReadString(keyOffset);
        var value = plugin.ReadBytes(valueOffset);

        Console.WriteLine($"Writing value={BitConverter.ToUInt32(value)} from key={key}");
        kvStore[key] = value.ToArray();
    })
};

using var plugin = new Plugin(manifest, functions, withWasi: true);

var output = Encoding.UTF8.GetString(
    plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello World!"))
);

Console.WriteLine($"Output: {output}");

output = Encoding.UTF8.GetString(
    plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello World!"))
);

Console.WriteLine($"Output: {output}");