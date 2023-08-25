using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;
using DiscordBot.Structures;
using System.Net;
using System.Linq;
using Microsoft.Extensions.Configuration;
using OpenAI_API;
using DiscordBot.Managers;
using PluginTest.Interfaces;
using PluginTest.Enums;
using Discord.Rest;
using DiscordBot.Utility;
using System.Reflection;

namespace DiscordBot.Handlers
{
    public enum CommandSource
    {
        slash,
        message,
        user,
        none
    }
    internal class CommandHandler : UtilityBase
    {
        public CommandHandler(IServiceProvider serviceProvider, AssemblyManager assemblyManager) : base(serviceProvider, assemblyManager) { }
        public async Task Handle(SocketSlashCommand slashCommand = null, SocketMessageCommand messageCommand = null, SocketUserCommand userCommand = null)
        {
            _ = Task.Run(async () =>
            {
                string pluginName = "";
                if (slashCommand != null)
                {
                    pluginName = await CheckPluginCommand(slashCommand.Data.Name.ToLower());
                }
                if (messageCommand != null)
                {
                    pluginName = await CheckPluginCommand(messageCommand.Data.Name.ToLower());
                }
                if (userCommand != null)
                {
                    pluginName = await CheckPluginCommand(userCommand.Data.Name.ToLower());
                }
                List<IPluginCommands> plugins = assemblyManager.Plugins.Get<IPluginCommands>();
                foreach (IPluginCommands plugin in plugins)
                {
                    if (plugin.Name.ToLower() != pluginName.ToLower())
                    {
                        continue;
                    }

                    object msg = null;

                    CommandSource source = CommandSource.none;
                    if (slashCommand != null) source = CommandSource.slash;
                    if (messageCommand != null) source = CommandSource.message;
                    if (userCommand != null) source = CommandSource.user;
                    switch (source)
                    {
                        case CommandSource.slash:
                            msg = plugin.ExecuteSlashCommand(slashCommand);
                            break;
                        case CommandSource.message:
                            msg = plugin.MessageCommandExecuted(messageCommand);
                            break;
                        case CommandSource.user:
                            msg = plugin.UserCommandExecuted(userCommand);
                            break;
                        default:
                            break;
                    }

                    if (msg == null)
                    {
                        break;

                    }

                    switch (msg.GetType().Name)
                    {
                        case nameof(IMessage):
                            await SaveMessageToDatabase((msg as IMessage).Id.ToString(), pluginName);
                            break;
                        case nameof(RestInteractionMessage):
                            await SaveMessageToDatabase((msg as RestInteractionMessage).Id.ToString(), pluginName);
                            break;
                        case nameof(RestUserMessage):
                            await SaveMessageToDatabase((msg as RestUserMessage).Id.ToString(), pluginName);
                            break;
                        default:
                            continue;
                    }

                }
            });
        }
        private async Task<bool> SaveMessageToDatabase(string messageId, string pluginName)
        {
            string query = "INSERT INTO message_info (message_id, plugin_name) VALUES (@MessageId, @PluginName)";
            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@MessageId",messageId),
                new KeyValuePair<string, string>("@PluginName",pluginName)
            };

