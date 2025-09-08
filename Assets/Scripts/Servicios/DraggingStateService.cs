using System;

public class DraggingStateService
{
    public bool IsDraggingAny { get; private set; }
    public event Action<bool> OnChanged;

    public void Set(bool value)
    {
        if (IsDraggingAny == value) return;
        IsDraggingAny = value;
        OnChanged?.Invoke(value);
    }
}
