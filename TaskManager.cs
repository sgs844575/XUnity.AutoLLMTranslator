using System.Collections.Generic;
using System.Threading.Tasks;

public class TaskManager
{
    private readonly List<TaskData> _taskDatas = new List<TaskData>();
    private readonly object _lockObject = new object();

    public async Task<TaskData> AddTask(string[] texts)
    {
        var task = new TaskData() { texts = texts };

        lock (_lockObject)
        {
            _taskDatas.Insert(0, task);
        }

        await task.WaitOne();

        lock (_lockObject)
        {
            _taskDatas.Remove(task);
        }

        return task;
    }

    public List<TaskData> SelectTasks(int maxTokenCount, int maxTextCount)
    {
        var tasks = new List<TaskData>();
        int totalTokens = 0;
        int totalTexts = 0;

        lock (_lockObject)
        {
            foreach (var task in _taskDatas)
            {
                if (task.state == TaskState.Waiting)
                {
                    int taskTokens = 0;
                    foreach (var txt in task.texts)
                    {
                        taskTokens += TokenEstimator.Estimate(txt);
                    }

                    if (totalTokens + taskTokens > maxTokenCount && tasks.Count > 0)
                    {
                        break;
                    }

                    if (totalTexts + task.texts.Length > maxTextCount && tasks.Count > 0)
                    {
                        break;
                    }

                    if (task.retryCount > 2 && tasks.Count > 0)
                    {
                        continue;
                    }

                    totalTokens += taskTokens;
                    totalTexts += task.texts.Length;
                    tasks.Add(task);
                    if (task.retryCount > 0) //错过就单独处理
                        break;
                }
            }
        }

        return tasks;
    }

    public int GetTaskCount()
    {
        lock (_lockObject)
        {
            return _taskDatas.Count;
        }
    }
}
