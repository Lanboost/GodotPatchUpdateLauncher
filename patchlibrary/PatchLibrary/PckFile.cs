

using System.Drawing;
using System.Reflection.PortableExecutable;

namespace PatchLibrary
{
    public class PckInnerRawFile
    {
        public int pathlen;
        public byte[] path;
        public long ofs;
        public long size;
        public byte[] hash;
        public int fileflags;
        public byte[] filedata;

        public PckInnerRawFile(int pathlen, byte[] path, long ofs, long size, byte[] hash, int fileflags, byte[] filedata)
        {
            this.pathlen = pathlen;
            this.path = path;
            this.ofs = ofs;
            this.size = size;
            this.hash = hash;
            this.fileflags = fileflags;
            this.filedata = filedata;
        }
    }

    public class PckInnerFile
    {
        public string path;
        public long offset;
        public long size;
        public string hash;
        public PckInnerRawFile raw;

        public PckInnerFile(string path, long offset, long size, string hash, PckInnerRawFile raw)
        {
            this.path = path;
            this.offset = offset;
            this.size = size;
            this.hash = hash;
            this.raw = raw;
        }

        public static long HeaderSize()
        {
            // Should be padded to 32
            var size = 4 + PckFile.MAX_FILE_NAME + 8 + 8 + 16 + 4;
            return size;
            /*var pad = 32 - size % 32;
            if(pad == 32)
            {
                pad = 0;
            }
            return 4 + PckFile.MAX_FILE_NAME + 8 + 8 + 16 + 4 + pad;*/
        }

        public void WriteHeader(BinaryWriter writer)
        {
            var extrapathpadding = PckFile.MAX_FILE_NAME - this.raw.pathlen;
            if (extrapathpadding < 0)
            {
                var realpath = System.Text.Encoding.UTF8.GetString(this.raw.path);
                throw new Exception($"Pathsize is above limit {PckFile.MAX_FILE_NAME} length was: {realpath}");
            }
            writer.Write((int) (this.raw.pathlen + extrapathpadding));
            writer.Write(this.raw.path);

            for (int i = 0; i < extrapathpadding; i++)
            {
                writer.Write((byte)0);
            }

            writer.Write(this.raw.ofs);
            writer.Write((long)this.raw.filedata.Length);
            writer.Write(this.raw.hash);
            writer.Write(this.raw.fileflags);

        }

        public void WriteFile(BinaryWriter writer)
        {
            writer.Write(this.raw.filedata);
        }
    }

    public class PckFile : IDisposable
    {
        public static int CHUNK_SIZE = 1024*8;

        public enum Offsets
        {
            Magic = 0,
            FormatVersion = 4,
            Major = 8,
            Minor = 12,
            Patch = 16,
            Flags = 20,
            OffsetBase = 24,
            // 16 ints reserved
            FileCount = 24+8+16*4,
            IndexStart = 24+8+ 16 * 4+4,
        }

        FileInfo path;

        FileStream? fs;
        BinaryReader? reader;
        BinaryWriter? writer;

        protected List<PckInnerFile>? index = null;
        long FileStartOffset;

        // Should be divisible by 4...
        public static long MAX_FILE_NAME = 200;
        public static long MAX_FILES = 10000;
        

        public struct FreeChunks
        {
            public int start;
            public int length;

            public FreeChunks(int start, int length)
            {
                this.start = start;
                this.length = length;
            }
        }

        public byte[] headerData;
        public List<FreeChunks> freeChunks = new List<FreeChunks>();

        public PckFile(FileInfo path) {
            this.path = path;
            this.FileStartOffset = (int)Offsets.IndexStart + PckInnerFile.HeaderSize() * MAX_FILES;


        }

        public void Dispose()
        {
            this.reader?.Dispose();
            this.fs?.Dispose();
        }

