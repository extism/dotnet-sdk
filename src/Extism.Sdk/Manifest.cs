using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using System.Xml.Linq;

namespace Extism.Sdk
{
    /// <summary>
    /// The manifest is a description of your plugin and some of the runtime constraints to apply to it.
    /// You can think of it as a blueprint to build your plugin.
    /// </summary>
    public class Manifest
    {
        /// <summary>
        /// Create an empty manifest.
        /// </summary>
        public Manifest()
        {

        }

        /// <summary>
        /// Create a manifest from one or more Wasm sources.
        /// </summary>
        /// <param name="sources"></param>
        public Manifest(params WasmSource[] sources)
        {
            Sources.AddRange(sources);
        }

        /// <summary>
        /// List of Wasm sources. See <see cref="PathWasmSource"/> and <see cref="ByteArrayWasmSource"/>.
        /// </summary>
        [JsonPropertyName("wasm")]
        public List<WasmSource> Sources { get; set; } = new();

        /// <summary>
        /// Configures memory for the Wasm runtime.
        /// Memory is described in units of pages (64KB) and represent contiguous chunks of addressable memory.
        /// </summary>
        [JsonPropertyName("memory")]
        public MemoryOptions? MemoryOptions { get; set; }

        /// <summary>
        /// List of host names the plugins can access. Example:
        /// <code>
        /// AllowedHosts = new List&lt;string&gt; {
        ///     "www.example.com",
        ///     "api.*.com",
        ///     "example.*",
        /// }
        /// </code>
        /// </summary>
        [JsonPropertyName("allowed_hosts")]
        public List<string> AllowedHosts { get; set; } = new();

        /// <summary>
        /// List of directories that can be accessed by the plugins. Examples:
        /// <code>
        /// AllowedPaths = new Dictionary&lt;string, string&gt;
        /// {
        ///     { "/usr/plugins/1/data", "/data" }, // src, dest
        ///     { "d:/plugins/1/data", "/data" }    // src, dest
        /// };
        /// </code>
        /// </summary>
        [JsonPropertyName("allowed_paths")]
        public Dictionary<string, string> AllowedPaths { get; set; } = new();

        /// <summary>
        /// Configurations available to the plugins. Examples:
        /// <code>
        /// Config = new Dictionary&lt;string, string&gt;
        /// {
        ///     { "userId", "55" }, // key, value
        ///     { "mySecret", "super-secret-key" } // key, value
        /// };
        /// </code>
        /// </summary>
        [JsonPropertyName("config")]
        public Dictionary<string, string> Config { get; set; } = new();

        /// <summary>
        /// Plugin call timeout.
        /// </summary>
        [JsonPropertyName("timeout_ms")]
        [JsonConverter(typeof(TimeSpanMillisecondsConverter))]
        public TimeSpan? Timeout { get; set; }
    }

    /// <summary>
    /// Configures memory for the Wasm runtime.
    /// Memory is described in units of pages (64KB) and represent contiguous chunks of addressable memory.
    /// </summary>
    public class MemoryOptions
    {
        /// <summary>
        /// Max number of pages. Each page is 64KB.
        /// </summary>
        [JsonPropertyName("max")]
        public int MaxPages { get; set; }
    }

    /// <summary>
    /// A named Wasm source.
    /// </summary>
    public abstract class WasmSource
    {
        /// <summary>
        /// Logical name of the Wasm source
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Hash of the WASM source
        /// </summary>
        [JsonPropertyName("hash")]
        public string? Hash { get; set; }
    }

    /// <summary>
    /// Wasm Source represented by a file referenced by a path.
    /// </summary>
    public class PathWasmSource : WasmSource
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">path to wasm plugin.</param>
        /// <param name="name"></param>
        /// <param name="hash"></param>
        public PathWasmSource(string path, string? name = null, string? hash = null)
        {
            Path = System.IO.Path.GetFullPath(path);
            Name = name ?? System.IO.Path.GetFileNameWithoutExtension(path);
            Hash = hash;
        }

        /// <summary>
        /// Path to wasm plugin.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; }
    }

    /// <summary>
    /// Wasm Source represented by a file referenced by a path.
    /// </summary>
    public class UrlWasmSource : WasmSource
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="url">uri to wasm plugin.</param>
        /// <param name="name"></param>
        /// <param name="hash"></param>
        public UrlWasmSource(string url, string? name = null, string? hash = null) : this(new Uri(url), name, hash)
        {

        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="url">uri to wasm plugin.</param>
        /// <param name="name"></param>
        /// <param name="hash"></param>
        public UrlWasmSource(Uri url, string? name = null, string? hash = null)
        {
            Url = url;
            Name = name;
            Hash = hash;
        }

        /// <summary>
        /// Uri to wasm plugin.
        /// </summary>
        [JsonPropertyName("url")]
        public Uri Url { get; set; }

        /// <summary>
        /// HTTP headers
        /// </summary>
        [JsonPropertyName("header")]
        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>
        /// HTTP Method
        /// </summary>
        [JsonPropertyName("method")]
        public HttpMethod? Method { get; set; }
    }

    public enum HttpMethod
    {
        GET,
        POST,
        PUT,
    }

    /// <summary>
    /// Wasm Source represented by raw bytes.
    /// </summary>
    public class ByteArrayWasmSource : WasmSource
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="data">the byte array representing the Wasm code</param>
        /// <param name="name"></param>
        /// <param name="hash"></param>
        public ByteArrayWasmSource(byte[] data, string? name, string? hash = null)
        {
            Data = data;
            Name = name;
            Hash = hash;
        }

        /// <summary>
        /// The byte array representing the Wasm code
        /// </summary>
        [JsonPropertyName("data")]
        public byte[] Data { get; }
    }

    class WasmSourceConverter : JsonConverter<WasmSource>
    {
        public override WasmSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, WasmSource value, JsonSerializerOptions options)
        {
            if (value is PathWasmSource path)
                JsonSerializer.Serialize(writer, path, typeof(PathWasmSource), options);
            else if (value is ByteArrayWasmSource bytes)
                JsonSerializer.Serialize(writer, bytes, typeof(ByteArrayWasmSource), options);
            else if (value is UrlWasmSource uri)
                JsonSerializer.Serialize(writer, uri, typeof(UrlWasmSource), options);
            else
                throw new ArgumentOutOfRangeException(nameof(value), "Unknown Wasm Source");
        }
    }

    class TimeSpanMillisecondsConverter : JsonConverter<TimeSpan?>
    {
        public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                long milliseconds = reader.GetInt64();
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            throw new JsonException($"Expected number, but got {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteNumberValue((long)value.Value.TotalMilliseconds);
            }
        }
    }
}
