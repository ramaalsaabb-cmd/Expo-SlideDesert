using System.Net;
using System.Net.Sockets;
using UnityEngine;
public class FlyptSender : MonoBehaviour
{
    [Header("FlyPT Custom UDP source")]
    public string host = "127.0.0.1";
    public int port = 55278;
[Header("Channel map")]
public int offsetRoll = 0;
public int offsetBump = 4;
public int offsetFlag = 8;
public int offsetDirection = 12;

HardwareOutput hw;
UdpClient udp;
byte[] packet = new byte[16];

void Awake()
{
    hw = GetComponent<HardwareOutput>();

    if (hw == null)
    {
        Debug.LogError("FlyptSender: HardwareOutput not found on this GameObject.");
        enabled = false;
        return;
    }

    udp = new UdpClient();
    udp.Connect(host, port);
}

void FixedUpdate()
{
    if (hw == null) return;

    Put(offsetRoll, hw.hardwareRoll);
    Put(offsetBump, hw.hardwareBump);
    Put(offsetFlag, hw.bumpDidHappen ? 1f : 0f);
    Put(offsetDirection, DirectionToFloat(hw.turnDirection));

    udp.Send(packet, packet.Length);
}

void Put(int offset, float value)
{
    if (offset < 0 || offset + 4 > packet.Length) return;

    byte[] bytes = System.BitConverter.GetBytes(value);
    System.Buffer.BlockCopy(bytes, 0, packet, offset, 4);
}

static float DirectionToFloat(string direction)
{
    if (direction == "LEFT") return -1f;
    if (direction == "RIGHT") return 1f;
    return 0f;
}

void OnDestroy()
{
    udp.Dispose();
}
}