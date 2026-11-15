using System.Numerics;
using DoNotRelease.Gameplay;
namespace DoNotRelease;
public static class InteractionSystem
{
    public static InteractionDefinition? Find(IEnumerable<CellInteractable> interactables,float maxDistance,Vector3 origin,Vector3 direction,Func<InteractionDefinition,bool> available,float occlusion=float.MaxValue)
    {
        InteractionDefinition? result=null;var nearest=Math.Min(maxDistance,occlusion+.08f);
        foreach(var def in interactables.Where(x=>x.IsActive).Select(x=>x.Bounds()))
            if(available(def) && RayBox(origin,direction,new Vector3(def.Min[0],def.Min[1],def.Min[2]),new Vector3(def.Max[0],def.Max[1],def.Max[2]),out var t) && t>=0 && t<nearest){nearest=t;result=def;}
        return result;
    }
    public static bool RayBox(Vector3 origin,Vector3 direction,Vector3 min,Vector3 max,out float distance)
    {
        float near=0,far=float.MaxValue;
        for(int i=0;i<3;i++){
            float o=origin[i],d=direction[i];
            if(Math.Abs(d)<.00001f){if(o<min[i]||o>max[i]){distance=0;return false;}continue;}
            float a=(min[i]-o)/d,b=(max[i]-o)/d;if(a>b)(a,b)=(b,a);near=Math.Max(near,a);far=Math.Min(far,b);if(near>far){distance=0;return false;}
        }
        distance=near;return true;
    }
}
