﻿using DiscordBot.Utility;
using Microsoft.Extensions.DependencyInjection;
using DiscordPluginAPI.Enums;
using DiscordPluginAPI.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DiscordBot.AssemblyHelpers
{
    internal class AssemblyLoader : IEnumerable<AssemblyData>, IDisposable
    {
        private string FilePath;
        private const string FileType = "*.dll";
        private List<AssemblyData> AssemblyData { get; set; }
        private readonly Logger Logger;
        /// <summary>
        /// Used to load all <see cref="IPlugin"/> assemblies in the given path.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="serviceProvider"></param>
        public AssemblyLoader(string path,IServiceProvider serviceProvider)
        {
            FilePath = path;
            AssemblyData = new List<AssemblyData>();
            Logger = serviceProvider.GetService<Logger>();
        }

        /// <summary>
        /// Loads and checks the compatibility of all assemblies in the given path.
        /// </summary>
        public void Load()
        {
            AssemblyData data;
            foreach (string file in Directory.GetFiles(FilePath, FileType))
            {
                Assembly assembly = null;
                try
                {
                    assembly = Assembly.LoadFrom(file);
                }
                catch (Exception ex)
                {
                    Logger.Log("Assembly Loader", $"Assembly Error from {file}: " + ex.Message, LogLevel.Error);
                    continue;
                }
                try
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (typeof(IPlugin).IsAssignableFrom(type))
                        {
                            IPlugin plugin = (IPlugin)Activator.CreateInstance(type);

                            string AssemblyFileName = Path.GetFileNameWithoutExtension(file);
                            string AssemblyVersion = assembly.GetName().Version.ToString();

                            data = new AssemblyData(AssemblyVersion, AssemblyFileName, plugin);

                            AssemblyData.Add(data);
                        }
                    }
                }catch(Exception ex)
                {
                    Logger.Log("AssemblyLoader", $"{file} is not compatibile: {ex.Message}", LogLevel.Error);
                }
            }
        }
        /// <summary>
        /// Iterates over all loaded assemblies.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<AssemblyData> GetEnumerator()
        {
            foreach(AssemblyData data in AssemblyData)
            {
                if(data != null)
                    yield return data;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
        public void Dispose()
        {
            FilePath = null;
            AssemblyData = null;
        }
    }
}
