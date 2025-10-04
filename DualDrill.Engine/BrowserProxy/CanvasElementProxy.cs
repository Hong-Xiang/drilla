using Microsoft.JSInterop;

namespace DualDrill.Engine.BrowserProxy;

public sealed record class CanvasPointerEvent(int OffsetX, int OffsetY, int Width, int Height)
{

}

public interface ICanvasElementObserver
{
    void OnPointerDown(CanvasPointerEvent e);
    void OnPointerMove(CanvasPointerEvent e);
    void OnPointerUp(CanvasPointerEvent e);
}

public sealed class CanvasElementProxy(
    JSClientModule Module
) : ICanvasElementObserver
{
    event EventHandler<CanvasPointerEvent> PointerDown;
    public IJSObjectReference Element { get; private set; }

    public async ValueTask<CanvasElementProxy> CreateAsync(
        JSClientModule module
    )
    {
        var result = new CanvasElementProxy(module);
        //module.CreateCanvasElementAsync(this);
        throw new NotImplementedException();
        return result;
    }

    [JSInvokable]
    public void OnPointerDown(CanvasPointerEvent pointerEvent)
    {
        PointerDown.Invoke(this, pointerEvent);
    }

    [JSInvokable]
    public void OnPointerMove(CanvasPointerEvent pointerEvent)
    {
        PointerDown.Invoke(this, pointerEvent);
    }

    [JSInvokable]
    public void OnPointerUp(CanvasPointerEvent pointerEvent)
    {
        PointerDown.Invoke(this, pointerEvent);
    }
}
