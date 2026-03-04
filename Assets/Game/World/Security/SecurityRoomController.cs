using Unity.Netcode;
using UnityEngine;

public class SecurityRoomController : NetworkBehaviour
{
    [Header("Buttons")]
    [SerializeField] private SecurityButton button1;
    [SerializeField] private SecurityButton button2;
    [SerializeField] private SecurityButton button3;

    [Header("Plates (all possible plates in scene)")]
    [SerializeField] private PressurePlate[] plates;

    [Header("Elevator")]
    [SerializeField] private ElevatorDoorController elevatorDoor;

    [Header("Rules")]
    [SerializeField] private int maxPlates = 5;
    [SerializeField] private float button2WindowSeconds = 60f;

    // 0 wait b1, 1 plates powered, 2 timer running, 3 b3 enabled, 4 done
    private NetworkVariable<int> stage = new(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<int> requiredPlates = new(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<int> activePlates = new(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<float> windowEndsAtServerTime = new(0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int Stage => stage.Value;
    public int RequiredPlates => requiredPlates.Value;
    public int ActivePlates => activePlates.Value;

    public float GetSecondsRemaining()
    {
        if (NetworkManager.Singleton == null) return 0f;
        float now = (float)NetworkManager.Singleton.ServerTime.Time;
        return Mathf.Max(0f, windowEndsAtServerTime.Value - now);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (plates != null)
        {
            foreach (var p in plates)
                if (p != null) p.RegisterController(this);
        }

        if (IsServer)
            ServerResetForNewRound();
    }

    // Called by SecurityButton on server
    public void ServerOnButtonPressed(int index, ulong presserClientId)
    {
        if (!IsServer) return;

        if (index == 1 && stage.Value == 0)
        {
            ServerStartPlatePhase();
            return;
        }

        if (index == 2 && stage.Value == 2)
        {
            if (GetSecondsRemaining() <= 0f) return;

            button2.ServerSetState(SecurityButton.ButtonState.GreenDone);
            stage.Value = 3;

            button3.ServerSetState(SecurityButton.ButtonState.YellowReady);
            return;
        }

        if (index == 3 && stage.Value == 3)
        {
            button3.ServerSetState(SecurityButton.ButtonState.GreenDone);
            stage.Value = 4;

            if (elevatorDoor != null)
                elevatorDoor.ServerOpenPermanently();

            return;
        }
    }

    public void ServerOnPlateChanged()
    {
        if (!IsServer) return;
        ServerRecountPlates();
        ServerEvaluate();
    }

    private int ServerComputeRequiredPlates()
    {
        // Count alive players (late joiners are killed, so they count as dead anyway)
        var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        int alive = 0;

        foreach (var p in players)
        {
            var ph = p.GetComponent<PlayerHealth>();
            if (ph != null && ph.IsSpawned && !ph.IsDead.Value)
                alive++;
        }

        alive = Mathf.Max(1, alive);

        // Rule: plates = players - 1 (security player stays inside)
        int req = Mathf.Clamp(alive - 1, 1, maxPlates);
        return req;
    }

    private void ServerStartPlatePhase()
    {
        stage.Value = 1;

        requiredPlates.Value = ServerComputeRequiredPlates();

        button1.ServerSetState(SecurityButton.ButtonState.GreenDone);
        button2.ServerSetState(SecurityButton.ButtonState.RedDisabled);
        button3.ServerSetState(SecurityButton.ButtonState.RedDisabled);

        if (plates == null) return;

        // Power first N plates (deterministic order)
        for (int i = 0; i < plates.Length; i++)
        {
            if (plates[i] == null) continue;

            bool on = (i < requiredPlates.Value);
            plates[i].ServerSetPowered(on);
            plates[i].ServerSetLatched(false);
        }

        ServerRecountPlates();
        ServerEvaluate();
    }

    private void ServerRecountPlates()
    {
        int count = 0;

        if (plates != null)
        {
            for (int i = 0; i < plates.Length; i++)
            {
                var p = plates[i];
                if (p != null && p.IsPowered && p.IsActive)
                    count++;
            }
        }

        activePlates.Value = count;
    }

    private void ServerEvaluate()
    {
        if (!IsServer) return;

        // Stage 1 -> if all active, start timer stage and latch
        if (stage.Value == 1 && requiredPlates.Value > 0 && activePlates.Value >= requiredPlates.Value)
        {
            stage.Value = 2;
            button2.ServerSetState(SecurityButton.ButtonState.YellowReady);

            if (plates != null)
            {
                for (int i = 0; i < plates.Length; i++)
                    if (plates[i] != null && plates[i].IsPowered)
                        plates[i].ServerSetLatched(true);
            }

            float now = (float)NetworkManager.Singleton.ServerTime.Time;
            windowEndsAtServerTime.Value = now + button2WindowSeconds;
        }

        // Timer expired
        if (stage.Value == 2 && GetSecondsRemaining() <= 0f)
        {
            ServerFailTimerWindow();
        }
    }

    private void ServerFailTimerWindow()
    {
        stage.Value = 1;
        button2.ServerSetState(SecurityButton.ButtonState.RedDisabled);

        // Unlatch so they must stand again simultaneously
        if (plates != null)
        {
            for (int i = 0; i < plates.Length; i++)
                if (plates[i] != null && plates[i].IsPowered)
                    plates[i].ServerSetLatched(false);
        }

        windowEndsAtServerTime.Value = 0f;

        ServerRecountPlates();
    }

    // Call this if roster changes (death/disconnect) mid puzzle
    public void ServerOnRosterChanged()
    {
        if (!IsServer) return;

        if (stage.Value < 1 || stage.Value > 3)
            return;

        requiredPlates.Value = ServerComputeRequiredPlates();

        if (plates != null)
        {
            for (int i = 0; i < plates.Length; i++)
            {
                if (plates[i] == null) continue;

                bool on = (i < requiredPlates.Value);
                plates[i].ServerSetPowered(on);
                plates[i].ServerSetLatched(stage.Value == 2);
            }
        }

        ServerRecountPlates();
        ServerEvaluate();
    }

    public void ServerResetForNewRound()
    {
        if (!IsServer) return;

        stage.Value = 0;
        requiredPlates.Value = 0;
        activePlates.Value = 0;
        windowEndsAtServerTime.Value = 0f;

        button1.ServerSetState(SecurityButton.ButtonState.YellowReady);
        button2.ServerSetState(SecurityButton.ButtonState.RedDisabled);
        button3.ServerSetState(SecurityButton.ButtonState.RedDisabled);

        if (plates != null)
        {
            foreach (var p in plates)
            {
                if (p == null) continue;
                p.ServerSetPowered(false);
                p.ServerSetLatched(false);
            }
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (stage.Value == 2)
            ServerEvaluate();
    }
}