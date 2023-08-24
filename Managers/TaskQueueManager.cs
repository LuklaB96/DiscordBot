using Discord;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Structures;
using DiscordBot.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;
using PluginTest.Enums;
using PluginTest.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace DiscordBot.Managers
{
    internal class TaskQueueManager
    {
        DiscordSocketClient _client { get; set; }
        private System.Timers.Timer _timer { get; set; }
        ConcurrentQueue<Func<Task>> actionQueue = new ConcurrentQueue<Func<Task>>();
        AssemblyManager assemblyManager { get; set; }
        Logger Logger { get; set; }
        Database Database { get; set; }

        public TaskQueueManager(DiscordSocketClient client, AssemblyManager assemblyManager, IServiceProvider serviceProvider, double seconds = 1)
        {
            _client = client;
            _timer = new System.Timers.Timer(seconds * 1000);
            this.assemblyManager = assemblyManager;
            Logger = serviceProvider.GetService<Logger>();
            Database = serviceProvider.GetService<Database>();
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
            Logger.Log("TaskQueueManager", "Action enqueue/dequeue thread started!", LogLevel.Info);
            dequeueThread.Start();
        }

        private async void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            await ExecutePluginUpdate();

        }
        private async Task ExecutePluginUpdate()
        {
            const string selectQuery = "SELECT guild_id FROM guildsettings";
            var result = await Database.SelectQueryAsync(selectQuery);

            while (result.Count > 0)
            {
                ulong currentGuildId = 0;
                ulong.TryParse(result.Take(1).ToList()[0], out currentGuildId);
                result = result.Skip(1).ToList();
                if (currentGuildId == 0) continue;

                SocketGuild guild = _client?.GetGuild(currentGuildId);
                if (guild == null) continue;

                List<IPlugin> plugins = assemblyManager.Plugins.Get<IPlugin>();
                foreach (IPlugin plugin in plugins)
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
