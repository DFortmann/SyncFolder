using SyncFolder.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;

namespace SyncFolder.Controller
{
    class FileUtils
    {
        public static string CopyFile(BackgroundWorker worker, MyPath file, List<MyPath> dstFiles, SyncType syncType)
        {
            string oldPath = file.srcPath;
            string newPath = file.dstPath;

            bool fileExists = false;
            FileInfo newFileInfo = null;

            if (syncType == SyncType.COPY) fileExists = File.Exists(newPath);
            else
            {

                MyPath buff = dstFiles.Find(p => p.srcPathUpper == file.dstPathUpper);
               
                if (buff != null)
                {
                    newFileInfo = buff.fileInfo;
                    fileExists = true;
                }
            }

            if (!fileExists)
            {
                LogCtrl.StatusThreadsafe("Copy: " + oldPath);

                string result = TryAndReturn(worker, () =>
                {
                    File.Copy(oldPath, newPath);
                    File.SetLastWriteTime(newPath, file.fileInfo.LastWriteTime);
                });

                if (result == "DirectoryNotFoundException")
                {
                    result = TryAndReturn(worker, () =>
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                        File.Copy(oldPath, newPath);
                        File.SetLastWriteTime(newPath, file.fileInfo.LastWriteTime);
                    });
                }

                if (result == "UnauthorizedAccessException")
                {
                    result = TryAndReturn(worker, () =>
                    {
                        string directoryName = Path.GetDirectoryName(newPath);
                        DirAccess(new DirectoryInfo(directoryName));
                        Directory.CreateDirectory(directoryName);
                        File.Copy(oldPath, newPath);
                        File.SetLastWriteTime(newPath, file.fileInfo.LastWriteTime);
                    });
                }

                return result == "Success" ? "Copied" : result;
            }
            else
            {
                if(syncType == SyncType.COPY) newFileInfo = new FileInfo(newPath);

                int isNewer = DateTime.Compare(file.fileInfo.LastWriteTime, newFileInfo.LastWriteTime);

                if (isNewer > 0 || (isNewer == 0 && file.fileInfo.Length > newFileInfo.Length))
                {
                    LogCtrl.StatusThreadsafe("Overwrite: " + newPath);

                    string result = TryAndReturn(worker, () =>
                    {
                        File.Delete(newPath);
                        File.Copy(oldPath, newPath);
                        File.SetLastWriteTime(newPath, file.fileInfo.LastWriteTime);
                    });

                    if (result == "UnauthorizedAccessException")
                    {
                        result = TryAndReturn(worker, () =>
                        {
                            FileAccess(file.fileInfo);
                            FileAccess(newFileInfo);
                            File.Delete(newPath);
                            File.Copy(oldPath, newPath);
                            File.SetLastWriteTime(newPath, file.fileInfo.LastWriteTime);
                        });
                    }

                    return result == "Success" ? "Copied" : result;
                }
                else
                {
                    string message = "Exists: " + oldPath;
                    LogCtrl.AppendToLogFile(new LogMessage(LogMessageType.STATUS, message));
                    return "Exists";
                }
            }
        }

        public static string DeleteFileWithAccess(BackgroundWorker worker, FileInfo fileInfo)
        {
            string result = TryAndReturn(worker, () => { File.Delete(fileInfo.FullName); });

            if (result == "UnauthorizedAccessException")
            {
                result = TryAndReturn(worker, () =>
                {
                    FileAccess(fileInfo);
                    File.Delete(fileInfo.FullName);
                });
            }
            return result;
        }

        public static string CreateDirWithAccess(BackgroundWorker worker, string path)
        {
            LogCtrl.AppendToLogFile(new LogMessage(LogMessageType.STATUS, "Creating: " + path));

            string result = TryAndReturn(worker, () => { Directory.CreateDirectory(path); });

            if (result == "UnauthorizedAccessException")
            {
                result = TryAndReturn(worker, () =>
                {
                    DirAccess(new DirectoryInfo(path).Parent);
                    Directory.CreateDirectory(path);
                });
            }
            return result;
        }

        public static string DeleteDirWithAccess(BackgroundWorker worker, DirectoryInfo dirInfo)
        {
            LogCtrl.StatusThreadsafe("Deleting: " + dirInfo.FullName);

            string result = TryAndReturn(worker, () => { Directory.Delete(dirInfo.FullName, true); });

            if (result == "UnauthorizedAccessException")
            {
                result = TryAndReturn(worker, () =>
                {
                    DirAccess(dirInfo.Parent);
                    Directory.Delete(dirInfo.FullName, true);
                });
            }
            return result;
        }

