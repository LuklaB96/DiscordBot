using DiscordBot.Structures;
using DiscordBot.Utility;
using DiscordPluginAPI.Interfaces;
using DiscordPluginAPI.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using DiscordBot.Enums;
using DiscordPluginAPI;
using DiscordBot.AssemblyHelpers;
using Discord;
using Discord.WebSocket;
using DiscordBot.Plugins;
using Microsoft.Extensions.DependencyInjection;
using DiscordPluginAPI.Helpers;

namespace DiscordBot.Managers
{
    public class AssemblyManager
    {
        private const string PATH = "Plugins/";
        private Logger Logger { get; set; }
        private Database Database { get; set; }
        private IServiceProvider ServiceProvider { get; set; }
        

        public PluginRegistry Plugins { get; set; }
        public AssemblyManager(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Logger = serviceProvider.GetService<Logger>();
            Database = serviceProvider.GetService<Database>();
            Plugins = new PluginRegistry(serviceProvider);
        }
        /// <summary>
        /// Loads all plugins to a List of ICommand objects for later use
        /// </summary>
        public async Task Initalize() 
        { 
            await LoadAllPlugins(PATH);
        }
        /// <summary>
        /// Loads all plugins from the specified <paramref name="PATH"/>. It checks if the plugin has plugin_config.xml and creates or loads it as needed.
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
                try
                {
                    foreach (KeyValuePair<string, string> TableProperties in plugin.DatabaseTableProperties)
                    {
                        await Database.CheckCreatePluginTable(plugin.Name, TableProperties.Key, TableProperties.Value);
                    }
                }
                catch(Exception ex)
                {
                    if (ex is NotImplementedException) { }
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
                Config cfg = new Config(path, false, false, plugin.Name, data.AssemblyName, Logger, "_config");
                plugin.Config = cfg;
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
                    plugin.Config.SaveToXml(data.AssemblyName,Logger);
                if (!plugin.Config.GlobalCommandCreated)
                {
                    try
                    {
                        foreach (SlashCommandBuilder slashCommandBuilder in plugin.slashCommandBuilder)
                        {
                            if (slashCommandBuilder == null)
                                continue;

                            await SaveCommandInfoToDatabase(slashCommandBuilder.Name, plugin.Name, data.AssemblyName);
                        }
                    }
                    catch(Exception ex)
                    {
                        if(ex is NullReferenceException || ex is NotImplementedException) { }
                    }
                }
                if (!plugin.Config.ModalsCreated)
                {
                    IPluginModals p = plugin as IPluginModals;
                    try
                    {
                        foreach (ModalBuilder modalBuilder in p.ModalBuilders)
                        {
                            if (modalBuilder == null)
                                continue;

                            await SaveModalInfoToDatabase(modalBuilder.CustomId, plugin.Name, data.AssemblyName);
                            plugin.Config.SaveToXml(data.AssemblyName, Logger);
                            plugin.Config.ModalsCreated = true;
                        }
                    }
                    catch (Exception e)
                    {

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
        /// Checks if assemblyName and pluginName param is equal to the one in database, if not, it returns the appropriate <see cref="AssemblyDatabaseStatus"/> enum type .
        /// </summary>
        /// <param name="pluginName"></param>
        /// <returns>Predefined plugin name or assembly.dll name</returns>
        private async Task<AssemblyDatabaseStatus> CheckPluginName(string pluginName, string assemblyName)
        {
            string query = "SELECT assembly_name,plugin_name FROM plugin_properties WHERE assembly_name = @AssemblyName";

            if (string.IsNullOrEmpty(pluginName)) return AssemblyDatabaseStatus.NOT_EXISTS;

            QueryParametersBuilder parametersBuilder = new QueryParametersBuilder();
            parametersBuilder.Add("@AssemblyName", assemblyName);

            var assemblyData = await Database.SelectQueryAsync(query,parametersBuilder);

            if (assemblyData == null || assemblyData.Count == 0) return AssemblyDatabaseStatus.NOT_EXISTS;
            if (assemblyData.Contains(pluginName)) return AssemblyDatabaseStatus.OK;
            else return AssemblyDatabaseStatus.MISMATCH;
        }
        public async Task SetPluginsVariables(DiscordSocketClient client, List<SocketGuild> guilds)
        {
            List<IPlugin> plugins = Plugins.GetAllBasePlugins();
            foreach (IPlugin plugin in plugins)
            {
                plugin.guilds = guilds;
                plugin.Client = client;
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

            pluginAlias ??= string.Empty;
            QueryParametersBuilder parametersBuilder = new QueryParametersBuilder();
            parametersBuilder.Add("@AssemblyName", assemblyName);
            parametersBuilder.Add("@PluginName", pluginName);
            parametersBuilder.Add("@PluginAlias", pluginAlias);

            var result = await Database.InsertQueryAsync(query,parametersBuilder);
            Logger.Log("Plugin Manager",$"Saved info for {pluginName} to database: {(result == 0 ? "No" : "yes")}",LogLevel.Info);
        }
        private async Task SaveCommandInfoToDatabase(string commandName,string pluginName, string assemblyName)
        {
            string query = "INSERT INTO command_info (command_name,plugin_name,assembly_name) VALUES (@CommandName,@PluginName,@AssemblyName)";

            QueryParametersBuilder parametersBuilder = new QueryParametersBuilder();
            parametersBuilder.Add("@AssemblyName", assemblyName);
            parametersBuilder.Add("@PluginName", pluginName);
            parametersBuilder.Add("@CommandName", commandName);
            await Database.InsertQueryAsync(query, parametersBuilder);
        }
        private async Task SaveModalInfoToDatabase(string modalName, string pluginName, string assemblyName)
        {
            string query = "INSERT INTO modal_info (modal_name,plugin_name,assembly_name) VALUES (@ModalName,@PluginName,@AssemblyName)";

            QueryParametersBuilder parametersBuilder = new QueryParametersBuilder();
            parametersBuilder.Add("@AssemblyName", assemblyName);
            parametersBuilder.Add("@PluginName", pluginName);
            parametersBuilder.Add("@ModalName", modalName);

            await Database.InsertQueryAsync(query, parametersBuilder);
        }
        private async Task UpdateAssemblyInfoInDatabase(string assemblyName, string pluginName, string pluginAlias = null)
        {
            string query = "UPDATE plugin_properties SET plugin_name = @PluginName WHERE assembly_name = @AssemblyName";

            QueryParametersBuilder parametersBuilder = new QueryParametersBuilder();
            parametersBuilder.Add("@AssemblyName", assemblyName);
            parametersBuilder.Add("@PluginName", pluginName);
            parametersBuilder.Add("@PluginAlias", pluginAlias);

            await Database.UpdateQueryAsync(query, parametersBuilder);
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

            QueryParametersBuilder parametersBuilder = new QueryParametersBuilder();
            parametersBuilder.Add("@CommandName", commandName);

            var data = await Database.SelectQueryAsync(query, parametersBuilder);

            if (data == null || data.Count == 0) return false;

            if (data[0] == pluginName) return false;

            return true;

        }
    }
}
