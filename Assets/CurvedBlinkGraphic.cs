using UnityEngine;
using UnityEngine.UI;

/// <summary>Curved eyelid silhouettes with transparent feathering, using the stereo UI renderer.</summary>
public class CurvedBlinkGraphic : MaskableGraphic
{
    [Range(0f,1f)] public float closure;
    [Range(0.005f,0.08f)] public float feather = 0.025f;
    const int Columns = 64;

    public void SetClosure(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(closure,value)) return;
        closure=value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if(closure<=0.001f) return;
        Rect rect=GetPixelAdjustedRect();
        float gap=(1f-closure)*0.58f;
        float softness=feather*(1f-closure);
        for(int lid=0;lid<2;lid++)
        {
            int start=vh.currentVertCount;
            for(int i=0;i<=Columns;i++)
            {
                float x=(float)i/Columns;
                float ellipse=Mathf.Sqrt(Mathf.Max(0f,1f-Mathf.Pow((x-0.5f)*1.9f,2f)));
                float boundary=lid==0 ? 0.5f+gap*ellipse : 0.5f-gap*ellipse;
                float outer=lid==0 ? 1f : 0f;
                float direction=lid==0 ? -1f : 1f;
                // Feather the moving edge; converge to a fully closed eye.
                float inner=boundary+direction*softness;
                Color solid=color;
                Color transparent=color; transparent.a=0f;
                Add(vh,rect,x,outer,solid);
                Add(vh,rect,x,boundary-direction*softness,solid);
                Add(vh,rect,x,inner,transparent);
                if(i==0) continue;
                int v=start+(i-1)*3;
                Quad(vh,v,v+1,v+4,v+3);
                Quad(vh,v+1,v+2,v+5,v+4);
            }
        }
    }
    static void Add(VertexHelper vh,Rect rect,float x,float y,Color color)
    {
        vh.AddVert(new Vector3(Mathf.Lerp(rect.xMin,rect.xMax,x),
            Mathf.Lerp(rect.yMin,rect.yMax,y),0f),color,Vector2.zero);
    }
    static void Quad(VertexHelper vh,int a,int b,int c,int d)
    {
        vh.AddTriangle(a,b,c); vh.AddTriangle(a,c,d);
    }
}

