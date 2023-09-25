using DiscordPluginAPI.Enums;
using DiscordPluginAPI.Interfaces;
using System;
using System.Text;

namespace DiscordBot.Utility
{
    internal struct LogBuilder : ILogBuilder
    {
        public LogLevel Level { get; }
        public string Message { get; }

        public string Source { get; }
        public bool TimeStamp { get; }

        public LogBuilder(LogLevel level, string message, string source, bool timeStamp = true)
        {
            Level = level;
            Message = message;
            Source = source;
            TimeStamp = timeStamp;
        }

        public string Log()
        {
            return Log(Level,Message,Source,timeStamp: TimeStamp);
        }
        public string Log(LogLevel level, string message,string source, bool timeStamp = true, DateTimeKind dateTimeKind = DateTimeKind.Local)
        {
            DateTime dateTime = dateTimeKind != DateTimeKind.Local ? DateTime.UtcNow : DateTime.Now;
            string timeFormat = "HH:mm:ss";

            int capacity = 2 + (timeStamp ? 8 : 0) + 2 + (source?.Length ?? 0) + 1 + (message?.Length ?? 0);
            StringBuilder builder = new StringBuilder(capacity);

            if (timeStamp)
            {
                builder.Append('[');
                builder.Append(dateTime.ToString(timeFormat));
                builder.Append(']');
            }

            if(!string.IsNullOrEmpty(source))
            {
                builder.Append('[');
                builder.Append(source);
                builder.Append(']');
            }
            builder.Append(' ');
            if(!string.IsNullOrEmpty(message))
            {
                foreach(char c in message)
                    if(!char.IsControl(c))
                        builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
