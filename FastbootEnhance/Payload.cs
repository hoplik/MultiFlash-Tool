using ChromeosUpdateEngine;
using Google.Protobuf;
using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OPFlashTool.FastbootEnhance
{
    public class Payload : IDisposable
    {
        string payload_tmp;
        BinaryReader binaryReader;
        public class PayloadInitException : Exception
        {
            public PayloadInitException(string message) : base(message) { }
        };
        public class PayloadExtractionException : Exception
        {
            public PayloadExtractionException(string message) : base(message) { }
        };

        const string magic = "CrAU";
        public UInt64 file_format_version;
        public UInt64 manifest_size;
        public UInt32 metadata_signature_size;
        public DeltaArchiveManifest manifest;
        public Signatures metadata_signature_message;
        //{data blocks}
        public UInt64 payload_signatures_message_size;
        public Signatures payload_signatures_message;

        long data_start;
        public ulong data_size;

        public PayloadExtractionException extract(string which, string path,
            bool ignore_unknown_op, bool ignore_checks)
        {
            SHA256Managed Sha256 = new SHA256Managed();

            foreach (PartitionUpdate partitionUpdate in manifest.Partitions)
            {
                if (partitionUpdate.PartitionName != which)
                    continue;

                using (FileStream fileStream = new FileStream(path + "\\" + which + ".img", FileMode.Create))
                {
                    foreach (InstallOperation installOperation in partitionUpdate.Operations)
                    {
                        binaryReader.BaseStream.Seek(data_start + (long)installOperation.DataOffset, SeekOrigin.Begin);
                        byte[] raw_data = binaryReader.ReadBytes((int)installOperation.DataLength);
                        if (!ignore_checks && installOperation.HasDataSha256Hash &&
                            installOperation.DataSha256Hash.ToBase64() != Convert.ToBase64String(Sha256.ComputeHash(raw_data)))
                            return new PayloadExtractionException("Block hash check failed");

                        if (installOperation.DstExtents == null)
                            return new PayloadExtractionException("No dst");

                        if (installOperation.DstExtents.Count > 1)
                            return new PayloadExtractionException("Multiple dst in one operation");

                        long dst_start = (long)installOperation.DstExtents[0].StartBlock * manifest.BlockSize;
                        long dst_length = (long)installOperation.DstExtents[0].NumBlocks * manifest.BlockSize;

                        fileStream.Seek(dst_start, SeekOrigin.Begin);

                        using (MemoryStream raw_data_stream = new MemoryStream(raw_data))
                        {
                            switch (installOperation.Type)
                            {
                                case InstallOperation.Types.Type.Replace:
                                    if (ignore_checks || (long)installOperation.DataLength == dst_length)
                                        raw_data_stream.CopyTo(fileStream);
                                    else
                                        return new PayloadExtractionException("REPLACE: Block size mismatch");
                                    break;
                                case InstallOperation.Types.Type.ReplaceBz:
                                    using (MemoryStream buf = new MemoryStream())
                                    {
                                        using (BZip2InputStream bZip = new BZip2InputStream(raw_data_stream))
                                        {
                                            bZip.CopyTo(buf);
                                        }

                                        if (ignore_checks || buf.Length == dst_length)
                                        {
                                            buf.Seek(0, SeekOrigin.Begin);
                                            buf.CopyTo(fileStream);
                                        }
                                        else
                                            return new PayloadExtractionException("BZ: Block size mismatch");
                                    }
                                    break;
                                case InstallOperation.Types.Type.ReplaceXz:
                                    using (MemoryStream buf = new MemoryStream())
                                    {
                                        using (XZ.NET.XZInputStream xZ = new XZ.NET.XZInputStream(raw_data_stream))
                                        {
                                            xZ.CopyTo(buf);
                                        }

                                        if (ignore_checks || buf.Length == dst_length)
                                        {
                                            buf.Seek(0, SeekOrigin.Begin);
                                            buf.CopyTo(fileStream);
                                        }
                                        else
                                            return new PayloadExtractionException("XZ: Block size mismatch");
                                    }
                                    break;
                                case InstallOperation.Types.Type.Zero:
                                    long i = dst_length;
                                    while (i-- != 0)
                                        fileStream.WriteByte(0);
                                    break;
                                default:
                                    if (!ignore_unknown_op)
                                        return new PayloadExtractionException("Unknown action type " + installOperation.Type.ToString());
                                    break;
                            }
                        }
                    }

                    fileStream.Seek(0, SeekOrigin.Begin);
                    if (!ignore_checks && partitionUpdate.NewPartitionInfo != null &&
                        (fileStream.Length != (long)partitionUpdate.NewPartitionInfo.Size
                        || (partitionUpdate.NewPartitionInfo.HasHash &&
                        Convert.ToBase64String(Sha256.ComputeHash(fileStream)) != partitionUpdate.NewPartitionInfo.Hash.ToBase64())))
                        return new PayloadExtractionException("Final image check failed");
                }

                return null;
            }

            return new PayloadExtractionException("Unable to find target");
        }
        public PayloadInitException init()
        {
            try
            {
                if (Encoding.ASCII.GetString(binaryReader.ReadBytes(4)) != magic)
                    return new PayloadInitException("Magic mismatch");

                byte[] versionBytes = binaryReader.ReadBytes(8);
                Array.Reverse(versionBytes);
                file_format_version = BitConverter.ToUInt64(versionBytes, 0);

                if (file_format_version < 2)
                    return new PayloadInitException("format version 1 is not supported");

                byte[] manifestSizeBytes = binaryReader.ReadBytes(8);
                Array.Reverse(manifestSizeBytes);
                manifest_size = BitConverter.ToUInt64(manifestSizeBytes, 0);

                byte[] signatureSizeBytes = binaryReader.ReadBytes(4);
                Array.Reverse(signatureSizeBytes);
                metadata_signature_size = BitConverter.ToUInt32(signatureSizeBytes, 0);

                if (manifest_size > Int32.MaxValue)
                    return new PayloadInitException("manifest_size overflowed");

                manifest = new MessageParser<DeltaArchiveManifest>(delegate { return new DeltaArchiveManifest(); })
                    .ParseFrom(binaryReader.ReadBytes((int)manifest_size));

                if (metadata_signature_size > Int32.MaxValue)
                    return new PayloadInitException("metadata_signature_size overflowed");

                metadata_signature_message = new MessageParser<Signatures>(delegate { return new Signatures(); })
                    .ParseFrom(binaryReader.ReadBytes((int)metadata_signature_size));

                data_start = binaryReader.BaseStream.Position;
                data_size = manifest.SignaturesOffset;

                binaryReader.BaseStream.Seek((long)manifest.SignaturesOffset, SeekOrigin.Current);
                payload_signatures_message_size = manifest.SignaturesSize;
                if (payload_signatures_message_size > Int32.MaxValue)
                    return new PayloadInitException("payload_signatures_message_size overflowed");
                payload_signatures_message = new MessageParser<Signatures>(delegate { return new Signatures(); })
                    .ParseFrom(binaryReader.ReadBytes((int)payload_signatures_message_size));

            }
            catch (Exception e)
            {
                return new PayloadInitException(e.Message);
            }

            return null;
        }

        public void Dispose()
        {
            if (binaryReader != null)
            {
                binaryReader.Close();
                binaryReader = null;
            }
            try
            {
                new DirectoryInfo(payload_tmp).Delete(true);
            }
            catch (DirectoryNotFoundException) { }
            catch (IOException) { }
        }

        public Payload(string path, String tmpdir)
        {
            payload_tmp = tmpdir;

            if (path.EndsWith(".zip"))
            {
                using (ZipFile zip = new ZipFile(path))
                {
                    foreach (ZipEntry entry in zip)
                    {
                        if (entry.Name == "payload.bin")
                        {
                            string extractPath = Path.Combine(payload_tmp, "payload.bin");
                            using (Stream zipStream = zip.GetInputStream(entry))
                            using (FileStream fs = File.Create(extractPath))
                            {
                                zipStream.CopyTo(fs);
                            }
                            binaryReader = new BinaryReader(
                                new FileStream(extractPath, FileMode.Open));
                            return;
                        }
                    }
                }
                throw new Exception("Unable to find entry for payload.bin");
            }
            binaryReader = new BinaryReader(new FileStream(path, FileMode.Open));
        }

        ~Payload()
        {
            Dispose();
        }
    }
}
