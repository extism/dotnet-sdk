using System.Text.Json.Serialization;
using System.Text.Json;

namespace Extism.Sdk
{
    [JsonSerializable(typeof(Manifest))]
    [JsonSerializable(typeof(HttpMethod))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(WasmSource))]
    [JsonSerializable(typeof(ByteArrayWasmSource))]
    [JsonSerializable(typeof(PathWasmSource))]
    [JsonSerializable(typeof(UrlWasmSource))]
    internal partial class ManifestJsonContext : JsonSerializerContext
    {

    }

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
            foreach (var source in sources)
            {
                Sources.Add(source);
            }
        }

        /// <summary>
        /// List of Wasm sources. See <see cref="PathWasmSource"/> and <see cref="ByteArrayWasmSource"/>.
        /// </summary>
        [JsonPropertyName("wasm")]
        public IList<WasmSource> Sources { get; set; } = new List<WasmSource>();

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
        public IList<string> AllowedHosts { get; set; } = new List<string>();

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
        public IDictionary<string, string> AllowedPaths { get; set; } = new Dictionary<string, string>();

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
        public IDictionary<string, string> Config { get; set; } = new Dictionary<string, string>();

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
        [JsonPropertyName("max_pages")]
        public int MaxPages { get; set; }


        /// <summary>
        /// Max number of bytes allowed in an HTTP response when using extism_http_request.
        /// </summary>
        [JsonPropertyName("max_http_response_bytes")]
        public int MaxHttpResponseBytes { get; set; }


        /// <summary>
        /// Max number of bytes allowed in the Extism var store
        /// </summary>
        [JsonPropertyName("max_var_bytes")]
        public int MaxVarBytes { get; set; }
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

    /// <summary>
    /// HTTP defines a set of request methods to indicate the desired action to be performed for a given resource.
    /// </summary>
    public enum HttpMethod
    {
        /// <summary>
        /// The GET method requests a representation of the specified resource. Requests using GET should only retrieve data.
        /// </summary>
        GET,

        /// <summary>
        /// The HEAD method asks for a response identical to a GET request, but without the response body.
        /// </summary>
        HEAD,

        /// <summary>
        /// The POST method submits an entity to the specified resource, often causing a change in state or side effects on the server.
        /// </summary>
        POST,

        /// <summary>
        /// The PUT method replaces all current representations of the target resource with the request payload.
        /// </summary>
        PUT,

        /// <summary>
        /// The DELETE method deletes the specified resource.
        /// </summary>
        DELETE,

        /// <summary>
        /// The CONNECT method establishes a tunnel to the server identified by the target resource.
        /// </summary>
        CONNECT,

        /// <summary>
        /// The OPTIONS method describes the communication options for the target resource.
        /// </summary>
        OPTIONS,

        /// <summary>
        /// The TRACE method performs a message loop-back test along the path to the target resource.
        /// </summary>
        TRACE,

        /// <summary>
        /// The PATCH method applies partial modifications to a resource.
        /// </summary>
        PATCH,
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
            // Clone it because a JsonSerializerOptions can't be shared by multiple JsonSerializerContexts
            var context = new ManifestJsonContext(new JsonSerializerOptions(options));
            if (value is PathWasmSource path)
            {
                JsonSerializer.Serialize(writer, path, context.PathWasmSource);
            }
            else if (value is ByteArrayWasmSource bytes)
            {
                JsonSerializer.Serialize(writer, bytes, context.ByteArrayWasmSource);
            }
            else if (value is UrlWasmSource uri)
            {
                JsonSerializer.Serialize(writer, uri, context.UrlWasmSource);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Unknown Wasm Source");
            }
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
