namespace SyncFolder.Controller
{
    /// <summary>
    /// This Class just makes the main window and its components accessible
    /// as static member for everyone.
    /// </summary>

    class Core
    {
        public static MainWindow win = null;

        public static void SetWindow(MainWindow _win)
        {
            win = _win;
        }
    }
}
