using System;

namespace BAHelper.Helpers;

public class Throttle
{
    private long _nextAllowed;
    public bool Exec(Action action, long throttle = 500) //ms
    {
        long now = Environment.TickCount64;
        if (now < _nextAllowed)
            return false;

        action();
        _nextAllowed = now + throttle;
        return true;
    }
}