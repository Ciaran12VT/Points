using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Global
{
    public static class AppPaths
    {
        public static string Root => FileSystem.AppDataDirectory; // maps tp "/data/data/<your.package.name>/files"

        public static string DbFolder => Ensure("db");
        public static string LogsFolder => Ensure("logs");
        public static string CacheFolder => Ensure("cache");
        public static string AchievementTrophiesFolder => Ensure("trophies");
        public static string MissionResourcesFolder => Ensure("resources");
        public static string ExportsFolder => Ensure("exports");

        public static string DatabasePath => Path.Combine(DbFolder, "points.db3");

        public static string GetAchievementTrophiesPath(int achievementID)
        {
            return Ensure(Path.Combine("trophies", $"AchievementID_{achievementID}"));
        }

        public static string GetMissionResourcesPath(int missionId)
        {
            return Ensure(Path.Combine("resources", $"MissionID_{missionId}"));
        }
        public static IEnumerable<string> EnumerateMissionResourceFiles(int missionId)
        {
            var folder = GetMissionResourcesPath(missionId); // ensures it exists
            return Directory.EnumerateFiles(folder);
        }


        private static string Ensure(string folder)
        {
            var path = Path.Combine(Root, folder);
            Directory.CreateDirectory(path);
            return path;
        }
    }

}
