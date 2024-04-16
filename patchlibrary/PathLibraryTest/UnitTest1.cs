using PatchLibrary;
using System.Linq;
using System.Text.RegularExpressions;
using static PatchLibrary.PathUtil;

namespace PathLibraryTest
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestFolderFilesAreRelative()
        {
            using (var tempFolder = new TempFolder())
            {
                var folder = tempFolder.Folder;
                var folder1 = folder.CreateSubdirectory("folder1");
                var folder2 = folder.CreateSubdirectory("folder2");
                var file1 = new FileInfo(Path.Combine(folder1.FullName, "file1.txt"));
                using (file1.Create()) { }
                var file2 = new FileInfo(Path.Combine(folder2.FullName, "file2.txt"));
                using (file2.Create()) { }
                var file3 = new FileInfo(Path.Combine(folder.FullName, "file3.txt"));
                using (file3.Create()) { }

                var expected = new string[]
                {
                    "folder1\\file1.txt",
                    "folder2\\file2.txt",
                    "file3.txt",
                };

                var result = PatchLibrary.PathUtil.FolderFiles(folder).ToArray();
                CollectionAssert.AreEquivalent(expected, result);
            }
        }

        void CreateTestFolderFiles(TempFolder tempFolder1, TempFolder tempFolder2)
        {
            {
                var folder = tempFolder1.Folder;
                var folder1 = folder.CreateSubdirectory("folder1");
                var folder2 = folder.CreateSubdirectory("folder2");
                var file1 = new FileInfo(Path.Combine(folder1.FullName, "file1.txt"));
                using (file1.Create()) { }
                var file2 = new FileInfo(Path.Combine(folder2.FullName, "file2.txt"));
                using (file2.Create()) { }
                var file3 = new FileInfo(Path.Combine(folder.FullName, "file3.txt"));
                using (file3.Create()) { }
                var file3diff = new FileInfo(Path.Combine(folder.FullName, "file3diff.pck"));
                using (var stream = file3diff.Create())
                using (var streamWriter = new StreamWriter(stream))
                {
                    streamWriter.WriteLine("Hello world1");
                }
            }
            {
                var folder = tempFolder2.Folder;
                var folder1 = folder.CreateSubdirectory("folder1");
                var folder2 = folder.CreateSubdirectory("folder2");
                var file1 = new FileInfo(Path.Combine(folder1.FullName, "file1.txt"));
                using (file1.Create()) { }
                var file1new = new FileInfo(Path.Combine(folder1.FullName, "file1new.txt"));
                using (file1new.Create()) { }
                var file3 = new FileInfo(Path.Combine(folder.FullName, "file3.txt"));
                using (file3.Create()) { }
                var file3diff = new FileInfo(Path.Combine(folder.FullName, "file3diff.pck"));
                using (var stream = file3diff.Create())
                using (var streamWriter = new StreamWriter(stream))
                {
                    streamWriter.WriteLine("Hello world2");
                }
            }
        }

        [Test]
        public void TestCompareFolders()
        {
            using (var tempFolder1 = new TempFolder())
            using (var tempFolder2 = new TempFolder())
            {
                CreateTestFolderFiles(tempFolder1, tempFolder2);

                var expected = new FolderDifference[]
                {
                    new FolderDifference("folder2\\file2.txt", EDifference.Removed),
                    new FolderDifference("folder1\\file1new.txt", EDifference.Added),
                    new FolderDifference("file3diff.pck", EDifference.Changed),
                };

                var result = PatchLibrary.PathUtil.CompareFolders(tempFolder1.Folder, tempFolder2.Folder).ToArray();
                CollectionAssert.AreEquivalent(expected, result);
            }
        }

        [Test]
        public void TestCompareFoldersWithIgnorePck()
        {
            using (var tempFolder1 = new TempFolder())
            using (var tempFolder2 = new TempFolder())
            {
                CreateTestFolderFiles(tempFolder1, tempFolder2);

                var expected = new FolderDifference[]
                {
                    new FolderDifference("folder2\\file2.txt", EDifference.Removed),
                    new FolderDifference("folder1\\file1new.txt", EDifference.Added),
                };

                var result = PatchLibrary.PathUtil.CompareFolders(tempFolder1.Folder, tempFolder2.Folder, new Regex(@"\.pck", RegexOptions.IgnoreCase)).ToArray();
                CollectionAssert.AreEquivalent(expected, result);
            }
        }

        [Test]
        public void TestPckFileConvertToChunked()
        {
            string[] expected;
            string[] result;

            using (var pckfile1 = new PckFile(new FileInfo("C:\\Users\\hugol\\Documents\\ExampleGodotLauncher\\patchlibrary\\PathLibraryTest\\export.pck")))
            using (var pckfile2 = new PckFile(new FileInfo("C:\\Users\\hugol\\Documents\\ExampleGodotLauncher\\patchlibrary\\PathLibraryTest\\export1.pck")))
            {
                pckfile1.Open();
                pckfile1.ReadIndex();

                pckfile2.CreateNewChunked(pckfile1.headerData);
                expected = pckfile1.GetFiles().ToArray();
                foreach (var file in pckfile1.GetFiles())
                {
                    var f = 0;
                    var raw = pckfile1.GetFile(file);

                    pckfile1.LoadFile(raw);
                    pckfile2.AddFile(raw);

                }
                // To Test chunk table
                pckfile2.freeChunks.Add(new PckFile.FreeChunks(-1, -2));
                pckfile2.WriteChunkTable();
            }
            using (var pckfile1 = new PckFile(new FileInfo("C:\\Users\\hugol\\Documents\\ExampleGodotLauncher\\patchlibrary\\PathLibraryTest\\export1.pck")))
            {
                pckfile1.Open();
                pckfile1.ReadIndex();

                result = pckfile1.GetFiles().ToArray();

                foreach(var file in result)
                {
                    var raw = pckfile1.GetFile(file);
                    string h = raw.hash;
                    
                    pckfile1.LoadFile(raw);
                    string hh = HashUtil.FileHash(raw.raw.filedata);
                    Assert.AreEqual(h, hh);
                }
                pckfile1.ReadChunkTable();
                Assert.AreEqual(1, pckfile1.freeChunks.Count);
                Assert.AreEqual(-1, pckfile1.freeChunks[0].start);
                Assert.AreEqual(-2, pckfile1.freeChunks[0].length);

            }
            CollectionAssert.AreEquivalent(expected, result);
        }
    }
}