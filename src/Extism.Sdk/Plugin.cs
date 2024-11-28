using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Extism.Sdk.Native;

namespace Extism.Sdk;

/// <summary>
/// Represents a WASM Extism plugin.
/// </summary>
public unsafe class Plugin : IDisposable
{
    private const int DisposedMarker = 1;

    private static readonly JsonSerializerOptions? _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HostFunction[] _functions;
    private int _disposed;
    private readonly IntPtr _cancelHandle;

    /// <summary>
    /// Native pointer to the Extism Plugin.
    /// </summary>
    internal LibExtism.ExtismPlugin* NativeHandle { get; }

    /// <summary>
    /// Instantiate a plugin from a compiled plugin.
    /// </summary>
    /// <param name="plugin"></param>
    internal Plugin(CompiledPlugin plugin)
    {
        char** errorMsgPtr;

        var handle = LibExtism.extism_plugin_new_from_compiled(plugin.NativeHandle, out errorMsgPtr);
        if (handle == null)
        {
            var msg = "Unable to intialize a plugin from compiled plugin";

            if (errorMsgPtr is not null)
            {
                msg = Marshal.PtrToStringAnsi(new IntPtr(errorMsgPtr));
            }

            throw new ExtismException(msg ?? "Unknown error");
        }

        NativeHandle = handle;
        _functions = plugin.Functions;
        _cancelHandle = LibExtism.extism_plugin_cancel_handle(NativeHandle);
    }

    /// <summary>
    /// Initialize a plugin from a Manifest.
    /// </summary>
    /// <param name="manifest"></param>
    /// <param name="functions"></param>
    /// <param name="options"></param>
    public Plugin(Manifest manifest, HostFunction[] functions, PluginIntializationOptions options)
    {
        _functions = functions;

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        jsonOptions.Converters.Add(new WasmSourceConverter());
        jsonOptions.Converters.Add(new JsonStringEnumConverter<HttpMethod>());

        var jsonContext = new ManifestJsonContext(jsonOptions);
        var json = JsonSerializer.Serialize(manifest, jsonContext.Manifest);

        var bytes = Encoding.UTF8.GetBytes(json);

        var functionHandles = functions.Select(f => f.NativeHandle).ToArray();
        fixed (byte* wasmPtr = bytes)
        fixed (IntPtr* functionsPtr = functionHandles)
        {
            NativeHandle = Initialize(wasmPtr, bytes.Length, functions, functionsPtr, options);
        }

        _cancelHandle = LibExtism.extism_plugin_cancel_handle(NativeHandle);
    }

    /// <summary>
    /// Create a plugin from a Manifest.
    /// </summary>
    /// <param name="manifest"></param>
    /// <param name="functions"></param>
    /// <param name="withWasi"></param>
    public Plugin(Manifest manifest, HostFunction[] functions, bool withWasi) : this(manifest, functions, new PluginIntializationOptions { WithWasi = withWasi })
    {

    }

    /// <summary>
    /// Create and load a plugin from a byte array.
    /// </summary>
    /// <param name="wasm">A WASM module (wat or wasm) or a JSON encoded manifest.</param>
    /// <param name="functions">List of host functions expected by the plugin.</param>
    /// <param name="withWasi">Enable/Disable WASI.</param>
    public Plugin(ReadOnlySpan<byte> wasm, HostFunction[] functions, bool withWasi)
    {
        _functions = functions;

        var functionHandles = functions.Select(f => f.NativeHandle).ToArray();
        fixed (byte* wasmPtr = wasm)
        fixed (IntPtr* functionsPtr = functionHandles)
        {
            NativeHandle = Initialize(wasmPtr, wasm.Length, functions, functionsPtr, new PluginIntializationOptions { WithWasi = withWasi });
        }

        _cancelHandle = LibExtism.extism_plugin_cancel_handle(NativeHandle);
    }

