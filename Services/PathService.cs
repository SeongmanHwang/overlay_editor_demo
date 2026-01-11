using System;
using System.IO;

namespace SimpleOverlayEditor.Services
{
    public static class PathService
    {
        public static string AppDataFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "SimpleOverlayEditor");

        public static string StateFilePath =>
            Path.Combine(AppDataFolder, "state.json");

        public static string DefaultTemplateFilePath =>
            Path.Combine(AppDataFolder, "default_template.json");

        public static string SessionFilePath =>
            Path.Combine(AppDataFolder, "session.json");

        public static string OutputFolder =>
            Path.Combine(AppDataFolder, "output");

        public static string AlignmentCacheFolder =>
            Path.Combine(AppDataFolder, "aligned_cache");

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(AppDataFolder);
            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory(AlignmentCacheFolder);
        }

        public static string DefaultInputFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                         "OverlayEditorInput");
    }
}