        public static string GetFilesWithAccess(BackgroundWorker worker, DirectoryInfo dirInfo, out FileInfo[] files)
        {
            FileInfo[] buffArr = new FileInfo[] { };

            string result = TryAndReturn(worker, () =>
            {
                buffArr = dirInfo.GetFiles()
                          .Where(f => !f.Attributes.HasFlag(FileAttributes.System)
                          && !f.Attributes.HasFlag(FileAttributes.Hidden)
                          && !f.Attributes.HasFlag(FileAttributes.Temporary)
                          && !f.Name.StartsWith(".")).ToArray();

            });

            if (result == "UnauthorizedAccessException")
            {
                result = TryAndReturn(worker, () =>
                {
                    DirAccess(dirInfo);

                    buffArr = dirInfo.GetFiles()
                          .Where(f => !f.Attributes.HasFlag(FileAttributes.System)
                          && !f.Attributes.HasFlag(FileAttributes.Hidden)
                          && !f.Attributes.HasFlag(FileAttributes.Temporary)
                          && !f.Name.StartsWith(".")).ToArray();
                });
            }

            files = buffArr;
            return result;
        }

        public static string GetDirectoriesWithAccess(BackgroundWorker worker, DirectoryInfo dirInfo, out DirectoryInfo[] directories)
        {
            DirectoryInfo[] buffArr =  new DirectoryInfo[]{};

            string result = TryAndReturn(worker, () =>
            {

                buffArr = dirInfo.GetDirectories().
                          Where(d => !d.Attributes.HasFlag(FileAttributes.System)
                          && !d.Attributes.HasFlag(FileAttributes.Hidden)
                          && !d.Attributes.HasFlag(FileAttributes.Temporary)
                          && !d.Name.StartsWith(".")).ToArray();
            });

            if (result == "UnauthorizedAccessException")
            {
                result = TryAndReturn(worker, () =>
                {
                    DirAccess(dirInfo);

                    buffArr = dirInfo.GetDirectories()
                         .Where(d => !d.Attributes.HasFlag(FileAttributes.System)
                         && !d.Attributes.HasFlag(FileAttributes.Hidden)
                         && !d.Attributes.HasFlag(FileAttributes.Temporary)
                         && !d.Name.StartsWith(".")).ToArray();
                });
            }

            directories = buffArr;
            return result;
        }

        public static void DirAccess(DirectoryInfo dirInfo)
        {
            string message = "Trying to get access rights to " + dirInfo.FullName;
            LogCtrl.AppendToLogFile(new LogMessage(LogMessageType.WARNING, message));

            FileSystemAccessRule fsar = new FileSystemAccessRule("Users", FileSystemRights.FullControl, AccessControlType.Allow);
            DirectorySecurity ds = dirInfo.GetAccessControl();
            ds.AddAccessRule(fsar);
            dirInfo.SetAccessControl(ds);
        }

        public static void FileAccess(FileInfo fileInfo)
        {
            string message = "Trying to get access rights to " + fileInfo.FullName;
            LogCtrl.AppendToLogFile(new LogMessage(LogMessageType.WARNING, message));

            FileSystemAccessRule fsar = new FileSystemAccessRule("Users", FileSystemRights.FullControl, AccessControlType.Allow);
            FileSecurity fs = fileInfo.GetAccessControl();
            fs.AddAccessRule(fsar);
            fileInfo.SetAccessControl(fs);
        }

        private static string TryAndReturn(BackgroundWorker worker, Action action)
        {
            try
            {
                action();
                return "Success";
            }
            catch (DirectoryNotFoundException) { return "DirectoryNotFoundException"; }
            catch (UnauthorizedAccessException) { return "UnauthorizedAccessException"; }
            catch (IOException e)
            {
                if (worker.CancellationPending) return "Success";
                LogCtrl.ErrorThreadsafe(e.Message);
                LogCtrl.WarningThreadsafe("Retrying.");
                Thread.Sleep(30000);
                string response = TryAndReturn(worker, action);
                return response;
            }
            catch (Exception e) { return e.Message; }
        }
    }
}
