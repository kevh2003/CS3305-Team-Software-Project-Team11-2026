using System;
using System.Threading.Tasks;

/*
 * INetSession
 * 
 * Networking abstraction used by UI/gameplay
 * Keeps the rest of the project independent from NGO/Relay specifics
 */

public enum NetMode
{
    OnlineRelay,
    Lan
}

public enum NetStartResult
{
    Success,
    NotInitialized,
    InvalidInput,
    ServicesUnavailable,
    TransportError,
    UnknownError
}

public readonly struct LanServerInfo
{
    public readonly string Name;
    public readonly string Address;
    public readonly ushort Port;
    public readonly int CurrentPlayers;
    public readonly int MaxPlayers;
    public readonly float LastSeenSecondsAgo;

    public LanServerInfo(string name, string address, ushort port, int currentPlayers, int maxPlayers, float lastSeenSecondsAgo)
    {
        Name = name;
        Address = address;
        Port = port;
        CurrentPlayers = currentPlayers;
        MaxPlayers = maxPlayers;
        LastSeenSecondsAgo = lastSeenSecondsAgo;
    }
}

public interface INetSession
{
    bool IsHost { get; }
    bool IsClient { get; }
    bool IsServer { get; }
    bool IsConnected { get; }

    int ConnectedPlayers { get; }

    event Action OnConnected;
    event Action<string> OnDisconnected;

    // Online (Relay)
    Task<(NetStartResult result, string joinCode)> HostOnlineAsync(int maxPlayers);
    Task<NetStartResult> JoinOnlineAsync(string joinCode);

    // LAN (Direct)
    NetStartResult HostLanAutoPort(out ushort chosenPort);
    NetStartResult JoinLan(string address, ushort port);

    // Host Authoritative Scene flow
    void LoadSceneForAll(string sceneName);

    void Shutdown();
}