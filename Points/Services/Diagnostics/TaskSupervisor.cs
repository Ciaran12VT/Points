using System.Diagnostics;

namespace Points.Services.Diagnostics
{
    public static class TaskSupervisor
    {
        public static void Forget(this Task task, string operationName)
        {
            if (task == null)
                return;

            _ = ObserveAsync(task, operationName);
        }

        private static async Task ObserveAsync(Task task, string operationName)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is an expected lifecycle path for supervised background work.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{operationName} failed: {ex}");
            }
        }
    }
}
