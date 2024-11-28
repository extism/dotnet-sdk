using System.Runtime.InteropServices;

namespace Extism.Sdk.Native;

/// <summary>
/// A union type for host function argument/return values.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct ExtismValUnion
{
    /// <summary>
    /// Set this for 32 bit integers
    /// </summary>
    [FieldOffset(0)]
    public int i32;

    /// <summary>
    /// Set this for 64 bit integers
    /// </summary>
    [FieldOffset(0)]
    public long i64;

    /// <summary>
    /// Set this for 64 bit integers
    /// </summary>
    [FieldOffset(0)]
    public long ptr;

    /// <summary>
    /// Set this for 32 bit floats
    /// </summary>
    [FieldOffset(0)]
    public float f32;

    /// <summary>
    /// Set this for 64 bit floats
    /// </summary>
    [FieldOffset(0)]
    public double f64;
}

/// <summary>
/// Represents Wasm data types that Extism can understand
/// </summary>
public enum ExtismValType : int
{
    /// <summary>
    /// Signed 32 bit integer. Equivalent of <see cref="int"/> or <see cref="uint"/>
    /// </summary>
    I32,

    /// <summary>
    /// Signed 64 bit integer. Equivalent of <see cref="long"/> or <see cref="long"/>
    /// </summary>
    I64,

    /// <summary>
    /// A wrapper around <see cref="I64"/> to specify arguments that are pointers to memory blocks
    /// </summary>
    PTR = I64,

    /// <summary>
    /// Floating point 32 bit integer. Equivalent of <see cref="float"/>
    /// </summary>
    F32,

    /// <summary>
    /// Floating point 64 bit integer. Equivalent of <see cref="double"/>
    /// </summary>
    F64,

    /// <summary>
    /// A 128 bit number.
    /// </summary>
    V128,

    /// <summary>
    /// A reference to opaque data in the Wasm instance.
    /// </summary>
    FuncRef,

    /// <summary>
    /// A reference to opaque data in the Wasm instance.
    /// </summary>
    ExternRef
}

/// <summary>
/// `ExtismVal` holds the type and value of a function argument/return
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ExtismVal
{
    /// <summary>
    /// The type for the argument
    /// </summary>
    public ExtismValType t;

    /// <summary>
    /// The value for the argument
    /// </summary>
    public ExtismValUnion v;
}

/// <summary>
/// Functions exposed by the native Extism library.
/// </summary>
internal static class LibExtism
{
    /// <summary>
    /// An Extism Plugin
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ExtismPlugin { }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ExtismCompiledPlugin { }

    /// <summary>
    /// Host function signature
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="inputs"></param>
    /// <param name="n_inputs"></param>
    /// <param name="outputs"></param>
    /// <param name="n_outputs"></param>
    /// <param name="data"></param>
    unsafe internal delegate void InternalExtismFunction(long plugin, ExtismVal* inputs, uint n_inputs, ExtismVal* outputs, uint n_outputs, IntPtr data);

    /// <summary>
    /// Returns a pointer to the memory of the currently running plugin.
    /// NOTE: this should only be called from host functions.
    /// </summary>
    /// <param name="plugin"></param>
    /// <returns></returns>
    [DllImport("extism", EntryPoint = "extism_current_plugin_memory")]
    internal static extern long extism_current_plugin_memory(long plugin);

    /// <summary>
    /// Allocate a memory block in the currently running plugin
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="n"></param>
    /// <returns></returns>
    [DllImport("extism", EntryPoint = "extism_current_plugin_memory_alloc")]
    internal static extern long extism_current_plugin_memory_alloc(long plugin, long n);

    /// <summary>
    /// Get the length of an allocated block.
    /// NOTE: this should only be called from host functions.
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="n"></param>
    /// <returns></returns>
    [DllImport("extism", EntryPoint = "extism_current_plugin_memory_length")]
    internal static extern long extism_current_plugin_memory_length(long plugin, long n);

    /// <summary>
    /// Get the length of an allocated block.
    /// NOTE: this should only be called from host functions.
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="ptr"></param>
    [DllImport("extism", EntryPoint = "extism_current_plugin_memory_free")]
    internal static extern void extism_current_plugin_memory_free(long plugin, long ptr);

