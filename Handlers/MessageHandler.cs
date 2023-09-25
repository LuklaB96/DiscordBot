using Discord;
using DiscordBot.Managers;
using DiscordBot.Utility;
using System;
using System.Threading.Tasks;

namespace DiscordBot.Handlers
{
    public class MessageHandler : UtilityBase
    {
        public MessageHandler(IServiceProvider serviceProvider, AssemblyManager assemblyManager) : base(serviceProvider, assemblyManager) { }

        public async Task HandleMessageDelete(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {

        }
    }
}
