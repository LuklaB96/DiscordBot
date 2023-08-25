using Discord.WebSocket;
using DiscordBot.Managers;
using System;
using System.Threading.Tasks;
using PluginTest.Interfaces;
using DiscordBot.Utility;
using PluginTest.Enums;
using System.Collections.Generic;

namespace DiscordBot.Handlers
{
    internal class ComponentHandler : UtilityBase
    {
        public ComponentHandler(IServiceProvider serviceProvider, AssemblyManager assemblyManager) : base(serviceProvider, assemblyManager) { }
        public async Task Handle(SocketMessageComponent component)
        {
            _ = Task.Run(async () =>
            {
                string[] componentData = component.Data.CustomId.Split('_');
                List<IPluginComponents> plugins = assemblyManager.Plugins.Get<IPluginComponents>();
                foreach (IPluginComponents plugin in plugins)
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
