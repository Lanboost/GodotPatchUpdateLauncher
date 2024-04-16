using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PatchLibrary
{
    public class Patch
    {

        static void WriteFile(BinaryWriter writer, FileInfo curr)
        {
            writer.Write(curr.Length);

            using (FileStream stream = File.OpenRead(curr.FullName))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                // create a buffer to hold the bytes 
                byte[] buffer = new Byte[1024];
                int bytesRead;

                // while the read method returns bytes
                // keep writing them to the output stream
                while ((bytesRead = stream.Read(buffer, 0, 1024)) > 0)
                {
                    writer.Write(buffer, 0, bytesRead);
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

        public static void CreatePatch(FileInfo output, DirectoryInfo folder1, DirectoryInfo folder2)
        {
            using (var fileStream = output.OpenWrite())
            using (var writer = new BinaryWriter(fileStream))
            {
                writer.Write((long)0); //from
                writer.Write((long)0); //to

                //Patch patch = new Patch();
                foreach (var folderDiff in PathUtil.CompareFolders(folder1, folder2, new Regex(@"\.pck", RegexOptions.IgnoreCase)))
                {
                    if (folderDiff.difference == PathUtil.EDifference.Added)
                    {
                        writer.Write(0);
                        writer.Write(folderDiff.relativePath);

                        var curr = folder2.File(folderDiff.relativePath);
                        WriteFile(writer, curr);
                    }
                    else if (folderDiff.difference == PathUtil.EDifference.Removed)
                    {
                        writer.Write(1);
                        writer.Write(folderDiff.relativePath);
                    }
                    else if (folderDiff.difference == PathUtil.EDifference.Changed)
                    {
                        writer.Write(2);
                        writer.Write(folderDiff.relativePath);

                        var curr = folder2.File(folderDiff.relativePath);
                        WriteFile(writer, curr);
                    }
                }

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
                        }
                        else if (folderDiff.difference == PathUtil.EDifference.Removed)
                        {
                            writer.Write(4);
                            writer.Write(folderDiff.relativePath);
                        }
                        else if (folderDiff.difference == PathUtil.EDifference.Changed)
                        {
                            writer.Write(5);
                            writer.Write(folderDiff.relativePath);

                            var file = pckstart.GetFile(folderDiff.relativePath);
                            pckstart.LoadFile(file);
                            WritePckFile(writer, file);
                        }
                    }
                }
            }
        }

        
    }
}
