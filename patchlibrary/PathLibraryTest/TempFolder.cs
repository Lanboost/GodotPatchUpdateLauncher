using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PathLibraryTest
{
    // https://codereview.stackexchange.com/questions/241029/random-temp-folder-implementation-for-unit-testing
    public class TempFolder : IDisposable
    {
        private static readonly Random _Random = new Random();

        public DirectoryInfo Folder { get; }

        public TempFolder(string prefix = "TempFolder")
        {
            string folderName;

            lock (_Random)
            {
                folderName = prefix + _Random.Next(1000000000);
            }

            Folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), folderName));
        }

        public void Dispose()
        {
            Directory.Delete(Folder.FullName, true);
        }
    }
}
