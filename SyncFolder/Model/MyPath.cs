using System.IO;

namespace SyncFolder.Model
{
    class MyPath
    {
        public string srcPath, srcPathUpper, dstPath, dstPathUpper;
        public FileInfo fileInfo;
        public DirectoryInfo dirInfo;

        public MyPath(string srcRoot, string dstRoot, FileInfo fileInfo)
        {
            this.fileInfo = fileInfo;
            srcPath = fileInfo.FullName;
            srcPathUpper = srcPath.ToUpperInvariant();
            dstPath = dstRoot + srcPath.Substring(srcRoot.Length);
            dstPath = dstPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            dstPathUpper = dstPath.ToUpperInvariant();
        }

        public MyPath(string srcRoot, string dstRoot, DirectoryInfo dirInfo)
        {
            this.dirInfo = dirInfo;
            srcPath = dirInfo.FullName;
            srcPathUpper = srcPath.ToUpperInvariant();
            dstPath = dstRoot + srcPath.Substring(srcRoot.Length);
            dstPath = dstPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            dstPathUpper = dstPath.ToUpperInvariant();
        }
    }
}
