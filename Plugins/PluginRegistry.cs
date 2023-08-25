using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DiscordBot.Utility;
using Microsoft.Extensions.DependencyInjection;
using PluginTest.Interfaces;

namespace DiscordBot.Plugins
{
    public class PluginRegistry
    {
        private List<IPlugin> PluginBase { get; set; }
        private List<IPluginChannels> PluginChannels { get; set; }
        private List<IPluginReactions> PluginReactions { get; set; }
        private List<IPluginCommands> PluginCommands { get; set; }
        private List<IPluginMessages> PluginMessages { get; set; }
        private List<IPluginComponents> PluginComponents { get; set; }
        Logger Logger { get; set; }
        public PluginRegistry(IServiceProvider serviceProvider) 
        { 
            PluginBase = new List<IPlugin>();
            PluginChannels = new List<IPluginChannels>();
            PluginReactions = new List<IPluginReactions>();
            PluginCommands = new List<IPluginCommands>();
            PluginMessages = new List<IPluginMessages>();
            PluginComponents = new List<IPluginComponents>();
            Logger = serviceProvider.GetService<Logger>();
        }

        public async Task<Task> Register(object plugin)
        {
            if (plugin == null) return Task.CompletedTask;

            Type[] types = plugin.GetType().GetInterfaces();
            foreach (Type type in types)
            {
                if (type == typeof(IPlugin))
                {
                    PluginBase.Add((IPlugin)plugin);
                }
                if (type == typeof(IPluginReactions))
                {
                    PluginReactions.Add((IPluginReactions)plugin);
                }
                if(type == typeof(IPluginChannels))
                {
                    PluginChannels.Add((IPluginChannels)plugin);
                }
                if(type == typeof(IPluginCommands))
                {
                    PluginCommands.Add((IPluginCommands)plugin);
                }
                if (type == typeof(IPluginMessages))
                {
                    PluginMessages.Add((IPluginMessages)plugin);
                }
                if( type == typeof(IPluginComponents))
                {
                    PluginComponents.Add((IPluginComponents)plugin);
                }
            }
            

            return Task.CompletedTask;
        }
        public async Task<Task> Unregister(object plugin)
        {
            if (plugin == null) return Task.CompletedTask;

            Type[] types = plugin.GetType().GetInterfaces();
            foreach (Type type in types)
            {
                if (type == typeof(IPlugin))
                {
                    PluginBase.Remove((IPlugin)plugin);
                }
            }

            return Task.CompletedTask;
        }
        public List<T> Get<T>()
        {
            if (typeof(T) == typeof(IPlugin))
            {
                return PluginBase.ConvertAll(plugin => (T)plugin);
            }
            if(typeof(T) == typeof(IPluginChannels))
            {
                return PluginChannels.ConvertAll(plugin => (T)plugin);
            }
            if (typeof(T) == typeof(IPluginReactions))
            {
                return PluginReactions.ConvertAll(plugin => (T)plugin);
            }
            if(typeof(T) == typeof(IPluginCommands))
            {
                return PluginCommands.ConvertAll(plugin => (T)plugin);
            }
            if (typeof(T) == typeof(IPluginMessages))
            {
                return PluginMessages.ConvertAll(plugin => (T)plugin);
            }
            if(typeof(T) == typeof(IPluginComponents))
            {
                return PluginComponents.ConvertAll(plugin => (T)plugin);
            }
            return new List<T>();
        } 
    }
}
