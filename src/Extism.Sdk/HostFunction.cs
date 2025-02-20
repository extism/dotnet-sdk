﻿using Extism.Sdk.Native;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Extism.Sdk;

/// <summary>
/// A host function signature.
/// </summary>
/// <param name="plugin">Plugin Index</param>
/// <param name="inputs">Input parameters</param>
/// <param name="outputs">Output parameters, the host function can change this.</param>
public delegate void ExtismFunction(CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs);

/// <summary>
/// A function provided by the host that plugins can call.
/// </summary>
public class HostFunction : IDisposable
{
    private const int DisposedMarker = 1;
    private int _disposed;
    private readonly ExtismFunction _function;
    private readonly LibExtism.InternalExtismFunction _callback;
    private readonly GCHandle? _userDataHandle;

    /// <summary>
    /// Registers a Host Function.
    /// </summary>
    /// <param name="functionName">The literal name of the function, how it would be called from a <see cref="Plugin"/>.</param>
    /// <param name="inputTypes">The types of the input arguments/parameters the <see cref="Plugin"/> caller will provide.</param>
    /// <param name="outputTypes">The types of the output returned from the host function to the <see cref="Plugin"/>.</param>
    /// <param name="userData">
    /// A state object that will be preserved and can be retrieved during function execution using <see cref="CurrentPlugin.GetUserData{T}"/>. 
    /// This allows you to maintain context between function calls.</param>
    /// <param name="hostFunction"></param>
    unsafe public HostFunction(
        string functionName,
        Span<ExtismValType> inputTypes,
        Span<ExtismValType> outputTypes,
        object? userData,
        ExtismFunction hostFunction)
    {
        // Make sure we store the delegate reference in a field so that it doesn't get garbage collected
        _function = hostFunction;
        _callback = CallbackImpl;
        _userDataHandle = userData is null ? null : GCHandle.Alloc(userData);

        fixed (ExtismValType* inputs = inputTypes)
        fixed (ExtismValType* outputs = outputTypes)
        {
            NativeHandle = LibExtism.extism_function_new(
                functionName, 
                inputs, 
                inputTypes.Length, 
                outputs, 
                outputTypes.Length,
                _callback, 
                _userDataHandle is null ? IntPtr.Zero : GCHandle.ToIntPtr(_userDataHandle.Value), 
                IntPtr.Zero);
        }
    }

    internal nint NativeHandle { get; }

    /// <summary>
    /// Sets the function namespace. By default it's set to `env`.
    /// </summary>
    /// <param name="ns"></param>
    public void SetNamespace(string ns)
    {
        if (!string.IsNullOrEmpty(ns))
        {
            LibExtism.extism_function_set_namespace(NativeHandle, ns);
        }
    }

    /// <summary>
    /// Sets the function namespace. By default it's set to `extism:host/user`.
    /// </summary>
    /// <param name="ns"></param>
    /// <returns></returns>
    public HostFunction WithNamespace(string ns)
    {
        this.SetNamespace(ns);
        return this;
    }

    private unsafe void CallbackImpl(
        LibExtism.ExtismCurrentPlugin* plugin,
        ExtismVal* inputsPtr,
        uint n_inputs,
        ExtismVal* outputsPtr,
        uint n_outputs,
        nint data)
    {
        var outputs = new Span<ExtismVal>(outputsPtr, (int)n_outputs);
        var inputs = new Span<ExtismVal>(inputsPtr, (int)n_inputs);

        _function(new CurrentPlugin(plugin, data), inputs, outputs);
    }

    /// <summary>
    /// Registers a <see cref="HostFunction"/> from a method that takes no parameters an returns no values.
    /// </summary>
    /// <param name="functionName">The literal name of the function, how it would be called from a <see cref="Plugin"/>.</param>
    /// <param name="userData">
    /// A state object that will be preserved and can be retrieved during function execution using <see cref="CurrentPlugin.GetUserData{T}"/>. 
    /// This allows you to maintain context between function calls.</param>
    /// <param name="callback">The host function implementation.</param>
    /// <returns></returns>
    public static HostFunction FromMethod(
        string functionName,
        object userData,
        Action<CurrentPlugin> callback)
    {
        var inputTypes = new ExtismValType[] { };
        var returnType = new ExtismValType[] { };

        return new HostFunction(functionName, inputTypes, returnType, userData,
            (CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs) =>
            {
                callback(plugin);
            });
    }

