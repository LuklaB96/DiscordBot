using DiscordPluginAPI.Enums;
using DiscordPluginAPI.Interfaces;
using System;

namespace DiscordBot.Utility
{
    public class Logger : ILogger
    {

        public ILogBuilder LogBuilder { get; set; }

        public Logger() 
        { 

        }
        public void Log(string Source, string Message, LogLevel Level, bool timeStamp = true)
        {
            LogBuilder = new LogBuilder(Level,Message,Source,timeStamp);
            Console.WriteLine(LogBuilder.Log());
        }
    }
}