    private unsafe LibExtism.ExtismPlugin* Initialize(byte* wasmPtr, int wasmLength, HostFunction[] functions, IntPtr* functionsPtr, PluginIntializationOptions options)
    {
        char** errorMsgPtr;

        var handle = options.FuelLimit is null ?
                LibExtism.extism_plugin_new(wasmPtr, wasmLength, functionsPtr, functions.Length, options.WithWasi, out errorMsgPtr) :
                LibExtism.extism_plugin_new_with_fuel_limit(wasmPtr, wasmLength, functionsPtr, functions.Length, options.WithWasi, options.FuelLimit.Value, out errorMsgPtr);

        if (handle == null)
        {
            var msg = "Unable to create plugin";

            if (errorMsgPtr is not null)
            {
                msg = Marshal.PtrToStringAnsi(new IntPtr(errorMsgPtr));
            }

            throw new ExtismException(msg ?? "Unknown error");
        }

        return handle;
    }

    /// <summary>
    /// Enable HTTP response headers in plugins using `extism:host/env::http_request`
    /// </summary>
    public void AllowHttpResponseHeaders()
    {
        LibExtism.extism_plugin_allow_http_response_headers(NativeHandle);
    }

    /// <summary>
    /// Update plugin config values, this will merge with the existing values.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="serializerOptions"></param>
    /// <returns></returns>
    public bool UpdateConfig(Dictionary<string, string> value, JsonSerializerOptions serializerOptions)
    {
        var jsonContext = new ManifestJsonContext(serializerOptions);

        var json = JsonSerializer.Serialize(value, jsonContext.DictionaryStringString);
        var bytes = Encoding.UTF8.GetBytes(json);
        return UpdateConfig(bytes);
    }

    /// <summary>
    ///  Update plugin config values, this will merge with the existing values.
    /// </summary>
    /// <param name="json">The configuration JSON encoded in UTF8.</param>
    unsafe public bool UpdateConfig(ReadOnlySpan<byte> json)
    {
        CheckNotDisposed();

        fixed (byte* jsonPtr = json)
        {
            return LibExtism.extism_plugin_config(NativeHandle, jsonPtr, json.Length);
        }
    }

    /// <summary>
    /// Checks if a specific function exists in the current plugin.
    /// </summary>
    unsafe public bool FunctionExists(string name)
    {
        CheckNotDisposed();

        return LibExtism.extism_plugin_function_exists(NativeHandle, name);
    }

    /// <summary>
    /// Calls a function in the current plugin and returns the output as a byte buffer.
    /// </summary>
    /// <param name="functionName">Name of the function in the plugin to invoke.</param>
    /// <param name="input">A buffer to provide as input to the function.</param>
    /// <param name="cancellationToken">CancellationToken used for cancelling the Extism call.</param>
    /// <returns>The output of the function call</returns>
    /// <exception cref="ExtismException"></exception>
    unsafe public ReadOnlySpan<byte> Call(string functionName, ReadOnlySpan<byte> input, CancellationToken? cancellationToken = null)
    {
        CheckNotDisposed();

        cancellationToken?.ThrowIfCancellationRequested();

        using var _ = cancellationToken?.Register(() => LibExtism.extism_plugin_cancel(_cancelHandle));

        fixed (byte* dataPtr = input)
        {
            int response = LibExtism.extism_plugin_call(NativeHandle, functionName, dataPtr, input.Length);
            var errorMsg = GetError();

            if (errorMsg != null)
            {
                throw new ExtismException($"{errorMsg}. Exit Code: {response}");
            }

            return OutputData();
        }
    }

