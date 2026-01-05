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
        public static string DbBackupsFolder => Ensure("db/backups");
        public static string LogsFolder => Ensure("logs");
        public static string CacheFolder => Ensure("cache");

        public static string DatabasePath =>
            Path.Combine(DbFolder, "points.db3");

        private static string Ensure(string folder)
        {
            var path = Path.Combine(Root, folder);
            Directory.CreateDirectory(path);
            return path;
        }
    }

}
