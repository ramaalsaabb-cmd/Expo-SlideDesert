using System;
using V3 = System.Numerics.Vector3;

/// <summary>Position-based rope solver independent of Unity rendering.</summary>
public sealed class TowRopeSimulation
{
    public readonly V3[] Points;
    readonly V3[] previous;
    V3 lastStart, lastEnd;
    public float Length;
    public float Damping = 0.7f;
    public int Iterations = 24;
    public Func<V3, V3> ResolveGround;
    public float EffectiveLength { get; private set; }

    public TowRopeSimulation(int count, float length, V3 start, V3 end)
    {
        Points = new V3[Math.Max(8, count)];
        previous = new V3[Points.Length];
        Length = Math.Max(0.1f, length);
        Reset(start, end);
    }

    public void Reset(V3 start, V3 end)
    {
        lastStart = start; lastEnd = end;
        float span = V3.Distance(start,end);
        float slack = Math.Min(2f, (float)Math.Sqrt(Math.Max(0f,Length*Length-span*span))*0.15f);
        V3 side = V3.Cross(end-start,V3.UnitY);
        if (side.LengthSquared() < 0.001f) side = V3.UnitX;
        side = V3.Normalize(side);
        for (int i=0;i<Points.Length;i++)
        {
            float t=(float)i/(Points.Length-1);
            V3 p=V3.Lerp(start,end,t) + side*(float)Math.Sin(t*Math.PI*2)*slack
                - V3.UnitY*(float)Math.Sin(t*Math.PI)*Math.Min(1f,slack);
            if (ResolveGround != null && i>0 && i<Points.Length-1) p=ResolveGround(p);
            Points[i]=previous[i]=p;
        }
        Points[0]=start; Points[Points.Length-1]=end;
    }

    public void Step(float deltaTime, V3 start, V3 end)
    {
        if (deltaTime <= 0f) return;
        if (V3.Distance(lastStart,start)>8f || V3.Distance(lastEnd,end)>8f) Reset(start,end);
        float dt=Math.Min(deltaTime,1f/15f);
        int steps=Math.Max(1,(int)Math.Ceiling(dt*120f));
        float h=dt/steps;
        for (int sub=0;sub<steps;sub++)
        {
            float t=(float)(sub+1)/steps;
            V3 a=V3.Lerp(lastStart,start,t), b=V3.Lerp(lastEnd,end,t);
            // Scripted endpoints can exceed physical rope length during a turn.
            // Stretch only the visual tether instead of moving the tracked player.
            EffectiveLength=Math.Max(Length,V3.Distance(a,b)*1.002f);
            float segment=EffectiveLength/(Points.Length-1);
            float retention=(float)Math.Exp(-Math.Max(0f,Damping)*h);
            for(int i=1;i<Points.Length-1;i++)
            {
                V3 current=Points[i];
                Points[i]+= (current-previous[i])*retention + new V3(0,-9.81f*h*h,0);
                previous[i]=current;
            }
            for(int iteration=0;iteration<Math.Max(4,Iterations);iteration++)
            {
                Points[0]=a; Points[Points.Length-1]=b;
                // Alternate direction to avoid concentrating error at one hitch.
                for(int k=0;k<Points.Length-1;k++)
                {
                    int i=iteration%2==0 ? k : Points.Length-2-k;
                    V3 d=Points[i+1]-Points[i];
                    float distance=d.Length();
                    if(distance < 0.00001f) continue;
                    V3 correction=d*((distance-segment)/distance);
                    if(i==0) Points[i+1]-=correction;
                    else if(i+1==Points.Length-1) Points[i]+=correction;
                    else { Points[i]+=correction*0.5f; Points[i+1]-=correction*0.5f; }
                }
                if (ResolveGround != null && iteration%4==3)
                    for(int i=1;i<Points.Length-1;i++) Points[i]=ResolveGround(Points[i]);
            }
            Points[0]=a; Points[Points.Length-1]=b;
        }
        lastStart=start; lastEnd=end;
    }
}