    /// <summary>
    /// Calls a function in the current plugin and returns the output as a UTF8 encoded string.
    /// </summary>
    /// <param name="functionName">Name of the function in the plugin to invoke.</param>
    /// <param name="input">A string that will be UTF8 encoded and passed to the plugin.</param>
    /// <param name="cancellationToken">CancellationToken used for cancelling the Extism call.</param>
    /// <returns>The output of the function as a UTF8 encoded string</returns>
    public string Call(string functionName, string input, CancellationToken? cancellationToken = null)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var outputBytes = Call(functionName, inputBytes, cancellationToken);
        return Encoding.UTF8.GetString(outputBytes);
    }

    /// <summary>
    /// Calls a function on the plugin with a payload. The payload is serialized into JSON and encoded in UTF8.
    /// </summary>
    /// <typeparam name="TInput">Type of the input payload.</typeparam>
    /// <typeparam name="TOutput">Type of the output payload returned by the function.</typeparam>
    /// <param name="functionName">Name of the function in the plugin to invoke.</param>
    /// <param name="input">An object that will be serialized into JSON and passed into the function as a UTF8 encoded string.</param>
    /// <param name="serializerOptions">JSON serialization options used for serialization/derserialization</param>
    /// <param name="cancellationToken">CancellationToken used for cancelling the Extism call.</param>
    /// <returns></returns>

#if NET7_0_OR_GREATER
    [RequiresUnreferencedCode("This function call can break in AOT compiled apps because it uses reflection for serialization. Use an overload that accepts an JsonTypeInfo instead.")]
    [RequiresDynamicCode("This function call can break in AOT compiled apps because it uses reflection for serialization. Use an overload that accepts an JsonTypeInfo instead.")]
#endif
    public TOutput? Call<TInput, TOutput>(string functionName, TInput input, JsonSerializerOptions? serializerOptions = null, CancellationToken? cancellationToken = null)
    {
        var inputJson = JsonSerializer.Serialize(input, serializerOptions ?? _serializerOptions);
        var outputJson = Call(functionName, inputJson, cancellationToken);
        return JsonSerializer.Deserialize<TOutput>(outputJson, serializerOptions ?? _serializerOptions);
    }

    /// <summary>
    /// Calls a function on the plugin with a payload. The payload is serialized into JSON and encoded in UTF8.
    /// </summary>
    /// <typeparam name="TInput">Type of the input payload.</typeparam>
    /// <typeparam name="TOutput">Type of the output payload returned by the function.</typeparam>
    /// <param name="functionName">Name of the function in the plugin to invoke.</param>
    /// <param name="input">An object that will be serialized into JSON and passed into the function as a UTF8 encoded string.</param>
    /// <param name="inputJsonInfo">Metadata about input type.</param>
    /// <param name="outputJsonInfo">Metadata about output type.</param>
    /// <param name="cancellationToken">CancellationToken used for cancelling the Extism call.</param>
    /// <returns></returns>
    public TOutput? Call<TInput, TOutput>(string functionName, TInput input, JsonTypeInfo<TInput> inputJsonInfo, JsonTypeInfo<TOutput?> outputJsonInfo, CancellationToken? cancellationToken = null)
    {
        var inputJson = JsonSerializer.Serialize(input, inputJsonInfo);
        var outputJson = Call(functionName, inputJson, cancellationToken);
        return JsonSerializer.Deserialize(outputJson, outputJsonInfo);
    }

    /// <summary>
    /// Calls a function on the plugin and deserializes the output as UTF8 encoded JSON.
    /// </summary>
    /// <typeparam name="TOutput">Type of the output payload returned by the function.</typeparam>
    /// <param name="functionName">Name of the function in the plugin to invoke.</param>
    /// <param name="input">Function input.</param>
    /// <param name="serializerOptions">JSON serialization options used for serialization/derserialization.</param>
    /// <param name="cancellationToken">CancellationToken used for cancelling the Extism call.</param>
    /// <returns></returns>
#if NET7_0_OR_GREATER
    [RequiresUnreferencedCode("This function call can break in AOT compiled apps because it uses reflection for serialization. Use an overload that accepts an JsonTypeInfo instead.")]
    [RequiresDynamicCode("This function call can break in AOT compiled apps because it uses reflection for serialization. Use an overload that accepts an JsonTypeInfo instead.")]