    /// <summary>
    /// Create a new host function.
    /// </summary>
    /// <param name="name">function name, this should be valid UTF-8</param>
    /// <param name="inputs">argument types</param>
    /// <param name="nInputs">number of argument types</param>
    /// <param name="outputs">return types</param>
    /// <param name="nOutputs">number of return types</param>
    /// <param name="func">the function to call</param>
    /// <param name="userData">a pointer that will be passed to the function when it's called this value should live as long as the function exists</param>
    /// <param name="freeUserData">a callback to release the `user_data` value when the resulting `ExtismFunction` is freed.</param>
    /// <returns></returns>
    [DllImport("extism", EntryPoint = "extism_function_new")]
    unsafe internal static extern IntPtr extism_function_new(string name, ExtismValType* inputs, long nInputs, ExtismValType* outputs, long nOutputs, InternalExtismFunction func, IntPtr userData, IntPtr freeUserData);

    /// <summary>
    /// Set the namespace of an <see cref="ExtismFunction"/>
    /// </summary>
    /// <param name="ptr"></param>
    /// <param name="namespace"></param>
    [DllImport("extism", EntryPoint = "extism_function_set_namespace")]
    internal static extern void extism_function_set_namespace(IntPtr ptr, string @namespace);

    /// <summary>
    /// Free an <see cref="ExtismFunction"/>
    /// </summary>
    /// <param name="ptr"></param>
    [DllImport("extism", EntryPoint = "extism_function_free")]
    internal static extern void extism_function_free(IntPtr ptr);

    /// <summary>
    /// Load a WASM plugin.
    /// </summary>
    /// <param name="wasm">A WASM module (wat or wasm) or a JSON encoded manifest.</param>
    /// <param name="wasmSize">The length of the `wasm` parameter.</param>
    /// <param name="functions">Array of host function pointers.</param>
    /// <param name="nFunctions">Number of host functions.</param>
    /// <param name="withWasi">Enables/disables WASI.</param>
    /// <param name="errmsg"></param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern ExtismPlugin* extism_plugin_new(byte* wasm, long wasmSize, IntPtr* functions, long nFunctions, [MarshalAs(UnmanagedType.I1)] bool withWasi, out char** errmsg);

    /// <summary>
    /// Load a WASM plugin with fuel limit.
    /// </summary>
    /// <param name="wasm">A WASM module (wat or wasm) or a JSON encoded manifest.</param>
    /// <param name="wasmSize">The length of the `wasm` parameter.</param>
    /// <param name="functions">Array of host function pointers.</param>
    /// <param name="nFunctions">Number of host functions.</param>
    /// <param name="withWasi">Enables/disables WASI.</param>
    /// <param name="fuelLimit">Max number of instructions that can be executed by the plugin.</param>
    /// <param name="errmsg"></param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern ExtismPlugin* extism_plugin_new_with_fuel_limit(byte* wasm, long wasmSize, IntPtr* functions, long nFunctions, [MarshalAs(UnmanagedType.I1)] bool withWasi, long fuelLimit, out char** errmsg);

    /// <summary>
    /// Frees a plugin error message.
    /// </summary>
    /// <param name="errorMessage"></param>
    [DllImport("extism")]
    unsafe internal static extern void extism_plugin_new_error_free(IntPtr errorMessage);

    /// <summary>
    /// Remove a plugin from the registry and free associated memory.
    /// </summary>
    /// <param name="plugin">Pointer to the plugin you want to free.</param>
    [DllImport("extism")]
    unsafe internal static extern void extism_plugin_free(ExtismPlugin* plugin);
    /// <summary>
    /// Pre-compile an Extism plugin
    /// </summary>
    /// <param name="wasm">A WASM module (wat or wasm) or a JSON encoded manifest.</param>
    /// <param name="wasmSize">The length of the `wasm` parameter.</param>
    /// <param name="functions">Array of host function pointers.</param>
    /// <param name="nFunctions">Number of host functions.</param>
    /// <param name="withWasi">Enables/disables WASI.</param>
    /// <param name="errmsg"></param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern ExtismCompiledPlugin* extism_compiled_plugin_new(byte* wasm, long wasmSize, IntPtr* functions, long nFunctions, [MarshalAs(UnmanagedType.I1)] bool withWasi, out char** errmsg);

