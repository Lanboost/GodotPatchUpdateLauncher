using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PatchLibrary
{
    public class HashUtil
    {
        public static string FileHash(FileInfo file)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = file.OpenRead())
                {
                    return Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }
        }

        public static string FileHash(byte[] data)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = new MemoryStream(data))
                {
                    return Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }
        }

        public static bool SameFile(FileInfo file1, FileInfo file2)
        {
            return FileHash(file1) == FileHash(file2);
        }
    }
}
