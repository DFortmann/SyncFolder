using System.IO;
using System.Windows.Forms;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using SyncFolder.Model;
using System.Diagnostics;

namespace SyncFolder.Controller
{
    class ScanFolders
    {
        public static List<MyPath> sourceFolders, destFolders, sourceFiles, destFiles;

        private static BackgroundWorker scanSourceFolderWorker, scanDestFolderWorker, compareFolderWorker;
        private static bool restartSourceFolderScan = false;
        private static bool restartDestFolderScan   = false;

        public static void Init()
        {
            sourceFolders   = new List<MyPath>();
            sourceFiles     = new List<MyPath>();
            destFolders     = new List<MyPath>();
            destFiles       = new List<MyPath>();

            Core.win.sourceFolderLabel.DragEnter    += FolderLabel_DragEnter;
            Core.win.sourceFolderLabel.DragOver     += FolderLabel_DragOver;
            Core.win.sourceFolderLabel.Drop         += SourceFolderLabel_Drop;
            Core.win.sourceFolderLabel.MouseDown    += SourceFolderLabel_MouseDown;

            if (Properties.Settings.Default.sourceFolder != null)
                Core.win.sourceFolderLabel.Content = Properties.Settings.Default.sourceFolder;

            Core.win.destFolderLabel.DragEnter  += FolderLabel_DragEnter;
            Core.win.destFolderLabel.DragOver   += FolderLabel_DragOver;
            Core.win.destFolderLabel.Drop       += DestFolderLabel_Drop;
            Core.win.destFolderLabel.MouseDown  += DestFolderLabel_MouseDown;

            if (Properties.Settings.Default.destFolder != null)
                Core.win.destFolderLabel.Content = Properties.Settings.Default.destFolder;

            Core.win.compareButton.Click += CompareButton_Click;

            scanSourceFolderWorker = new BackgroundWorker();
            scanSourceFolderWorker.WorkerSupportsCancellation = true;

            scanDestFolderWorker = new BackgroundWorker();
            scanDestFolderWorker.WorkerSupportsCancellation = true;

            compareFolderWorker = new BackgroundWorker();
            compareFolderWorker.WorkerSupportsCancellation = false;

            scanSourceFolderWorker.DoWork               += ScanSourceFolderWorker_DoWork;
            scanSourceFolderWorker.RunWorkerCompleted   += ScanSourceFolderWorker_RunWorkerCompleted;

            scanDestFolderWorker.DoWork                 += ScanDestFolderWorker_DoWork;
            scanDestFolderWorker.RunWorkerCompleted     += ScanDestFolderWorker_RunWorkerCompleted;

            compareFolderWorker.DoWork                  += CompareFolderWorker_DoWork;
        }

        //This section handles the scanning of the source and destination directories

        public static void Stop()
        {
            restartDestFolderScan = false;
            scanDestFolderWorker.CancelAsync();
            restartSourceFolderScan = false;
            scanSourceFolderWorker.CancelAsync();
        }

        public static void ScanSource(string srcRoot, string dstRoot)
        {
            if (scanSourceFolderWorker.IsBusy)
            {
                restartSourceFolderScan = true;
                scanSourceFolderWorker.CancelAsync();
            }
            else
            {
                //Since ScanSource is always called, reinit files and folders here
                sourceFolders   = new List<MyPath>();
                sourceFiles     = new List<MyPath>();
                destFolders     = new List<MyPath>();
                destFiles       = new List<MyPath>();

                scanSourceFolderWorker.RunWorkerAsync(new Tuple<string, string>(srcRoot, dstRoot));
            }
        }

        public static void ScanDest(string srcRoot, string dstRoot)
        {
            if (scanDestFolderWorker.IsBusy)
            {
                restartDestFolderScan = true;
                scanDestFolderWorker.CancelAsync();
            }
            else scanDestFolderWorker.RunWorkerAsync(new Tuple<string, string>(dstRoot, srcRoot));
        }

