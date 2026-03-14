public struct InputStateTracker
{
    public InputFlags currentFlags;
    public InputFlags previousFlags;

    private int holdUp;
    private int holdDown;
    private int holdForward;
    private int holdBack;
    private int holdLP;
    private int holdRP;
    private int holdLK;
    private int holdRK;

    public void Initialize()
    {
        holdUp = 0; holdDown = 0; holdForward = 0; holdBack = 0;
        holdLP = 0; holdRP = 0; holdLK = 0; holdRK = 0;
        currentFlags = InputFlags.None;
        previousFlags = InputFlags.None;
    }

    public void UpdateTick(InputFlags rawFlags)
    {
        previousFlags = currentFlags;
        currentFlags = ResolveSOCD(rawFlags);

        bool isUpPressed = (currentFlags & InputFlags.Up) != 0;
        holdUp = isUpPressed ? holdUp + 1 : 0;

        bool isDownPressed = (currentFlags & InputFlags.Down) != 0;
        holdDown = isDownPressed ? holdDown + 1 : 0;

        bool isForwardPressed = (currentFlags & InputFlags.Forward) != 0;
        holdForward = isForwardPressed ? holdForward + 1 : 0;

        bool isBackPressed = (currentFlags & InputFlags.Back) != 0;
        holdBack = isBackPressed ? holdBack + 1 : 0;

        bool isLPPressed = (currentFlags & InputFlags.LP) != 0;
        holdLP = isLPPressed ? holdLP + 1 : 0;

        bool isRPPressed = (currentFlags & InputFlags.RP) != 0;
        holdRP = isRPPressed ? holdRP + 1 : 0;

        bool isLKPressed = (currentFlags & InputFlags.LK) != 0;
        holdLK = isLKPressed ? holdLK + 1 : 0;

        bool isRKPressed = (currentFlags & InputFlags.RK) != 0;
        holdRK = isRKPressed ? holdRK + 1 : 0;
    }

    public bool IsHeld(InputFlags flag)
    {
        return (currentFlags & flag) != 0;
    }

    public bool IsJustPressed(InputFlags flag)
    {
        return (currentFlags & flag) != 0 && (previousFlags & flag) == 0;
    }

    public int GetHoldDuration(InputFlags flag)
    {
        if ((flag & InputFlags.Up) != 0) return holdUp;
        if ((flag & InputFlags.Down) != 0) return holdDown;
        if ((flag & InputFlags.Forward) != 0) return holdForward;
        if ((flag & InputFlags.Back) != 0) return holdBack;
        if ((flag & InputFlags.LP) != 0) return holdLP;
        if ((flag & InputFlags.RP) != 0) return holdRP;
        if ((flag & InputFlags.LK) != 0) return holdLK;
        if ((flag & InputFlags.RK) != 0) return holdRK;
        return 0;
    }

    private InputFlags ResolveSOCD(InputFlags flags)
    {
        bool isUpPressed = (flags & InputFlags.Up) != 0;
        bool isDownPressed = (flags & InputFlags.Down) != 0;
        bool isForwardPressed = (flags & InputFlags.Forward) != 0;
        bool isBackPressed = (flags & InputFlags.Back) != 0;

        bool isVerticalConflict = isUpPressed && isDownPressed;
        if (isVerticalConflict)
        {
            flags &= ~InputFlags.Up;
            flags &= ~InputFlags.Down;
        }

        bool isHorizontalConflict = isForwardPressed && isBackPressed;
        if (isHorizontalConflict)
        {
            flags &= ~InputFlags.Forward;
            flags &= ~InputFlags.Back;
        }

        return flags;
    }
}