using Extism.Sdk.Native;

using System.Text;

namespace Extism.Sdk
{
    /// <summary>
    /// Represents the current plugin. Can only be used within <see cref="HostFunction"/>s.
    /// </summary>
    public class CurrentPlugin
    {
        internal CurrentPlugin(long nativeHandle, nint userData)
        {
            NativeHandle = nativeHandle;
            UserData = userData;
        }

        internal long NativeHandle { get; }

        /// <summary>
        /// An opaque pointer to an object from the host, passed in when a <see cref="HostFunction"/> is registered.
        /// </summary>
        public nint UserData { get; set; }

        /// <summary>
        /// Returns a offset to the memory of the currently running plugin.
        /// NOTE: this should only be called from host functions.
        /// </summary>
        /// <returns></returns>
        public long GetMemory()
        {
            return LibExtism.extism_current_plugin_memory(NativeHandle);
        }

        /// <summary>
        /// Reads a string from a memory block using UTF8.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public string ReadString(long offset)
        {
            return ReadString(offset, Encoding.UTF8);
        }

        /// <summary>
        /// Reads a string form a memory block.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public string ReadString(long offset, Encoding encoding)
        {
            var buffer = ReadBytes(offset);

            return encoding.GetString(buffer);
        }

        /// <summary>
        /// Returns a span of bytes for a given block.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public unsafe Span<byte> ReadBytes(long offset)
        {
            var mem = GetMemory();
            var length = (int)BlockLength(offset);
            var ptr = (byte*)mem + offset;

            return new Span<byte>(ptr, length);
        }

        /// <summary>
        /// Writes a string into the current plugin memory using UTF-8 encoding and returns the offset of the block.
        /// </summary>
        /// <param name="value"></param>
        public long WriteString(string value)
            => WriteString(value, Encoding.UTF8);

        /// <summary>
        /// Writes a string into the current plugin memory and returns the offset of the block.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="encoding"></param>
        public long WriteString(string value, Encoding encoding)
        {
            var bytes = encoding.GetBytes(value);
            var offset = AllocateBlock(bytes.Length);
            WriteBytes(offset, bytes);

            return offset;
        }

        /// <summary>
        /// Writes a byte array into a newly allocated block of memory.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>Returns the offset of the allocated block</returns>
        public long WriteBytes(Span<byte> bytes)
        {
            var offset = AllocateBlock(bytes.Length);
            WriteBytes(offset, bytes);
            return offset;
        }

        /// <summary>
        /// Writes a byte array into a block of memory.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="bytes"></param>
        public unsafe void WriteBytes(long offset, Span<byte> bytes)
        {
            var length = BlockLength(offset);
            if (length < bytes.Length)
            {
                throw new InvalidOperationException("Destination block length is less than source block length.");
            }

            var mem = GetMemory();
            var ptr = (void*)(mem + offset);
            var destination = new Span<byte>(ptr, bytes.Length);

            bytes.CopyTo(destination);
        }

        /// <summary>
        /// Frees a block of memory belonging to the current plugin.
        /// </summary>
        /// <param name="offset"></param>
        public void FreeBlock(long offset)
        {
            LibExtism.extism_current_plugin_memory_free(NativeHandle, offset);
        }

        /// <summary>
        /// Allocate a memory block in the currently running plugin.
        /// 
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public long AllocateBlock(long length)
        {
            return LibExtism.extism_current_plugin_memory_alloc(NativeHandle, length);
        }

        /// <summary>
        /// Get the length of an allocated block.
        /// NOTE: this should only be called from host functions.
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public long BlockLength(long offset)
        {
            return LibExtism.extism_current_plugin_memory_length(NativeHandle, offset);
        }
    }
}