        private static void ScanSourceFolderWorker_DoWork(object sender, DoWorkEventArgs e)
        {          
            Tuple<string, string> args  = (Tuple<string, string>) e.Argument;
            string result = StartScan(args.Item1, args.Item2, sourceFolders, sourceFiles, scanSourceFolderWorker);
            if (result == "Success" || result == "Canceled") e.Result = "Success";
            else
            {
                LogCtrl.ErrorThreadsafe(result);
                e.Result = null;
            }
        }

        private static void ScanDestFolderWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Tuple<string, string> args  = (Tuple<string, string>) e.Argument;
            string result = StartScan(args.Item1, args.Item2, destFolders, destFiles, scanDestFolderWorker);
            if (result != "Success" && result != "Canceled") 
            {
                LogCtrl.ErrorThreadsafe(result);
                e.Result = null;
            }
            else e.Result = "Success";
        }

        private static string StartScan(string srcRoot, string dstRoot, List<MyPath> folders, List<MyPath> files, BackgroundWorker worker)
        {
            if (!srcRoot.EndsWith("/") && !srcRoot.EndsWith(@"\")) srcRoot += "/";
            if (!dstRoot.EndsWith("/") && !dstRoot.EndsWith(@"\")) dstRoot += "/";

            FileInfo[] fileArray;
            string result = FileUtils.GetFilesWithAccess(worker, new DirectoryInfo(srcRoot), out fileArray);

            if (result != "Success") return result;

            foreach (FileInfo fi in fileArray)
            {
                CopyCtrl.CheckPause();
                if (worker.CancellationPending) return "Canceled";
                files.Add(new MyPath(srcRoot, dstRoot, fi));
            }

            return DirScan(new DirectoryInfo(srcRoot), srcRoot, dstRoot, folders, files, worker);
        }

        private static string DirScan(DirectoryInfo dirInfo, string srcRoot, string dstRoot, List<MyPath> folders, List<MyPath> files, BackgroundWorker worker)
        {
            DirectoryInfo[] directories;
            string result = FileUtils.GetDirectoriesWithAccess(worker, dirInfo, out directories);

            if (result != "Success") return result;

            foreach (DirectoryInfo di in directories)
            {
                CopyCtrl.CheckPause();
                if (worker.CancellationPending) return "Canceled";

                folders.Add(new MyPath(srcRoot, dstRoot, di));

                FileInfo[] fileArray;
                result = FileUtils.GetFilesWithAccess(worker, di, out fileArray);

                if (result != "Success") return result;

                foreach (FileInfo fi in fileArray)
                {
                    CopyCtrl.CheckPause();
                    if (worker.CancellationPending) return "Canceled";

                    files.Add(new MyPath(srcRoot, dstRoot, fi));
                }
                result = DirScan(di, srcRoot, dstRoot, folders, files, worker);
                if (result != "Success") return result; 
            }
            return "Success";
        }

        private static void ScanSourceFolderWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!e.Cancelled)
            {
                if (e.Result == null)
                {
                    sourceFolders = null;
                    sourceFiles = null;
                }
            }
            else
            {
                sourceFolders = new List<MyPath>();
                sourceFiles = new List<MyPath>();
            }
            if (restartSourceFolderScan)
            {
                restartSourceFolderScan = false;
                scanSourceFolderWorker.RunWorkerAsync(Properties.Settings.Default.sourceFolder);
            }
        }

        private static void ScanDestFolderWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!e.Cancelled)
            {
                if (e.Result == null)
                {
                    destFolders = null;
                    destFiles = null;
                }
            }
            else
            {
                destFolders = new List<MyPath>();
                destFiles = new List<MyPath>();
            }
            if (restartDestFolderScan)
            {
                restartDestFolderScan = false;
                scanDestFolderWorker.RunWorkerAsync(Properties.Settings.Default.destFolder);
            }
        }

        public static bool IsSourceScanDone()
        {
            return !scanSourceFolderWorker.IsBusy;
        }

