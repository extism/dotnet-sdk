# Extism .NET Host SDK

> **Note**: This houses the 1.0 version of the .NET SDK and is a work in progress. Please use the .NET SDK in extism/extism until we hit 1.0.

This repo houses the .NET SDK for integrating with the [Extism](https://extism.org/) runtime. Install this library into your host .NET applications to run Extism plugins.

## Installation

You first need to [install the Extism runtime](https://extism.org/docs/install).

> **__NOTE:__** We provide a native package for Windows. You can install with:
> ```
> dotnet add package Extism.runtime.win-64
>```

Add this NuGet package to your project:

```
dotnet add package Extism.Sdk
```

## Getting Started

First you should add a using statement for Extism:

```
using Extism.Sdk;
using Extism.Sdk.Native;
```

## Creating A Plug-in

The primary concept in Extism is the plug-in. You can think of a plug-in as a code module. It has imports and it has exports. These imports and exports define the interface, or your API. You decide what they are called and typed, and what they do. Then the plug-in developer implements them and you can call them.

The code for a plug-in exist as a binary wasm module. We can load this with the raw bytes or we can use the manifest to tell Extism how to load it from disk or the web.

For simplicity let's load one from the web:

```csharp
var manifest = new Manifest(new UrlWasmSource("https://raw.githubusercontent.com/extism/extism/main/wasm/code.wasm"));

using var plugin = new Plugin(manifest, new HostFunction[] { }, withWasi: true);
```

> **Note**: The schema for this manifest can be found here: https://extism.org/docs/concepts/manifest/


This plug-in was written in C and it does one thing, it counts vowels in a string. As such it exposes one "export" function: `count_vowels`. We can call exports using `Plugin.CallFunction`:

```csharp
var output = Encoding.UTF8.GetString(
    plugin.CallFunction("count_vowels", Encoding.UTF8.GetBytes("Hello, World!"))
);

// => {"count": 3, "total": 3, "vowels": "aeiouAEIOU"}
```

All exports have a simple interface of optional bytes in, and optional bytes out. This plug-in happens to take a string and return a JSON encoded string with a report of results.

### Plug-in State

Plug-ins may be stateful or stateless. Plug-ins can maintain state b/w calls by the use of variables. Our count vowels plug-in remembers the total number of vowels it's ever counted in the "total" key in the result. You can see this by making subsequent calls to the export:

```csharp
var output = Encoding.UTF8.GetString(
    plugin.CallFunction("count_vowels", Encoding.UTF8.GetBytes("Hello, World!"))
);

// => {"count": 3, "total": 6, "vowels": "aeiouAEIOU"}

output = Encoding.UTF8.GetString(
    plugin.CallFunction("count_vowels", Encoding.UTF8.GetBytes("Hello, World!"))
);
// => {"count": 3, "total": 9, "vowels": "aeiouAEIOU"}
```

These variables will persist until this plug-in is freed or you initialize a new one.

### Configuration

Plug-ins may optionally take a configuration object. This is a static way to configure the plug-in. Our count-vowels plugin takes an optional configuration to change out which characters are considered vowels. Example:

```csharp
var manifest = new Manifest(new UrlWasmSource("https://raw.githubusercontent.com/extism/extism/main/wasm/code.wasm"));

using var plugin = new Plugin(manifest, new HostFunction[] { }, withWasi: true);

var output = Encoding.UTF8.GetString(
    plugin.CallFunction("count_vowels", Encoding.UTF8.GetBytes("Yellow, World!"))
);

// => {"count": 3, "total": 3, "vowels": "aeiouAEIOU"}

var manifest = new Manifest(new UrlWasmSource("https://raw.githubusercontent.com/extism/extism/main/wasm/count-vowels-host.wasm"))
{
    Config = new Dictionary<string, string>
    {
        { "vowels", "aeiouyAEIOUY" }
    },
};

using var plugin = new Plugin(manifest, new HostFunction[] { }, withWasi: true);

var output = Encoding.UTF8.GetString(
    plugin.CallFunction("count_vowels", Encoding.UTF8.GetBytes("Yellow, World!"))
);

// => {"count": 4, "total": 4, "vowels": "aeiouAEIOUY"}
```

### Host Functions

Host functions can be a complicated concept. You can think of them like custom syscalls for your plug-in. You can use them to add capabilities to your plug-in through a simple interface.

Another way to look at it is this: Up until now we've only invoked functions given to us by our plug-in, but what if our plug-in needs to invoke a function in our .NET app? Host functions allow you to do this by passing a reference to a .NET method to the plug-in.

Let's load up a version of count vowels with a host function:

```csharp
var manifest = new Manifest(new UrlWasmSource("https://raw.githubusercontent.com/extism/extism/main/wasm/count-vowels-host.wasm"));
```

Unlike our original plug-in, this plug-in expects you to provide your own implementation of "is_vowel" in C#.

First let's write our host function:

```csharp
void HelloWorld(CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs, nint data)
{
    Console.WriteLine("Hello from .NET!");

    var letter = plugin.ReadString((nint)inputs[0].v.i64);

    outputs[0].v.i64 = "aeiouAEIOU".Contains(letter) ? 1 : 0;
}
```

This method will be exposed to the plug-in in it's native language. We need to know the inputs and outputs and their types ahead of time. This function expects a string (single character) as the first input and expects a 0 (false) or 1 (true) in the output (returns).

We need to pass these imports to the plug-in to create them. All imports of a plug-in must be satisfied for it to be initialized:

```csharp
// we need to give it the Wasm signature, it takes one i64 as input which acts as a pointer to a string
// and it returns an i64 which is the 0 or 1 result
using var helloWorld = new HostFunction(
    "is_vowel",
    "env",
    new[] { ExtismValType.I64 },
    new[] { ExtismValType.I64 },
    null,
    HelloWorld);

using var plugin = new Plugin(manifest, new [] { helloWorld }, withWasi: true);

var output = Encoding.UTF8.GetString(
    plugin.CallFunction("count_vowels", Encoding.UTF8.GetBytes("Yellow, World!"))
);

// => Hello From .NET!
// => {"count": 3, "total": 3}
```

Although this is a trivial example, you could imagine some more elaborate APIs for host functions. This is truly how you unleash the power of the plugin. You could, for example, imagine giving the plug-in access to APIs your app normally has like reading from a database, authenticating a user, sending messages, etc.