using DiscordBot.Utility;
using DiscordPluginAPI.Enums;
using DiscordPluginAPI.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace DiscordBot.Configuration
{
    internal class ConfigProvider
    {
        private IConfigurationRoot _appConfig;
        private readonly ILogger Logger;
        private const string ModuleName = "ConfigBuilder";
        public ConfigProvider(string path, string file, IServiceProvider serviceProvider)
        {
            Logger = serviceProvider.GetService<Logger>();
            if(File.Exists($"{path}/{file}"))
            {
                try
                {
                    _appConfig = new ConfigurationBuilder().SetBasePath(path).AddJsonFile(file).Build();
                    Logger.Log(ModuleName, $"Main application config {file} loaded from {path}", LogLevel.Info);
                }
                catch(Exception ex)
                {
                    Logger.Log(ModuleName, $"An error occured while building configuration {file} in {path}, {ex.Message}", LogLevel.Error);
                }
            }
            else
            {
                CreateBaseConfig(path,file);
            }
        }
        public IConfigurationRoot get()
        { 
            return _appConfig; 
        }
        private void CreateBaseConfig(string path,string file)
        {
            try
            {
                string baseConfigString = "{\r\n  \"bot_token\": \"YOUR_BOT_TOKEN\",\r\n  \"BackupInterval\": 1200,\r\n  \"BotActivityStatus\": \"Send help\",\r\n}";
                File.WriteAllText($"{path}/{file}", baseConfigString);
                _appConfig = new ConfigurationBuilder().SetBasePath(path).AddJsonFile(file).Build();
                Logger.Log(ModuleName, $"Successfully created base {file} in: {path}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Log(ModuleName,$"Could not create base config: {ex}",LogLevel.Error);
            }
        }
    }
}
