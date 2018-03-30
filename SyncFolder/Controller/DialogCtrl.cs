using System;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SyncFolder.Controller
{

    public enum DialogType { QUESTION, WARNING, ERROR }
    public enum OptionType { YESNO, OKCANCEL }

    class DialogCtrl
    {
        private static StatusDialog dialog;

        public static Nullable<bool> Show(DialogType type, OptionType option, string header, string message)
        {
            dialog = new StatusDialog();
            dialog.Owner = Core.win.mainWindow;

            switch (type)
            {
                case DialogType.QUESTION:
                    dialog.iconBox.Child = (XamlReader.Parse(Properties.Resources.question) as Canvas);
                    break;
                case DialogType.WARNING:
                    dialog.iconBox.Child = (XamlReader.Parse(Properties.Resources.warning) as Canvas);
                    break;
                case DialogType.ERROR:
                    dialog.iconBox.Child = (XamlReader.Parse(Properties.Resources.error) as Canvas);
                    break;
            }

            switch (option)
            {
                case OptionType.YESNO:
                    dialog.okButton.Content = "YES";
                    dialog.cancelButton.Content = "NO";
                    break;
                case OptionType.OKCANCEL:
                    dialog.okButton.Content = "OK";
                    dialog.cancelButton.Content = "CANCEL";
                    break;
            }

            dialog.statusWindow.Title = header;
            dialog.textBlock.Text = message;
            return dialog.ShowDialog();
        }
    }
}
