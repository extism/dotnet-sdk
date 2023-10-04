open Extism.Sdk
open Extism.Sdk.Native
open System
open System.Text
open System.Collections.Generic

printfn "hiiii"

let manifest = Manifest(new UrlWasmSource(Uri("https://github.com/extism/plugins/releases/latest/download/count_vowels_kvstore.wasm")))

let kvStore = new Dictionary<string, byte[]>()

let functions =
    [|
        HostFunction.FromMethod("kv_read", "env", IntPtr.Zero, fun (plugin: CurrentPlugin) (keyOffset: int64) ->
            let key = plugin.ReadString(keyOffset)
            let value = 
                match kvStore.TryGetValue(key) with
                | true, v -> v
                | _ -> [| 0uy; 0uy; 0uy; 0uy |] // Default value if key not found

            Console.WriteLine($"Read {BitConverter.ToUInt32(value, 0)} from key={key}")
            plugin.WriteBytes(value)
        )

        HostFunction.FromMethod("kv_write", "env", IntPtr.Zero, fun (plugin: CurrentPlugin) (keyOffset: int64) (valueOffset: int64) ->
            let key = plugin.ReadString(keyOffset)
            let value = plugin.ReadBytes(valueOffset).ToArray()

            Console.WriteLine($"Writing value={BitConverter.ToUInt32(value, 0)} from key={key}")
            kvStore.[key] <- value
        )
    |]

use plugin = 
    Plugin(manifest, functions, withWasi = true)

printfn "plugin created"

let inputBytes = Encoding.UTF8.GetBytes("Hello, World!")
let output = Encoding.UTF8.GetString(plugin.Call("count_vowels", inputBytes))

printfn "%s" output

let output1 = Encoding.UTF8.GetString(plugin.Call("count_vowels", inputBytes))
// => {"count": 3, "total": 6, "vowels": "aeiouAEIOU"}

let output2 = Encoding.UTF8.GetString(plugin.Call("count_vowels", inputBytes))
// => {"count": 3, "total": 9, "vowels": "aeiouAEIOU"}

printfn "%s" output2