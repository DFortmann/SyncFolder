using SyncFolder.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;

namespace SyncFolder.Controller
{
    enum SyncType { COPY = 0, OVERWRITE = 1, SYNC = 2 };

    class CopyCtrl
    {
        public static bool createEmptyFolders       = false;
        public static SyncType syncType             = SyncType.COPY;
        public static volatile bool paused          = false;

        private static long errorCount, filesCopied, filesDeleted, fileCount, filesProcessed;
        private static List<string> errorMessages   = new List<string>();
        private static BackgroundWorker copyWorker  = new BackgroundWorker();     

        public static void Init()
        {
            copyWorker.WorkerSupportsCancellation   = true;
            copyWorker.WorkerReportsProgress        = true;
            copyWorker.DoWork                       += CopyWorker_DoWork;
            copyWorker.RunWorkerCompleted           += CopyWorker_RunWorkerCompleted;
            copyWorker.ProgressChanged              += CopyWorker_ProgressChanged;

            Core.win.startButton.Click  += StartButton_Click;
            Core.win.pauseButton.Click  += PauseButton_Click;
            Core.win.cancelButton.Click += CancelButton_Click;
        }

        private static void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (ScanFolders.IsCompareRunning())
                LogCtrl.Warning("The compare process is running and cannot be canceled.");
            else
            {
                ScanFolders.Stop();

                if (copyWorker.IsBusy)
                {
                    LogCtrl.Warning("Canceling!");
                    Stop();
                    ScanFolders.Stop();
                    paused = false;
                }
            }
        }

        public static void Stop()
        {
            copyWorker.CancelAsync();
            ScanFolders.Stop();
        }