    /// <summary>
    /// Registers a <see cref="HostFunction"/> from a method that takes 1 parameter an returns no values. Supported parameter types:
    /// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/>
    /// </summary>
    /// <typeparam name="I1">Type of first parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <param name="functionName">The literal name of the function, how it would be called from a <see cref="Plugin"/>.</param>
    /// <param name="userData">
    /// A state object that will be preserved and can be retrieved during function execution using <see cref="CurrentPlugin.GetUserData{T}"/>. 
    /// This allows you to maintain context between function calls.</param>
    /// <param name="callback">The host function implementation.</param>
    /// <returns></returns>
    public static HostFunction FromMethod<I1>(
        string functionName,
        object userData,
        Action<CurrentPlugin, I1> callback)
        where I1 : struct
    {
        var inputTypes = new ExtismValType[] { ToExtismType<I1>() };
        var returnType = new ExtismValType[] { };

        return new HostFunction(functionName, inputTypes, returnType, userData,
            (CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs) =>
            {
                callback(plugin, GetValue<I1>(inputs[0]));
            });
    }

    /// <summary>
    /// Registers a <see cref="HostFunction"/> from a method that takes 2 parameters an returns no values. Supported parameter types:
    /// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/>
    /// </summary>
    /// <typeparam name="I1">Type of the first parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <typeparam name="I2">Type of the second parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <param name="functionName">The literal name of the function, how it would be called from a <see cref="Plugin"/>.</param>
    /// <param name="userData">
    /// A state object that will be preserved and can be retrieved during function execution using <see cref="CurrentPlugin.GetUserData{T}"/>. 
    /// This allows you to maintain context between function calls.</param>
    /// <param name="callback">The host function implementation.</param>
    /// <returns></returns>
    public static HostFunction FromMethod<I1, I2>(
        string functionName,
        object userData,
        Action<CurrentPlugin, I1, I2> callback)
        where I1 : struct
        where I2 : struct
    {
        var inputTypes = new ExtismValType[] { ToExtismType<I1>(), ToExtismType<I2>() };

        var returnType = new ExtismValType[] { };

        return new HostFunction(functionName, inputTypes, returnType, userData,
            (CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs) =>
            {
                callback(plugin, GetValue<I1>(inputs[0]), GetValue<I2>(inputs[1]));
            });
    }

    /// <summary>
    /// Registers a <see cref="HostFunction"/> from a method that takes 3 parameters an returns no values. Supported parameter types:
    /// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/>
    /// </summary>
    /// <typeparam name="I1">Type of the first parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <typeparam name="I2">Type of the second parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <typeparam name="I3">Type of the third parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <param name="functionName">The literal name of the function, how it would be called from a <see cref="Plugin"/>.</param>
    /// <param name="userData">
    /// A state object that will be preserved and can be retrieved during function execution using <see cref="CurrentPlugin.GetUserData{T}"/>. 
    /// This allows you to maintain context between function calls.</param>
    /// <param name="callback">The host function implementation.</param>
    /// <returns></returns>
    public static HostFunction FromMethod<I1, I2, I3>(
        string functionName,
        object userData,
        Action<CurrentPlugin, I1, I2, I3> callback)
        where I1 : struct
        where I2 : struct
        where I3 : struct
    {
        var inputTypes = new ExtismValType[] { ToExtismType<I1>(), ToExtismType<I2>(), ToExtismType<I3>() };
        var returnType = new ExtismValType[] { };

        return new HostFunction(functionName, inputTypes, returnType, userData,
            (CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs) =>
            {
                callback(plugin, GetValue<I1>(inputs[0]), GetValue<I2>(inputs[1]), GetValue<I3>(inputs[2]));
            });
    }

