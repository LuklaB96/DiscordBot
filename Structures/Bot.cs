using Discord.WebSocket;
using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DiscordBot.Handlers;
using DiscordBot.Managers;
using Microsoft.Extensions.Configuration;
using System.IO;
using DiscordBot.Utility;
using PluginTest.Interfaces;
using PluginTest.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace DiscordBot.Structures
{
    internal class Bot
    {
        private readonly IConfigurationRoot appConfig;
        private string[] args;
        private readonly List<ulong> guilds = new();
        private DiscordSocketClient _client;
        private TaskQueueManager TaskManager;
        private readonly AssemblyManager AssemblyManager;
        private readonly BackupManager BackupManager;
        private readonly Logger Logger;
        private readonly Database Database;
        private readonly IServiceProvider ServiceProvider;
        private readonly string BotName = "BOT";

        public Bot()
        {
            appConfig = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("config.json").Build();
            ServiceProvider = CreateServices();
            Database = ServiceProvider.GetService<Database>();
            Logger = ServiceProvider.GetService<Logger>();
            AssemblyManager = new AssemblyManager(ServiceProvider);
            BackupManager = new BackupManager(10,"backup",ServiceProvider);
        }
        public async Task MainAsync(string[] args = null)
        {
            await Database.Initalize();
            await AssemblyManager.Initalize();
            await BackupManager.Initalize();
            var token = appConfig["bot_token"];
            
            this.args = args;

            var config = ServiceProvider.GetService<DiscordSocketConfig>();
            
            _client = new DiscordSocketClient(config);
            //bot
            _client.Log += Log;
            _client.Ready += Client_Ready;
            _client.JoinedGuild += JoinedGuild;
            //guilds
            _client.GuildAvailable += GuildAvailableEventHandler;
            //components
            _client.ButtonExecuted += ComponentExecuted;
            //commands
            _client.SlashCommandExecuted += SlashCommandHandler;
            _client.UserCommandExecuted += UserCommandExecuted;
            _client.MessageCommandExecuted += MessageCommandExecuted;
            //reactions
            _client.ReactionAdded += HandleReactionAdded;
            _client.ReactionRemoved += HandleReactionRemoved;
            _client.ReactionsCleared += HandleReactionsCleared;
            _client.ReactionsRemovedForEmote += HandleReactionsRemovedForEmote;
            //users
            _client.UserJoined += UserJoined;
            _client.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
            
            //messages
            _client.MessageDeleted += MessageDeleted;
            _client.MessageReceived += MessageReceived;
            _client.MessageUpdated += MessageUpdated;
            _client.MessagesBulkDeleted += MessageBulkDeleted;
            //channels
            _client.ChannelDestroyed += ChannelDestroyed;
            _client.ChannelCreated += ChannelCreated;
            _client.ChannelUpdated += ChannelUpdated;
            //modals
            _client.ModalSubmitted += ModalSubmitted;


            var BotActivityStatus = appConfig["BotActivityStatus"];

            await _client.SetGameAsync(BotActivityStatus);
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);


        }

        private async Task<Task> ModalSubmitted(SocketModal modal)
        {
            AllowedMentions m = new()
            {
                AllowedTypes = AllowedMentionTypes.Users
            };
            await modal.RespondAsync($"test {modal.User.Mention}",allowedMentions: m);
            List<SocketMessageComponentData> components = modal.Data.Components.ToList();
            string v1 = components.First(x => x.CustomId == "test").Value;
            return Task.CompletedTask;
        }
        private static IServiceProvider CreateServices()
        {

            var config = new DiscordSocketConfig
            {
                MessageCacheSize = 999999,
                AlwaysDownloadUsers = true,
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.MessageContent | GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMembers | GatewayIntents.GuildVoiceStates | GatewayIntents.GuildPresences
            };

            Database database = new();
            Logger logger = new();

            var collection = new ServiceCollection()
                .AddSingleton(config)
                .AddSingleton(database)
                .AddSingleton(logger);
            return collection.BuildServiceProvider();
        }
        private async Task<List<SocketGuild>> GetAllGuilds()
        {
            List<SocketGuild> socketGuilds = new();
            foreach(ulong id in guilds)
            {
                socketGuilds.Add(_client.GetGuild(id));
            }
            return socketGuilds;
        }
        
        private async Task<Task> OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {

            List<IPluginUsers> plugins = AssemblyManager.Plugins.Get<IPluginUsers>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.OnUserVoiceStateUpdated(user, oldState, newState);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) continue;
                    Logger.Log(BotName, $"Error in: {data.Name}, message: {ex}", LogLevel.Error);
                }
            }

            return Task.CompletedTask;

        }
        private async Task<Task> ChannelCreated(SocketChannel channel)
        {
            List<IPluginChannels> plugins = AssemblyManager.Plugins.Get<IPluginChannels>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.ChannelCreated(channel);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) continue;

                    Logger.Log(BotName, $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
            return Task.CompletedTask;
        }
        private async Task<Task> ChannelUpdated(SocketChannel channelOld, SocketChannel channelNew)
        {
            List<IPluginChannels> plugins = AssemblyManager.Plugins.Get<IPluginChannels>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.ChannelUpdated(channelOld, channelNew);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) continue;

                    Logger.Log(BotName, $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
            return Task.CompletedTask;
        }
        private async Task<Task> ChannelDestroyed(SocketChannel channel)
        {
            List<IPluginChannels> plugins = AssemblyManager.Plugins.Get<IPluginChannels>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.ChannelDestroyed(channel);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) continue;

                    Logger.Log(BotName, $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
            return Task.CompletedTask;
        }
        private async Task<Task> JoinedGuild(SocketGuild guild)
        {
            await SetupGuildSettings(guild);
            return Task.CompletedTask;
        }
        private async Task<Task> SlashCommandHandler(SocketSlashCommand command)
        {
            
            CommandHandler cmdHandler = new(ServiceProvider, AssemblyManager);
            await cmdHandler.Handle(command);
            return Task.CompletedTask;
        }
        private async Task<Task> GuildAvailableEventHandler(SocketGuild guild)
        {
            guilds.Add(guild.Id);
            return Task.CompletedTask;
        }
        private async Task<Task> UserJoined(SocketGuildUser user)
        {
            List<IPluginUsers> plugins = AssemblyManager.Plugins.Get<IPluginUsers>();
            foreach (IPluginUsers plugin in plugins)
            {
                try
                {
                    await plugin.UserJoinedGuild(user);
                }catch (Exception ex) 
                {
                    if (ex is NotImplementedException) continue;
                    
                    Logger.Log(plugin.Name, "A problem occured in UserJoinedGuild() method: " + ex.Message, LogLevel.Info);
                }
            }
            return Task.CompletedTask;
        }

        private async Task<Task> Client_Ready()
        {
            //-gcbuild as an argument that forces bot to rebuild all global slash commands.
            bool gcbuild = false;
            if (args != null)
                foreach (string arg in args)
                {
                    if (arg == "-gcbuild") 
                    { 
                        gcbuild = true;
                    }
                }

            List<IPlugin> plugins = AssemblyManager.Plugins.GetAllBasePlugins();
            foreach (IPlugin plugin in plugins)
            { 
                if (plugin.Config.GlobalCommandCreated && !gcbuild) continue;
                try
                {
                    if (plugin.commandType == CommandType.Prefix) 
                        continue;

                    foreach (SlashCommandBuilder slashCommandBuilder in plugin.slashCommandBuilder)
                    {
                        if (slashCommandBuilder == null) continue;
                        if (await AssemblyManager.CheckBuildGlobalCommandConflict(plugin.Config.pluginName, slashCommandBuilder.Name) == true)
                        {
                            Logger.Log(plugin.Config.pluginName, $"Could not build global command \"{slashCommandBuilder.Name}\", there is a conflict between plugins and commands in Database.", LogLevel.Error);
                            continue;
                        }

                        await _client.CreateGlobalApplicationCommandAsync(slashCommandBuilder.Build());

                        Logger.Log(plugin.Config.pluginName, $"Global command \"{slashCommandBuilder.Name}\" builded successfully!", LogLevel.Info);
                    }

                    plugin.Config.GlobalCommandCreated = true;
                    plugin.Config.SaveToXml(plugin.Config.assemblyName,Logger);

                }
                catch (Exception ex)
                {
                    Logger.Log(plugin.Config.pluginName, $"Failed to build global command: {ex.Message}", LogLevel.Error);
                }
            }
            //We give all plugins the ability to control all guilds in which the bot works.
            List<SocketGuild> socketGuilds = await GetAllGuilds();
            await AssemblyManager.SetPluginsVariables(_client, socketGuilds);

            TaskManager = new TaskQueueManager(_client, AssemblyManager, ServiceProvider, 1);
            await TaskManager.Start(AutoReset: true);


            //When each plugin has been properly configured by the bot, we can send information that it is ready and the plugin has access to all functions provided by the API.
            foreach (IPlugin plugin in plugins)
            {
                try
                {
                    await plugin.ClientReady();
                }
                catch(Exception ex)
                {
                    //ignore it if plugin throws NotImplementedException
                    if (ex is NotImplementedException) continue;

                    Logger.Log(BotName, $"Error in: {plugin.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }

            int.TryParse(appConfig["BackupInterval"], out int BackupUpdateInterval);
            if (BackupUpdateInterval > 0)
            {
                await BackupManager.Start(BackupUpdateInterval);
            }
            else
                await BackupManager.Start();

            //register all files for backup here
            await BackupManager.RegisterBackupFile("bot.db");

            return Task.CompletedTask;
        }
        private async Task<Task> HandleReactionsRemovedForEmote(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, IEmote emote)
        {
            ReactionHandler rh = new(ServiceProvider, AssemblyManager);
            await rh.Handle(message, channel, ReactionType.RemovedForEmotes, emote: emote);
            return Task.CompletedTask;
        }
        private async Task<Task> HandleReactionsCleared(Cacheable<IUserMessage,ulong> message, Cacheable<IMessageChannel,ulong> channel)
        {
            ReactionHandler rh = new(ServiceProvider, AssemblyManager);
            await rh.Handle(message, channel, ReactionType.Cleared);
            return Task.CompletedTask;
        }
        private async Task<Task> HandleReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            ReactionHandler rh = new(ServiceProvider, AssemblyManager);
            await rh.Handle(message, channel, ReactionType.Added, reaction);
            return Task.CompletedTask;
        }
        private async Task<Task> HandleReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            ReactionHandler rh = new(ServiceProvider, AssemblyManager);
            await rh.Handle(message, channel, ReactionType.Removed, reaction);
            return Task.CompletedTask;
        }
        private async Task<Task> ComponentExecuted(SocketMessageComponent component)
        {
            ComponentHandler componentHandler = new(ServiceProvider, AssemblyManager);
            await componentHandler.Handle(component);
            return Task.CompletedTask;
        }
        private async Task<Task> SetupGuildSettings(SocketGuild guild)
        {
            const string selectGuildSettingsQuery = "SELECT * FROM guildsettings WHERE guild_id = @GuildId";
            const string insertSettingsQuery = "INSERT INTO guildsettings (guild_id,prefix) VALUES (@GuildId,@Prefix)";

            string guildId = guild.Id.ToString();
            string prefix = "!";

            List<KeyValuePair<string, string>> parameters = new()
            {
                new KeyValuePair<string, string>("@GuildId",guildId),
                new KeyValuePair<string, string>("@Prefix",prefix),
            };

            var data = await Database.SelectQueryAsync(selectGuildSettingsQuery, parameters);

            if (data.Count > 0) return Task.CompletedTask;

            await Database.InsertQueryAsync(insertSettingsQuery, parameters);

            return Task.CompletedTask;
        }

        private async Task<Task> MessageReceived(SocketMessage message)
        {
            List<IPluginMessages> plugins = AssemblyManager.Plugins.Get<IPluginMessages>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.MessageReceived(message);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) continue;

                    Logger.Log(BotName, $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
            return Task.CompletedTask;
        }
        private async Task<Task> MessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            List<IPluginMessages> plugins = AssemblyManager.Plugins.Get<IPluginMessages>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.MessageDeleted(message,channel);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) continue;

                    Logger.Log(BotName, $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
            return Task.CompletedTask;
        }
        private async Task<Task> MessageUpdated(Cacheable<IMessage, ulong> messageOld, SocketMessage messageNew, ISocketMessageChannel channel)
        {
            List<IPluginMessages> plugins = AssemblyManager.Plugins.Get<IPluginMessages>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.MessageUpdated(messageOld,messageNew,channel);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) continue;

                    Logger.Log("BOT", $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
            return Task.CompletedTask;
        }
        private async Task<Task> MessageBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages, Cacheable<IMessageChannel, ulong> channel)
        {
            List<IPluginMessages> plugins = AssemblyManager.Plugins.Get<IPluginMessages>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.MessageBulkDeleted(messages,channel);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) continue;

                    Logger.Log("BOT", $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
            return Task.CompletedTask;
        }
        private async Task<Task> UserCommandExecuted(SocketUserCommand command)
        {
            return Task.CompletedTask;
        }
        private async Task<Task> MessageCommandExecuted(SocketMessageCommand command)
        {
            return Task.CompletedTask;
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