        private static void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (paused)
            {
                paused = false;
                LogCtrl.Warning("Continue...");
            }
            else
            {
                paused = true;
                LogCtrl.Warning("Paused - Press the Pause button again to continue.");
            }
        }

        public static void CheckPause()
        {
            while (paused)
            {
                if (copyWorker.CancellationPending) return;
                Thread.Sleep(100);
            }
        }

        private static void StartButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ScanFolders.IsCompareRunning())
            {
                LogCtrl.Warning("Wait for the compare process to finish.");
                return;
            }

            if(copyWorker.IsBusy)
            {
                LogCtrl.Warning("Wait for the current process to finish or click cancel.");
                return;
            }

            string srcFolder = Properties.Settings.Default.sourceFolder;
            string dstFolder = Properties.Settings.Default.destFolder;

            if (srcFolder == null)
            {
                string title = "No valid source folder.";
                string text  = "Choose a source folder first.";
                bool? result = DialogCtrl.Show(DialogType.WARNING, OptionType.OKCANCEL, title, text);
                return;
            }
            if (dstFolder == null)
            {
                string title = "No valid destination folder.";
                string text  = "Choose a destination folder first.";
                bool? result = DialogCtrl.Show(DialogType.WARNING, OptionType.OKCANCEL, title, text);
                return;
            }
            if (srcFolder == dstFolder)
            {
                string title = "Source and destination folders are the same.";
                string text  = "Choose a different source or destination folder.";
                bool? result = DialogCtrl.Show(DialogType.WARNING, OptionType.OKCANCEL, title, text);
                return;
            }

            Start(srcFolder, dstFolder, Core.win.copyModeMenu.SelectedIndex, Core.win.emptyFoldersCheckBox.IsChecked == true);
        }

        public static void Start(string srcFolder, string dstFolder, int mode, bool createEmptyFolders)
        {
            syncType = (SyncType)mode;

            if (!IsRunning())
            {
                if (syncType == SyncType.OVERWRITE)
                {
                    string title = "Overwrite Folder";
                    string text = "Files and folders that are not in the source folder will be deleted!";
                    bool? result = DialogCtrl.Show(DialogType.WARNING, OptionType.OKCANCEL, title, text);
                    if (result == false) return;
                }

                Core.win.progressBar.IsIndeterminate = true;
                LogCtrl.Clear();

                if (syncType == SyncType.COPY)
                {
                    string message =  "Starting to copy " + srcFolder + " to " + dstFolder + ".";
                    LogCtrl.AppendToLogFile(new LogMessage(LogMessageType.STATUS, message));
                }
                else if (syncType == SyncType.OVERWRITE)
                {
                    string message =  "Starting to overwrite " + dstFolder + " with " + srcFolder + ".";
                    LogCtrl.AppendToLogFile(new LogMessage(LogMessageType.STATUS, message));
                }
                else
                {
                    string message =  "Starting to synchronize " + srcFolder + " and " + dstFolder + ".";
                    LogCtrl.AppendToLogFile(new LogMessage(LogMessageType.STATUS, message));
                }
                LogCtrl.Status("Scanning Files...");
                copyWorker.RunWorkerAsync(new Tuple<string, string, bool, SyncType>(srcFolder, dstFolder, createEmptyFolders, syncType));
            }
            else LogCtrl.Warning("Wait for current process to finish...");
        }

        private static void CopyWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Tuple<string, string, bool, SyncType> args = (Tuple<string, string, bool, SyncType>) e.Argument;

            string sourcePath       = args.Item1;
            string destPath         = args.Item2;
            bool createEmptyFolders = args.Item3;
            SyncType syncType       = args.Item4;

            ScanFolders.ScanSource(sourcePath, destPath);

            if (syncType != SyncType.COPY) ScanFolders.ScanDest(sourcePath, destPath);

            while (true)
            {
                if (copyWorker.CancellationPending) return;

                if (syncType != SyncType.COPY)
                {
                    if (ScanFolders.IsSourceScanDone() && ScanFolders.IsDestScanDone())
                        break;
                }
                else if (ScanFolders.IsSourceScanDone()) break;

                Thread.Sleep(100);
            }

            List<MyPath> sourceFolders  = ScanFolders.sourceFolders;
            List<MyPath> sourceFiles    = ScanFolders.sourceFiles;
            List<MyPath> destFolders    = ScanFolders.destFolders;
            List<MyPath> destFiles      = ScanFolders.destFiles;

            if (sourceFolders == null || sourceFiles == null || destFolders == null || destFiles == null)
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    LogCtrl.Error("Error scanning Files!");
                    Core.win.progressBar.IsIndeterminate = false;
                    Core.win.progressBar.Value = 100;
                }));
                return;
            }

            if (syncType == SyncType.COPY) fileCount = sourceFiles.Count;
            else fileCount = sourceFiles.Count + destFiles.Count;

            filesCopied = 0;
            filesDeleted = 0;
            filesProcessed = 0;
            errorCount = 0;
            errorMessages.Clear();

            LogCtrl.StatusThreadsafe("Found " + fileCount + " files to process.");

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Core.win.progressBar.IsIndeterminate = false;
                Core.win.progressBar.Value = 0;
            }));

            if (createEmptyFolders)
            {
                LogCtrl.StatusThreadsafe("Creating directories.");

                CreateFolders(sourceFolders, destFolders, syncType);

                if (syncType == SyncType.SYNC)
                    CreateFolders(destFolders, sourceFolders, syncType);

                LogCtrl.StatusThreadsafe("Processing files");  
            }

            CopyFiles(sourceFiles, destFiles, syncType);
            if (syncType == SyncType.SYNC) CopyFiles(destFiles, sourceFiles, syncType);

            else if (syncType == SyncType.OVERWRITE)
            {
                foreach (MyPath file in destFiles)
                {
                    CheckPause();
                    if (copyWorker.CancellationPending) return;

                    MyPath found = sourceFiles.Find(p => p.srcPathUpper == file.dstPathUpper);

                    if (found == null)
                    {
                        LogCtrl.StatusThreadsafe("Deleting: " + file.srcPath);

                        string result = FileUtils.DeleteFileWithAccess(copyWorker, file.fileInfo);

                        if (result == "Success") filesDeleted++;
                        else
                        {
                            LogCtrl.ErrorThreadsafe("Failed to delete " + file.srcPath);
                            LogCtrl.ErrorThreadsafe(result);
                            errorMessages.Add(result);
                            errorCount++;
                        }

                        filesProcessed++;
                        copyWorker.ReportProgress((int)(filesProcessed / (float)fileCount * 100));
                    }
                }

                for (int i = destFolders.Count - 1; i >= 0; i--)
                {
                    CheckPause();
                    if (copyWorker.CancellationPending) return;

                    MyPath folder = destFolders[i];
                    MyPath found = sourceFolders.Find(p => p.srcPathUpper == folder.dstPathUpper);

                    if (found == null)
                    {                    
                        string result = FileUtils.DeleteDirWithAccess(copyWorker, folder.dirInfo);

                        if (result != "Success")
                        {
                            LogCtrl.ErrorThreadsafe("Failed to delete " + folder.srcPath);
                            LogCtrl.ErrorThreadsafe(result);
                            errorCount++;
                            errorMessages.Add(result);
                        }
                    }
                }
            }
        }

        private static void CreateFolders(List<MyPath> srcFolders, List<MyPath> dstFolders, SyncType syncType)
        {
            foreach (MyPath folder in srcFolders)
            {
                CheckPause();
                if (copyWorker.CancellationPending) return;

                MyPath found = null;

                if(syncType != SyncType.COPY)
                    found = dstFolders.Find(p => p.srcPathUpper == folder.dstPathUpper);

                if (found == null || (syncType == SyncType.COPY && !Directory.Exists(folder.dstPath)))
                {
                    string result = FileUtils.CreateDirWithAccess(copyWorker, folder.dstPath);

                    if (result != "Success")
                    {
                        LogCtrl.ErrorThreadsafe("Failed to create " + folder.dstPath);
                        LogCtrl.ErrorThreadsafe(result);
                        errorCount++;
                        errorMessages.Add(result);
                    }
                }
            }
        }

        private static void CopyFiles(List<MyPath> srcFiles, List<MyPath> dstFiles, SyncType syncType)
        {
            foreach (MyPath file in srcFiles)
            {
                CheckPause();
                if (copyWorker.CancellationPending) return;

                string result = FileUtils.CopyFile(copyWorker, file, dstFiles, syncType);

                if (result == "Copied") filesCopied++;
                else if (result != "Exists")
                {
                    LogCtrl.ErrorThreadsafe("Failed to copy " + file.srcPath);
                    LogCtrl.ErrorThreadsafe(result);
                    errorMessages.Add(result);
                    errorCount++;
                }

                filesProcessed++;
                copyWorker.ReportProgress((int)(filesProcessed / (float)fileCount * 100));
            }
        }

        public static bool IsRunning()
        {
            return copyWorker.IsBusy;
        }

        private static void CopyWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Core.win.progressBar.Value = e.ProgressPercentage;
        }

        private static void CopyWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Core.win.progressBar.IsIndeterminate    = false;
            Core.win.progressBar.Value              = 100;

            LogCtrl.Success("Done");
            LogCtrl.Success(filesCopied  + " files copied.");
            LogCtrl.Success(filesDeleted + " files deleted.");

            if (errorCount > 0)
            {
                LogCtrl.Error(errorCount + " errors occurred.");
                LogCtrl.Error("Listing all errors: ");

                foreach (string message in errorMessages)
                    LogCtrl.Error(message);
            }

            LogCtrl.SaveLogs();
        }
    }
}
