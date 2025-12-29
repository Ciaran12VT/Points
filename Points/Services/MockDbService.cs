using Points.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Services
{
    public class MockDbService : IDatabaseMaintenance
    {
        public string BackupsFolderPath => throw new NotImplementedException();

        public Task BackupAsync()
        {
            return Task.CompletedTask;
        }

        public DateTime? GetLastBackupUtc()
        {
            return DateTime.Now;
        }

        public Task RestoreAsync(string backupFilePath)
        {
            return Task.CompletedTask;
        }

        public Task WipeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
