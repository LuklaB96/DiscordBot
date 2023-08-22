using Discord.WebSocket;
using DiscordBot.Managers;
using System;
using System.Threading.Tasks;
using PluginTest.Interfaces;
using DiscordBot.Utility;
using PluginTest.Enums;

namespace DiscordBot.Handlers
{
    internal class ComponentHandler : UtilityBase
    {
        public ComponentHandler(ILogger logger, IDatabase database, AssemblyManager assemblyManager) : base(logger, database, assemblyManager) { }
        public async Task Handle(SocketMessageComponent component)
        {
            _ = Task.Run(async () =>
            {
                string[] componentData = component.Data.CustomId.Split('_');

                foreach (ICommand plugin in assemblyManager.Plugins)
                {
                    if (plugin.Config.pluginName.ToLower() != componentData[0].ToLower())
                        continue;
                    try
                    {
                        await plugin.HandleComponent(component);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Component Handler", $"Received error: {ex.Message}", LogLevel.Error);

                    }
                }
            });
        }
    }
}
