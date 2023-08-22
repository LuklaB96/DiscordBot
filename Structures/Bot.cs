using Discord.WebSocket;
using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Net;
using DiscordBot.Handlers;
using DiscordBot.Managers;
using Microsoft.Extensions.Configuration;
using System.IO;
using DiscordBot.Utility;
using PluginTest.Interfaces;
using PluginTest.Enums;
using PluginTest;

namespace DiscordBot.Structures
{
    internal class Bot
    {
        private readonly IConfigurationRoot appConfig;
        private string[] args;
        private readonly List<ulong> guilds = new List<ulong>();
        private DiscordSocketClient _client;
        private TaskQueueManager TaskManager;
        private IDatabase Database;
        private AssemblyManager assemblyManager;
        private readonly ILogger Logger;

        public Bot()
        {
            appConfig = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("config.json").Build();
            Logger = new Logger();
            assemblyManager = new AssemblyManager();
            Database = new Database();

        }
        public async Task MainAsync(string[] args = null)
        {
            
            await Database.Initalize();
            await assemblyManager.Initalize();
            var token = appConfig["bot_token"];
            this.args = args;

            var guild = _client?.GetGuild(0);


            //await Giveaway.UpdateList();

            var config = new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true,
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.MessageContent | GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMembers
            };
            
            _client = new DiscordSocketClient(config);

            _client.Log += Log;
            _client.MessageReceived += MessageReceived;
            _client.Ready += Client_Ready;
            _client.SlashCommandExecuted += SlashCommandHandler;
            _client.ButtonExecuted += ButtonHandler;
            _client.GuildAvailable += GuildAvailableEventHandler;
            _client.ReactionAdded += HandleReactionAdded;
            _client.ReactionRemoved += HandleReactionRemoved;
            _client.UserJoined += UserJoined;
            _client.JoinedGuild += JoinedGuild;

            await _client.SetGameAsync("Gram w gre");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);


        }
        private async Task JoinedGuild(SocketGuild guild)
        {
            await setupGuildSettings(guild);
        }
        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            CommandHandler cmdHandler = new CommandHandler(Logger, Database, assemblyManager);
            await cmdHandler.Handle(command);
        }
        private async Task GuildAvailableEventHandler(SocketGuild guild)
        {
            guilds.Add(guild.Id);
        }
        private async Task UserJoined(SocketGuildUser user)
        {
            foreach(ICommand plugin in assemblyManager.Plugins)
            {
                try
                {
                    await plugin.UserJoinedGuild(user);
                }catch (Exception ex) 
                { 
                    if(ex is NotImplementedException) { }
                    else { Logger.Log(plugin.Name,"A problem occured in UserJoinedGuild() method: " + ex.Message,LogLevel.Info); }
                }
            }
        }

        private async Task Client_Ready()
        {
            bool gcbuild = false;
            if (args != null)
                foreach (string arg in args)
                {
                    if (arg == "-gcbuild") { gcbuild = true; }
                }
        
            foreach (ICommand plugin in assemblyManager.Plugins)
            { 
                if (plugin.Config.GlobalCommandCreated && !gcbuild) continue;
                try
                {
                    if (plugin.commandType == CommandType.Prefix) 
                        continue;

                    foreach (SlashCommandBuilder slashCommandBuilder in plugin.slashCommandBuilder)
                    {
                        if (slashCommandBuilder == null) continue;
                        if (await assemblyManager.CheckBuildGlobalCommandConflict(plugin.Config.pluginName, slashCommandBuilder.Name) == true)
                        {
                            Logger.Log(plugin.Config.pluginName, $"Could not build global command \"{slashCommandBuilder.Name}\", there is a conflict between plugins and commands in Database.", LogLevel.Error);
                            continue;
                        }

                        await _client.CreateGlobalApplicationCommandAsync(slashCommandBuilder.Build());

                        Logger.Log(plugin.Config.pluginName, $"Global command \"{slashCommandBuilder.Name}\" builded successfully!", LogLevel.Info);
                    }

                    plugin.Config.GlobalCommandCreated = true;
                    plugin.Config.SaveToXml(plugin.Config.assemblyName);

                }
                catch (Exception ex)
                {
                    Logger.Log(plugin.Config.pluginName, $"Failed to build global command: {ex.Message}", LogLevel.Error);
                }
            }

            TaskManager = new TaskQueueManager(_client, assemblyManager, 1);
            TaskManager.Start(AutoReset: true);
        }

        private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            ReactionHandler rh = new ReactionHandler(Logger, Database, assemblyManager);
            await rh.Handle(cache, channel, reaction, ReactionType.Added);
        }
        private async Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            ReactionHandler rh = new ReactionHandler(Logger, Database, assemblyManager);
            await rh.Handle(cache, channel, reaction, ReactionType.Removed);
        }
        private async Task ButtonHandler(SocketMessageComponent component)
        {
            ComponentHandler componentHandler = new ComponentHandler(Logger, Database, assemblyManager);
            await componentHandler.Handle(component);
        }
        private async Task setupGuildSettings(SocketGuild guild)
        {
            const string selectGuildSettingsQuery = "SELECT * FROM guildsettings WHERE guild_id = @GuildId";
            const string insertSettingsQuery = "INSERT INTO guildsettings (guild_id,prefix) VALUES (@GuildId,@Prefix)";

            string guildId = guild.Id.ToString();
            string prefix = "!";

            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@GuildId",guildId),
                new KeyValuePair<string, string>("@Prefix",prefix),
            };

            var data = await Database.SelectQueryAsync(selectGuildSettingsQuery, parameters);

            if (data.Count > 0) return;

            await Database.InsertQueryAsync(insertSettingsQuery, parameters);

        }

        private async Task MessageReceived(SocketMessage message)
        {
            CommandHandler cmdHandler = new CommandHandler(Logger, Database, assemblyManager);
            await cmdHandler.Handle(message,appConfig);
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
