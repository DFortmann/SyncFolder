using System;
using MahApps.Metro.Controls;
using SyncFolder.Controller;
using System.Collections.Generic;
using System.Security.Principal;

namespace SyncFolder
{
    public partial class MainWindow : MetroWindow
    {
        private List<string> helpText = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            mainWindow.Closing += MainWindow_Closing;

            Core.SetWindow(this);
            LogCtrl.Init();
            ScanFolders.Init();
            CopyCtrl.Init();

            helpButton.Click += HelpButton_Click;

            copyModeMenu.SelectionChanged += CopyModeMenu_SelectionChanged;
            copyModeMenu.SelectedIndex = Properties.Settings.Default.copyMode;
            emptyFoldersCheckBox.Checked += EmptyFoldersCheckBox_Checked;
            emptyFoldersCheckBox.Unchecked += EmptyFoldersCheckBox_Unchecked;
            emptyFoldersCheckBox.IsChecked = Properties.Settings.Default.emptyFoldersCheck;

            CreateHelpText();

            LogCtrl.Status("SyncFolder 1.3 (2018) - DavidFortmann.ch");

            WindowsIdentity  identity  = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                LogCtrl.Warning("Not admin");
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CopyCtrl.IsRunning())
            {
                string title = "Copying is still in progress.";
                string text = "Do you want to cancel the current process?";
                bool? result = DialogCtrl.Show(DialogType.WARNING, OptionType.OKCANCEL, title, text);
                if (result == false)
                {
                    e.Cancel = true;
                    return;
                }
            }
            CopyCtrl.Stop();
        }

        private void CopyModeMenu_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.copyMode = copyModeMenu.SelectedIndex;
            Properties.Settings.Default.Save();
        }

        private void EmptyFoldersCheckBox_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.emptyFoldersCheck = false;
            Properties.Settings.Default.Save();
        }

        private void EmptyFoldersCheckBox_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            Properties.Settings.Default.emptyFoldersCheck = true;
            Properties.Settings.Default.Save();
        }

        private void HelpButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            LogCtrl.Clear();

            foreach (string s in helpText)
                LogCtrl.Status(s);
        }

        private void CreateHelpText()
        {
            helpText.Add("------ SyncFolder Help ------");
            helpText.Add(Environment.NewLine);
            helpText.Add("    This app was created especially for file transfers with network drives (but can be used with any drive).");
            helpText.Add("    It copies only new or changed files and will retry, if the computer looses connection to a network location.");
            helpText.Add(Environment.NewLine);
            helpText.Add("    Choose a source folder by clicking the source box or dragging a folder onto it.");
            helpText.Add("    Choose a destination folder by clicking the destination box or dragging a folder onto it.");
            helpText.Add("    Choose a transfer mode and click the Start button.");
            helpText.Add("        - It is safe to pause or cancel at any time, since a running file process will always be finished first.");
            helpText.Add("        - This also means, that canceling or pausing can take a moment to happen.");
            helpText.Add(Environment.NewLine);
            helpText.Add("    Transfer modes:");
            helpText.Add("        Copy");
            helpText.Add("            - Copies files from the source to the destination folder.");
            helpText.Add("        Overwrite");
            helpText.Add("            - Copies files from the source to the destination folder.");
            helpText.Add("            - Deletes files and folders from the destination folder, which are not present in the source folder.");
            helpText.Add("        Synchronize");
            helpText.Add("            - Copies files from the source to the destination folder.");
            helpText.Add("            - Copies files from the destination to the source folder.");
            helpText.Add(Environment.NewLine);
            helpText.Add("    When the \"Create folders\" option is selected, empty folders are created as well.");
            helpText.Add("        - This can increase the processing time, especially with networks drives.");
            helpText.Add(Environment.NewLine);
            helpText.Add("    Select the \"Create log file\" option to generate a more detailed log in the app folder.");
            helpText.Add(Environment.NewLine);
            helpText.Add("    The compare button starts a robocopy process, that checks, if all the source files are present in the destination directory.");
            helpText.Add("        - This process cannot be canceled and will create a log file in the app folder.");
            helpText.Add("        - If the log file is empty, it means that there are no files missing.");
            helpText.Add(Environment.NewLine);
            helpText.Add("------ SyncFolder Help ------");
        }
    }
}
