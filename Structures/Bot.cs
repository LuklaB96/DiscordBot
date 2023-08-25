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
        private AssemblyManager assemblyManager;
        private readonly Logger Logger;
        private Database Database;
        private readonly IServiceProvider ServiceProvider;

        public Bot()
        {
            appConfig = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("config.json").Build();
            ServiceProvider = CreateServices();
            Database = ServiceProvider.GetService<Database>();
            Logger = ServiceProvider.GetService<Logger>();
            assemblyManager = new AssemblyManager(ServiceProvider);
        }
        public async Task MainAsync(string[] args = null)
        {
            await Database.Initalize();
            await assemblyManager.Initalize();
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

            await _client.SetGameAsync("Gram w gre");
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);


        }

        private async Task<Task> ModalSubmitted(SocketModal modal)
        {
            AllowedMentions m = new AllowedMentions();
            m.AllowedTypes = AllowedMentionTypes.Users;
            await modal.RespondAsync($"test {modal.User.Mention}",allowedMentions: m);
            List<SocketMessageComponentData> components = modal.Data.Components.ToList();
            string v1 = components.First(x => x.CustomId == "test").Value;
            return Task.CompletedTask;
        }
        private IServiceProvider CreateServices()
        {

            var config = new DiscordSocketConfig
            {
                MessageCacheSize = 100,
                AlwaysDownloadUsers = true,
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.MessageContent | GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMembers | GatewayIntents.GuildVoiceStates | GatewayIntents.GuildPresences
            };

            Database database = new Database();
            Logger logger = new Logger();

            var collection = new ServiceCollection()
                .AddSingleton(config)
                .AddSingleton(database)
                .AddSingleton(logger);
            return collection.BuildServiceProvider();
        }
        private async Task<List<SocketGuild>> GetAllGuilds()
        {
            List<SocketGuild> socketGuilds = new List<SocketGuild>();
            foreach(ulong id in guilds)
            {
                socketGuilds.Add(_client.GetGuild(id));
            }
            return socketGuilds;
        }
        
        private async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            List<IPlugin> plugins = assemblyManager.Plugins.Get<IPlugin>();
            foreach (var data in plugins)
            {
                await data.OnUserVoiceStateUpdated(user, oldState, newState);
            }
            
        }
        private async Task ChannelCreated(SocketChannel channel)
        {
            List<IPluginChannels> plugins = assemblyManager.Plugins.Get<IPluginChannels>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.ChannelCreated(channel);
                }
                catch (Exception ex)
                {
                    if(ex is NotImplementedException) { }
                    else
                        Logger.Log("BOT", $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
        }
        private async Task ChannelUpdated(SocketChannel channelOld, SocketChannel channelNew)
        {
            List<IPluginChannels> plugins = assemblyManager.Plugins.Get<IPluginChannels>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.ChannelUpdated(channelOld, channelNew);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) { }
                    else
                        Logger.Log("BOT", $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
        }
        private async Task ChannelDestroyed(SocketChannel channel)
        {
            List<IPluginChannels> plugins = assemblyManager.Plugins.Get<IPluginChannels>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.ChannelDestroyed(channel);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) { }
                    else
                        Logger.Log("BOT", $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
        }
        private async Task JoinedGuild(SocketGuild guild)
        {
            await setupGuildSettings(guild);
        }
        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            
            CommandHandler cmdHandler = new CommandHandler(ServiceProvider, assemblyManager);
            await cmdHandler.Handle(command);
        }
        private async Task GuildAvailableEventHandler(SocketGuild guild)
        {
            guilds.Add(guild.Id);
        }
        private async Task UserJoined(SocketGuildUser user)
        {
            List<IPlugin> plugins = assemblyManager.Plugins.Get<IPlugin>();
            foreach (IPlugin plugin in plugins)
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
            List<IPlugin> plugins = assemblyManager.Plugins.Get<IPlugin>();
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
                        if (await assemblyManager.CheckBuildGlobalCommandConflict(plugin.Config.pluginName, slashCommandBuilder.Name) == true)
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
            List<SocketGuild> socketGuilds = await GetAllGuilds();
            await assemblyManager.FeedPluginWithGuilds(socketGuilds);

            TaskManager = new TaskQueueManager(_client, assemblyManager, ServiceProvider, 1);
            await TaskManager.Start(AutoReset: true);

            

            foreach (IPlugin plugin in plugins)
            {
                await plugin.ClientReady();
            }
        }
        private async Task HandleReactionsRemovedForEmote(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, IEmote emote)
        {
            ReactionHandler rh = new ReactionHandler(ServiceProvider, assemblyManager);
            await rh.Handle(message, channel, ReactionType.RemovedForEmotes, emote: emote);
        }
        private async Task HandleReactionsCleared(Cacheable<IUserMessage,ulong> message, Cacheable<IMessageChannel,ulong> channel)
        {
            ReactionHandler rh = new ReactionHandler(ServiceProvider, assemblyManager);
            await rh.Handle(message, channel, ReactionType.Cleared);
        }
        private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            ReactionHandler rh = new ReactionHandler(ServiceProvider, assemblyManager);
            await rh.Handle(message, channel, ReactionType.Added, reaction);
        }
        private async Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            ReactionHandler rh = new ReactionHandler(ServiceProvider, assemblyManager);
            await rh.Handle(message, channel, ReactionType.Removed, reaction);
        }
        private async Task ComponentExecuted(SocketMessageComponent component)
        {
            ComponentHandler componentHandler = new ComponentHandler(ServiceProvider, assemblyManager);
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
            List<IPluginMessages> plugins = assemblyManager.Plugins.Get<IPluginMessages>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.MessageReceived(message);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) { }
                    else
                        Logger.Log("BOT", $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
        }
        private async Task MessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            List<IPluginMessages> plugins = assemblyManager.Plugins.Get<IPluginMessages>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.MessageDeleted(message,channel);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) { }
                    else
                        Logger.Log("BOT", $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
        }
        private async Task MessageUpdated(Cacheable<IMessage, ulong> messageOld, SocketMessage messageNew, ISocketMessageChannel channel)
        {
            List<IPluginMessages> plugins = assemblyManager.Plugins.Get<IPluginMessages>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.MessageUpdated(messageOld,messageNew,channel);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) { }
                    else
                        Logger.Log("BOT", $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
        }
        private async Task MessageBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages, Cacheable<IMessageChannel, ulong> channel)
        {
            List<IPluginMessages> plugins = assemblyManager.Plugins.Get<IPluginMessages>();
            foreach (var data in plugins)
            {
                try
                {
                    await data.MessageBulkDeleted(messages,channel);
                }
                catch (Exception ex)
                {
                    if (ex is NotImplementedException) { }
                    else
                        Logger.Log("BOT", $"Error in: {data.Name}, message: {ex.Message}", LogLevel.Error);
                }
            }
        }
        private async Task UserCommandExecuted(SocketUserCommand command)
        {

        }
        private async Task MessageCommandExecuted(SocketMessageCommand command)
        {

        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