        public void Open()
        {
            if(fs != null)
            {
                fs.Close();
                reader?.Dispose();
                writer?.Dispose();

            }

            fs = File.Open(path.FullName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            reader = new BinaryReader(fs);
            writer = new BinaryWriter(fs);
        }

        public void ReadIndex()
        {
            BinaryReader reader = this.reader ?? throw new InvalidOperationException();


            headerData = reader.ReadBytes((int)Offsets.IndexStart);
            reader.BaseStream.Seek((int)Offsets.OffsetBase, SeekOrigin.Begin);
            FileStartOffset = reader.ReadInt64();
            reader.BaseStream.Seek((int)Offsets.FileCount, SeekOrigin.Begin);
            var filecount = reader.ReadInt32();

            reader.BaseStream.Seek((int)Offsets.IndexStart, SeekOrigin.Begin);
            this.index = new List<PckInnerFile>();
            for (int i=0; i<filecount; i++)
            {
                var pathlen = reader.ReadInt32();
                var path = reader.ReadBytes(pathlen);
                var ofs = reader.ReadInt64();
                var size = reader.ReadInt64();
                var hash = reader.ReadBytes(16);
                var fileflags = reader.ReadInt32();

                var raw = new PckInnerRawFile(
                    pathlen, path, ofs, size, hash, fileflags, new byte[0]
                );
                this.index?.Add(new PckInnerFile(System.Text.Encoding.UTF8.GetString(path).TrimEnd((Char)0), ofs, size, Convert.ToBase64String(hash), raw));
            }
        }

        public void LoadFile(PckInnerFile file)
        {
            var reader = this.reader ?? throw new InvalidOperationException();

            reader.BaseStream.Seek(FileStartOffset + file.raw.ofs, SeekOrigin.Begin);
            file.raw.filedata = reader.ReadBytes((int)file.raw.size);    
        }

        public IEnumerable<string> GetFiles()
        {
            var index = this.index ?? throw new InvalidOperationException();
            foreach (var ind in index)
            {
                yield return ind.path;
            }
        }


        public void RemoveFile(string path) {
            var index = this.index ?? throw new InvalidOperationException();
            BinaryWriter writer = this.writer ?? throw new InvalidOperationException();

            var file = GetFile(path);
            // Free chunks
            var chunkCount = SizeToChunkCount(file.raw.size);
            var chunkStart = SizeToChunkCount(file.raw.ofs);
            this.freeChunks.Add(new FreeChunks(chunkStart, chunkCount));

            // Overwrite index by last index
            if (index.Count > 1)
            {
                // Move index around...
                var fileindexid = index.IndexOf(file);
                var lastfile = index[index.Count - 1];
                index.RemoveAt(fileindexid);
                index.RemoveAt(index.Count - 1);
                index.Insert(fileindexid, lastfile);

                WriteUpdateFileCount();
                GotoIndexFileOffset(fileindexid);
                file.WriteHeader(writer);
            }
            else
            {
                index.Remove(file);
                WriteUpdateFileCount();
                // File is empty, clean free chunks I guess
                freeChunks.Clear();
            }
        }

        void GotoIndexFileOffset(int fileId)
        {
            BinaryWriter writer = this.writer ?? throw new InvalidOperationException();
            writer.BaseStream.Seek((int)Offsets.IndexStart+ fileId*PckInnerFile.HeaderSize(), SeekOrigin.Begin);
        }

        void WriteUpdateOffsetBase(long offset)
        {
            this.FileStartOffset = offset;
            BinaryWriter writer = this.writer ?? throw new InvalidOperationException();
            writer.BaseStream.Seek((int)Offsets.OffsetBase, SeekOrigin.Begin);
            writer.Write(offset);
        }

        void WriteUpdateFileCount()
        {
            BinaryWriter writer = this.writer ?? throw new InvalidOperationException();
            var index = this.index ?? throw new InvalidOperationException();

            writer.BaseStream.Seek((int)Offsets.FileCount, SeekOrigin.Begin);
            writer.Write(index.Count);
        }

        public void AddFile(PckInnerFile file) {
            // TODO ensure we dont overwrite exising file with new file metadata

            //check if first file, if so set offset to predetermined value as of now
            var index = this.index ?? throw new InvalidOperationException();
            BinaryWriter writer = this.writer ?? throw new InvalidOperationException();

            int fileChunkCount = SizeToChunkCount(file.raw.filedata.Length);
            int fileChunkStart = 0;
            if(index.Count > 0) {
                fileChunkStart = FindFreeSlot(fileChunkCount);
                if (fileChunkStart == -1)
                {
                    fileChunkStart = GetEndChunkId();
                }
            }
            file.raw.ofs = fileChunkStart*CHUNK_SIZE;

            index.Add(file);
            WriteUpdateFileCount();

            // Write header
            GotoIndexFileOffset(index.Count - 1);
            file.WriteHeader(writer);

            // Write file
            GotoFileDataOffset(file);
            file.WriteFile(writer);
        }

        void GotoFileDataOffset(PckInnerFile file)
        {
            BinaryWriter writer = this.writer ?? throw new InvalidOperationException();
            var offset = this.FileStartOffset + file.raw.ofs;
            writer.BaseStream.Seek(offset, SeekOrigin.Begin);
        }

        int GetEndChunkId()
        {
            var index = this.index ?? throw new InvalidOperationException();
            long maxoffset = 0;
            foreach (var ind in index)
            {
                maxoffset = Math.Max(maxoffset, this.FileStartOffset + ind.raw.ofs + ind.size);
            }
            return SizeToChunkCount(maxoffset);
        }

        long GetEndChunkPosition()
        {
            return GetEndChunkId() * CHUNK_SIZE;
        }

        public void ReadChunkTable() {
            var startpos = GetEndChunkPosition();
            fs?.Seek(startpos, SeekOrigin.Begin);
            BinaryReader reader = this.reader ?? throw new InvalidOperationException();
            var count = reader.ReadInt32();
            for(int i = 0; i < count; i++)
            {
                var chunk = new FreeChunks(reader.ReadInt32(), reader.ReadInt32());
                freeChunks.Add(chunk);
            }
        }

        public void WriteChunkTable() {
            BinaryWriter writer = this.writer ?? throw new InvalidOperationException();

            var startpos = GetEndChunkPosition();
            writer.BaseStream.Seek(startpos, SeekOrigin.Begin);
            writer.Write((int)freeChunks.Count);
            foreach(var chunk in freeChunks)
            {
                writer.Write((int)chunk.start);
                writer.Write((int)chunk.length);
            }
        }

        int FindFreeSlot(int chunkCount)
        {
            int bestIndex = -1;
            int bestExtra = -1;

            //var chunkSize = SizeToChunkSize(size);

            for (int i=0; i<freeChunks.Count; i++)
            {
                var extra = freeChunks[i].length - chunkCount;
                if (extra >= 0)
                {
                    if (bestIndex == -1 || extra < bestExtra)
                    {
                        bestIndex = i;
                        bestExtra = extra;
                    }
                }
            }

            if(bestIndex != -1)
            {
                var free = freeChunks[bestIndex];
                freeChunks.RemoveAt(bestIndex);
                if(bestExtra > 0)
                {
                    freeChunks.Add(new FreeChunks(free.start + chunkCount, bestExtra));
                }

                return bestIndex;
            }
            return -1;
        }

        int SizeToChunkCount(long size)
        {
            return (int) Math.Ceiling((double)size / CHUNK_SIZE);
        }

        public void CreateNewChunked(byte[] headerData)
        {
            this.index = new List<PckInnerFile>();

            this.headerData = headerData;
            this.Open();
            BinaryWriter writer = this.writer ?? throw new InvalidOperationException();
            writer.Write(headerData);
            WriteUpdateOffsetBase(this.FileStartOffset);
            WriteUpdateFileCount();

        }

        public PckInnerFile GetFile(string path)
        {
            var index = this.index ?? throw new InvalidOperationException();
            foreach (var ind in index)
            {
                if (ind.path == path)
                {
                    return ind;
                }
            }
            throw new Exception("Not found");
        }
    }
}