    /// <summary>
    /// Registers a <see cref="HostFunction"/> from a method that takes no parameters an returns a value. Supported return types:
    /// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/>
    /// </summary>
    /// <typeparam name="R">Type of the first parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <param name="functionName">The literal name of the function, how it would be called from a <see cref="Plugin"/>.</param>
    /// <param name="userData">
    /// A state object that will be preserved and can be retrieved during function execution using <see cref="CurrentPlugin.GetUserData{T}"/>. 
    /// This allows you to maintain context between function calls.</param>
    /// <param name="callback">The host function implementation.</param>
    /// <returns></returns>
    public static HostFunction FromMethod<R>(
        string functionName,
        object userData,
        Func<CurrentPlugin, R> callback)
        where R : struct
    {
        var inputTypes = new ExtismValType[] { };
        var returnType = new ExtismValType[] { ToExtismType<R>() };

        return new HostFunction(functionName, inputTypes, returnType, userData,
            (CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs) =>
            {
                var value = callback(plugin);
                SetValue(ref outputs[0], value);
            });
    }

    /// <summary>
    /// Registers a <see cref="HostFunction"/> from a method that takes 1 parameter an returns a value. Supported return and parameter types:
    /// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/>
    /// </summary>
    /// <typeparam name="I1">Type of the first parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <typeparam name="R">Type of the first parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <param name="functionName">The literal name of the function, how it would be called from a <see cref="Plugin"/>.</param>
    /// <param name="userData">
    /// A state object that will be preserved and can be retrieved during function execution using <see cref="CurrentPlugin.GetUserData{T}"/>. 
    /// This allows you to maintain context between function calls.</param>
    /// <param name="callback">The host function implementation.</param>
    /// <returns></returns>
    public static HostFunction FromMethod<I1, R>(
        string functionName,
        object userData,
        Func<CurrentPlugin, I1, R> callback)
        where I1 : struct
        where R : struct
    {
        var inputTypes = new ExtismValType[] { ToExtismType<I1>() };
        var returnType = new ExtismValType[] { ToExtismType<R>() };

        return new HostFunction(functionName, inputTypes, returnType, userData,
            (CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs) =>
            {
                var value = callback(plugin, GetValue<I1>(inputs[0]));
                SetValue(ref outputs[0], value);
            });
    }

    /// <summary>
    /// Registers a <see cref="HostFunction"/> from a method that takes 2 parameter an returns a value. Supported return and parameter types:
    /// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/>
    /// </summary>
    /// <typeparam name="I1">Type of the first parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <typeparam name="I2">Type of the second parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <typeparam name="R">Type of the first parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <param name="functionName">The literal name of the function, how it would be called from a <see cref="Plugin"/>.</param>
    /// <param name="userData">
    /// A state object that will be preserved and can be retrieved during function execution using <see cref="CurrentPlugin.GetUserData{T}"/>. 
    /// This allows you to maintain context between function calls.</param>
    /// <param name="callback">The host function implementation.</param>
    /// <returns></returns>
    public static HostFunction FromMethod<I1, I2, R>(
        string functionName,
        object userData,
        Func<CurrentPlugin, I1, I2, R> callback)
        where I1 : struct
        where I2 : struct
        where R : struct
    {
        var inputTypes = new ExtismValType[] { ToExtismType<I1>(), ToExtismType<I2>() };
        var returnType = new ExtismValType[] { ToExtismType<R>() };

        return new HostFunction(functionName, inputTypes, returnType, userData,
            (CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs) =>
            {
                var value = callback(plugin, GetValue<I1>(inputs[0]), GetValue<I2>(inputs[1]));
                SetValue(ref outputs[0], value);
            });
    }