        public static bool IsDestScanDone()
        {
            return !scanDestFolderWorker.IsBusy;
        }

        // This section handles the comparing of the source and destination directories

        private static void CompareButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (compareFolderWorker.IsBusy || CopyCtrl.IsRunning())
                LogCtrl.Warning("Wait for the current process to finish.");
            else
            {
                string title = "Compare Directories";
                string text = "This process can take a while and cannot be canceled.";
                bool? result = DialogCtrl.Show(DialogType.WARNING, OptionType.OKCANCEL, title, text);
                if (result == true)
                {
                    LogCtrl.Warning("Comparing source and destination folders.");
                    string sourcePath = Properties.Settings.Default.sourceFolder;
                    string destPath = Properties.Settings.Default.destFolder;
                    compareFolderWorker.RunWorkerAsync(new Tuple<string, string>(sourcePath, destPath));
                }
            }
        }

        private static void CompareFolderWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Tuple<string, string> args = (Tuple<string, string> ) e.Argument;
            string sourcePath = args.Item1;
            string destPath = args.Item2;

            if (sourcePath.EndsWith("\\") || sourcePath.EndsWith("/"))
                sourcePath = sourcePath.Substring(0, sourcePath.Length - 1);
            if (destPath.EndsWith("\\") || destPath.EndsWith("/"))
                destPath = destPath.Substring(0, destPath.Length - 1);

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "robocopy.exe";
            
            startInfo.Arguments = "\"" + sourcePath + "\" \"" + destPath + "\" " + "/e /l /ndl /ns /njs /njh /fp /XA:SH  /XF .* /XD \"*System Volume Information\" \"*$RECYCLE.BIN*\" /log:compareLog.txt";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            Process p = Process.Start(startInfo);
            string errors = p.StandardError.ReadToEnd();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            LogCtrl.WarningThreadsafe("Comparing folders is complete.");
            LogCtrl.WarningThreadsafe(output);
        }

        public static bool IsCompareRunning()
        {
            return compareFolderWorker.IsBusy;
        }

        // This section handles the GUI methods to choose a source and destination directory

        private static void SourceFolderLabel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!CopyCtrl.IsRunning())
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    DialogResult result = fbd.ShowDialog();

                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        string paths = fbd.SelectedPath;
                        Core.win.sourceFolderLabel.Content = paths;
                        Properties.Settings.Default.sourceFolder = paths;
                        Properties.Settings.Default.Save();
                    }
                }
            }
        }

        private static void DestFolderLabel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!CopyCtrl.IsRunning())
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    DialogResult result = fbd.ShowDialog();

                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        string paths = fbd.SelectedPath;
                        Core.win.destFolderLabel.Content = paths;
                        Properties.Settings.Default.destFolder = paths;
                        Properties.Settings.Default.Save();
                    }
                }
            }
        }

        private static void SourceFolderLabel_Drop(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;

            if (!CopyCtrl.IsRunning() && CheckForFolder(e))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                Core.win.sourceFolderLabel.Content = paths[0];
                Properties.Settings.Default.sourceFolder = paths[0];
                Properties.Settings.Default.Save();
            }
        }

        private static void DestFolderLabel_Drop(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;

            if (!CopyCtrl.IsRunning() && CheckForFolder(e))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                Core.win.destFolderLabel.Content = paths[0];
                Properties.Settings.Default.destFolder = paths[0];
                Properties.Settings.Default.Save();
            }
        }

        private static void FolderLabel_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;

            if (CheckForFolder(e)) e.Effects = System.Windows.DragDropEffects.Copy;
            else e.Effects = System.Windows.DragDropEffects.None;
        }

        private static void FolderLabel_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Handled = true;

            if (CheckForFolder(e)) e.Effects = System.Windows.DragDropEffects.Copy;
            else e.Effects = System.Windows.DragDropEffects.None;
        }

        private static bool CheckForFolder(System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                if (Directory.Exists(paths[0])) return true;
            }
            return false;
        }
    }
}

