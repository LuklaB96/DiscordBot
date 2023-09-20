using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DiscordBot.Utility;
using Microsoft.Extensions.DependencyInjection;
using PluginTest.Enums;
using PluginTest.Interfaces;

namespace DiscordBot.Plugins
{
    public class PluginRegistry
    {
        private Dictionary<Type, List<object>> pluginDictionary { get; set; }
        private List<IPlugin> PluginBase { get; set; }
        Logger Logger { get; set; }
        public PluginRegistry(IServiceProvider serviceProvider) 
        {
            pluginDictionary = new Dictionary<Type, List<object>>();
            PluginBase = new List<IPlugin>();
            Logger = serviceProvider.GetService<Logger>();
        }

        /// <summary>
        /// Registers the plugin in the PluginBase List. If the plugin uses an interface other than IPlugin, then its special type will be additionally registered. 
        /// This is a very flexible registration method, where you can register any type of plugin and then recall it using the Get<T> method.
        /// </summary>
        /// <param name="plugin"></param>
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
                else if (type != typeof(IPlugin))
                {
                    if (!pluginDictionary.ContainsKey(type))
                    {
                        pluginDictionary[type] = new List<object>();
                    }
                    pluginDictionary[type].Add(plugin);
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
                else if (type != typeof(IPlugin) && pluginDictionary.ContainsKey(type))
                {
                    pluginDictionary[type].Remove(plugin);
                }
            }

            return Task.CompletedTask;
        }
        public List<T> Get<T>()
        {
            if (pluginDictionary.TryGetValue(typeof(T), out var plugins))
            {
                return plugins.Cast<T>().ToList();
            }
            return new List<T>();
        }
        public List<IPlugin> GetAllBasePlugins()
        {
            return PluginBase;
        }
    }
}
