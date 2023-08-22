using Discord;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Structures;
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
        private static System.Timers.Timer _timer;
        ConcurrentQueue<Action> actionQueue = new ConcurrentQueue<Action>();

        public TaskQueueManager(DiscordSocketClient client, double seconds = 1)
        {
            _client = client;
            _timer = new System.Timers.Timer(seconds * 1000);
        }

        // Start the timer
        public void Start(bool AutoReset = false)
        {
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = AutoReset;
            _timer.Enabled = true;

            Thread dequeueThread = new Thread(() =>
            {
                while (true)
                {
                    if (actionQueue.TryDequeue(out Action function))
                    {
                        function();
                        Thread.Sleep(5000); //global rate limit for ModifyMessageAsync is set to 5 seconds per action
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

            await UpdateGiveawaysAsync();
            await UpdatePollsAcyns();
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
