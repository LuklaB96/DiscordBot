using DiscordBot.Structures;

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
