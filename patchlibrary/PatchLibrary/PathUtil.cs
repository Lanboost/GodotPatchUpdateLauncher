using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PatchLibrary
{
    public class PathUtil
    {
        public enum EDifference
        {
            None,
            Added,
            Removed,
            Changed
        }

        public struct FolderDifference
        {
            public string relativePath;
            public EDifference difference;

            public FolderDifference(string relativePath, EDifference difference)
            {
                this.relativePath = relativePath;
                this.difference = difference;
            }
        }


        public static IEnumerable<FolderDifference> CompareFolders(DirectoryInfo path1, DirectoryInfo path2, Regex? ignoreFileName = null)
        {
            Dictionary<string, int> folderFiles = new Dictionary<string, int>();

            foreach(var path in FolderFiles(path1, ignoreFileName))
            {
                folderFiles.Add(path, -1);
            }

            foreach (var path in FolderFiles(path2, ignoreFileName))
            {
                if (folderFiles.ContainsKey(path))
                {
                    folderFiles[path] = 0;
                }
                else
                {
                    folderFiles.Add(path, 1);
                }
            }

            foreach(var path in folderFiles.Keys)
            {
                var value = folderFiles[path];
                if(value == -1)
                {
                    yield return new FolderDifference(path, EDifference.Removed);
                }
                else if (value == 1)
                {
                    yield return new FolderDifference(path, EDifference.Added);
                }
                else
                {
                    var file1 = path1.File(path);
                    var file2 = path2.File(path);

                    if (!HashUtil.SameFile(file1, file2))
                    {
                        yield return new FolderDifference(path, EDifference.Changed);
                    }
                }
            }
        }

        public static IEnumerable<FolderDifference> ComparePckFolders(DirectoryInfo path1, DirectoryInfo path2, FileInfo pckPath)
        {
            Dictionary<string, string> folderHash = new Dictionary<string, string>();
            Dictionary<string, int> folderFiles = new Dictionary<string, int>();

            using (var pckstart = new PckFile(path1.File(pckPath.FullName)))
            {
                pckstart.Open();
                pckstart.ReadIndex();
                foreach (var file in pckstart.GetFiles())
                {
                    var raw = pckstart.GetFile(file);
                    folderFiles.Add(file, -1);
                    folderHash.Add(file, raw.hash);
                }
            }

            using (var pckstart = new PckFile(path2.File(pckPath.FullName)))
            {
                pckstart.Open();
                pckstart.ReadIndex();
                foreach (var file in pckstart.GetFiles())
                {
                    var raw = pckstart.GetFile(file);
                    if(folderFiles.ContainsKey(file))
                    {
                        folderFiles[file] = 0;
                        if(raw.hash == folderHash[file])
                        {
                            folderFiles.Remove(file);
                        }
                    }
                    else
                    {
                        folderFiles.Add(file, 1);
                    }
                }
            }

            foreach (var path in folderFiles.Keys)
            {
                var value = folderFiles[path];
                if (value == -1)
                {
                    yield return new FolderDifference(path, EDifference.Removed);
                }
                else if (value == 1)
                {
                    yield return new FolderDifference(path, EDifference.Added);
                }
                else
                {
                    yield return new FolderDifference(path, EDifference.Changed);
                }
            }
        }



        public static IEnumerable<string> FolderFiles(DirectoryInfo path, Regex? ignoreFileName = null)
        {
            Queue<DirectoryInfo> queue = new Queue<DirectoryInfo>();
            queue.Enqueue(path);
            while (queue.Count > 0)
            {
                var currpath = queue.Dequeue();
                foreach (var subDir in currpath.GetDirectories())
                {
                    queue.Enqueue(subDir);
                }
                foreach(var fileInfo in currpath.GetFiles())
                {
                    if (ignoreFileName == null || !ignoreFileName.IsMatch(fileInfo.Name)) {

                        yield return Path.GetRelativePath(path.FullName, fileInfo.FullName);
                    }
                }
            }
        }

    }

    public static class DirectoryInfoExtensions
    {
        public static FileInfo File(this DirectoryInfo directory, string file)
        {
            return new FileInfo(Path.Combine(directory.FullName, file));
        }
    }
}