#endif
    public TOutput? Call<TOutput>(string functionName, string input, JsonSerializerOptions? serializerOptions = null, CancellationToken? cancellationToken = null)
    {
        var outputJson = Call(functionName, input, cancellationToken);
        return JsonSerializer.Deserialize<TOutput>(outputJson, serializerOptions ?? _serializerOptions);
    }

    /// <summary>
    /// Calls a function on the plugin with a payload. The payload is serialized into JSON and encoded in UTF8.
    /// </summary>
    /// <typeparam name="TOutput">Type of the output payload returned by the function.</typeparam>
    /// <param name="functionName">Name of the function in the plugin to invoke.</param>
    /// <param name="input">Function input.</param>
    /// <param name="outputJsonInfo">Metadata about output type.</param>
    /// <param name="cancellationToken">CancellationToken used for cancelling the Extism call.</param>
    /// <returns></returns>
    public TOutput? Call<TOutput>(string functionName, string input, JsonTypeInfo<TOutput?> outputJsonInfo, CancellationToken? cancellationToken = null)
    {
        var outputJson = Call(functionName, input, cancellationToken);
        return JsonSerializer.Deserialize(outputJson, outputJsonInfo);
    }

    /// <summary>
    /// Get the length of a plugin's output data.
    /// </summary>
    /// <returns></returns>
    unsafe internal int OutputLength()
    {
        CheckNotDisposed();

        return (int)LibExtism.extism_plugin_output_length(NativeHandle);
    }

    /// <summary>
    /// Get the plugin's output data.
    /// </summary>
    internal ReadOnlySpan<byte> OutputData()
    {
        CheckNotDisposed();

        var length = OutputLength();

        unsafe
        {
            var ptr = LibExtism.extism_plugin_output_data(NativeHandle).ToPointer();
            return new Span<byte>(ptr, length);
        }
    }

    /// <summary>
    /// Get the error associated with the current plugin.
    /// </summary>
    /// <returns></returns>
    unsafe internal string? GetError()
    {
        CheckNotDisposed();

        var result = LibExtism.extism_plugin_error(NativeHandle);
        return Marshal.PtrToStringUTF8(result);
    }

    /// <summary>
    /// Frees all resources held by this Plugin.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, DisposedMarker) == DisposedMarker)
        {
            // Already disposed.
            return;
        }

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Throw an appropriate exception if the plugin has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException"></exception>
    protected void CheckNotDisposed()
    {
        Interlocked.MemoryBarrier();
        if (_disposed == DisposedMarker)
        {
            ThrowDisposedException();
        }
    }

    [DoesNotReturn]
    private static void ThrowDisposedException()
    {
        throw new ObjectDisposedException(nameof(Plugin));
    }

    /// <summary>
    /// Frees all resources held by this Plugin.
    /// </summary>
    unsafe protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Free up any managed resources here
        }

        // Free up unmanaged resources
        LibExtism.extism_plugin_free(NativeHandle);
    }

    /// <summary>
    /// Destructs the current Plugin and frees all resources used by it.
    /// </summary>
    ~Plugin()
    {
        Dispose(false);
    }

    /// <summary>
    /// Get Extism Runtime version.
    /// </summary>
    /// <returns></returns>
    public static string ExtismVersion()
    {
        var version = LibExtism.extism_version();
        return Marshal.PtrToStringAnsi(version) ?? "unknown";
    }

    /// <summary>
    /// Set log file and level
    /// </summary>
    /// <param name="path">Log file path</param>
    /// <param name="level">Minimum log level</param>
    public static void ConfigureFileLogging(string path, LogLevel level)
    {
        var logLevel = Enum.GetName(typeof(LogLevel), level)?.ToLowerInvariant()
            ?? throw new ArgumentOutOfRangeException(nameof(level));

        LibExtism.extism_log_file(path, logLevel);
    }

    /// <summary>
    /// Enable a custom log handler, this will buffer logs until <see cref="DrainCustomLogs(LoggingSink)"/> is called.
    /// </summary>
    /// <param name="level"></param>
    public static void ConfigureCustomLogging(LogLevel level)
    {
        var logLevel = Enum.GetName(typeof(LogLevel), level)?.ToLowerInvariant()
            ?? throw new ArgumentOutOfRangeException(nameof(level));

        LibExtism.extism_log_custom(logLevel);
    }

    /// <summary>
    /// Calls the provided callback function for each buffered log line.
    /// This only needed when <see cref="ConfigureCustomLogging(LogLevel)"/> is used.
    /// </summary>
    /// <param name="callback"></param>
    public static void DrainCustomLogs(LoggingSink callback)
    {
        LibExtism.extism_log_drain((line, length) =>
        {
            callback(line);
        });
    }
}

