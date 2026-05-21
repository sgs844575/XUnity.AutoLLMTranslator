using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class TranslationScheduler
{
    private readonly TaskManager _taskManager;
    private readonly TranslationProcessor _translationProcessor;
    private readonly TranslationConfiguration _configuration;
    private readonly SemaphoreSlim _semaphore;

    public TranslationScheduler(
        TaskManager taskManager,
        TranslationProcessor translationProcessor,
        TranslationConfiguration configuration)
    {
        _taskManager = taskManager;
        _translationProcessor = translationProcessor;
        _configuration = configuration;
        _semaphore = new SemaphoreSlim(configuration.ParallelCount, configuration.ParallelCount);
    }

    public void Start()
    {
        Task.Run(() => RunAsync());
    }

    private async Task RunAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(_configuration.PollingInterval);

                int currentCount = _configuration.ParallelCount - _semaphore.CurrentCount;
                if (currentCount > 0)
                {
                    Logger.Debug("Scheduler", $"Polling curProcessingCount: {currentCount}/{_configuration.ParallelCount} TASKS: {_taskManager.GetTaskCount()}");
                }

                await _semaphore.WaitAsync();

                try
                {
                    var taskBatch = new List<List<TaskData>>();
                    List<TaskData> tasks;

                    tasks = _taskManager.SelectTasks(_configuration.MaxWordCount, _configuration.MaxBatchTexts);
                    while (tasks.Count > 0)
                    {
                        foreach (var task in tasks)
                        {
                            task.state = TaskState.Processing;
                        }

                        taskBatch.Add(tasks);
                        tasks = _taskManager.SelectTasks(_configuration.MaxWordCount, _configuration.MaxBatchTexts);
                    }

                    if (taskBatch.Count > 0)
                    {
                        foreach (var taskList in taskBatch)
                        {
                            _ = ProcessTaskBatchAsync(taskList);
                        }
                    }
                    else
                    {
                        _semaphore.Release();
                    }
                }
                catch
                {
                    _semaphore.Release();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Scheduler", ex, "调度循环异常");
            }
        }
    }

    private async Task ProcessTaskBatchAsync(List<TaskData> tasks)
    {
        try
        {
            await _translationProcessor.ProcessTaskBatch(tasks);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
