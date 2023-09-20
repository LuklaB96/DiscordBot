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
        private Dictionary<Type, List<object>> PluginDictionary { get; set; }
        private List<IPlugin> PluginBase { get; set; }
        Logger Logger { get; set; }
        public PluginRegistry(IServiceProvider serviceProvider) 
        {
            PluginDictionary = new Dictionary<Type, List<object>>();
            PluginBase = new List<IPlugin>();
            Logger = serviceProvider.GetService<Logger>();
        }
        /// <summary>
        /// Registers the plugin in the <see cref="PluginBase"/> List. If the plugin uses an interface other than <see cref="IPlugin"/>, then its special type will be additionally registered in <see cref="PluginDictionary"/>.
        /// <br>This is a very flexible registeration method, where you can register any type of plugin or <see cref="object"/> and then recall it using the <see cref="Get{T}"/> method.</br>
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns><see langword="True"/> or <see langword="False"/></returns>
        public async Task<bool> Register(object plugin)
        {
            if (plugin == null) return false;
            bool registered = false;
            Type[] types = plugin.GetType().GetInterfaces();
            foreach (Type type in types)
            {
                if (type == typeof(IPlugin))
                {
                    PluginBase.Add((IPlugin)plugin);
                    registered = true;
                }
                else if (type != typeof(IPlugin))
                {
                    if (!PluginDictionary.ContainsKey(type))
                    {
                        PluginDictionary[type] = new List<object>();
                    }
                    PluginDictionary[type].Add(plugin);
                    registered = true;
                }
            }

            return registered;
        }
        /// <summary>
        /// Unregister any type of object in <see cref="PluginBase"/> and/or <see cref="PluginDictionary"/>
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns><see langword="True"/> or <see langword="False"/></returns>
        public async Task<bool> Unregister(object plugin)
        {
            if (plugin == null) return false;
            bool unregistered = false;
            Type[] types = plugin.GetType().GetInterfaces();
            foreach (Type type in types)
            {
                if (type == typeof(IPlugin))
                {
                    PluginBase.Remove((IPlugin)plugin);
                    unregistered = true;
                }
                else if (type != typeof(IPlugin) && PluginDictionary.ContainsKey(type))
                {
                    PluginDictionary[type].Remove(plugin);
                    unregistered = true;
                }
            }

            return unregistered;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns><see cref="List{T}"/> of objects of a given type. If you want to get all objects of a base type <see cref="IPlugin"/>, use <see cref="GetAllBasePlugins()."/> </returns>
        public List<T> Get<T>()
        {
            if (PluginDictionary.TryGetValue(typeof(T), out var plugins))
            {
                return plugins.Cast<T>().ToList();
            }
            return new List<T>();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns>Base plugin <see cref="List{T}"/> where T is <see cref="IPlugin"/></returns>
        public List<IPlugin> GetAllBasePlugins()
        {
            return PluginBase;
        }
    }
}
