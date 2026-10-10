namespace IoTPlatform.Web.Services;

public record Toast(Guid Id, String Message, Boolean IsError);

/// <summary>界面提示消息（每个 Blazor 线路一个实例）</summary>
public class ToastService
{
    private readonly List<Toast> _items = [];

    public IReadOnlyList<Toast> Items => _items;

    public event Action? Changed;

    public void Success(String message) => Add(message, false);

    public void Error(String message) => Add(message, true);

    public void Remove(Guid id)
    {
        _items.RemoveAll(e => e.Id == id);
        Changed?.Invoke();
    }

    private void Add(String message, Boolean isError)
    {
        var toast = new Toast(Guid.NewGuid(), message, isError);
        _items.Add(toast);
        Changed?.Invoke();

        _ = Task.Delay(isError ? 6000 : 3500).ContinueWith(_ => Remove(toast.Id));
    }
}
