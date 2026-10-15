using System;

/// <summary>Pure telemetry helpers; units remain degrees, metres/second and a boolean impact.</summary>
public static class SledTelemetryMath
{
    public static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    public static float ClampFinite(float value, float limit)
    {
        if (!IsFinite(value) || !IsFinite(limit)) return 0f;
        limit = Math.Abs(limit);
        return Math.Max(-limit, Math.Min(limit,value));
    }
    public static float Smooth(float previous, float target, float seconds, float dt)
    {
        if (!IsFinite(target)) return 0f;
        if (!IsFinite(previous)) previous=target;
        if (dt<=0f || !IsFinite(dt)) return previous;
        if (seconds<=0f) return target;
        return previous+(target-previous)*(1f-(float)Math.Exp(-dt/seconds));
    }
    public static string Direction(float roll, string previous, float enter, float exit)
    {
        if (!IsFinite(roll)) return "STRAIGHT";
        enter=Math.Max(0.01f,enter);exit=Math.Max(0f,Math.Min(enter,exit));
        if(roll>=enter) return "RIGHT";
        if(roll<=-enter) return "LEFT";
        if(previous=="RIGHT" && roll>exit) return "RIGHT";
        if(previous=="LEFT" && roll<-exit) return "LEFT";
        return "STRAIGHT";
    }
}

/// <summary>Keep a short impact until a successful send, but never replay an old disconnected impact.</summary>
public sealed class SledImpactLatch
{
    public uint Sequence { get; private set; }
    bool pending;
    double expiry;
    public void Trigger(double now, float lifetime)
    { unchecked { Sequence++; } pending=true;expiry=now+Math.Max(0.05f,lifetime); }
    public bool IsPending(double now) { if(now>expiry) pending=false;return pending; }
    public void Acknowledge(uint sequence) { if(sequence==Sequence) pending=false; }
    public void Clear() { pending=false; }
}
