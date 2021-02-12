using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    private int nextID = 0; // TODO: mutex lock?!

    public List<TimeMachineController> timeMachines = new List<TimeMachineController>();
    public PlayerController player;

    public List<PlayerController> pastPlayers = new List<PlayerController>();

    private List<ITimeTracker> timeTrackerObjects = new List<ITimeTracker>();
    private Dictionary<int, List<Dictionary<string, object>>> snapshotHistoryById = new Dictionary<int, List<Dictionary<string, object>>>();
    private Dictionary<int, int> historyStartById = new Dictionary<int, int>();
    private int timeStep = 0;
    private int furthestTimeStep = 0;

    void Start()
    {
        //TODO: assert player not null
        player.Init(this, nextID++);
        timeTrackerObjects.Add(player);

        foreach (var timeMachine in timeMachines)
        {
            timeMachine.Init(this, nextID++);
            timeTrackerObjects.Add(timeMachine);
        }
    }

    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        LoadSnapshot();

        bool doTimeTravel = false;
        TimeMachineController targetTimeMachine = null;
        if (player.IsActivating)
        {
            foreach (var timeMachine in timeMachines)
            {
                if (timeMachine.IsTouching(player.gameObject))
                {
                    targetTimeMachine = timeMachine;
                    timeMachine.Activate(timeStep, player, out doTimeTravel);
                    break;
                }
            }
        }

        SaveSnapshot();
        //ValidatePastPlayers();
        player.ClearActivate();

        if (doTimeTravel)
        {
            DoTimeTravel(targetTimeMachine, player);
        }
    }

    void LoadSnapshot()
    {
        if (NumActiveTimeMachines() == 0) return;
        //if (timeStep <= furthestTimeStep) return;

        foreach (ITimeTracker timeTracker in timeTrackerObjects)
        {
            if (snapshotHistoryById.TryGetValue(timeTracker.ID, out var history))
            {
                int startTimeStep = historyStartById[timeTracker.ID];
                int relativeSnapshotIndex = timeStep - startTimeStep;

                if (relativeSnapshotIndex > 0 && relativeSnapshotIndex < history.Count)
                {
                    timeTracker.LoadSnapshot(history[relativeSnapshotIndex]);
                }
                else
                {
                    //TODO: pool or deactivate object
                }
            }
            else
            {
                //TODO: pool or deactivate object
            }
        }
    }

    void SaveSnapshot()
    {
        if (NumActiveTimeMachines() == 0) return;

        foreach (ITimeTracker timeTracker in timeTrackerObjects)
        {
            if (!snapshotHistoryById.TryGetValue(timeTracker.ID, out var history))
            {
                history = snapshotHistoryById[timeTracker.ID] = new List<Dictionary<string, object>>();
                historyStartById[timeTracker.ID] = timeStep;
            }

            int startTimeStep = historyStartById[timeTracker.ID];
            int relativeSnapshotIndex = timeStep - startTimeStep;
            if (relativeSnapshotIndex >= history.Count)
            {
                var frame = new Dictionary<string, object>();
                timeTracker.SaveSnapshot(frame);
                history.Add(frame);
            }
        }

        timeStep++;
    }

    public int NumActiveTimeMachines()
    {
        int result = 0;
        foreach (var timeMachine in timeMachines)
        {
            if (timeMachine.IsActivated || timeMachine.Occupied)
                result++;
        }
        return result;
    }

    public void DoTimeTravel(TimeMachineController timeMachine, PlayerController player)
    {
        // TODO: if/when adding lerping to updates need to force no lerp when travelling in time

        furthestTimeStep = Mathf.Max(timeStep, furthestTimeStep);
        timeStep = timeMachine.ActivatedTimeStep;

        player.PlayerInput.enabled = false;

        PlayerController newPlayer = Instantiate(player);
        newPlayer.PlayerInput.enabled = true;
        newPlayer.Init(this, nextID++);
        timeTrackerObjects.Add(newPlayer);

        this.player = newPlayer;
        pastPlayers.Add(player);
    }
}
