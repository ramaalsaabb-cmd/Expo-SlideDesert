using System;
using UnityEngine;
using NativeWebSocket;

[DefaultExecutionOrder(1000)]
public class WebSocketSender : MonoBehaviour
{
    public HardwareOutput hardwareOutput;
    public string serverURL = "ws://192.168.4.1:8080";
    public float sendRate = 20f;
    public string connectionStatus = "Not started";
    public string lastConnectionError = "";
    public int connectionAttempts;
    WebSocket websocket;
    float sendTimer;
    bool connected, sending, closing, connecting, retryScheduled;
    const float ReconnectDelay = 1f;
    const float ConnectTimeout = 10f;
    float retryAt, connectDeadline;

    bool IsCurrent(WebSocket socket) { return !closing && ReferenceEquals(websocket,socket); }
    void Retry(WebSocket socket,string status,string error = null)
    {
        if(!IsCurrent(socket))return;
        connected=false;connecting=false;
        if(error!=null)lastConnectionError=error;
        if(!retryScheduled){retryScheduled=true;retryAt=Time.unscaledTime+ReconnectDelay;}
        connectionStatus=status+"; retrying in 1s";
        Debug.LogWarning("[ESP] "+connectionStatus,this);
    }

    public static WebSocketSender EnsureForScene(UnityEngine.SceneManagement.Scene scene)
    {
        HardwareOutput output = null;
        WebSocketSender sender = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (!output) output = root.GetComponentInChildren<HardwareOutput>(true);
            if (!sender) sender = root.GetComponentInChildren<WebSocketSender>(true);
        }
        if (!sender || !sender.gameObject.activeInHierarchy)
        {
            var host = new GameObject("ESP Connection");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host,scene);
            sender = host.AddComponent<WebSocketSender>();
            Debug.Log("[ESP] Attached missing connection component.",sender);
        }
        if (!sender.hardwareOutput) sender.hardwareOutput = output;
        if (string.IsNullOrWhiteSpace(sender.serverURL) || sender.serverURL.Contains("localhost"))
            sender.serverURL = "ws://192.168.4.1:8080";
        sender.enabled = true;
        if (!output) Debug.LogWarning("[ESP] HardwareOutput missing; connection can start, but telemetry will wait.",sender);
        return sender;
    }
    void Start() { BeginConnection(); }
    async void BeginConnection()
    {
        if(closing||connecting)return;
        // Replace a failed socket; callbacks from that old socket cannot affect the new one.
        var previous=websocket;
        websocket=null;
        try{previous?.CancelConnection();}catch(Exception){}
        connecting=true;connected=false;sending=false;retryScheduled=false;sendTimer=0f;
        connectDeadline=Time.unscaledTime+ConnectTimeout;
        connectionAttempts++;
        connectionStatus="Connecting to "+serverURL;
        Debug.Log("[ESP] "+connectionStatus,this);
        WebSocket socket=null;
        try
        {
            socket=new WebSocket(serverURL);websocket=socket;
            socket.OnOpen+=()=>{
                if(!IsCurrent(socket))return;
                connected=true;connecting=false;retryScheduled=false;
                connectionStatus="Connected";lastConnectionError="";
                Debug.Log("WebSocket Connected! "+serverURL,this);
            };
            socket.OnError+=error=>Retry(socket,"Connection error",error);
            socket.OnClose+=code=>Retry(socket,"Closed: "+code);
            // NativeWebSocket's Connect task lasts until the receive loop ends.
            await socket.Connect();
        }
        catch(Exception error)
        {
            if(socket!=null)Retry(socket,"Connection error",error.ToString());
            else if(!closing)
            {
                connecting=false;retryScheduled=true;retryAt=Time.unscaledTime+ReconnectDelay;
                lastConnectionError=error.ToString();connectionStatus="Connection error; retrying in 1s";
            }
        }
    }
    async void LateUpdate()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if(websocket != null) websocket.DispatchMessageQueue();
#endif
        if(closing)return;
        if(connecting && Time.unscaledTime>=connectDeadline)
            Retry(websocket,"Connection timed out","Connection did not open within 10 seconds.");
        if(retryScheduled && Time.unscaledTime>=retryAt)BeginConnection();
        if (!connected || sending || !hardwareOutput || !hardwareOutput.isActiveAndEnabled) return;
        sendTimer += Time.deltaTime;
        if(sendTimer < 1f/Mathf.Max(1f,sendRate)) return;
        sendTimer %= 1f/Mathf.Max(1f,sendRate);sending = true;
        var socket=websocket;
        try
        {
            string message = HardwareSignalFormat.Message(hardwareOutput.hardwareRoll,hardwareOutput.hardwareBump);
            await socket.SendText(message);
        }
        catch(Exception error) { Retry(socket,"Send error",error.ToString()); }
        finally { if(IsCurrent(socket))sending=false; }
    }
    // A temporary component disable must not permanently shut the connection down.
    void OnApplicationQuit() { CloseConnection(); }
    void OnDestroy() { CloseConnection(); }
    async void CloseConnection()
    {
        if(closing)return;
        closing=true;retryScheduled=false;connecting=false;
        if(websocket==null)return;
        try
        {
            if(connected) await websocket.Close();
            websocket.CancelConnection();
        }
        catch(Exception) { }
        connected = false;
    }
}
