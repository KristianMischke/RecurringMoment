using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Serialization;

public class TimeAnomalyException : Exception
{
    public TimeAnomalyException()
    {
        
    }
    public TimeAnomalyException(string reason) : base($"Time Anomaly: {reason}")
    {
        
    }
}

public class GameController : MonoBehaviour
{
    public const string FLAG_DESTROY = "FLAG_DESTROY";
    public const int TIME_STEP_SKIP_AMOUNT = 100;
    public const int TIME_SKIP_ANIMATE_FPS = 10;
    public const int TIME_TRAVEL_REWIND_MULT = 10;
    public const float POSITION_ANOMALY_ERROR = 0.75f;

    public List<TimeMachineController> timeMachines = new List<TimeMachineController>();
    public List<BasicTimeTracker> basicTimeTrackers = new List<BasicTimeTracker>();
    public PlayerController player;
    public PlayerController playerPrefab;
    public BoxCollider2D levelEndObject;
    public string nextLevel;
    // visuals
    public TMP_Text timerText;
    public RetryPopup retryPopupPrefab;
    public Canvas mainUICanvas;
    
    private Pool<PlayerController> playerObjectPool;
	
	public GameObject playerItem;
	public Sprite tempImage; 

    public IEnumerable<PlayerController> PastPlayers
    {
        get
        {
            foreach(ITimeTracker timeTracker in TimeTrackerObjects)
            {
                PlayerController playerController = timeTracker as PlayerController;
                if (playerController != null && playerController != player)
                {
                    yield return playerController;
                }
            }
        }
    }

    public static ITimeTracker GetTimeTrackerComponent(GameObject gameObject)
    {
        TimeMachineController timeMachineController = gameObject.GetComponent<TimeMachineController>();
        if (timeMachineController != null) return timeMachineController;
        
        PlayerController playerController = gameObject.GetComponent<PlayerController>();
        if (playerController != null) return playerController;
        
        BasicTimeTracker basicTimeTracker = gameObject.GetComponent<BasicTimeTracker>();
        if (basicTimeTracker != null) return basicTimeTracker;
        
        return null;
    }

    private class GameState
    {
        public int nextID = 0; // TODO: mutex lock?!
    
        public List<ITimeTracker> timeTrackerObjects = new List<ITimeTracker>();
        public Dictionary<int, List<Dictionary<string, object>>> snapshotHistoryById = new Dictionary<int, List<Dictionary<string, object>>>();
        public Dictionary<int, int> historyStartById = new Dictionary<int, int>();
        public int timeStep = 0;
        public int furthestTimeStep = 0;
        public int skipTimeStep = -1;
        public bool isPresent = true;
        public bool doTimeSkip = false;
        public bool activatedLastFrame = false;
    
        // for animation logic
        public bool animateRewind = false;
        public int animateFrame = -1;
        public TimeMachineController occupiedTimeMachine = null;

        public void DeepCopy(GameState other)
        {
            nextID = other.nextID;
            
            timeTrackerObjects.Clear();
            foreach (var x in other.timeTrackerObjects)
            {
                timeTrackerObjects.Add(x);
            }

            snapshotHistoryById.Clear();
            foreach (var kvp in other.snapshotHistoryById)
            {
                var history = snapshotHistoryById[kvp.Key] = new List<Dictionary<string, object>>();
                foreach (var y in kvp.Value)
                {
                    history.Add(new Dictionary<string, object>(y));
                }
            }

            historyStartById = new Dictionary<int, int>(other.historyStartById);

            timeStep = other.timeStep;
            furthestTimeStep = other.furthestTimeStep;
            skipTimeStep = other.skipTimeStep;
            isPresent = other.isPresent;
            doTimeSkip = other.doTimeSkip;
            activatedLastFrame = other.activatedLastFrame;

            animateRewind = other.animateRewind;
            animateFrame = other.animateFrame;
            occupiedTimeMachine = other.occupiedTimeMachine;//TODO: this might need to be stored by ID not reference to object
        }
    }

    private GameState spawnState = null;
    private GameState currentState = new GameState();
    private bool paused = false;
    
    // ---Easy accessors for current state---
    public int NextID
    {
        get => currentState.nextID;
        private set => currentState.nextID = value;
    }

