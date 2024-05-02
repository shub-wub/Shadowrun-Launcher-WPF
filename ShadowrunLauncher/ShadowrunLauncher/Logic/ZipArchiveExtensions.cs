using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShadowrunLauncher.Logic
{
    public static class ZipArchiveExtensions
    {
        public static void ExtractToDirectory(string sourceDirectoryName, string destinationDirectoryName, bool overwrite)
        {
            if (!overwrite)
            {
                ZipFile.ExtractToDirectory(sourceDirectoryName, destinationDirectoryName);
                return;
            }

            using (ZipArchive source = ZipFile.Open(sourceDirectoryName, ZipArchiveMode.Read, null))
            {
                foreach (ZipArchiveEntry entry in source.Entries)
                {
                    string fullPath = Path.GetFullPath(Path.Combine(destinationDirectoryName, entry.FullName));

                    if (Path.GetFileName(fullPath).Length != 0)
                    {
                        if (!entry.FullName.Contains("dxvk.conf"))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                            // The boolean parameter determines whether an existing file that has the same name as the destination file should be overwritten
                            entry.ExtractToFile(fullPath, true);
                        }
                    }
                }
            }
        }
    }
}
