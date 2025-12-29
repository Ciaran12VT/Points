using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public interface IDatabaseMaintenance
    {
        string BackupsFolderPath { get; }

        Task WipeAsync();
        Task BackupAsync();
        Task RestoreAsync(string backupFilePath);

        DateTime? GetLastBackupUtc();
    }
}