    private List<ITimeTracker> TimeTrackerObjects => currentState.timeTrackerObjects;
    private Dictionary<int, List<Dictionary<string, object>>> SnapshotHistoryById => currentState.snapshotHistoryById;
    private Dictionary<int, int> HistoryStartById => currentState.historyStartById;
    public int TimeStep
    {
        get => currentState.timeStep;
        private set => currentState.timeStep = value;
    }

    public int FurthestTimeStep
    {
        get => currentState.furthestTimeStep;
        private set => currentState.furthestTimeStep = value;
    }

    public int SkipTimeStep
    {
        get => currentState.skipTimeStep;
        private set => currentState.skipTimeStep = value;
    }

    public bool IsPresent
    {
        get => currentState.isPresent;
        private set => currentState.isPresent = value;
    }

    public bool DoTimeSkip
    {
        get => currentState.doTimeSkip;
        private set => currentState.doTimeSkip = value;
    }

    public bool ActivatedLastFrame
    {
        get => currentState.activatedLastFrame;
        private set => currentState.activatedLastFrame = value;
    }

    // for animation logic
    public bool AnimateRewind
    {
        get => currentState.animateRewind;
        private set => currentState.animateRewind = value;
    }

    public int AnimateFrame
    {
        get => currentState.animateFrame;
        private set => currentState.animateFrame = value;
    }

    public TimeMachineController OccupiedTimeMachine
    {
        get => currentState.occupiedTimeMachine;
        private set => currentState.occupiedTimeMachine = value;
    }
    // ---End Easy accessors of current state---

    void Start()
    {
        //TODO: find all ITimeTracker objects here?!
        
        //TODO: assert nextLevel is a valid level
        //TODO: assert player not null
        player.Init(this, NextID++);
        TimeTrackerObjects.Add(player);

        foreach (var timeMachine in timeMachines)
        {
            timeMachine.Init(this, NextID++);
            TimeTrackerObjects.Add(timeMachine);
        }

        foreach (var basicTimeTracker in basicTimeTrackers)
        {
            basicTimeTracker.Init(this, NextID++);
            TimeTrackerObjects.Add(basicTimeTracker);
        }

        playerObjectPool = new Pool<PlayerController>(
            InstantiatePlayer,
            x =>
            {
                x.gameObject.SetActive(true);
                x.PlayerInput.enabled = false;
            },
            x => {
                x.gameObject.SetActive(false);
                x.PlayerInput.enabled = false;
                x.ClearState();
                });
        
        Physics2D.simulationMode = SimulationMode2D.Script; // GameController will call Physics2D.Simulate()
    }

    private PlayerController InstantiatePlayer()
    {
        return Instantiate(playerPrefab);
    }

    void Update()
    {
        timerText.text = $"Test Timer:\n{TimeStep.ToString()}\n{(TimeStep * Time.fixedDeltaTime):0.0}s";
    }

    private void FixedUpdate()
    {
        if (paused)
        {
            return;
        }

        if (AnimateRewind)
        {
            player.gameObject.SetActive(false);
            AnimateFrame -= TIME_TRAVEL_REWIND_MULT;
            AnimateFrame = Math.Max(AnimateFrame, TimeStep);  
            LoadSnapshotFull(AnimateFrame, true, forceLoad:AnimateFrame == TimeStep);
            Physics2D.Simulate(Time.fixedDeltaTime); // needed to update rigidbodies after loading

            if (AnimateFrame == TimeStep) // we are done rewinding
            {
                AnimateFrame = -1;
                AnimateRewind = false;
                
                player.gameObject.SetActive(true);
                TimeTrackerObjects.Add(player);
                OccupiedTimeMachine.CurrentlyOccupied = true;
                OccupiedTimeMachine = null;
                
                // set the spawn state to this point
                if (spawnState == null) spawnState = new GameState();
                spawnState.DeepCopy(currentState);
            }
        }
        else
        {
            try
            {
                DoTimeStep();

                if (DoTimeSkip)
                {
                    DoTimeSkip = false;
                    SkipTimeStep = TimeStep + TIME_STEP_SKIP_AMOUNT;
                }
                if (TimeStep < SkipTimeStep && SkipTimeStep != -1)
                {
                    for (int i = 0; i < TIME_SKIP_ANIMATE_FPS-1; i++)
                    {
                        DoTimeStep();
                    }
                }
                if (TimeStep >= SkipTimeStep)
                {
                    SkipTimeStep = -1;
                }
            }
            catch (TimeAnomalyException e)
            {
                SetPause(true);
                ShowRetryPopup(e);
            }
        }
    }

