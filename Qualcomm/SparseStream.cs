using System;
using System.IO;

namespace OPFlashTool.Qualcomm
{
    public class SparseStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _totalExpandedSize;
        private long _position;
        
        // Sparse Header Info
        private ushort _chunkHeaderSize;
        private uint _blockSize;
        private uint _totalChunks;
        
        // Current Chunk State
        private int _currentChunkIndex;
        private ushort _currentChunkType;
        private long _currentChunkRemainingBytes; // Bytes remaining in the current chunk (expanded view)
        private uint _currentFillValue;
        
        // Constants
        private const uint SPARSE_HEADER_MAGIC = 0xED26FF3A;
        private const ushort CHUNK_TYPE_RAW = 0xCAC1;
        private const ushort CHUNK_TYPE_FILL = 0xCAC2;
        private const ushort CHUNK_TYPE_DONT_CARE = 0xCAC3;
        private const ushort CHUNK_TYPE_CRC32 = 0xCAC4;

        public SparseStream(Stream baseStream)
        {
            if (!baseStream.CanRead) throw new ArgumentException("Base stream must be readable");
            _baseStream = baseStream;
            
            // Read Header
            byte[] header = new byte[28];
            if (_baseStream.Read(header, 0, 28) != 28) throw new IOException("Invalid sparse header");
            
            uint magic = BitConverter.ToUInt32(header, 0);
            if (magic != SPARSE_HEADER_MAGIC) throw new IOException("Not a sparse image");

            ushort fileHeaderSize = BitConverter.ToUInt16(header, 8);
            _chunkHeaderSize = BitConverter.ToUInt16(header, 10);
            _blockSize = BitConverter.ToUInt32(header, 12);
            uint totalBlocks = BitConverter.ToUInt32(header, 16);
            _totalChunks = BitConverter.ToUInt32(header, 20);

            _totalExpandedSize = (long)totalBlocks * _blockSize;
            
            // Seek to first chunk
            if (fileHeaderSize > 28)
                _baseStream.Seek(fileHeaderSize - 28, SeekOrigin.Current);

            _currentChunkIndex = -1;
            _currentChunkRemainingBytes = 0;
            _position = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _totalExpandedSize;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;

            while (count > 0)
            {
                // If current chunk is exhausted, move to next
                if (_currentChunkRemainingBytes == 0)
                {
                    if (!MoveToNextChunk()) break; // End of stream
                }

                int toProcess = (int)Math.Min(count, _currentChunkRemainingBytes);
                int bytesProcessed = 0;

                switch (_currentChunkType)
                {
                    case CHUNK_TYPE_RAW:
                        bytesProcessed = _baseStream.Read(buffer, offset, toProcess);
                        break;

                    case CHUNK_TYPE_FILL:
                        // Fill buffer with _currentFillValue
                        byte[] fillBytes = BitConverter.GetBytes(_currentFillValue);
                        for (int i = 0; i < toProcess; i++)
                        {
                            buffer[offset + i] = fillBytes[i % 4];
                        }
                        bytesProcessed = toProcess;
                        break;

                    case CHUNK_TYPE_DONT_CARE:
                        // Fill with zeros
                        Array.Clear(buffer, offset, toProcess);
                        bytesProcessed = toProcess;
                        break;
                        
                    default:
                        // Should not happen if MoveToNextChunk handles types correctly
                        break;
                }

                if (bytesProcessed == 0 && _currentChunkType == CHUNK_TYPE_RAW) break; // Unexpected EOF in raw chunk

                _currentChunkRemainingBytes -= bytesProcessed;
                _position += bytesProcessed;
                
                offset += bytesProcessed;
                count -= bytesProcessed;
                totalRead += bytesProcessed;
            }

            return totalRead;
        }

        private bool MoveToNextChunk()
        {
            _currentChunkIndex++;
            if (_currentChunkIndex >= _totalChunks) return false;

            byte[] header = new byte[12];
            if (_baseStream.Read(header, 0, 12) != 12) return false;

            _currentChunkType = BitConverter.ToUInt16(header, 0);
            // ushort reserved1 = BitConverter.ToUInt16(header, 2);
            uint chunkBlocks = BitConverter.ToUInt32(header, 4);
            uint totalSize = BitConverter.ToUInt32(header, 8);
            
            long dataSize = totalSize - _chunkHeaderSize;
            _currentChunkRemainingBytes = (long)chunkBlocks * _blockSize;

            // Handle specific chunk types setup
            if (_currentChunkType == CHUNK_TYPE_FILL)
            {
                byte[] fillValBytes = new byte[4];
                if (_baseStream.Read(fillValBytes, 0, 4) != 4) return false;
                _currentFillValue = BitConverter.ToUInt32(fillValBytes, 0);
            }
            else if (_currentChunkType == CHUNK_TYPE_CRC32)
            {
                // Skip CRC chunk and move to next immediately
                _baseStream.Seek(dataSize, SeekOrigin.Current);
                return MoveToNextChunk();
            }
            
            return true;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
