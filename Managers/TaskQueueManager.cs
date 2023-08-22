using Discord;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Structures;
using DiscordBot.Utility;
using Microsoft.VisualBasic;
using PluginTest.Enums;
using PluginTest.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace DiscordBot.Managers
{
    internal class TaskQueueManager
    {
        static Database db = new Database();
        DiscordSocketClient _client;
        private System.Timers.Timer _timer;
        ConcurrentQueue<Action> actionQueue = new ConcurrentQueue<Action>();
        AssemblyManager assemblyManager;
        ILogger Logger = new Logger();

        public TaskQueueManager(DiscordSocketClient client, AssemblyManager assemblyManager, double seconds = 1)
        {
            _client = client;
            _timer = new System.Timers.Timer(seconds * 1000);
            this.assemblyManager = assemblyManager;
        }

        // Start the timer
        public void Start(bool AutoReset = false)
        {
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = true;
            _timer.Enabled = true;

            Thread dequeueThread = new Thread(() =>
            {
                while (true)
                {
                    if (actionQueue.TryDequeue(out Action function))
                    {
                        function();
                        Thread.Sleep(1000); //global rate limit for ModifyMessageAsync is set to 5 seconds per action
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            });
            Console.WriteLine("Starting action dequeue thread...");
            dequeueThread.Start();
        }

        private async void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            await ExecutePluginUpdate();

        }
        private async Task ExecutePluginUpdate()
        {
            const string selectQuery = "SELECT guild_id FROM guildsettings";
            var result = await db.SelectQueryAsync(selectQuery);

            while (result.Count > 0)
            {
                ulong currentGuildId = 0;
                ulong.TryParse(result.Take(1).ToList()[0], out currentGuildId);
                result = result.Skip(1).ToList();
                if (currentGuildId == 0) continue;

                SocketGuild guild = _client?.GetGuild(currentGuildId);
                if (guild == null) continue;

                foreach (ICommand plugin in assemblyManager.Plugins)
                {
                    Action update = await plugin.Update(guild);
                    if (update != null)
                    {
                        actionQueue.Enqueue(() =>
                        {
                            try
                            {
                                update();
                            }
                            catch (Exception ex)
                            {
                                if (ex is NotImplementedException) { }
                                else Console.WriteLine(ex.Message);
                            }
                        }
                        );
                    }
                }
            }
        }
        private async Task UpdatePollsAcyns()
        {
            DateTime date = DateTime.Now;
            string dateNow = date.ToString("yyyy-MM-dd HH:mm:ss");

            const string selectQuery = "SELECT guild_id,channel_id,message_id,closed FROM polls WHERE ends <= @Ends";

            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@Ends",dateNow),
            };

            var result = await db.SelectQueryAsync(selectQuery, parameters);

            const int setSize = 4;
            while (result.Count > 0)
            {
                var currentSet = result.Take(setSize).ToList();
                result = result.Skip(setSize).ToList();

                ulong guildId = ulong.Parse(currentSet[0]);
                ulong channelId = ulong.Parse(currentSet[1]);
                ulong messageId = ulong.Parse(currentSet[2]);
                string closed = currentSet[3];

                if (closed == "0")
                {
                    var channel = _client.GetGuild(guildId).GetTextChannel(channelId);
                    var messageData = await channel.GetMessageAsync(messageId);
                    var guildtest = _client.GetGuild(guildId);
                    actionQueue.Enqueue(() =>
                    {

                        //embedManager.EditPollEmbed(messageId, channel);
                        const string updateQuery = "UPDATE polls SET closed = @Closed WHERE message_id = @MessageId";

                        parameters.Add(new KeyValuePair<string, string>("@MessageId", messageId.ToString()));
                        parameters.Add(new KeyValuePair<string, string>("@Closed", "1"));

                        db.UpdateQueryAsync(updateQuery, parameters);

                    });
                }
            }
        }
        private async Task UpdateGiveawaysAsync()
        {
            DateTime date = DateTime.Now;
            string dateNow = date.ToString("yyyy-MM-dd HH:mm:ss");

            const string selectQuery = "SELECT guild_id,channel_id,message_id,closed FROM giveaways WHERE ends <= @Ends";

            List<KeyValuePair<string, string>> parameters = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("@Ends",dateNow),
            };

            var result = await db.SelectQueryAsync(selectQuery, parameters);


            const int setSize = 4;
            while (result.Count > 0)
            {
                var currentSet = result.Take(setSize).ToList();
                result = result.Skip(setSize).ToList();

                ulong guildId = ulong.Parse(currentSet[0]);
                ulong channelId = ulong.Parse(currentSet[1]);
                ulong messageId = ulong.Parse(currentSet[2]);
                string closed = currentSet[3];

                if (closed == "0")
                {
                    var channel = _client.GetGuild(guildId).GetTextChannel(channelId);
                    var messageData = await channel.GetMessageAsync(messageId);

                    var embed = messageData.Embeds.First().ToEmbedBuilder();
                    //var newEmbed = await embedManager.EditGiveawayEmbed(embed, options: new PluginTest.EmbedOptions[] { PluginTest.EmbedOptions.CloseGiveaway });

                    //if (newEmbed == null) newEmbed = embed;
                    actionQueue.Enqueue(() =>
                    {

                        //channel.ModifyMessageAsync(messageId, x => x.Embed = newEmbed.Build());
                        const string updateQuery = "UPDATE giveaways SET closed = @Closed WHERE message_id = @MessageId";

                        parameters.Add(new KeyValuePair<string, string>("@MessageId", messageId.ToString()));
                        parameters.Add(new KeyValuePair<string, string>("@Closed", "1"));

                        db.UpdateQueryAsync(updateQuery, parameters);

                    });



                }
            }
        }
    }
}