    private void SetPause(bool newPaused)
    {
        paused = newPaused;
    }

    public void DoTimeStep()
    {
        LoadSnapshotFull(TimeStep, false);
        Physics2D.Simulate(Time.fixedDeltaTime);
        
        // restore history to current state if back to present
        if (TimeStep == FurthestTimeStep && !IsPresent)
        {
            foreach (var timeMachine in timeMachines)
            {
                timeMachine.BackToPresent();
            }
            IsPresent = true;
        }

        if (levelEndObject.IsTouching(player.CapsuleCollider))
        {
            //NOTE: in the future, we may want past players to be able to have some slight assymetry by being able to progress to the next level
            SceneManager.LoadScene(nextLevel);
        }

        foreach (var timeMachine in timeMachines)
        {
            timeMachine.GameUpdate();
        }

        int timeTravelStep = -1;
        bool didActivate = false;
        TimeMachineController targetTimeMachine = null;
        if (player.IsActivating && !ActivatedLastFrame)
        {
            foreach (var timeMachine in timeMachines)
            {
                if (timeMachine.IsTouching(player.gameObject))
                {
                    targetTimeMachine = timeMachine;
                    didActivate = timeMachine.Activate(out timeTravelStep);
                    if (timeTravelStep >= 0)
                    {
                        player.FlagDestroy = true;
                    }
                    break;
                }
            }
        }
        ActivatedLastFrame = player.IsActivating;

        int thisTimeStep = TimeStep;
        SaveSnapshotFull(TimeStep);
        ValidateTimeAnomalies();
        TimeStep++;
        FurthestTimeStep = Mathf.Max(TimeStep, FurthestTimeStep);
        player.ClearActivate();

        if (timeTravelStep >= 0)
        {
            DoTimeTravel(timeTravelStep, targetTimeMachine, player);
        }
    }

    public void SkipTime()
    {
        DoTimeSkip = true;
    }

    void LoadSnapshotFull(int timeStep, bool rewind, bool forceLoad = false)
    {
        if (timeStep == FurthestTimeStep) return;

        // instantiate objects not present
        foreach (var kvp in SnapshotHistoryById)
        {
            int id = kvp.Key;
            var history = kvp.Value;

            if (player.ID == id || TimeTrackerObjects.Exists(x => x.ID == id)) continue; // exists not most efficient, need better structure to store all time-tracking objects (prolly a dict)

            int startTimeStep = HistoryStartById[id];
            int relativeSnapshotIndex = timeStep - startTimeStep;
            if (relativeSnapshotIndex >= 0 && relativeSnapshotIndex < history.Count)
            {
                // TODO: better structure to determine type of object and instantiate from appropriate pool (instead of just players)
                PlayerController newPlayer = playerObjectPool.Aquire();
                newPlayer.Init(this, id);
                newPlayer.PlayerInput.enabled = false;
                TimeTrackerObjects.Add(newPlayer);
            }
        }

        for(int i = TimeTrackerObjects.Count-1; i >= 0; i--)
        {
            ITimeTracker timeTracker = TimeTrackerObjects[i];
            LoadSnapshot(timeStep, timeTracker, rewind, out bool delete, forceLoad);

            if (delete && timeTracker.ID != player.ID)
            {
                PoolObject(timeTracker);
                TimeTrackerObjects.RemoveAt(i);
            }
        }
    }

    void LoadSnapshot(int timeStep, ITimeTracker timeTracker, bool rewind, bool forceLoad = false) => LoadSnapshot(timeStep, timeTracker, rewind, out _, forceLoad);
    void LoadSnapshot(int timeStep, ITimeTracker timeTracker, bool rewind, out bool delete, bool forceLoad = false)
    {
        delete = false;
        if (SnapshotHistoryById.TryGetValue(timeTracker.ID, out var history))
        {
            int startTimeStep = HistoryStartById[timeTracker.ID];
            int relativeSnapshotIndex = timeStep - startTimeStep;

            if (relativeSnapshotIndex >= 0 && relativeSnapshotIndex < history.Count)
            {
                if (history[relativeSnapshotIndex].ContainsKey(FLAG_DESTROY) && !rewind)
                {
                    delete = true;
                }
                else if (forceLoad)
                {
                    timeTracker.ForceLoadSnapshot(history[relativeSnapshotIndex]);
                }
                else
                {
                    timeTracker.LoadSnapshot(history[relativeSnapshotIndex]);
                }
            }
            else
            {
                delete = true;
            }
        }
        else
        {
            delete = true;
        }
    }

