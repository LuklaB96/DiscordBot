using Discord.WebSocket;
using DiscordBot.Structures;
using DiscordBot.Utility;
using Microsoft.Extensions.DependencyInjection;
using DiscordPluginAPI.Enums;
using DiscordPluginAPI.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace DiscordBot.Managers
{
    internal class TaskQueueManager
    {
        DiscordSocketClient _client { get; set; }
        System.Timers.Timer _timer { get; set; }
        readonly ConcurrentQueue<Func<Task>> actionQueue = new ConcurrentQueue<Func<Task>>();
        AssemblyManager AssemblyManager { get; set; }
        Logger Logger { get; set; }
        Database Database { get; set; }
        /// <summary>
        /// Provides a built-in ability to control the number of actions performed by the plugin. Works in the background without affecting the main thread.
        /// <br></br>It should be enabled by default. The plugin API provides an Update method that is called by the <see cref="TaskQueueManager"/> at a specified interval.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="assemblyManager"></param>
        /// <param name="serviceProvider"></param>
        /// <param name="seconds"></param>
        public TaskQueueManager(DiscordSocketClient client, AssemblyManager assemblyManager, IServiceProvider serviceProvider, double seconds = 1)
        {
            _client = client;
            _timer = new System.Timers.Timer(seconds * 1000);
            this.AssemblyManager = assemblyManager;
            Logger = serviceProvider.GetService<Logger>();
            Database = serviceProvider.GetService<Database>();
        }

        /// <summary>
        /// Starts ConcurrentQueue. Waiting for work from any of the active plugins. Dequeue thread can take action every 1 second by default.
        /// TO DO: make dequeue thread sleep time configurable.
        /// </summary>
        /// <param name="AutoReset"></param>
        /// <returns></returns>
        public async Task Start(bool AutoReset = true)
        {
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = AutoReset;
            _timer.Enabled = true;

            Thread dequeueThread = new Thread(async () =>
            {
                while (true)
                {
                    if (actionQueue.TryDequeue(out Func<Task> function))
                    {
                        await function();
                        Thread.Sleep(1000);
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
        /// <summary>
        /// Calls the Update method of all active plugins at a specified interval. If plugin returns function, it will enqueued in dequeue thread and executed when it turn comes.
        /// </summary>
        /// <returns></returns>
        private async Task ExecutePluginUpdate()
        {
            const string selectQuery = "SELECT guild_id FROM guildsettings";
            var result = await Database.SelectQueryAsync(selectQuery);

            while (result.Count > 0)
            {
                ulong.TryParse(result.Take(1).ToList()[0], out ulong currentGuildId);
                result = result.Skip(1).ToList();
                if (currentGuildId == 0) continue;

                SocketGuild guild = _client?.GetGuild(currentGuildId);
                if (guild == null) continue;

                List<IPlugin> plugins = AssemblyManager.Plugins.GetAllBasePlugins();
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
