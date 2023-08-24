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
        ConcurrentQueue<Func<Task>> actionQueue = new ConcurrentQueue<Func<Task>>();
        AssemblyManager assemblyManager;
        ILogger Logger = new Logger();

        public TaskQueueManager(DiscordSocketClient client, AssemblyManager assemblyManager, double seconds = 1)
        {
            _client = client;
            _timer = new System.Timers.Timer(seconds * 1000);
            this.assemblyManager = assemblyManager;
        }

        // Start the timer
        public async Task Start(bool AutoReset = false)
        {
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = true;
            _timer.Enabled = true;

            Thread dequeueThread = new Thread(async () =>
            {
                while (true)
                {
                    if (actionQueue.TryDequeue(out Func<Task> function))
                    {
                        await function();
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
                    try
                    {
                        Func<Task> update = await plugin.Update(guild);
                        if (update != null)
                        {
                            actionQueue.Enqueue(async() =>
                            {
                                try
                                {
                                    Logger.Log("TaskQueueManager", $"Got new work! Trying to execute update from: {plugin.Name}",LogLevel.Info);
                                    _ = Task.Run(async () =>
                                    {
                                        await update();
                                    });
                                }
                                catch (Exception ex)
                                {
                                    if (ex is NotImplementedException) { }
                                    else Logger.Log("TaskQueueManager", $"An error occurred in returned action from {plugin.Name}: {ex.Message}", LogLevel.Error);
                                }
                            }
                            );
                        }
                    }catch(Exception ex)
                    {
                        if (ex is NotImplementedException) { }
                        else Logger.Log("TaskQueueManager", $"An error occurred in {plugin.Name} Update method: {ex.Message}",LogLevel.Error);
                    }
                }
            }
        }
    }
}
