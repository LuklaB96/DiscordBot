using Discord;
using DiscordBot.Managers;
using DiscordBot.Utility;
using PluginTest.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Handlers
{
    public class MessageHandler : UtilityBase
    {
        public MessageHandler(ILogger logger, IDatabase database, AssemblyManager assemblyManager) : base(logger, database, assemblyManager) { }

        public async Task HandleMessageDelete(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {

        }
    }
}
