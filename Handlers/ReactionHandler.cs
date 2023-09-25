using Discord;
using Discord.WebSocket;
using DiscordBot.Managers;
using DiscordBot.Structures;
using DiscordBot.Utility;
using DiscordPluginAPI.Helpers;
using DiscordPluginAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiscordBot.Handlers
{
    public enum ReactionType
    {
        Added,
        Removed,
        Cleared,
        RemovedForEmotes
    }
    public enum ReactionSource
    {
        Poll,
        ReactionRole,
        None
    }
    internal class ReactionHandler : UtilityBase
    {
        public ReactionHandler(IServiceProvider serviceProvider, AssemblyManager assemblyManager) : base(serviceProvider, assemblyManager) { }
        public async Task Handle(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, ReactionType type, SocketReaction reaction = null, IEmote emote = null)
        {
            _ = Task.Run(async () =>
            {
                var user = reaction.User.Value;
                if (user.IsBot) return;

                var message = await cache.GetOrDownloadAsync();

                string query = "SELECT plugin_name FROM message_info where message_id = @MessageId";

                QueryParametersBuilder parametersBuilder = new QueryParametersBuilder();
                parametersBuilder.Add("@MessageId", message.Id.ToString());

                var pluginName = await Database.SelectQueryAsync(query,parametersBuilder);
                List<IPluginReactions> plugins = AssemblyManager.Plugins.Get<IPluginReactions>();
                foreach (IPluginReactions plugin in plugins)
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
                            case ReactionType.Cleared:
                                break;
                        }
                        break;
                    }
                }
            });
        }
    }
}
