using Discord;
using Discord.WebSocket;
using DiscordBot.Managers;
using DiscordBot.Structures;
using DiscordBot.Utility;
using PluginTest.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiscordBot.Handlers
{
    public enum ReactionType
    {
        Added,
        Removed
    }
    public enum ReactionSource
    {
        Poll,
        ReactionRole,
        None
    }
    internal class ReactionHandler : UtilityBase
    {
        public ReactionHandler(ILogger logger, IDatabase database, AssemblyManager assemblyManager) : base(logger, database, assemblyManager) { }
        public async Task Handle(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction, ReactionType type)
        {
            _ = Task.Run(async () =>
            {
                var user = reaction.User.Value;
                if (user.IsBot) return;

                var message = await cache.GetOrDownloadAsync();

                string query = "SELECT plugin_name FROM message_info where message_id = @MessageId";

                List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("@MessageId",message.Id.ToString()),
                };

                var pluginName = await Database.SelectQueryAsync(query,parameters);
                foreach(ICommand plugin in assemblyManager.Plugins)
                {
                    if(plugin.Config.pluginName.ToLower() == pluginName[0].ToLower())
                    {
                        switch (type)
                        {
                            case ReactionType.Added:
                                await plugin.ReactionAdded(message, channel, reaction);
                                break;
                            case ReactionType.Removed:
                                await plugin.ReactionRemoved(message, channel, reaction);
                                break;
                        }
                        break;
                    }
                }
            });
        }
    }
}
