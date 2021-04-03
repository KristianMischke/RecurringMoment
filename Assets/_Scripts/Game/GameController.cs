using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Assertions;

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

    public const string TYPE_BOX = "MoveableBox";
    public const string TYPE_EXPLOAD_BOX = "ExploadingBox";
    public const string TYPE_PLAYER = "Player";
    public const string TYPE_TIME_MACHINE = "TimeMachine";

    public Dictionary<string, GameObject> timeTrackerPrefabs = new Dictionary<string, GameObject>();
    
    public List<TimeMachineController> timeMachines = new List<TimeMachineController>();
    public PlayerController player;
    public PlayerController playerPrefab;
    public BoxCollider2D levelEndObject;
    public string nextLevel;
    // visuals
    public TMP_Text timerText;
    public RetryPopup retryPopupPrefab;
    public Canvas mainUICanvas;

    private Dictionary<string, Pool<ITimeTracker>> timeTrackerPools = new Dictionary<string, Pool<ITimeTracker>>();
	
	public GameObject playerItem;
	public Sprite tempImage; 

    public IEnumerable<PlayerController> PastPlayers
    {
        get
        {
            foreach(var kvp in TimeTrackerObjects)
            {
                PlayerController playerController = kvp.Value as PlayerController;
                if (playerController != null && playerController != player)
                {
                    yield return playerController;
                }
            }
        }
    }

    public static ITimeTracker GetTimeTrackerComponent(GameObject gameObject, bool checkParents = false)
    {
        if (gameObject == null) return null;
        
        TimeMachineController timeMachineController = gameObject.GetComponent<TimeMachineController>();
        if (timeMachineController != null) return timeMachineController;
        
        PlayerController playerController = gameObject.GetComponent<PlayerController>();
        if (playerController != null) return playerController;
        
        ExplodeBox explodeBox = gameObject.GetComponent<ExplodeBox>();
        if (explodeBox != null) return explodeBox;
        
        BasicTimeTracker basicTimeTracker = gameObject.GetComponent<BasicTimeTracker>();
        if (basicTimeTracker != null) return basicTimeTracker;

        if (checkParents && gameObject.transform.parent != null)
        {
            return GetTimeTrackerComponent(gameObject.transform.parent.gameObject, true);
        }
        
        return null;
    }
    
    public static string GetTimeTrackerType(ITimeTracker timeTracker)
    {
        TimeMachineController timeMachineController = timeTracker as TimeMachineController;
        if (timeMachineController != null) return TYPE_TIME_MACHINE;
        
        PlayerController playerController = timeTracker as PlayerController;
        if (playerController != null) return TYPE_PLAYER;
        
        ExplodeBox explodeBox = timeTracker as ExplodeBox;
        if (explodeBox != null) return TYPE_EXPLOAD_BOX;
        
        BasicTimeTracker basicTimeTracker = timeTracker as BasicTimeTracker;
        if (basicTimeTracker != null)
        {
            if(basicTimeTracker.name.Contains("Box")) return TYPE_BOX;
        }

        return null;
    }

    private class GameState
    {
        public int nextID = 0; // TODO: mutex lock?!
    
        public Dictionary<int, ITimeTracker> timeTrackerObjects = new Dictionary<int, ITimeTracker>();
        public Dictionary<int, TimeDict> snapshotHistoryById = new Dictionary<int, TimeDict>();
        public Dictionary<int, string> objectTypeByID = new Dictionary<int, string>();
        public Dictionary<int, int> historyStartById = new Dictionary<int, int>();
        public int timeStep = 0;
        public int furthestTimeStep = 0;
        public int skipTimeStep = -1;
        public bool isPresent = true;
        public bool doTimeSkip = false;
        public bool didTimeTravelThisFrame = false;
        public bool activatedLastFrame = false;
    
        // for animation logic
        public bool animateRewind = false;
        public int animateFrame = -1;
        public TimeMachineController occupiedTimeMachine = null;

        public void DeepCopy(GameState other)
        {
            nextID = other.nextID;
            
            timeTrackerObjects.Clear();
            foreach (var kvp in other.timeTrackerObjects)
            {
                timeTrackerObjects[kvp.Key] = kvp.Value;
            }

            snapshotHistoryById.Clear();
            foreach (var kvp in other.snapshotHistoryById)
            {
                snapshotHistoryById[kvp.Key] = new TimeDict(kvp.Value);
            }

            objectTypeByID = new Dictionary<int, string>(other.objectTypeByID);
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

    #region EasyAccessorsForCurrentState

    // ---Easy accessors for current state---

    public int NextID
    {
        get => currentState.nextID;
        private set => currentState.nextID = value;
    }

    private Dictionary<int, ITimeTracker> TimeTrackerObjects => currentState.timeTrackerObjects;

    private Dictionary<int, TimeDict> SnapshotHistoryById => currentState.snapshotHistoryById;

    private Dictionary<int, string> ObjectTypeByID => currentState.objectTypeByID;

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

    public bool DidTimeTravelThisFrame
    {
        get => currentState.didTimeTravelThisFrame;
        set => currentState.didTimeTravelThisFrame = value;
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

    #endregion

    void Start()
    {
        //--- Setup object prefabs and pools
        timeTrackerPrefabs[TYPE_BOX] = Resources.Load<GameObject>("Prefabs/MoveableBox");
        timeTrackerPrefabs[TYPE_EXPLOAD_BOX] = Resources.Load<GameObject>("Prefabs/ExploadingBox");
        timeTrackerPrefabs[TYPE_PLAYER] = Resources.Load<GameObject>("Prefabs/Player");
        timeTrackerPrefabs[TYPE_TIME_MACHINE] = Resources.Load<GameObject>("Prefabs/TimeMachine");

        void CreatePool(string type)
        {
            timeTrackerPools[type] = new Pool<ITimeTracker>(
                () => AcquireTimeTracker<ITimeTracker>(type),
                InitTimeTracker,
                ReleaseTimeTracker 
            );
        }
        
        CreatePool(TYPE_BOX);
        CreatePool(TYPE_EXPLOAD_BOX);
        CreatePool(TYPE_PLAYER);
        CreatePool(TYPE_TIME_MACHINE);
        //------
        
        timeMachines.Clear();
        TimeTrackerObjects.Clear();
        
        // Find the player, store and initialize it
        var playersInScene = FindObjectsOfType<PlayerController>();
        Assert.AreEqual(1, playersInScene.Length, "There should be exactly one (1) player in the scene");
        player = playersInScene[0];
        player.Init(this, NextID++);
        TimeTrackerObjects[player.ID] = player;
        
        void GatherTimeTrackerObjects<T>() where T : UnityEngine.Object, ITimeTracker
        {
            var timeTrackerObjects = FindObjectsOfType<T>();

            foreach (var timeTracker in timeTrackerObjects)
            {
                if(TimeTrackerObjects.ContainsValue(timeTracker)) continue; // Already gathered this object
                
                TimeMachineController timeMachine = timeTracker as TimeMachineController;
                if (timeMachine != null)
                {
                    timeMachines.Add(timeMachine);
                }
                
                timeTracker.Init(this, NextID++);
                TimeTrackerObjects[timeTracker.ID] = timeTracker;
            }
        }

        // Gather other TimeTracker Objects
        GatherTimeTrackerObjects<TimeMachineController>();
        GatherTimeTrackerObjects<ExplodeBox>();
        GatherTimeTrackerObjects<BasicTimeTracker>();
        
        //TODO: assert nextLevel is a valid level

        Physics2D.simulationMode = SimulationMode2D.Script; // GameController will call Physics2D.Simulate()
    }

    //---These methods are to be used in our pooling to acquire and release generic TimeTracker objects
    private T AcquireTimeTracker<T>(string type) where T : ITimeTracker
    {
        T timeTracker = default;
        if (timeTrackerPrefabs.TryGetValue(type, out var prefabObject))
        {
            timeTracker = Instantiate(prefabObject).GetComponent<T>();
            timeTracker.OnPoolInstantiate();
        }
        
        return timeTracker;
    }
    private void InitTimeTracker<T>(T obj) where T : ITimeTracker
    {
        obj.gameObject.SetActive(true);
        obj.OnPoolInit();
    }
    private void ReleaseTimeTracker<T>(T obj) where T : ITimeTracker
    {
        obj.gameObject.SetActive(false);
        obj.OnPoolRelease();
    }
    //------

    private ITimeTracker AcquireAndInitPooledTimeTracker(string type, int id, bool addToTrackerList = true)
    {
        if (timeTrackerPools.TryGetValue(type, out var pool))
        {
            var obj = pool.Aquire();
            obj.Init(this, id);
            obj.FlagDestroy = false;
            if (addToTrackerList)
            {
                TimeTrackerObjects[id] = obj;
            }

            return obj;
        }

        return default;
    }

    private void SaveObjectToPool(ITimeTracker timeTracker)
    {
        if (!ObjectTypeByID.TryGetValue(timeTracker.ID, out string type))
        {
            type = ObjectTypeByID[timeTracker.ID] = GetTimeTrackerType(timeTracker);
        }

        if (timeTrackerPools.TryGetValue(type, out var pool))
        {
            pool.Release(timeTracker);
        }
    }

    void Update()
    {
        if (timerText != null)
        {
            timerText.text = $"Test Timer:\n{TimeStep.ToString()}\n{(TimeStep * Time.fixedDeltaTime):0.0}s";
        }
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
                TimeTrackerObjects[player.ID] = player;
                OccupiedTimeMachine.Occupied.Current = true;
                OccupiedTimeMachine = null;
                DidTimeTravelThisFrame = true;
                
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
        LoadSnapshotFull(TimeStep, false, DidTimeTravelThisFrame);

        if (DidTimeTravelThisFrame) DidTimeTravelThisFrame = false;
        
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

        for (int i = 0; i < NextID; i++)
        {
            if (TimeTrackerObjects.TryGetValue(i, out var timeTracker))
            {
                timeTracker.GameUpdate();
            }
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

            TimeTrackerObjects.TryGetValue(id, out var timeTracker);
            if (!history.Get<bool>(timeStep, FLAG_DESTROY)
                && timeTracker != null
                && !timeTracker.ShouldPoolObject)
            {
                // FLAG_DESTORY is false this timeStep AND this object is not pooled
                timeTracker.FlagDestroy = false; // reset current FlagDestroy value
                timeTracker.gameObject.SetActive(true);
            }
            
            if (player.ID == id || timeTracker != null) continue; // object already exists, so continue 

            int startTimeStep = HistoryStartById[id];
            int relativeSnapshotIndex = timeStep - startTimeStep;
            if (relativeSnapshotIndex >= 0 && !history.Get<bool>(timeStep, FLAG_DESTROY))
            {
                AcquireAndInitPooledTimeTracker(ObjectTypeByID[id], id);
            }
        }

        // load the snapshot into all objects for this timeStep
        for(int i = 0; i < NextID; i++)
        {
            if (TimeTrackerObjects.TryGetValue(i, out var timeTracker))
            {
                LoadSnapshot(timeStep, timeTracker, rewind, out bool delete, forceLoad);

                // if object was deleted this timeStep, then pool it
                if (delete && timeTracker.ID != player.ID)
                {
                    if (timeTracker.ShouldPoolObject)
                    {
                        SaveObjectToPool(timeTracker);
                        TimeTrackerObjects.Remove(i);
                    }
                    else
                    {
                        timeTracker.gameObject.SetActive(false);
                    }
                }
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

            if (relativeSnapshotIndex >= 0)
            {
                if (history.Get<bool>(timeStep, FLAG_DESTROY) && !rewind)
                {
                    delete = true;
                }
                else if (forceLoad)
                {
                    timeTracker.ForceLoadSnapshot(history[timeStep]);
                }
                else
                {
                    timeTracker.LoadSnapshot(history[timeStep]);
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

            if (relativeSnapshotIndex >= 0)
            {
                return history.Get<T>(timeStep, parameter);
            }
        }

        return defaultValue;
    }

    public void DropItem(int id)
    {
        foreach (var kvp in TimeTrackerObjects)
        {
            var timeTracker = kvp.Value;
            if (timeTracker.ID == id)
            {
                timeTracker.Position.Current = player.Position.Get;
                timeTracker.SetItemState(false);
                break;
            }
        }
    }

    void SaveSnapshotFull(int timeStep)
    {
        for (int i = 0; i < NextID; i++)
        {
            if (TimeTrackerObjects.TryGetValue(i, out var timeTracker))
            {

                SaveSnapshot(timeStep, timeTracker);

                // if object was deleted this timeStep, then pool it
                if (timeTracker.FlagDestroy && timeTracker.ID != player.ID)
                {
                    if (timeTracker.ShouldPoolObject)
                    {
                        SaveObjectToPool(timeTracker);
                        TimeTrackerObjects.Remove(i);
                    }
                    else
                    {
                        timeTracker.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    void SaveSnapshot(int timeStep, ITimeTracker timeTracker, bool force=false)
    {
        if (!SnapshotHistoryById.TryGetValue(timeTracker.ID, out var history))
        {
            history = SnapshotHistoryById[timeTracker.ID] = new TimeDict();
            HistoryStartById[timeTracker.ID] = timeStep;
            ObjectTypeByID[timeTracker.ID] = GetTimeTrackerType(timeTracker);
        }

        int startTimeStep = HistoryStartById[timeTracker.ID];
        int relativeSnapshotIndex = timeStep - startTimeStep;

        timeTracker.SaveSnapshot(history[timeStep], force);
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

                // grab columns history entry
                foreach (string key in history.Keys)
                {
                    columns.Add($"{id}.{key}");
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
                        if (relativeSnapshotIndex >= 0 && (!history.Get<bool>(i, FLAG_DESTROY) || column.Contains(FLAG_DESTROY)))
                        {
                            row.Add(history[i].Get<object>(field)?.ToString() ?? "");
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
        // check if past player(s) died
        foreach (PlayerController p in PastPlayers)
        {
            if (p.FlagDestroy && !p.DidTimeTravel)
            {
                // player is destroyed & not timetravelling
                throw new TimeAnomalyException("Past Player was killed!");
            }
        }
        
        // ensure that time machines are not used in such a way that it prevents a past player from going to the time they intended
        foreach (TimeMachineController timeMachine in timeMachines)
        {
            if (timeMachine.Activated.Current && timeMachine.Countdown.History != -1)
            {
                throw new TimeAnomalyException("Doppelganger tried activating an already active Time Machine!");
            }
            if (timeMachine.Countdown.History != -1 && timeMachine.Countdown.Current != -1 && timeMachine.Countdown.Current != timeMachine.Countdown.History)
            {
                throw new TimeAnomalyException("Doppelganger tried activating a Time Machine in count-down!");
            }
        }

        // ensure that past player's paths of motion are uninterrupted
        foreach (PlayerController p in PastPlayers)
        {
            //string historyColliderState = GetSnapshotValue<string>(p, TimeStep, nameof(PlayerController.GetCollisionStateString));
            //string currentColliderState = p.GetCollisionStateString(); 
            //if (historyColliderState != currentColliderState)
            //{
            //    Debug.Log($"{historyColliderState}\n{currentColliderState}");
            //    throw new TimeAnomalyException("Past player was unable to follow his previous path of motion!");
            //}
             Vector2 historyPosition = GetSnapshotValue(p, TimeStep, p.Position.HistoryName, Vector2.positiveInfinity);
             if (Vector2.Distance(historyPosition, p.Position.Get) > POSITION_ANOMALY_ERROR)
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
        
        // flag player to destroy
        this.player.FlagDestroy = true;
        this.player.DidTimeTravel = true;
        SaveSnapshot(AnimateFrame-1, this.player);
        this.player.FlagDestroy = false;
        
        // addToTrackerList=false because object will be added to the tracker list
        // when time resumes after rewinding the past
        PlayerController newPlayer = AcquireAndInitPooledTimeTracker(TYPE_PLAYER, NextID++, addToTrackerList:false) as PlayerController;
        newPlayer.PlayerInput.enabled = true;
        newPlayer.Rigidbody.velocity = player.Rigidbody.velocity;
        newPlayer.Rigidbody.position = player.Rigidbody.position;
        newPlayer.Rigidbody.rotation = player.Rigidbody.rotation;
        SaveSnapshot(timeTravelStep, newPlayer); // save the spawn position for the new player

        this.player = newPlayer;

        { // clear 'history' values on the time machine for the frame this was activated
            timeMachine.Countdown.History = -1;
            timeMachine.ActivatedTimeStep.History = -1;
            timeMachine.Activated.History = false;
            timeMachine.Occupied.History = false;
            
            timeMachine.Countdown.Current = -1;
            timeMachine.ActivatedTimeStep.Current = -1;
            timeMachine.Activated.Current = false;
            timeMachine.Occupied.Current = false;
            SaveSnapshot(AnimateFrame - 1, timeMachine, force:true);
        }

        // clear 'current' values from all time machines at the point in time the player is travelling back to
        // these values should already be recorded to history
        foreach (TimeMachineController otherTimeMachine in timeMachines)
        {
            LoadSnapshot(timeTravelStep, otherTimeMachine, false, forceLoad: true);
            otherTimeMachine.Countdown.Current = -1;
            otherTimeMachine.ActivatedTimeStep.Current = -1;
            otherTimeMachine.Activated.Current = false;
            otherTimeMachine.Occupied.Current = false;
            SaveSnapshot(timeTravelStep, otherTimeMachine, force:true);
        }
    }
}
