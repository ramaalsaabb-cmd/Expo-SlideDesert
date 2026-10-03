using UnityEngine;
using NativeWebSocket;
using System.Text;

public class WebSocketSender : MonoBehaviour
{
    [Header("References")]
    public HardwareOutput hardwareOutput;

    [Header("WebSocket")]
    public string serverURL = "ws://localhost:8080";

    [Header("Send Rate")]
    public float sendRate = 20f; // 20 messages per second

    private WebSocket websocket;
    private float sendTimer = 0f;

    async void Start()
    {
        websocket = new WebSocket(serverURL);

        websocket.OnOpen += () =>
        {
            Debug.Log("WebSocket Connected!");
        };

        websocket.OnError += (error) =>
        {
            Debug.LogError("WebSocket Error: " + error);
        };

        websocket.OnClose += (code) =>
        {
            Debug.Log("WebSocket Closed!");
        };

        await websocket.Connect();
    }

    async void Update()
    {
        if (websocket == null || hardwareOutput == null)
            return;

        if (websocket.State != WebSocketState.Open)
            return;

        // Count time
        sendTimer += Time.deltaTime;

        // Send only at selected rate
        if (sendTimer >= 1f / sendRate)
        {
            sendTimer = 0f;

            string message =
                "{"
                + "\"roll\":" + hardwareOutput.hardwareRoll + ","
                + "\"bump\":" + hardwareOutput.hardwareBump + ","
                + "\"speed\":" + hardwareOutput.hardwareSpeed + ","
                + "\"direction\":\"" + hardwareOutput.turnDirection + "\""
                + "}";

            await websocket.SendText(message);
        }
    }

    async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }
}