    /// <summary>
    /// Free `ExtismCompiledPlugin`
    /// </summary>
    /// <param name="plugin"></param>
    [DllImport("extism")]
    unsafe internal static extern void extism_compiled_plugin_free(ExtismCompiledPlugin* plugin);

    /// <summary>
    ///  Create a new plugin from an `ExtismCompiledPlugin`
    /// </summary>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern ExtismPlugin* extism_plugin_new_from_compiled(ExtismCompiledPlugin* compiled, out char** errmsg);

    /// <summary>
    /// Enable HTTP response headers in plugins using `extism:host/env::http_request`
    /// </summary>
    /// <param name="plugin"></param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern ExtismPlugin* extism_plugin_allow_http_response_headers(ExtismPlugin* plugin);

    /// <summary>
    /// Get handle for plugin cancellation
    /// </summary>
    /// <param name="plugin"></param>
    /// <returns></returns>
    [DllImport("extism")]
    internal unsafe static extern IntPtr extism_plugin_cancel_handle(ExtismPlugin* plugin);

    /// <summary>
    /// Cancel a running plugin
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    [DllImport("extism")]
    internal static extern bool extism_plugin_cancel(IntPtr handle);

    /// <summary>
    /// Update plugin config values, this will merge with the existing values.
    /// </summary>
    /// <param name="plugin">Pointer to the plugin you want to update the configurations for.</param>
    /// <param name="json">The configuration JSON encoded in UTF8.</param>
    /// <param name="jsonLength">The length of the `json` parameter.</param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern bool extism_plugin_config(ExtismPlugin* plugin, byte* json, int jsonLength);

    /// <summary>
    /// Returns true if funcName exists.
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="funcName"></param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern bool extism_plugin_function_exists(ExtismPlugin* plugin, string funcName);

    /// <summary>
    /// Call a function.
    /// </summary>
    /// <param name="plugin"></param>
    /// <param name="funcName">The function to call.</param>
    /// <param name="data">Input data.</param>
    /// <param name="dataLen">The length of the `data` parameter.</param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern int extism_plugin_call(ExtismPlugin* plugin, string funcName, byte* data, int dataLen);

    /// <summary>
    /// Get the error associated with a Plugin
    /// </summary>
    /// <param name="plugin">A plugin pointer</param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern IntPtr extism_plugin_error(ExtismPlugin* plugin);

    /// <summary>
    /// Get the length of a plugin's output data.
    /// </summary>
    /// <param name="plugin"></param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern long extism_plugin_output_length(ExtismPlugin* plugin);

    /// <summary>
    /// Get the plugin's output data.
    /// </summary>
    /// <param name="plugin"></param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern IntPtr extism_plugin_output_data(ExtismPlugin* plugin);

    /// <summary>
    /// Reset the Extism runtime, this will invalidate all allocated memory
    /// </summary>
    /// <param name="plugin"></param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern bool extism_plugin_reset(ExtismPlugin* plugin);

    /// <summary>
    /// Get a plugin's ID, the returned bytes are a 16 byte buffer that represent a UUIDv4
    /// </summary>
    /// <param name="plugin"></param>
    /// <returns></returns>
    [DllImport("extism")]
    unsafe internal static extern byte* extism_plugin_id(ExtismPlugin* plugin);

    /// <summary>
    /// Set log file and level for file logger.
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="logLevel"></param>
    /// <returns></returns>
    [DllImport("extism")]
    internal static extern bool extism_log_file(string filename, string logLevel);

    /// <summary>
    /// Enable a custom log handler, this will buffer logs until `extism_log_drain` is called. 
    /// this will buffer logs until `extism_log_drain` is called
    /// </summary>
    /// <param name="logLevel"></param>
    /// <returns></returns>
    [DllImport("extism")]
    internal static extern bool extism_log_custom(string logLevel);

    internal delegate void LoggingSink(string line, ulong length);

    /// <summary>
    /// Calls the provided callback function for each buffered log line.
    /// This is only needed when `extism_log_custom` is used.
    /// </summary>
    /// <param name="callback"></param>
    [DllImport("extism")]
    internal static extern void extism_log_drain(LoggingSink callback);

    /// <summary>
    /// Get Extism Runtime version.
    /// </summary>
    /// <returns></returns>
    [DllImport("extism")]
    internal static extern IntPtr extism_version();
}
