using System;
using System.Windows.Media;

namespace SyncFolder.Model
{
    public enum LogMessageType { STATUS, SUCCESS, WARNING, ERROR }

    class LogMessage
    {
        public string time { get; }
        public string message { get; }      
        public Brush bgColor { get; }
        public LogMessageType type;

        public LogMessage(LogMessageType type, string message)
        {
            this.type = type;
            this.message = message;
            time = DateTime.Now.ToString("hh:mm:ss.fff");

            switch (type)
            {
                case LogMessageType.SUCCESS:
                    bgColor = new SolidColorBrush(Colors.Green);
                    break;

                case LogMessageType.WARNING:
                    bgColor = new SolidColorBrush(Colors.Orange);
                    break;

                case LogMessageType.ERROR:
                    bgColor = new SolidColorBrush(Colors.Red);
                    break;

                default:
                    bgColor = new SolidColorBrush(Colors.Transparent);
                    break;
            }
        }

    }
}
