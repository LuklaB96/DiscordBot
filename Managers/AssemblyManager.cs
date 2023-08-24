using DiscordBot.Structures;
using DiscordBot.Utility;
using PluginTest.Interfaces;
using PluginTest.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using DiscordBot.Enums;
using PluginTest;
using DiscordBot.AssemblyHelpers;
using Discord;
using Discord.WebSocket;
using DiscordBot.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Managers
{
    public class AssemblyManager
    {
        private const string path = "Plugins/";
        private Logger Logger { get; set; }
        private Database Database { get; set; }
        private IServiceProvider ServiceProvider { get; set; }
        

        public PluginRegistry Plugins { get; set; }
        public AssemblyManager(IServiceProvider serviceProvider)
        {
            Logger = serviceProvider.GetService<Logger>();
            Database = serviceProvider.GetService<Database>();
            Plugins = new PluginRegistry(serviceProvider);
            this.ServiceProvider = serviceProvider;
        }
        /// <summary>
        /// Loads all plugins to a List of ICommand objects for later use
        /// </summary>
        public async Task Initalize() 
        { 
            await LoadAllPlugins(path);
        }
        /// <summary>
        /// Loads all plugins from the specified <paramref name="path"/>. It checks if the plugin has plugin_config.xml and creates or loads it as needed.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>Returns <see cref="List{T}"/> containing all active <see cref="ICommand"/> plugins</returns>
        public async Task<Task> LoadAllPlugins(string path)
        {
            /*
             * TO DO:
             * Clean up this mess
             * 
             */
            Logger.Log("Plugin Manager", "Loading plugins...", LogLevel.Info);

            AssemblyLoader assemblyLoader = new AssemblyLoader(path,ServiceProvider);
            assemblyLoader.Load();

            foreach (AssemblyData data in assemblyLoader)
            {
                IPlugin plugin = (IPlugin)data.Plugin;
                await plugin.Initalize(Database, Logger);

                if (string.IsNullOrEmpty(plugin.Name))
                    plugin.Name = data.AssemblyName;


                string assemblyVersion = data.AssemblyVersion;
                var pluginNameStatus = await CheckPluginName(plugin.Name, data.AssemblyName);

                foreach (KeyValuePair<string, string> TableProperties in plugin.DatabaseTableProperties)
                {
                    await Database.CheckCreatePluginTable(plugin.Name, TableProperties.Key, TableProperties.Value);
                }

                switch (pluginNameStatus)
                {
                    case AssemblyDatabaseStatus.NOT_EXISTS:
                        await SaveAssemblyInfoToDatabase(data.AssemblyName, plugin.Name);
                        break;
                    case AssemblyDatabaseStatus.MISMATCH:
                        await UpdateAssemblyInfoInDatabase(data.AssemblyName, plugin.Name);
                        break;
                    case AssemblyDatabaseStatus.OK:
                        break;
                }

                plugin.Config = new Config(path, false, plugin.Name, data.AssemblyName, "_config", logger: Logger);
                plugin.Config.version = assemblyVersion;
                var loadedConfig = plugin.Config.LoadXml(data.AssemblyName);

                if (loadedConfig != null)
                {
                    plugin.Config = loadedConfig;
                    if (!VerifyPluginVersion(plugin.Config.version, assemblyVersion))
                    {
                        Logger.Log("Plugin Manager", $"{plugin.Name} version mismatch, updating config file.", LogLevel.Info);
                        plugin.Config.GlobalCommandCreated = false;
                        plugin.Config.version = assemblyVersion;
                    }
                }
                else
                    plugin.Config.SaveToXml(data.AssemblyName);
                if (!plugin.Config.GlobalCommandCreated)
                {
                    foreach (SlashCommandBuilder slashCommandBuilder in plugin.slashCommandBuilder)
                    {
                        if (slashCommandBuilder == null) 
                            continue;

                        await SaveCommandInfoToDatabase(slashCommandBuilder.Name, plugin.Name, data.AssemblyName);
                    }
                }

                await Plugins.Register(plugin);

                Logger.Log("Plugin Manager", "Loaded Plugin: " + plugin.Name + ", version: " + plugin.Config.version, LogLevel.Info);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Compares assembly version and value from config file.
        /// </summary>
        /// <param name="configVersion"></param>
        /// <param name="assemblyVersion"></param>
        /// <returns><see langword="true"/> if <see cref="Config.version"/> and <see cref="Assembly"/> version is matching, otherwise <see langword="false"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private bool VerifyPluginVersion(string configVersion, string assemblyVersion)
        {
            if (configVersion == null || assemblyVersion == null)
                throw new ArgumentNullException(); //this should never be a thing if plugin is loaded correctly
            if (configVersion == assemblyVersion) { return true; }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pluginName"></param>
        /// <returns>Predefined plugin name or assembly.dll name</returns>
        private async Task<AssemblyDatabaseStatus> CheckPluginName(string pluginName, string assemblyName)
        {
            string query = "SELECT assembly_name,plugin_name FROM plugin_properties WHERE assembly_name = @AssemblyName";

            if (string.IsNullOrEmpty(pluginName)) return AssemblyDatabaseStatus.NOT_EXISTS;

            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@AssemblyName",assemblyName)
            };

            var assemblyData = await Database.SelectQueryAsync(query,parameters);

            if (assemblyData == null || assemblyData.Count == 0) return AssemblyDatabaseStatus.NOT_EXISTS;
            if (assemblyData.Contains(pluginName)) return AssemblyDatabaseStatus.OK;
            else return AssemblyDatabaseStatus.MISMATCH;
        }
        public async Task FeedPluginWithGuilds(List<SocketGuild> guilds)
        {
            List<IPlugin> plugins = Plugins.Get<IPlugin>();
            foreach(IPlugin plugin in plugins)
            {
                plugin.guilds = guilds;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <param name="pluginName"></param>
        /// <param name="pluginAlias"></param>
        /// <returns></returns>
        private async Task SaveAssemblyInfoToDatabase(string assemblyName, string pluginName, string pluginAlias = null)
        {
            string query = "INSERT INTO plugin_properties (assembly_name,plugin_name,plugin_alias) VALUES (@AssemblyName,@PluginName,@PluginAlias)";

            if (pluginAlias == null) pluginAlias = string.Empty;

            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@AssemblyName",assemblyName),
                new KeyValuePair<string, string>("@PluginName",pluginName),
                new KeyValuePair<string, string>("@PluginAlias",pluginAlias)
            };

            var result = await Database.InsertQueryAsync(query,parameters);
            Logger.Log("Plugin Manager",$"Saved info for {pluginName} to database: {(result == 0 ? "No" : "yes")}",LogLevel.Info);
        }
        private async Task SaveCommandInfoToDatabase(string commandName,string pluginName, string assemblyName)
        {
            string query = "INSERT INTO command_info (command_name,plugin_name,assembly_name) VALUES (@CommandName,@PluginName,@AssemblyName)";

            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@CommandName",commandName),
                new KeyValuePair<string, string>("@PluginName",pluginName),
                new KeyValuePair<string, string>("@AssemblyName",assemblyName)
            };

            var result = await Database.InsertQueryAsync(query, parameters);
        }
        private async Task UpdateAssemblyInfoInDatabase(string assemblyName, string pluginName, string pluginAlias = null)
        {
            string query = "UPDATE plugin_properties SET plugin_name = @PluginName WHERE assembly_name = @AssemblyName";

            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@AssemblyName",assemblyName),
                new KeyValuePair<string, string>("@PluginName",pluginName),
                new KeyValuePair<string, string>("@PluginAlias",pluginAlias)
            };

            await Database.UpdateQueryAsync(query, parameters);
        }
        /// <summary>
        /// Check if global command is claimed by other source.
        /// </summary>
        /// <param name="commandName">Insert SlashCommandBuilder command name</param>
        /// <returns><see langword="true"/> if there is a conflict between plugin commands, or <see langword="false"/> if command is free to claim.</returns>
        public async Task<bool> CheckBuildGlobalCommandConflict(string pluginName, string commandName)
        {
            if (string.IsNullOrEmpty(commandName))
                return true;
            string query = "SELECT plugin_name FROM command_info WHERE command_name = @CommandName";

            List<KeyValuePair<string,string>> parameters = new List<KeyValuePair<string, string>> 
            { 
                new KeyValuePair<string, string>("@CommandName",commandName) 
            };

            var data = await Database.SelectQueryAsync(query, parameters);

            if (data == null || data.Count == 0) return false;

            if (data[0] == pluginName) return false;

            return true;

        }
    }
}
