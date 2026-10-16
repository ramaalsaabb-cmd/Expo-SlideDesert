using UnityEngine;

/// <summary>Holds only the two translation drivers; hardware and sled motion remain live.</summary>
[DefaultExecutionOrder(-12000)]
public sealed class KatheebGameplayStartAdapter : MonoBehaviour
{
    public CarPathFollower carMovement;
    public SledFollowCar sledFollowing;
    bool carWasEnabled,sledWasEnabled,held,begun;
    public bool HasBegun=>begun;
    void Awake(){Hold();}
    public void Hold()
    {
        if(held||begun)return;
        if(!carMovement){Debug.LogError("KATHEEB: existing CarPathFollower is not assigned.",this);return;}
        carWasEnabled=carMovement.enabled;carMovement.enabled=false;
        if(sledFollowing){sledWasEnabled=sledFollowing.enabled;sledFollowing.enabled=false;}
        held=true;
    }
    public void BeginOnce()
    {
        if(begun)return;begun=true;
        if(carMovement&&carWasEnabled)carMovement.enabled=true;
        if(sledFollowing&&sledWasEnabled)sledFollowing.enabled=true;
        Debug.Log("[KATHEEB] Countdown complete; existing movement drivers resumed once. Hardware components were not paused.",this);
    }
    void OnDestroy()
    {
        if(!held||begun)return;
        if(carMovement&&carWasEnabled)carMovement.enabled=true;
        if(sledFollowing&&sledWasEnabled)sledFollowing.enabled=true;
    }
}
