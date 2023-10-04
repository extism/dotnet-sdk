# Extism .NET Host SDK

This repo houses the .NET SDK for integrating with the [Extism](https://extism.org/) runtime. Install this library into your host .NET applications to run Extism plugins.

> **Note:** If you're unsure what Extism is or what an SDK is see our homepage: https://extism.org.

> **Note**: This houses the 1.0 version of the .NET SDK and is a work in progress. Please use the .NET SDK in extism/extism until we hit 1.0.

## Installation

For this library, you first need to install the Extism Runtime.

### Windows

We provide [a native package](https://www.nuget.org/packages/Extism.runtime.win-x64) for Windows. You can install with:
> ```
> dotnet add package Extism.runtime.win-64
>```

### Linux and macOS

You can [download the shared object directly from a release](https://github.com/extism/extism/releases) or use the [Extism CLI](https://github.com/extism/cli) to install it:

```
sudo extism lib install latest

#=> Fetching https://github.com/extism/extism/releases/download/v0.5.2/libextism-aarch64-apple-darwin-v0.5.2.tar.gz
#=> Copying libextism.dylib to /usr/local/lib/libextism.dylib
#=> Copying extism.h to /usr/local/include/extism.h
```

> Note: This library has breaking changes and targets 1.0 of the runtime. For the time being, install the runtime from our nightly development builds on git: sudo extism lib install --version git.

### Install the NuGet package

Add this [NuGet package](https://www.nuget.org/packages/Extism.Sdk) to your project:

```
dotnet add package Extism.Sdk
```

## Getting Started

This guide should walk you through some of the concepts in Extism and this .NET library.

First you should add a using statement for Extism:

C#:
```csharp
using Extism.Sdk;
using Extism.Sdk.Native;
```

F#:
```
open Extism.Sdk
open Extism.Sdk.Native
```

## Creating A Plug-in

The primary concept in Extism is the [plug-in](https://extism.org/docs/concepts/plug-in). You can think of a plug-in as a code module stored in a `.wasm` file.

Since you may not have an Extism plug-in on hand to test, let's load a demo plug-in from the web:

C#:
```csharp
var manifest = new Manifest(new UrlWasmSource("https://github.com/extism/plugins/releases/latest/download/count_vowels.wasm"));

using var plugin = new Plugin(manifest, new HostFunction[] { }, withWasi: true);
```

F#:
```fsharp
let manifest = 
    Manifest(new UrlWasmSource(Uri("https://github.com/extism/plugins/releases/latest/download/count_vowels.wasm")))

use plugin = 
    Plugin(manifest, Array.empty<HostFunction>(), withWasi = true)
```

> **Note**: The schema for this manifest can be found here: https://extism.org/docs/concepts/manifest/

### Calling A Plug-in's Exports

This plug-in was written in Rust and it does one thing, it counts vowels in a string. As such, it exposes one "export" function: `count_vowels`. We can call exports using `Plugin.Call`:

C#:
```csharp
var inputBytes = Encoding.UTF8.GetBytes("Hello, World!");
var output = Encoding.UTF8.GetString(plugin.Call("count_vowels", inputBytes));

// => {"count": 3, "total": 3, "vowels": "aeiouAEIOU"}
```

F#:
```fsharp
let inputBytes = Encoding.UTF8.GetBytes("Hello, World!")
let output = Encoding.UTF8.GetString(plugin.Call("count_vowels", inputBytes))

// => {"count": 3, "total": 3, "vowels": "aeiouAEIOU"}
```

All exports have a simple interface of optional bytes in, and optional bytes out. This plug-in happens to take a string and return a JSON encoded string with a report of results.

### Plug-in State

Plug-ins may be stateful or stateless. Plug-ins can maintain state b/w calls by the use of variables. Our count vowels plug-in remembers the total number of vowels it's ever counted in the "total" key in the result. You can see this by making subsequent calls to the export:

C#:
```csharp
var output = Encoding.UTF8.GetString(
    plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello, World!"))
);

// => {"count": 3, "total": 6, "vowels": "aeiouAEIOU"}

output = Encoding.UTF8.GetString(
    plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello, World!"))
);
// => {"count": 3, "total": 9, "vowels": "aeiouAEIOU"}
```

F#:
```fsharp
let inputBytes = Encoding.UTF8.GetBytes("Hello, World!")

let output1 = Encoding.UTF8.GetString(plugin.Call("count_vowels", inputBytes))
// => {"count": 3, "total": 6, "vowels": "aeiouAEIOU"}

let output2 = Encoding.UTF8.GetString(plugin.Call("count_vowels", inputBytes))
// => {"count": 3, "total": 9, "vowels": "aeiouAEIOU"}
```

These variables will persist until this plug-in is freed or you initialize a new one.

### Configuration

Plug-ins may optionally take a configuration object. This is a static way to configure the plug-in. Our count-vowels plugin takes an optional configuration to change out which characters are considered vowels. Example:

C#:
```csharp
var manifest = new Manifest(new UrlWasmSource("https://github.com/extism/plugins/releases/latest/download/count_vowels.wasm"));

using var plugin = new Plugin(manifest, new HostFunction[] { }, withWasi: true);

var output = Encoding.UTF8.GetString(
    plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Yellow, World!"))
);

// => {"count": 3, "total": 3, "vowels": "aeiouAEIOU"}

var manifest = new Manifest(new UrlWasmSource("https://github.com/extism/plugins/releases/latest/download/count_vowels.wasm"))
{
    Config = new Dictionary<string, string>
    {
        { "vowels", "aeiouyAEIOUY" }
    },
};

using var plugin = new Plugin(manifest, new HostFunction[] { }, withWasi: true);

var output = Encoding.UTF8.GetString(
    plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Yellow, World!"))
);

// => {"count": 4, "total": 4, "vowels": "aeiouAEIOUY"}
```

F#:
```fsharp
let manifest = Manifest(new UrlWasmSource(Uri("https://github.com/extism/plugins/releases/latest/download/count_vowels.wasm")))

use plugin = Plugin(manifest, Array.empty<HostFunction>(), withWasi = true)

let outputBytes = plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Yellow, World!"))
let output = Encoding.UTF8.GetString(outputBytes)

let manifest = 
    Manifest(new UrlWasmSource(Uri("https://github.com/extism/plugins/releases/latest/download/count_vowels.wasm")),
            Config = Dictionary<string, string>([("vowels", "aeiouyAEIOUY")]))

use plugin = 
    Plugin(manifest, Array.empty<HostFunction>(), withWasi = true)

let outputBytes = 
    plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Yellow, World!"))
let output = Encoding.UTF8.GetString(outputBytes)
```

### Host Functions

Let's extend our count-vowels example a little bit: Instead of storing the `total` in an ephemeral plug-in var, let's store it in a persistent key-value store!

Wasm can't use our KV store on it's own. This is where `Host Functions` come in.

[Host functions](https://extism.org/docs/concepts/host-functions) allow us to grant new capabilities to our plug-ins from our application. They are simply some Go functions you write which can be passed down and invoked from any language inside the plug-in.

Let's load the manifest like usual but load up this `count_vowels_kvstore` plug-in:

C#:
```csharp
var manifest = new Manifest(new UrlWasmSource("https://github.com/extism/plugins/releases/latest/download/count_vowels_kvstore.wasm"));
```

F#:
```fsharp
let manifest = Manifest(new UrlWasmSource(Uri("https://github.com/extism/plugins/releases/latest/download/count_vowels_kvstore.wasm")))
```

> *Note*: The source code for this is [here](https://github.com/extism/plugins/blob/main/count_vowels_kvstore/src/lib.rs) and is written in rust, but it could be written in any of our PDK languages.

Unlike our previous plug-in, this plug-in expects you to provide host functions that satisfy our its import interface for a KV store.

We want to expose two functions to our plugin, `void kv_write(key string, value byte[])` which writes a bytes value to a key and `byte[] kv_read(key string)` which reads the bytes at the given `key`.

C#:
```csharp
// pretend this is Redis or something :)
var kvStore = new Dictionary<string, byte[]>();

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
```

F#:
```
```

> *Note*: In order to write host functions you should get familiar with the methods on the CurrentPlugin type. The `plugin` parameter is an instance of this type.

We need to pass these imports to the plug-in to create them. All imports of a plug-in must be satisfied for it to be initialized:

C#:
```csharp
using var plugin = new Plugin(manifest, functions, withWasi: true);

var output = Encoding.UTF8.GetString(
    plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello World!"))
);

Console.WriteLine($"Output: {output}");
// => Read from key=count-vowels"
// => Writing value=3 from key=count-vowels"
// => {"count": 3, "total": 3, "vowels": "aeiouAEIOU"}

output = Encoding.UTF8.GetString(
    plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello World!"))
);

Console.WriteLine($"Output: {output}");
// => Read from key=count-vowels"
// => Writing value=6 from key=count-vowels"
// => {"count": 3, "total": 6, "vowels": "aeiouAEIOU"}
```

F#:
```fsharp
use plugin = Plugin(manifest, functions, withWasi = true)

let outputBytes = plugin.Call("count_vowels", Encoding.UTF8.GetBytes("Hello World!"))
printfn "Output: %s" Encoding.UTF8.GetString(outputBytes)
// => Read from key=count-vowels
// => Writing value=3 from key=count-vowels
// => {"count": 3, "total": 3, "vowels": "aeiouAEIOU"}

let outputBytes2 = plugin.Call("count_vowels", inputBytes)
printfn "Output: %s" Encoding.UTF8.GetString(outputBytes2)
// => Read from key=count-vowels
// => Writing value=6 from key=count-vowels
// => {"count": 3, "total": 6, "vowels": "aeiouAEIOU"}
```