    private T GetSnapshotValue<T>(ITimeTracker timeTracker, int timeStep, string parameter, T defaultValue = default)
    {
        if (SnapshotHistoryById.TryGetValue(timeTracker.ID, out var history))
        {
            int startTimeStep = HistoryStartById[timeTracker.ID];
            int relativeSnapshotIndex = timeStep - startTimeStep;

            if (relativeSnapshotIndex >= 0 && relativeSnapshotIndex < history.Count)
            {
                return (T)history[relativeSnapshotIndex][parameter];
            }
        }

        return defaultValue;
    }

    private void PoolObject(ITimeTracker timeTracker)
    {
        if (timeTracker is PlayerController)
        {
            playerObjectPool.Release(timeTracker as PlayerController);
        }
    }

    public void DropItem(int id)
    {
        foreach (var timeTracker in TimeTrackerObjects)
        {
            if (timeTracker.ID == id)
            {
                timeTracker.Position = player.Position;
                timeTracker.ItemForm = false;
                break;
            }
        }
    }

    void SaveSnapshotFull(int timeStep)
    {
        foreach (ITimeTracker timeTracker in TimeTrackerObjects)
        {
            SaveSnapshot(timeStep, timeTracker);   
        }
    }

    void SaveSnapshot(int timeStep, ITimeTracker timeTracker)
    {
        if (!SnapshotHistoryById.TryGetValue(timeTracker.ID, out var history))
        {
            history = SnapshotHistoryById[timeTracker.ID] = new List<Dictionary<string, object>>();
            HistoryStartById[timeTracker.ID] = timeStep;
        }

        int startTimeStep = HistoryStartById[timeTracker.ID];
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

    public void RetryLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void RespawnLatest()
    {
        if (spawnState == null)
        {
            RetryLevel();
            return;
        }

        // load player snapshot from current state (at the timestep of the spawnState)
        int playerStartFrame = currentState.historyStartById[player.ID];
        player.ForceLoadSnapshot(currentState.snapshotHistoryById[player.ID][spawnState.timeStep - playerStartFrame]);
            
        currentState.DeepCopy(spawnState);
        LoadSnapshotFull(TimeStep, false, true);
        SetPause(false);
    }

    public void ShowRetryPopup(TimeAnomalyException e)
    {
        var popup = Instantiate(retryPopupPrefab, mainUICanvas.transform);
        popup.Init("Symmetry Broken!", e.Message, RetryLevel, RespawnLatest);
    }

    public void ExportHistory()
    {
        string debugDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        debugDirectory = Path.Combine(debugDirectory, "My Games/Recurring Moment/Debug/");
        Directory.CreateDirectory(debugDirectory);

        string filename = $"HistoryExport_{System.DateTime.Now:MM-dd-yy H_mm_ss}.tsv";
        string path = Path.Combine(debugDirectory, filename);

        using (StreamWriter sw = File.CreateText(path))
        {
            List<string> columns = new List<string>();

            columns.Add("timeStep");
            for (int id = 0; id < NextID; id++)
            {
                var history = SnapshotHistoryById[id];

                if (history.Count > 0)
                {
                    columns.Add($"{id}.{FLAG_DESTROY}");

                    //NOTE: this would need to be adjusted for a sparse data structure
                    // grab columns from first history entry
                    foreach (var kvp in history[0])
                    {
                        columns.Add($"{id}.{kvp.Key}");
                    }
                }
            }

            // header
            sw.WriteLine(string.Join("\t", columns));

            // content
            for (int i = 0; i < FurthestTimeStep; i++)
            {
                List<string> row = new List<string>();
                foreach (string column in columns)
                {
                    if (column == "timeStep")
                    {
                        row.Add(i.ToString());
                    }
                    else
                    {
                        string[] split = column.Split('.');
                        int id = int.Parse(split[0]);
                        string field = split[1];

                        var history = SnapshotHistoryById[id];

                        int startTimeStep = HistoryStartById[id];
                        int relativeSnapshotIndex = i - startTimeStep;
                        if (relativeSnapshotIndex >= 0 && relativeSnapshotIndex < history.Count && history[relativeSnapshotIndex].TryGetValue(field, out object value))
                        {
                            row.Add(value.ToString());
                        }
                        else
                        {
                            row.Add("");
                        }
                    }
                }

                sw.WriteLine(string.Join("\t", row));
            }
        }
    }

    private void ValidateTimeAnomalies()
    {
        // ensure that past player's paths of motion are uninterrupted

        // ensure that time machines are not used in such a way that it prevents a past player from going to the time they intended
        foreach (TimeMachineController timeMachine in timeMachines)
        {
            if (timeMachine.CurrentlyActivated && timeMachine.HistoryCountdown != -1)
            {
                throw new TimeAnomalyException("Doppelganger tried activating an already active Time Machine!");
            }
            if (timeMachine.HistoryCountdown != -1 && timeMachine.CurrentCountdown != -1 && timeMachine.CurrentCountdown != timeMachine.HistoryCountdown)
            {
                throw new TimeAnomalyException("Doppelganger tried activating a Time Machine in count-down!");
            }
        }

        foreach (PlayerController p in PastPlayers)
        {
            //string historyColliderState = GetSnapshotValue<string>(p, TimeStep, nameof(PlayerController.GetCollisionStateString));
            //string currentColliderState = p.GetCollisionStateString(); 
            //if (historyColliderState != currentColliderState)
            //{
            //    Debug.Log($"{historyColliderState}\n{currentColliderState}");
            //    throw new TimeAnomalyException("Past player was unable to follow his previous path of motion!");
            //}
             Vector2 historyPosition = GetSnapshotValue(p, TimeStep, nameof(p.Rigidbody.position), Vector2.positiveInfinity);
             Debug.Log(Vector2.Distance(historyPosition, p.Position).ToString());
             if (Vector2.Distance(historyPosition, p.Position) > POSITION_ANOMALY_ERROR)
             {
                 throw new TimeAnomalyException("Past player was unable to follow his previous path of motion!");
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
        IsPresent = false;

        AnimateRewind = true;
        AnimateFrame = TimeStep;
        TimeStep = timeTravelStep;
        OccupiedTimeMachine = timeMachine;

        this.player.PlayerInput.enabled = false;
        
        PlayerController newPlayer = playerObjectPool.Aquire();
        newPlayer.PlayerInput.enabled = true;
        newPlayer.Init(this, NextID++);
        newPlayer.Rigidbody.velocity = player.Rigidbody.velocity;
        newPlayer.Rigidbody.position = player.Rigidbody.position;
        newPlayer.Rigidbody.rotation = player.Rigidbody.rotation;
        SaveSnapshot(timeTravelStep, newPlayer); // save the spawn position for the new player

        this.player = newPlayer;

        { // clear 'history' values on the time machine for the frame this was activated
            timeMachine.HistoryCountdown = -1;
            timeMachine.HistoryActivatedTimeStep = -1;
            timeMachine.HistoryActivated = false;
            timeMachine.HistoryOccupied = false;
            
            timeMachine.CurrentCountdown = -1;
            timeMachine.CurrentActivatedTimeStep = -1;
            timeMachine.CurrentlyActivated = false;
            timeMachine.CurrentlyOccupied = false;
            SaveSnapshot(AnimateFrame - 1, timeMachine);
        }

        // clear 'current' values from all time machines at the point in time the player is travelling back to
        // these values should already be recorded to history
        foreach (TimeMachineController otherTimeMachine in timeMachines)
        {
            LoadSnapshot(timeTravelStep, otherTimeMachine, false, forceLoad:true);
            otherTimeMachine.CurrentCountdown = -1;
            otherTimeMachine.CurrentActivatedTimeStep = -1;
            otherTimeMachine.CurrentlyActivated = false;
            otherTimeMachine.CurrentlyOccupied = false;
            SaveSnapshot(timeTravelStep, otherTimeMachine);
        }
    }
}
