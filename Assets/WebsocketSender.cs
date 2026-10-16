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
    bool connected, sending, closing;

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
    async void Start()
    {
        connectionAttempts++;
        connectionStatus = "Connecting to " + serverURL;
        Debug.Log("[ESP] " + connectionStatus, this);
        try
        {
            websocket = new WebSocket(serverURL);
            websocket.OnOpen += () => {
                connected = true;connectionStatus = "Connected";lastConnectionError = "";
                Debug.Log("WebSocket Connected! " + serverURL);
            };
            websocket.OnError += error => {
                connected = false;lastConnectionError = error;connectionStatus = "Connection error";
                Debug.LogWarning("WebSocket Error: " + error);
            };
            websocket.OnClose += code => {
                connected = false;connectionStatus = "Closed: " + code;
                Debug.LogWarning("WebSocket Closed: " + code);
            };
            // Original connection flow: no forced Android routing or cancellation timer.
            await websocket.Connect();
        }
        catch (Exception error)
        {
            connected = false;lastConnectionError = error.ToString();connectionStatus = "Connection error";
            Debug.LogWarning("[ESP] " + error,this);
        }
    }
    async void LateUpdate()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if(websocket != null) websocket.DispatchMessageQueue();
#endif
        if (!connected || closing || sending || !hardwareOutput || !hardwareOutput.isActiveAndEnabled) return;
        sendTimer += Time.deltaTime;
        if(sendTimer < 1f/Mathf.Max(1f,sendRate)) return;
        sendTimer %= 1f/Mathf.Max(1f,sendRate);sending = true;
        try
        {
            string message = HardwareSignalFormat.Message(hardwareOutput.hardwareRoll,hardwareOutput.hardwareBump);
            await websocket.SendText(message);
        }
        catch(Exception error) { connected=false;lastConnectionError=error.ToString();connectionStatus="Send error";Debug.LogWarning("[ESP] " + error,this); }
        finally { sending=false; }
    }
    // A temporary component disable must not permanently shut the connection down.
    void OnApplicationQuit() { CloseConnection(); }
    void OnDestroy() { CloseConnection(); }
    async void CloseConnection()
    {
        if(closing || websocket == null) return;
        closing = true;
        try
        {
            if(connected) await websocket.Close();
            websocket.CancelConnection();
        }
        catch(Exception) { }
        connected = false;
    }
}
