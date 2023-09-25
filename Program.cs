using DiscordBot.Structures;
using System;

namespace DiscordBot
{
    class Program
    {
        static void Main(string[] args)
        {
            new Bot().MainAsync(args).GetAwaiter().GetResult();
        }
    }
}
