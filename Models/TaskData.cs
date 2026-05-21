using System.Threading.Tasks;

public class TaskData
{
    public string[] texts { get; set; } = new string[0];
    public string[]? result { get; set; } = null;
    public int reqID { get; set; }
    public int retryCount { get; set; } = 0;

    private TaskState _state = TaskState.Waiting;
    private readonly object _stateLock = new object();
    private readonly TaskCompletionSource<bool> _completionSource = new TaskCompletionSource<bool>();

    public TaskState state
    {
        get
        {
            lock (_stateLock) return _state;
        }
        set
        {
            lock (_stateLock)
            {
                _state = value;
                if (_state == TaskState.Completed || _state == TaskState.Failed)
                {
                    _completionSource.TrySetResult(true);
                }
            }
        }
    }

    public Task WaitOne()
    {
        return _completionSource.Task;
    }
}
