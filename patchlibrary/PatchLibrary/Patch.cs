using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace PatchLibrary
{
    public class Patch
    {

        static void WriteFile(BinaryWriter writer, FileInfo curr)
        {
            writer.Write(curr.Length);

            using (FileStream stream = File.OpenRead(curr.FullName))
            {
                foreach(var (written, total) in WriteDirectFile(writer, stream))
                {

                }
            }
        }

        internal static IEnumerable<(long, long)> WriteDirectFile(BinaryWriter writer, Stream reader, long left = -1)
        {
            // create a buffer to hold the bytes 
            byte[] buffer = new Byte[1024];
            int bytesRead;
            long totalRead = 0;
            long total = left;

            // while the read method returns bytes
            // keep writing them to the output stream

            if (left > 0)
            {
                while ((bytesRead = reader.Read(buffer, 0, (int) Math.Min(1024, left))) > 0)
                {
                    writer.Write(buffer, 0, bytesRead);
                    left -= bytesRead;
                    totalRead += bytesRead;
                    yield return (totalRead, total);
                }
            }
            else
            {
                while ((bytesRead = reader.Read(buffer, 0, 1024)) > 0)
                {
                    writer.Write(buffer, 0, bytesRead);
                    yield return (totalRead, total);
                }
            }
        }

        private static void WritePckFile(BinaryWriter writer, PckInnerFile file)
        {
            writer.Write(file.raw.pathlen);
            writer.Write(file.raw.path);
            writer.Write(file.raw.hash);
            writer.Write(file.raw.fileflags);

            writer.Write(file.raw.size);
            writer.Write(file.raw.filedata);
        }

        static FileInfo GetPckFile(DirectoryInfo dir)
        {
            var files = dir.GetFiles("*.pck");
            if(files.Length != 1)
            {
                throw new Exception("Unexpected count of pck files ");
            }
            return new FileInfo(Path.GetRelativePath(dir.FullName, files[0].FullName));
        }

        public static IEnumerable<(long, long, string, long, long)> ApplyPatch(FileInfo patch, DirectoryInfo folder)
        {
            var cache = folder.CreateSubdirectory("cache");
            var cachefiles = cache.GetFiles();
            yield return (0, cachefiles.Length, $"Start Cleaning Cache: 0 / {cachefiles.Length}", 0, 1);
            for(int i=0; i< cachefiles.Length; i++)
            {
                FileInfo file = cachefiles[i];
                file.Delete();
                yield return (cachefiles.Length, cachefiles.Length, $"Cleaning Cache: {i+1} / {cachefiles.Length}", 0, 1);
            }

            Dictionary<FileInfo, FileInfo> toCopyFromCache = new Dictionary<FileInfo, FileInfo>();
            List<FileInfo> toRemove = new List<FileInfo>();

            int steps = 0;
            using (var fileStream = patch.OpenRead())
            using (var reader = new BinaryReader(fileStream))
            {
                var from = reader.ReadInt64();
                var to = reader.ReadInt64();
                steps = reader.ReadInt32(); // TODO make move also count?

                for(int i= 0; i < steps; i++)
                {
                    var cmd = reader.ReadInt32();
                    if (cmd == 0 || cmd == 2)
                    {
                        var tempname = i.ToString();
                        var tempfile = cache.File(tempname);
                        var name = reader.ReadString();
                        var len = reader.ReadInt64();

                        yield return (0, len, $"Start Extracting File: {name}", i, steps * 2);

                        using (var tempfileStream = tempfile.OpenWrite())
                        using (var writer = new BinaryWriter(tempfileStream))
                        {
                            foreach(var (written, total) in WriteDirectFile(writer, fileStream, len))
                            {
                                yield return (written, total, $"Extracting File: {name}", i, steps * 2);
                            }
                        }
                        toCopyFromCache.Add(tempfile, folder.File(name));

                        yield return (i, steps, "Extraction Complete", i + 1, steps * 2);
                    }
                    else if(cmd == 1)
                    {
                        var name = reader.ReadString();
                        toRemove.Add(folder.File(name));
                    }
                }
            }

            // So far so good, now the danger starts, if application stops here, program will be corrupt...
            // TODO, make it so we store patch state somewhere, and can replay from where it stopped...

            int index = 0;
            foreach(var item in toCopyFromCache)
            {
                yield return (0, 1, $"Start Moving file: {item.Value.FullName}", index, steps * 2);
                Directory.CreateDirectory(item.Value.Directory.FullName);
                System.IO.File.Copy(item.Key.FullName, item.Value.FullName, true);
                yield return (1, 1, $"File Moved: {item.Value.FullName}", index, steps * 2);
                index++;
                //var v = 0;
                //yield return (0, 0, $"Create Directory {item.Value.Directory.FullName}");
                //yield return (0, 0, $"Copy file {item.Key.FullName},{item.Value.FullName}");
            }

            foreach (var item in toRemove)
            {
                item.Delete();
                yield return (1, 1, $"Deleted File: {item.FullName}", index, steps * 2);
                index++;
                //var v = 0;
                //yield return (0, 0, $"Delete file {item.FullName}");
            }

            // Clean cache again
            foreach (FileInfo file in cache.GetFiles())
            {
                file.Delete();
            }
            cache.Delete();

            yield return (1, 1, $"Patch Applied", 1, 1);
        }

        public static void CreatePatch(FileInfo output, DirectoryInfo folder1, DirectoryInfo folder2)
        {
            using (var fileStream = output.OpenWrite())
            using (var writer = new BinaryWriter(fileStream))
            {
                // TODO
                writer.Write((long)0); //from
                writer.Write((long)0); //to
                var stepsoffset = writer.BaseStream.Position;
                var steps = 0;
                writer.Write((int)0); //steps (will be updated after patch is written)

                foreach (var folderDiff in PathUtil.CompareFolders(folder1, folder2, new Regex(@"\.pck", RegexOptions.IgnoreCase)))
                {
                    if (folderDiff.difference == PathUtil.EDifference.Added)
                    {
                        writer.Write(0);
                        writer.Write(folderDiff.relativePath);

                        var curr = folder2.File(folderDiff.relativePath);
                        WriteFile(writer, curr);
                        steps++;
                    }
                    else if (folderDiff.difference == PathUtil.EDifference.Removed)
                    {
                        writer.Write(1);
                        writer.Write(folderDiff.relativePath);
                        steps++;
                    }
                    else if (folderDiff.difference == PathUtil.EDifference.Changed)
                    {
                        writer.Write(2);
                        writer.Write(folderDiff.relativePath);

                        var curr = folder2.File(folderDiff.relativePath);
                        WriteFile(writer, curr);
                        steps++;
                    }
                }
                /*
                var pckfile = GetPckFile(folder1);
                var folderDiffList = PathUtil.ComparePckFolders(folder1, folder2, pckfile).ToList();
                // Read pck files and write them
                using (var pckstart = new PckFile(folder2.File(pckfile.FullName)))
                {
                    pckstart.Open();
                    pckstart.ReadIndex();
                    foreach (var folderDiff in folderDiffList)
                    {
                        if (folderDiff.difference == PathUtil.EDifference.Added)
                        {
                            writer.Write(3);
                            writer.Write(folderDiff.relativePath);

                            var file = pckstart.GetFile(folderDiff.relativePath);
                            pckstart.LoadFile(file);
                            WritePckFile(writer, file);
                            steps++;
                        }
                        else if (folderDiff.difference == PathUtil.EDifference.Removed)
                        {
                            writer.Write(4);
                            writer.Write(folderDiff.relativePath);
                            steps++;
                        }
                        else if (folderDiff.difference == PathUtil.EDifference.Changed)
                        {
                            writer.Write(5);
                            writer.Write(folderDiff.relativePath);

                            var file = pckstart.GetFile(folderDiff.relativePath);
                            pckstart.LoadFile(file);
                            WritePckFile(writer, file);
                            steps++;
                        }
                    }
                }*/

                writer.BaseStream.Seek(stepsoffset, SeekOrigin.Begin);
                writer.Write(steps);
            }
        }
    }
}
