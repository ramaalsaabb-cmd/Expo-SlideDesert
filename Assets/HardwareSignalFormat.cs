using System;
using System.Globalization;

/// <summary>Wire contract: roll,bump; exactly two finite numeric values.</summary>
public static class HardwareSignalFormat
{
    static bool Finite(float value) { return !float.IsNaN(value)&&!float.IsInfinity(value); }
    public static float Roll(float value,float maximum)
    {
        if(!Finite(value)||!Finite(maximum)||maximum<=0f)return 0f;
        return Math.Max(-1f,Math.Min(1f,value/maximum))*10f;
    }
    public static float Bump(float value,float maximum)
    {
        if(!Finite(value)||!Finite(maximum)||maximum<=0f)return 0f;
        return Math.Min(1f,Math.Abs(value)/maximum)*10f;
    }
    public static string Message(float roll,float bump)
    {
        roll=Finite(roll)?Math.Max(-10f,Math.Min(10f,roll)):0f;
        bump=Finite(bump)?Math.Max(0f,Math.Min(10f,bump)):0f;
        return roll.ToString("0",CultureInfo.InvariantCulture)+","+bump.ToString("0",CultureInfo.InvariantCulture);
    }
}