            var result = await Database.InsertQueryAsync(query, parameters);
            if (result == 1)
            {
                Logger.Log(pluginName, "Message created: " + messageId, LogLevel.Info);
                return true;
            }
            else
            {
                Logger.Log(pluginName, "Could not save message to database", LogLevel.Info);
                return false;
            }
        }
        private async Task<string> CheckPluginCommand(string commandName)
        {
            string query = "SELECT plugin_name FROM command_info WHERE command_name = @CommandName";

            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@CommandName",commandName)
            };
            var items = await Database.SelectQueryAsync(query, parameters);
            if (items.Count == 0) return string.Empty;
            return items[0];
        }
        public async Task Handle(SocketMessage message, IConfigurationRoot appConfig)
        {
            _ = Task.Run(async () =>
            { 
                if (message.Author.IsBot) return;

                const string selectPrefixQuery = "SELECT prefix FROM guildsettings WHERE guild_id = @GuildId";

                var channel = message.Channel as ITextChannel;
                string guildId = channel.GuildId.ToString();

                List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("@GuildId",guildId)
                };

                var data = await Database.SelectQueryAsync(selectPrefixQuery, parameters);

                if (data.Count == 0)
                    return;

                string prefix = data[0];



                string[] args = message.Content.Split(' ');

                if (args[0].ToLower() == $"{prefix}roll")
                {
                    Random rng = new Random();
                    await message.Channel.SendMessageAsync((rng.Next(0, 100) + 1).ToString());
                }
                if (args[0].ToLower() == $"{prefix}weather")
                {
                    await Weather(message, args, appConfig);
                }
                if (args[0].ToLower() == $"{prefix}setprefix")
                {
                    await SetPrefix(message, args);
                }
                if (args[0].ToLower() == $"{prefix}counter")
                {
                    for (int i = 3; i > 0; i--)
                    {
                        var msg = await channel.SendMessageAsync($"{i}");
                        await Task.Delay(500);
                        await msg.DeleteAsync();
                    }
                    await channel.SendMessageAsync("=====================");
                }
                if (args[0].ToLower() == $"{prefix}help")
                {
                    await message.Author.SendMessageAsync("Help here!");
                    await message.DeleteAsync();
                }
                if (args[0].ToLower() == $"{prefix}kick")
                {
                    await Kick(message, channel);
                }
                if (args[0].ToLower() == $"{prefix}mute")
                {
                    await Mute(message, channel);
                }
                if (args[0].ToLower() == $"{prefix}banlist")
                {
                    await BanList(message, channel);
                }
                if (args[0].ToLower() == $"{prefix}ban")
                {
                    await Ban(message, channel);
                }
                if (args[0].ToLower() == $"{prefix}unban")
                {
                    await Unban(message, channel, args);
                }
                if (args[0].ToLower() == $"{prefix}suggest")
                {
                    if (args.Length < 2)
                    {
                        await message.DeleteAsync();
                        return;
                    }
                    string suggestion = string.Join(' ', args.Where(s => s != args[0]));
                    string userName = message.Author.Username;

                    const string insertQuery = "INSERT INTO suggestions (guild_id,user_id,suggestion) VALUES (@GuildId,@UserName,@Suggestion)";
                    parameters.Add(new KeyValuePair<string, string>("@UserName", userName));
                    parameters.Add(new KeyValuePair<string, string>("@Suggestion", suggestion));

                    await Database.InsertQueryAsync(insertQuery, parameters);
                    await message.DeleteAsync();
                }
                if (args[0].ToLower() == $"{prefix}suggestions")
                {
                    var user = message.Author as SocketGuildUser;
                    if (user.GuildPermissions.Administrator)
                    {
                        const string selectQuery = "SELECT user_id,suggestion FROM suggestions WHERE guild_id = @GuildId";
                        var suggestionList = await Database.SelectQueryAsync(selectQuery, parameters);

                        string respond = "Suggestions list:\r\n";

                        while(suggestionList.Count > 0)
                        {
                            var currentSet = suggestionList.Take(2).ToList();
                            suggestionList = suggestionList.Skip(2).ToList();

                            respond += $"[{currentSet[0]}]: {currentSet[1]}\r\n";
                        }

                        await message.Author.SendMessageAsync(respond);
                        await message.DeleteAsync();
                    }
                }
                if (args[0] == $"{prefix}ai")
                {
                    try
                    {
                        OpenAIAPI openai = new OpenAIAPI(appConfig["openai_token"]);
                        string request = string.Join(' ', args.Where(s => s != args[0]));
                        string result = "";
                        await openai.Completions.StreamCompletionAsync(
                            new CompletionRequest(request, Model.DavinciText, 200, 0.5, presencePenalty: 0.1, frequencyPenalty: 0.1),
                            res => result = res.ToString());
                        await channel.SendMessageAsync(result);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Wrong token or no connection with openai.com");
                    }

                }

            });
        }
        private async Task Unban(SocketMessage message, ITextChannel channel, string[] args)
        {
            var user = message.Author as SocketGuildUser;
            if (!user.GuildPermissions.BanMembers)
            {
                await message.DeleteAsync();
                return;
            }
            var guild = channel.Guild;
            try
            {
                ulong userId = ulong.Parse(args[1]);
                var banList = guild.GetBansAsync();
                await foreach(var bannedUser in banList)
                {
                    var unban = bannedUser.First(x => x.User.Id == userId).User;

                    await guild.RemoveBanAsync(unban);
                    await message.Channel.SendMessageAsync($"User unbanned: {unban.Username}");
                }
                await message.DeleteAsync();

            }
            catch(Exception e)
            {
                await message.Channel.SendMessageAsync("Wrong user id, check !banlist");
                return;
            }

            
        }
        private async Task BanList(SocketMessage message, ITextChannel channel)
        {
            var user = message.Author as SocketGuildUser;
            if (!user.GuildPermissions.BanMembers)
            {
                await message.DeleteAsync();
                return;
            }

            var guild = channel.Guild;
            var banlist = guild.GetBansAsync();
            string s = "";
            await foreach (var u in banlist)
            {
                foreach (var c in u)
                {
                    s += $"User name: {c.User.Username}, user id: {c.User.Id}\r\n";
                }
            }
            await message.DeleteAsync();
            await message.Author.SendMessageAsync(s);
        }
        private async Task Ban(SocketMessage message, ITextChannel channel)
        {
            var user = message.Author as SocketGuildUser;


            if (!user.GuildPermissions.BanMembers)
            {
                await message.DeleteAsync();
                return;
            }

            var mentions = message.MentionedUsers;
            var guild = channel.Guild;

            await message.DeleteAsync();
            foreach (var mention in mentions)
            {
                var GuildUser = await guild.GetUserAsync(mention.Id);
                if (GuildUser == user)
                {
                    await message.Channel.SendMessageAsync("You can't ban yourself!");
                    return;
                }
                if (GuildUser.GuildPermissions.BanMembers)
                {
                    await message.Channel.SendMessageAsync("You can't ban this user!");
                    return;
                }
                await GuildUser.BanAsync();

            }
        }
        private async Task Kick(SocketMessage message, ITextChannel channel)
        {
            var user = message.Author as SocketGuildUser;


            if (!user.GuildPermissions.KickMembers)
            {
                await message.DeleteAsync();
                return;
            }

            var mentions = message.MentionedUsers;
            var guild = channel.Guild;

            await message.DeleteAsync();
            foreach (var mention in mentions)
            {
                var GuildUser = await guild.GetUserAsync(mention.Id);
                if (GuildUser == user)
                {
                    await message.Channel.SendMessageAsync("You can't kick yourself!");
                    return;
                }
                if (GuildUser.GuildPermissions.KickMembers)
                {
                    await message.Channel.SendMessageAsync("You can't kick this user!");
                    return;
                }
                await GuildUser.KickAsync();

            }
        }
        private async Task Mute(SocketMessage message, ITextChannel channel)
        {
            var user = message.Author as SocketGuildUser;


            if (!user.GuildPermissions.MuteMembers)
            {
                await message.DeleteAsync();
                return;
            }

            var mentions = message.MentionedUsers;
            var guild = channel.Guild;

            await message.DeleteAsync();
            foreach (var mention in mentions)
            {
                var GuildUser = await guild.GetUserAsync(mention.Id);
                if (GuildUser == user)
                {
                    await message.Channel.SendMessageAsync("You can't mute yourself!");
                    return;
                }
                if (GuildUser.GuildPermissions.MuteMembers)
                {
                    await message.Channel.SendMessageAsync("You can't mute this user!");
                    return;
                }
                try
                {
                    await GuildUser.ModifyAsync(x => { x.Mute = true; });
                }
                catch (Exception e)
                {
                    await message.Channel.SendMessageAsync($"<@{mention.Id}> is not connected to voice!");
                }
            }
            await message.Channel.SendMessageAsync($"User/users muted!");
        }
        private async Task SetPrefix(SocketMessage message, string[] args = null)
        {
            var user = message.Author as SocketGuildUser;

            if (!user.GuildPermissions.Administrator)
            {
                await message.DeleteAsync();
                return;
            }

            var channel = message.Channel as ITextChannel;
            string guildId = channel.GuildId.ToString();

            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@GuildId",guildId)
            };

            if (args.Length > 1 && args[1].Length == 1)
            {
                if (args[1] != "/")
                {
                    await message.Channel.SendMessageAsync($"You can't use this prefix: {args[1]}");
                    return;
                }
                const string updateQuery = "UPDATE guildsettings SET prefix = @Prefix WHERE guild_id = @GuildId";
                string newPrefix = args[1];
                parameters.Add(new KeyValuePair<string, string>("@Prefix", newPrefix));
                var result = await Database.UpdateQueryAsync(updateQuery, parameters);

                if (result > 0)
                    await message.Channel.SendMessageAsync($"Prefix updated, new prefix: {newPrefix}");

            }
            else
            {
                await message.Channel.SendMessageAsync("Prefix needs to have 1 character length");
            }
        }

        private async Task Weather(SocketMessage message,string[] args, IConfigurationRoot appConfig)
        {
            string apiToken = appConfig["weather_api_token"];
            if (args.Length < 2) return;
            string city = "";
            if (args.Length >= 2)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    city += args[i];
                    if ((i + 1) < args.Length) city += "-";
                }
            }

            using WebClient client = new WebClient();
            try
            {
                string jsonString = client.DownloadString($"https://api.weatherapi.com/v1/current.json?key={apiToken}={city}&aqi=no");

                var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(jsonString);
                string temp = data["current"]["temp_c"].ToString();
                string humidity = data["current"]["humidity"].ToString();
                string feelslike_c = data["current"]["feelslike_c"].ToString();
                string wind_kph = data["current"]["wind_kph"].ToString();
                string cloud = data["current"]["cloud"].ToString();
                string pressure_mb = data["current"]["pressure_mb"].ToString();
                await message.Channel.SendMessageAsync($"Aktualna temperatura w {city} to {temp}°C\r\nTemperatura odczuwalna: {feelslike_c}°C\r\nWilgotność powietrza: {humidity}%\r\nPrędkość wiatru: {wind_kph} km/h\r\nZachmurzenie: {cloud}%\r\nCiśnienie: {pressure_mb} hPa");
            }
            catch(Exception e)
            {
                await message.Channel.SendMessageAsync("Nie ma takiego miasta: " + e.Message);
            }
        }
    }
}
