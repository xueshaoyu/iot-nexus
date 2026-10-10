using IoTPlatform.Services;
using IoTPlatform.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using NewLife.Log;

namespace IoTPlatform.Web.Components;

/// <summary>管理页面基类：统一异常提示、确认框与定时刷新</summary>
public abstract class PlatformPage : ComponentBase, IDisposable
{
    [Inject] protected ToastService Toasts { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected IStringLocalizer<SharedResource> Loc { get; set; } = default!;

    private Timer? _timer;
    private Int32 _refreshing;

    /// <summary>执行操作，业务异常转为提示</summary>
    protected async Task<Boolean> Run(Func<Task> action, String? success = null)
    {
        try
        {
            await action();
            if (success != null) Toasts.Success(success);
            return true;
        }
        catch (PlatformException ex)
        {
            Toasts.Error(Translate(ex));
        }
        catch (ArgumentException ex)
        {
            Toasts.Error(ex.Message);
        }
        catch (XCode.XCodeException ex)
        {
            Toasts.Error(ex.Message);
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
            Toasts.Error(Loc["Unexpected error: {0}", ex.Message]);
        }

        return false;
    }

    /// <summary>按当前语言翻译业务异常：模板与字符串参数（如 "Product"）均查找翻译，找不到时保持原文</summary>
    protected String Translate(PlatformException ex) =>
        Loc[ex.Template, ex.Arguments.Select(a => a is String s ? (Object)Loc[s].Value : a ?? "").ToArray()];

    protected Task<Boolean> Run(Action action, String? success = null) => Run(() => { action(); return Task.CompletedTask; }, success);

    protected async Task<Boolean> Confirm(String message) => await JS.InvokeAsync<Boolean>("confirm", message);

    /// <summary>启动定时刷新</summary>
    protected void AutoRefresh(TimeSpan interval, Func<Task> refresh)
    {
        _timer?.Dispose();
        _timer = new Timer(async _ =>
        {
            if (Interlocked.Exchange(ref _refreshing, 1) == 1) return;
            try
            {
                await InvokeAsync(async () =>
                {
                    await refresh();
                    StateHasChanged();
                });
            }
            catch (Exception ex) when (ex is ObjectDisposedException or TaskCanceledException or JSDisconnectedException) { }
            catch (Exception ex) { XTrace.WriteException(ex); }
            finally { Interlocked.Exchange(ref _refreshing, 0); }
        }, null, interval, interval);
    }

    public virtual void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
