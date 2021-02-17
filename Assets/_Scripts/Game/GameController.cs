﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameController : MonoBehaviour
{
    public const string FLAG_DESTROY = "FLAG_DESTROY";
    public const int TIME_STEP_SKIP_AMOUNT = 100;

    private int nextID = 0; // TODO: mutex lock?!

    public List<TimeMachineController> timeMachines = new List<TimeMachineController>();
    public PlayerController player;
    public PlayerController playerPrefab;
    // visuals
    public TMP_Text timerText;

    private Pool<PlayerController> playerObjectPool;
    public List<PlayerController> pastPlayers = new List<PlayerController>();

    private List<ITimeTracker> timeTrackerObjects = new List<ITimeTracker>();
    private Dictionary<int, List<Dictionary<string, object>>> snapshotHistoryById = new Dictionary<int, List<Dictionary<string, object>>>();
    private Dictionary<int, int> historyStartById = new Dictionary<int, int>();
    private int timeStep = 0;
    private int furthestTimeStep = 0;
    private bool isPresent = true;
    private bool doTimeSkip = false;

    public int TimeStep => timeStep;

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

        playerObjectPool = new Pool<PlayerController>(() => Instantiate(player), x => x.gameObject.SetActive(true), x => x.gameObject.SetActive(false));
    }

    void Update()
    {
        timerText.text = $"Debug Timer:\n{timeStep}";
    }

    private void FixedUpdate()
    {
        DoTimeStep();

        if (doTimeSkip)
        {
            doTimeSkip = false;

            Physics2D.simulationMode = SimulationMode2D.Script;
            for (int i = 0; i < TIME_STEP_SKIP_AMOUNT; i++)
            {
                Debug.Log(Time.fixedDeltaTime);
                Physics2D.Simulate(Time.fixedDeltaTime);
                DoTimeStep();
            }
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        }
    }

    public void DoTimeStep()
    {
        LoadSnapshot();

        // restore history to current state if back to present
        if (timeStep == furthestTimeStep && !isPresent)
        {
            foreach (var timeMachine in timeMachines)
            {
                timeMachine.BackToPresent();
            }
            isPresent = true;
        }

        int timeTravelStep = -1;
        bool didActivate = false;
        TimeMachineController targetTimeMachine = null;
        if (player.IsActivating)
        {
            foreach (var timeMachine in timeMachines)
            {
                if (timeMachine.IsTouching(player.gameObject))
                {
                    targetTimeMachine = timeMachine;
                    didActivate = timeMachine.Activate(timeStep, player, out timeTravelStep);
                    if (timeTravelStep >= 0)
                    {
                        player.FlagDestroy = true;
                    }
                    break;
                }
            }
        }

        SaveSnapshot(didActivate);
        timeStep++;
        ValidateTimeAnomolies();
        player.ClearActivate();

        if (timeTravelStep >= 0)
        {
            DoTimeTravel(timeTravelStep, targetTimeMachine, player);
        }
    }

    public void SkipTime()
    {
        doTimeSkip = true;
    }

    void LoadSnapshot()
    {
        if (NumActiveTimeMachines() == 0) return;
        //if (timeStep <= furthestTimeStep) return;

        // instantiate objects not present
        foreach (var kvp in snapshotHistoryById)
        {
            int id = kvp.Key;
            var history = kvp.Value;

            if (player.ID == id || timeTrackerObjects.Exists(x => x.ID == id)) continue; // exists not most efficient, need better structure to store all time-tracking objects (prolly a dict)

            int startTimeStep = historyStartById[id];
            int relativeSnapshotIndex = timeStep - startTimeStep;
            if (relativeSnapshotIndex >= 0 && relativeSnapshotIndex < history.Count)
            {
                // TODO: better structure to determine type of object and instantiate from appropriate pool (instead of just players)
                PlayerController newPlayer = playerObjectPool.Aquire();
                newPlayer.Init(this, id);
                newPlayer.PlayerInput.enabled = false;
                timeTrackerObjects.Add(newPlayer);
            }
        }

        for(int i = timeTrackerObjects.Count-1; i >= 0; i--)
        {
            ITimeTracker timeTracker = timeTrackerObjects[i];

            if (snapshotHistoryById.TryGetValue(timeTracker.ID, out var history))
            {
                int startTimeStep = historyStartById[timeTracker.ID];
                int relativeSnapshotIndex = timeStep - startTimeStep;

                if (relativeSnapshotIndex >= 0 && relativeSnapshotIndex < history.Count)
                {
                    if (history[relativeSnapshotIndex].ContainsKey(FLAG_DESTROY))
                    {
                        PoolObject(timeTracker);
                        timeTrackerObjects.RemoveAt(i);
                    }
                    else
                    {
                        timeTracker.LoadSnapshot(history[relativeSnapshotIndex]);
                    }
                }
                else
                {
                    //PoolObject(timeTracker);
                    //timeTrackerObjects.RemoveAt(i);
                }
            }
            else
            {
                //PoolObject(timeTracker);
                //timeTrackerObjects.RemoveAt(i);
            }
        }
    }

    private void PoolObject(ITimeTracker timeTracker)
    {
        if (timeTracker is PlayerController)
        {
            playerObjectPool.Release(timeTracker as PlayerController);
        }
    }

    void SaveSnapshot(bool force)
    {
        if (!force && NumActiveTimeMachines() == 0) return;

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
            else if (relativeSnapshotIndex >= 0 && timeTracker is TimeMachineController)
            {
                // time machines can re-write their past state if they are used
                var frame = history[relativeSnapshotIndex];
                frame.Clear();
                timeTracker.SaveSnapshot(frame);
            }
        }
    }

    private void ValidateTimeAnomolies()
    {
        // ensure that past player's paths of motion are uninterrupted

        // ensure that time machines are not used in such a way that it prevents a past player from going to the time they intended
        foreach (TimeMachineController timeMachine in timeMachines)
        {
            if (timeMachine.CurrentlyActivated && timeMachine.HistoryActivated)
            {
                Debug.LogError("Time anamoly, restart from previous spawn point or retry level!");
            }
        }
    }

    public int NumActiveTimeMachines()
    {
        int result = 0;
        foreach (var timeMachine in timeMachines)
        {
            if (timeMachine.IsActivatedOrOccupied)
                result++;
        }
        return result;
    }

    public void DoTimeTravel(int timeTravelStep, TimeMachineController timeMachine, PlayerController player)
    {
        // TODO: if/when adding lerping to updates need to force no lerp when travelling in time
        isPresent = false;

        furthestTimeStep = Mathf.Max(timeStep, furthestTimeStep);
        timeStep = timeTravelStep;
        timeMachine.CurrentlyOccupied = true;

        player.PlayerInput.enabled = false;
        player.FlagDestroy = false;

        PlayerController newPlayer = playerObjectPool.Aquire();
        newPlayer.PlayerInput.enabled = true;
        newPlayer.Init(this, nextID++);
        timeTrackerObjects.Add(newPlayer);

        this.player = newPlayer;
        pastPlayers.Add(player);

        foreach (TimeMachineController otherMachine in timeMachines)
        {
            otherMachine.DoTimeTravel(otherMachine == timeMachine);
        }
    }
}
