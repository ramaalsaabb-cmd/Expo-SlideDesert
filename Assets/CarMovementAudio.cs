using UnityEngine;
using UnityEngine.Video;

/// <summary>Two quiet driving layers; both follow the car's actual movement state.</summary>
[DisallowMultipleComponent]
public class CarMovementAudio : MonoBehaviour
{
    public CarPathFollower movement;
    public AudioClip driftingSound;
    public VideoClip ambienceSound;
    [Range(0f,1f)] public float driftingVolume=.30f;
    [Range(0f,1f)] public float ambienceVolume=.10f;
    AudioSource drifting, ambience;
    VideoPlayer player;
    bool playing, failed;
    void Awake()
    {
        if(!movement)movement=GetComponent<CarPathFollower>();
        drifting=gameObject.AddComponent<AudioSource>();Configure(drifting,driftingVolume);drifting.clip=driftingSound;drifting.loop=true;
        ambience=gameObject.AddComponent<AudioSource>();Configure(ambience,ambienceVolume);
        player=gameObject.AddComponent<VideoPlayer>();player.playOnAwake=false;player.renderMode=VideoRenderMode.APIOnly;player.isLooping=true;player.waitForFirstFrame=false;player.skipOnDrop=true;
        player.source=VideoSource.VideoClip;player.clip=ambienceSound;player.audioOutputMode=VideoAudioOutputMode.AudioSource;player.controlledAudioTrackCount=1;player.EnableAudioTrack(0,true);player.SetTargetAudioSource(0,ambience);
        player.errorReceived+=OnVideoError;
        if(driftingSound)driftingSound.LoadAudioData();
        if(ambienceSound)player.Prepare();
    }
    static void Configure(AudioSource source,float volume)
    {
        source.playOnAwake=false;source.volume=volume;source.spatialBlend=0;source.dopplerLevel=0;source.priority=150;
    }
    void LateUpdate()
    {
        bool moving=Time.timeScale>0&&movement&&movement.enabled&&movement.CurrentSpeed>.08f;
        if(!moving){if(playing)StopBoth();return;}
        if(playing||failed||!driftingSound||!ambienceSound)return;
        // Prepare both before starting either layer, so they always begin together.
        if(!player.isPrepared)return;
        if(driftingSound.loadState!=AudioDataLoadState.Loaded)return;
        drifting.volume=driftingVolume;ambience.volume=ambienceVolume;
        drifting.timeSamples=0;player.time=0;
        drifting.Play();player.Play();playing=true;
    }
    void StopBoth()
    {
        if(drifting)drifting.Stop();
        // Pause retains the decoded video audio, ready for another synchronized start.
        if(player){player.Pause();player.time=0;}
        if(ambience)ambience.Stop();playing=false;
    }
    void OnVideoError(VideoPlayer source,string message){failed=true;StopBoth();Debug.LogError("Driving ambience could not load: "+message,this);}
    void OnDisable(){StopBoth();}
    void OnDestroy(){if(player)player.errorReceived-=OnVideoError;}
}




