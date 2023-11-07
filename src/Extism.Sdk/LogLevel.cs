namespace Extism.Sdk.Native;

/// <summary>
/// Extism Log Levels
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Designates very serious errors.
    /// </summary>
    Error = 1,

    /// <summary>
    /// Designates hazardous situations.
    /// </summary>
    Warn,

    /// <summary>
    /// Designates useful information.
    /// </summary>
    Info,

    /// <summary>
    /// Designates lower priority information.
    /// </summary>
    Debug,

    /// <summary>
    /// Designates very low priority, often extremely verbose, information.
    /// </summary>
    Trace
}