    /// <summary>
    /// Registers a <see cref="HostFunction"/> from a method that takes 3 parameter an returns a value. Supported return and parameter types:
    /// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/>
    /// </summary>
    /// <typeparam name="I1">Type of the first parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <typeparam name="I2">Type of the second parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <typeparam name="I3">Type of the third parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <typeparam name="R">Type of the first parameter. Supported parameter types: <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/></typeparam>
    /// <param name="functionName">The literal name of the function, how it would be called from a <see cref="Plugin"/>.</param>
    /// <param name="userData">
    /// A state object that will be preserved and can be retrieved during function execution using <see cref="CurrentPlugin.GetUserData{T}"/>. 
    /// This allows you to maintain context between function calls.</param>
    /// <param name="callback">The host function implementation.</param>
    /// <returns></returns>
    public static HostFunction FromMethod<I1, I2, I3, R>(
        string functionName,
        object userData,
        Func<CurrentPlugin, I1, I2, I3, R> callback)
        where I1 : struct
        where I2 : struct
        where I3 : struct
        where R : struct
    {
        var inputTypes = new ExtismValType[] { ToExtismType<I1>(), ToExtismType<I2>(), ToExtismType<I3>() };
        var returnType = new ExtismValType[] { ToExtismType<R>() };

        return new HostFunction(functionName, inputTypes, returnType, userData,
            (CurrentPlugin plugin, Span<ExtismVal> inputs, Span<ExtismVal> outputs) =>
            {
                var value = callback(plugin, GetValue<I1>(inputs[0]), GetValue<I2>(inputs[1]), GetValue<I3>(inputs[2]));
                SetValue(ref outputs[0], value);
            });
    }

    private static ExtismValType ToExtismType<T>() where T : struct
    {
        return typeof(T) switch
        {
            Type t when t == typeof(int) || t == typeof(uint) => ExtismValType.I32,
            Type t when t == typeof(long) || t == typeof(ulong) => ExtismValType.I64,
            Type t when t == typeof(float) => ExtismValType.F32,
            Type t when t == typeof(double) => ExtismValType.F64,
            _ => throw new NotImplementedException($"Unsupported type: {typeof(T).Name}"),
        };
    }

    private static T GetValue<T>(ExtismVal val) where T : struct
    {
        return typeof(T) switch
        {
            Type intType when intType == typeof(int) && val.t == ExtismValType.I32 => (T)(object)val.v.i32,
            Type longType when longType == typeof(long) && val.t == ExtismValType.I64 => (T)(object)val.v.i64,
            Type floatType when floatType == typeof(float) && val.t == ExtismValType.F32 => (T)(object)val.v.f32,
            Type doubleType when doubleType == typeof(double) && val.t == ExtismValType.F64 => (T)(object)val.v.f64,
            _ => throw new InvalidOperationException($"Unsupported conversion from {Enum.GetName(typeof(ExtismValType), val.t)} to {typeof(T).Name}")
        };
    }

    private static void SetValue<T>(ref ExtismVal val, T t)
    {
        if (t is int i32)
        {
            val.t = ExtismValType.I32;
            val.v.i32 = i32;
        }
        else if (t is uint u32)
        {
            val.t = ExtismValType.I32;
            val.v.i32 = (int)u32;
        }
        else if (t is long i64)
        {
            val.t = ExtismValType.I64;
            val.v.i64 = i64;
        }
        else if (t is ulong u64)
        {
            val.t = ExtismValType.I64;
            val.v.i64 = (long)u64;
        }
        else if (t is float f32)
        {
            val.t = ExtismValType.F32;
            val.v.f32 = f32;
        }
        else if (t is double f64)
        {
            val.t = ExtismValType.F64;
            val.v.f64 = f64;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported value type: {typeof(T).Name}");
        }
    }

    /// <summary>
    /// Frees all resources held by this Host Function.
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
    /// Throw an appropriate exception if the Host Function has been disposed.
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
        throw new ObjectDisposedException(nameof(HostFunction));
    }

    /// <summary>
    /// Frees all resources held by this Host Function.
    /// </summary>
    unsafe protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _userDataHandle?.Free();
        }

        // Free up unmanaged resources
        LibExtism.extism_function_free(NativeHandle);
    }

    /// <summary>
    /// Destructs the current Host Function and frees all resources used by it.
    /// </summary>
    ~HostFunction()
    {
        Dispose(false);
    }
}