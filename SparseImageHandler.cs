using System;
using System.IO;
using System.Threading.Tasks;

namespace OPFlashTool
{
    public static class SparseImageHandler
    {
        private const uint SPARSE_HEADER_MAGIC = 0xED26FF3A;
        private const ushort CHUNK_TYPE_RAW = 0xCAC1;
        private const ushort CHUNK_TYPE_FILL = 0xCAC2;
        private const ushort CHUNK_TYPE_DONT_CARE = 0xCAC3;
        private const ushort CHUNK_TYPE_CRC32 = 0xCAC4;

        /// <summary>
        /// 检查文件是否为 Android Sparse 格式
        /// </summary>
        public static bool IsSparseImage(string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var br = new BinaryReader(fs))
                {
                    if (fs.Length < 28) return false;
                    uint magic = br.ReadUInt32();
                    return magic == SPARSE_HEADER_MAGIC;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 将 Sparse 镜像转换为 Raw 镜像 (异步)
        /// </summary>
        public static async Task<bool> ConvertToRawAsync(string sparsePath, string rawPath, Action<string> logger)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var fsIn = new FileStream(sparsePath, FileMode.Open, FileAccess.Read))
                    using (var br = new BinaryReader(fsIn))
                    using (var fsOut = new FileStream(rawPath, FileMode.Create, FileAccess.Write))
                    {
                        // Read Header
                        uint magic = br.ReadUInt32();
                        if (magic != SPARSE_HEADER_MAGIC)
                        {
                            logger("Not a sparse image.");
                            return false;
                        }

                        ushort majorVersion = br.ReadUInt16();
                        ushort minorVersion = br.ReadUInt16();
                        ushort fileHeaderSize = br.ReadUInt16();
                        ushort chunkHeaderSize = br.ReadUInt16();
                        uint blockSize = br.ReadUInt32();
                        uint totalBlocks = br.ReadUInt32();
                        uint totalChunks = br.ReadUInt32();
                        uint imageChecksum = br.ReadUInt32();

                        logger($"[Sparse] Header: Ver={majorVersion}.{minorVersion}, BlockSize={blockSize}, Chunks={totalChunks}");

                        if (fileHeaderSize > 28)
                            fsIn.Seek(fileHeaderSize - 28, SeekOrigin.Current);

                        for (int i = 0; i < totalChunks; i++)
                        {
                            ushort chunkType = br.ReadUInt16();
                            ushort reserved1 = br.ReadUInt16();
                            uint chunkBlocks = br.ReadUInt32();
                            uint totalSize = br.ReadUInt32();
                            
                            long dataSize = totalSize - chunkHeaderSize;

                            switch (chunkType)
                            {
                                case CHUNK_TYPE_RAW:
                                    byte[] buffer = new byte[4096];
                                    long remaining = dataSize;
                                    while (remaining > 0)
                                    {
                                        int toRead = (int)Math.Min(remaining, buffer.Length);
                                        int read = fsIn.Read(buffer, 0, toRead);
                                        fsOut.Write(buffer, 0, read);
                                        remaining -= read;
                                    }
                                    break;

                                case CHUNK_TYPE_FILL:
                                    if (dataSize != 4) throw new Exception("Fill chunk data size must be 4 bytes");
                                    uint fillValue = br.ReadUInt32();
                                    byte[] fillBytes = BitConverter.GetBytes(fillValue);
                                    byte[] fillBuffer = new byte[blockSize];
                                    for(int k=0; k<blockSize; k+=4) 
                                        Array.Copy(fillBytes, 0, fillBuffer, k, 4);
                                    
                                    for (int b = 0; b < chunkBlocks; b++)
                                    {
                                        fsOut.Write(fillBuffer, 0, (int)blockSize);
                                    }
                                    break;

                                case CHUNK_TYPE_DONT_CARE:
                                    byte[] zeros = new byte[blockSize];
                                    for (int b = 0; b < chunkBlocks; b++)
                                    {
                                        fsOut.Write(zeros, 0, (int)blockSize);
                                    }
                                    break;

                                case CHUNK_TYPE_CRC32:
                                    fsIn.Seek(dataSize, SeekOrigin.Current);
                                    break;

                                default:
                                    logger($"[Sparse] Unknown chunk type: {chunkType:X4}");
                                    return false;
                            }
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    logger($"[Sparse] Unsparse failed: {ex.Message}");
                    return false;
                }
            });
        }
    }
}