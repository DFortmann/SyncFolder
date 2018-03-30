using MahApps.Metro.Controls;
using System.Windows;

namespace SyncFolder
{

    public partial class StatusDialog : MetroWindow
    {

        public StatusDialog()
        {
            InitializeComponent();
            okButton.Click += OkButton_Click;

        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
