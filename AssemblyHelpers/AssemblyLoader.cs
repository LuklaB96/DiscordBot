﻿using DiscordBot.Utility;
using PluginTest.Enums;
using PluginTest.Interfaces;
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
        private List<AssemblyData> assemblyData { get; set; }
        private Logger Logger;
        public AssemblyLoader(string path)
        {
            FilePath = path;
            assemblyData = new List<AssemblyData>();
            Logger = new Logger();
        }

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
                        if (!typeof(ICommand).IsAssignableFrom(type))
                            continue;
                        ICommand plugin = (ICommand)Activator.CreateInstance(type);

                        string AssemblyFileName = Path.GetFileNameWithoutExtension(file);
                        string AssemblyVersion = assembly.GetName().Version.ToString();

                        data = new AssemblyData(AssemblyVersion, AssemblyFileName, plugin);

                        assemblyData.Add(data);
                    }
                }catch(Exception ex)
                {
                    Logger.Log("AssemblyLoader", $"{file} is not compatibile: {ex.Message}", LogLevel.Error);
                }
            }
        }
        public IEnumerator<AssemblyData> GetEnumerator()
        {
            foreach(AssemblyData data in assemblyData)
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
            assemblyData = null;
        }
    }
}
