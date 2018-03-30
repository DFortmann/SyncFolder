using SyncFolder.Model;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows;
using System;
using System.Text;
using System.IO;

namespace SyncFolder.Controller
{
    class LogCtrl
    {
        private static ObservableCollection<LogMessage> logs = new ObservableCollection<LogMessage>();
        private static StringBuilder builder = new StringBuilder();
        private static volatile bool createLog = false;

        public static void Init()
        {
            Core.win.clearLogButton.Click += ClearLogButton_Click;
            Core.win.createLogCheckBox.Checked += CreateLogCheckBox_Checked;
            Core.win.createLogCheckBox.Unchecked += CreateLogCheckBox_Unchecked;
            Core.win.logTable.ItemsSource = logs;
            Core.win.logTable.IsReadOnly = true;
            Core.win.logTable.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;         
        }

        private static void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            logs.Clear();
            builder.Clear();
        }

        public static void Status(string message)
        {
            AddLog(new LogMessage(LogMessageType.STATUS, message));
        }

        public static void StatusThreadsafe(string message)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                AddLog(new LogMessage(LogMessageType.STATUS, message));
            }));
        }

        public static void Success(string message)
        {
            AddLog(new LogMessage(LogMessageType.SUCCESS, message));
        }

        public static void SuccessThreadsafe(string message)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                AddLog(new LogMessage(LogMessageType.SUCCESS, message));
            }));
        }

        public static void Warning(string message)
        {
            AddLog(new LogMessage(LogMessageType.WARNING, message));
        }

        public static void WarningThreadsafe(string message)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                AddLog(new LogMessage(LogMessageType.WARNING, message));
            }));
        }

        public static void Error(string message)
        {
            AddLog(new LogMessage(LogMessageType.ERROR, message));
        }

        public static void ErrorThreadsafe(string message)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                AddLog(new LogMessage(LogMessageType.ERROR, message));
            }));
        }

        private static void AddLog(LogMessage log)
        {
            //if (logs.Count > 100000) logs.RemoveAt(0);                     
            logs.Add(log);
            AppendToLogFile(log);
            Core.win.logTable.ScrollIntoView(log);
        }

        public static void Clear()
        {
            logs.Clear();
            builder.Clear();
        }

        private static void CreateLogCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            createLog = false;
        }

        private static void CreateLogCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            createLog = true;
        }

        public static void AppendToLogFile(LogMessage log)
        {
            if (createLog)
            {
               builder.Append(log.time).Append("\t").Append(log.type).Append("\t");
               builder.Append(log.message).Append(Environment.NewLine);
            }
        }

        public static void SaveLogs()
        {
            if (createLog)
            {
                string path = AppDomain.CurrentDomain.BaseDirectory + "logs.txt";

                using (StreamWriter file = new StreamWriter(path))
                    file.WriteLine(builder.ToString());

                string message = "Saved logs to " + path;
                AddLog(new LogMessage(LogMessageType.STATUS, message));
            }
        }
    }
}