/// <summary>
/// Options for initializing a plugin.
/// </summary>
public class PluginIntializationOptions
{
    /// <summary>
    /// Enable WASI support.
    /// </summary>
    public bool WithWasi { get; set; }

    /// <summary>
    /// Limits number of instructions that can be executed by the plugin.
    /// </summary>
    public long? FuelLimit { get; set; }
}

/// <summary>
/// Custom logging callback.
/// </summary>
/// <param name="line"></param>
public delegate void LoggingSink(string line);

/// <summary>
/// A pre-compiled plugin ready to be instantiated.
/// </summary>
public unsafe class CompiledPlugin : IDisposable
{
    private const int DisposedMarker = 1;
    private int _disposed;

    internal LibExtism.ExtismCompiledPlugin* NativeHandle { get; }
    internal HostFunction[] Functions { get; }

    /// <summary>
    /// Compile a plugin from a Manifest.
    /// </summary>
    /// <param name="manifest"></param>
    /// <param name="functions"></param>
    /// <param name="withWasi"></param>
    public CompiledPlugin(Manifest manifest, HostFunction[] functions, bool withWasi)
    {
        Functions = functions;

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        options.Converters.Add(new WasmSourceConverter());
        options.Converters.Add(new JsonStringEnumConverter<HttpMethod>());

        var jsonContext = new ManifestJsonContext(options);
        var json = JsonSerializer.Serialize(manifest, jsonContext.Manifest);

        var bytes = Encoding.UTF8.GetBytes(json);

        var functionHandles = functions.Select(f => f.NativeHandle).ToArray();
        fixed (byte* wasmPtr = bytes)
        fixed (IntPtr* functionsPtr = functionHandles)
        {
            NativeHandle = Initialize(wasmPtr, bytes.Length, functions, withWasi, functionsPtr);
        }
    }

    /// <summary>
    /// Instantiate a plugin from this compiled plugin.
    /// </summary>
    /// <returns></returns>
    public Plugin Instantiate()
    {
        CheckNotDisposed();
        return new Plugin(this);
    }

    private unsafe LibExtism.ExtismCompiledPlugin* Initialize(byte* wasmPtr, int wasmLength, HostFunction[] functions, bool withWasi, IntPtr* functionsPtr)
    {
        char** errorMsgPtr;

        var handle = LibExtism.extism_compiled_plugin_new(wasmPtr, wasmLength, functionsPtr, functions.Length, withWasi, out errorMsgPtr);
        if (handle == null)
        {
            var msg = "Unable to compile plugin";

            if (errorMsgPtr is not null)
            {
                msg = Marshal.PtrToStringAnsi(new IntPtr(errorMsgPtr));
            }

            throw new ExtismException(msg ?? "Unknown error");
        }

        return handle;
    }


    /// <summary>
    /// Frees all resources held by this Plugin.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, DisposedMarker) == DisposedMarker)
        {
            // Already disposed.
            return;
        }

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Throw an appropriate exception if the plugin has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException"></exception>
    protected void CheckNotDisposed()
    {
        Interlocked.MemoryBarrier();
        if (_disposed == DisposedMarker)
        {
            ThrowDisposedException();
        }
    }

    [DoesNotReturn]
    private static void ThrowDisposedException()
    {
        throw new ObjectDisposedException(nameof(Plugin));
    }

    /// <summary>
    /// Frees all resources held by this Plugin.
    /// </summary>
    unsafe protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Free up any managed resources here
        }

        // Free up unmanaged resources
        LibExtism.extism_compiled_plugin_free(NativeHandle);
    }

    /// <summary>
    /// Destructs the current Plugin and frees all resources used by it.
    /// </summary>
    ~CompiledPlugin()
    {
        Dispose(false);
    }
}