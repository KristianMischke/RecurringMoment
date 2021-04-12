using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Assertions;
using UnityEngine.UI;
using Vector2 = UnityEngine.Vector2;

public class TimeAnomalyException : Exception
{
    public string Title;
    public TimeAnomalyException(string title, string reason) : base($"Time Anomaly: {reason}")
    {
        Title = title;
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
    public const string TYPE_EXPLOAD_BOX = "ExplodeBox";
    public const string TYPE_EXPLOSION = "Explosion";
    public const string TYPE_PLAYER = "Player";
    public const string TYPE_TIME_MACHINE = "TimeMachine";
    public const string TYPE_GUARD = "Guard";

    public Dictionary<string, GameObject> timeTrackerPrefabs = new Dictionary<string, GameObject>();
    
    public List<TimeMachineController> timeMachines = new List<TimeMachineController>();
    public PlayerController player;
    public BoxCollider2D levelEndObject;
    public string nextLevel;
    // visuals
    private Image rewindIndicator;
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

        Explosion explosion = gameObject.GetComponent<Explosion>();
        if (explosion != null) return explosion;
        
        Guard_AI guardAI = gameObject.GetComponent<Guard_AI>();
        if (guardAI != null) return guardAI;
        
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
        
        Explosion explosion = timeTracker as Explosion;
        if (explosion != null) return TYPE_EXPLOSION;
        
        Guard_AI guardAI = timeTracker as Guard_AI;
        if (guardAI != null) return TYPE_GUARD;
        
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
    
        public Dictionary<int, ICustomObject> allReferencedObjects = new Dictionary<int, ICustomObject>();
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
            
            allReferencedObjects.Clear();
            foreach (var kvp in other.allReferencedObjects)
            {
                allReferencedObjects[kvp.Key] = kvp.Value;
            }
            
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

    private Dictionary<int, ICustomObject> AllReferencedObjects => currentState.allReferencedObjects;
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

    public void Log(string message)
    {
        Debug.Log($"[GameController] t={TimeStep.ToString()} | {message}");
    }
    public void LogError(string message)
    {
        Debug.LogError($"[GameController] t={TimeStep.ToString()} | {message}");
    }
    
    void Start()
    {
        //--- Setup object prefabs and pools
        timeTrackerPrefabs[TYPE_BOX] = Resources.Load<GameObject>("Prefabs/MoveableBox");
        timeTrackerPrefabs[TYPE_EXPLOAD_BOX] = Resources.Load<GameObject>("Prefabs/ExplodingBox");
        timeTrackerPrefabs[TYPE_EXPLOSION] = Resources.Load<GameObject>("Prefabs/Explosion");
        timeTrackerPrefabs[TYPE_PLAYER] = Resources.Load<GameObject>("Prefabs/Player");
        timeTrackerPrefabs[TYPE_TIME_MACHINE] = Resources.Load<GameObject>("Prefabs/TimeMachine");
        timeTrackerPrefabs[TYPE_GUARD] = Resources.Load<GameObject>("Prefabs/Guard");

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
        CreatePool(TYPE_EXPLOSION);
        CreatePool(TYPE_PLAYER);
        CreatePool(TYPE_TIME_MACHINE);
        CreatePool(TYPE_GUARD);
        //------
        
        timeMachines.Clear();
        TimeTrackerObjects.Clear();
        AllReferencedObjects.Clear();
        
        // Find the player, store and initialize it
        var playersInScene = FindObjectsOfType<PlayerController>();
        Assert.AreEqual(1, playersInScene.Length, "There should be exactly one (1) player in the scene");
        player = playersInScene[0];
        player.Init(this, NextID++);
        TimeTrackerObjects[player.ID] = player;
        AllReferencedObjects[player.ID] = player;
        
        void GatherSceneObjects<T>() where T : UnityEngine.Object, ICustomObject
        {
            var foundObjects = FindObjectsOfType<T>();

            foreach (var obj in foundObjects)
            {
                if(AllReferencedObjects.ContainsValue(obj)) continue; // Already gathered this object
                
                TimeMachineController timeMachine = obj as TimeMachineController;
                if (timeMachine != null)
                {
                    timeMachines.Add(timeMachine);
                }
                
                obj.Init(this, NextID++);
                AllReferencedObjects[obj.ID] = obj;

                var timeTracker = obj as ITimeTracker;
                if (timeTracker != null)
                {
                    TimeTrackerObjects[timeTracker.ID] = timeTracker;
                }
            }
        }

        // Gather non-TimeTracker Objects, but still ones we need IDs for
        GatherSceneObjects<ActivatableBehaviour>();
        GatherSceneObjects<IndestructableObject>(); // always do generic type last
        
        // Gather other TimeTracker Objects
        GatherSceneObjects<TimeMachineController>();
        GatherSceneObjects<ExplodeBox>();
        GatherSceneObjects<Guard_AI>();
        GatherSceneObjects<BasicTimeTracker>(); // always do generic type last

        //TODO: assert nextLevel is a valid level

        // get rewind indicator object
        rewindIndicator = GameObject.Find("RewindIndicator").GetComponent<Image>();
        Assert.IsNotNull(rewindIndicator);
        
        Physics2D.simulationMode = SimulationMode2D.Script; // GameController will call Physics2D.Simulate()
    }

    //---These methods are to be used in our pooling to acquire and release generic TimeTracker objects
    private T AcquireTimeTracker<T>(string type) where T : ITimeTracker
    {
        T timeTracker = default;
        if (timeTrackerPrefabs.TryGetValue(type, out var prefabObject))
        {
            Log($"Instantiating {type} for pool");
            timeTracker = Instantiate(prefabObject).GetComponent<T>();
            timeTracker.OnPoolInstantiate();
        }
        
        return timeTracker;
    }
    private void InitTimeTracker<T>(T obj) where T : ITimeTracker
    {
        Log($"Initializing {GetTimeTrackerType(obj)} from pool");
        obj.gameObject.SetActive(true);
        obj.OnPoolInit();
    }
    private void ReleaseTimeTracker<T>(T obj) where T : ITimeTracker
    {
        Log($"Releasing {GetTimeTrackerType(obj)} to pool");
        obj.gameObject.SetActive(false);
        obj.OnPoolRelease();
    }
    //------

    private ITimeTracker AcquireAndInitPooledTimeTracker(string type, int id, bool addToTrackerList = true)
    {
        if (timeTrackerPools.TryGetValue(type, out var pool))
        {
            var obj = pool.Aquire();
            Log($"{id.ToString()}.Init()");
            obj.Init(this, id);
            obj.FlagDestroy = false;
            if (addToTrackerList)
            {
                AllReferencedObjects[id] = TimeTrackerObjects[id] = obj;
                if (type == TYPE_TIME_MACHINE)
                {
                    timeMachines.Add(obj as TimeMachineController);
                }
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

    public Explosion CreateExplosion(Vector2 location, float radius)
    {
        int newID = NextID++;
        Explosion explosionObject = AcquireTimeTracker<Explosion>(TYPE_EXPLOSION);

        // Init and add to trackers
        Log($"{newID.ToString()}.Init()");
        explosionObject.Init(this, newID);
        explosionObject.FlagDestroy = false;
        AllReferencedObjects[newID] = TimeTrackerObjects[newID] = explosionObject;
        HistoryStartById[newID] = TimeStep;
        
        // set initial variables
        explosionObject.Position.Current = location;
        explosionObject.destroyStep = TimeStep + explosionObject.lifetime;
        explosionObject.radius = radius;
        explosionObject.DrawExplosion();
        return explosionObject;
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

        rewindIndicator.enabled = AnimateRewind;
        
        if (AnimateRewind)
        {
            player.gameObject.SetActive(false);
            AnimateFrame -= TIME_TRAVEL_REWIND_MULT;
            AnimateFrame = Math.Max(AnimateFrame, TimeStep);  
            LoadSnapshotFull(AnimateFrame, true, forceLoad:AnimateFrame == TimeStep);
            Physics2D.Simulate(Time.fixedDeltaTime); // needed to update rigidbodies after loading

            if (AnimateFrame == TimeStep) // we are done rewinding
            {
                Log($"Finish Rewind Animation");
                
                AnimateFrame = -1;
                AnimateRewind = false;
                
                // show player & add back to tracking list
                player.gameObject.SetActive(true);
                
                OccupiedTimeMachine.Occupied.Current = true;
                OccupiedTimeMachine.Occupied.SaveSnapshot(SnapshotHistoryById[OccupiedTimeMachine.ID][TimeStep], force:true);
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
            if (AllReferencedObjects.TryGetValue(i, out var obj))
            {
                var timeTracker = obj as ITimeTracker;
                if (timeTracker != null && GetSnapshotValue<bool>(timeTracker, TimeStep, FLAG_DESTROY))
                {
                    continue; // skip destroyed time trackers (i.e. this skips non-pooled time trackers)
                }
                obj.GameUpdate();
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

        if (player.FlagDestroy)
        {
            throw new TimeAnomalyException("Oh no!", "You died!");
        }

        int thisTimeStep = TimeStep;
        PreSaveValidateTimeAnomalies();
        SaveSnapshotFull(TimeStep);
        PostSaveValidateTimeAnomalies();
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

            // already destroyed if it was marked destroyed last frame AND this frame
            bool alreadyDestroyed =
                history.Get<bool>(timeStep - 1, FLAG_DESTROY) && history.Get<bool>(timeStep, FLAG_DESTROY);

            TimeTrackerObjects.TryGetValue(id, out var timeTracker);
            if (!alreadyDestroyed
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
            if (relativeSnapshotIndex >= 0 && !alreadyDestroyed)
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
                        AllReferencedObjects.Remove(i);
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
        // NOTE: the only reason we should delete (aka pool) and object when Loading the snapshot, is because it is not 
        // in this timeStep... if FLAG_DESTROY is true, that should always be handled in the SaveSnapshot method
        delete = false;
        if (SnapshotHistoryById.TryGetValue(timeTracker.ID, out var history))
        {
            int startTimeStep = HistoryStartById[timeTracker.ID];
            int relativeSnapshotIndex = timeStep - startTimeStep;

            if (relativeSnapshotIndex >= 0)
            {
                if (forceLoad)
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

    public T GetSnapshotValue<T>(ITimeTracker timeTracker, int timeStep, string parameter, T defaultValue = default)
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

    public string GetObjectTypeByID(int id) => ObjectTypeByID.TryGetValue(id, out var result) ? result : null;
    public ICustomObject GetObjectByID(int id) => AllReferencedObjects.TryGetValue(id, out var result) ? result : null;
    public ITimeTracker GetTimeTrackerByID(int id) => TimeTrackerObjects.TryGetValue(id, out var result) ? result : null;
    
    public bool DropItem(int id)
    {
        if (TimeTrackerObjects.TryGetValue(id, out var timeTracker))
        {
            Log($"Drop Item {id.ToString()}");
            Vector2 offset = new Vector2(player.facingRight ? 1.2f : -1.2f, 0); 
            timeTracker.Position.Current = player.Position.Get + offset;
            return timeTracker.SetItemState(false);
        }
        else
        {
            LogError($"could not drop item {id.ToString()}");
            return false;
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
                        AllReferencedObjects.Remove(i);
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

        // pool objects not yet created/active
        for(int id = 0; id < NextID; id++)
        {
            // destroy if not found in history (i.e. was created this frame, or if it starts after the spawn time)
            bool destroy = !HistoryStartById.TryGetValue(id, out var startTime) || startTime > spawnState.timeStep; 
            if (destroy && TimeTrackerObjects.TryGetValue(id, out var timeTracker))
            {
                if(timeTracker.ShouldPoolObject)
                {
                    SaveObjectToPool(timeTracker);
                    TimeTrackerObjects.Remove(id);
                    AllReferencedObjects.Remove(id);
                }
                else
                {
                    timeTracker.gameObject.SetActive(false);
                }                
            }
        }
        
        currentState.DeepCopy(spawnState);
        LoadSnapshotFull(TimeStep, false, true);
        SetPause(false);
    }

    public void ShowRetryPopup(TimeAnomalyException e)
    {
        var popup = Instantiate(retryPopupPrefab, mainUICanvas.transform);
        popup.Init(e.Title, e.Message, RetryLevel, RespawnLatest);
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
                if (SnapshotHistoryById.TryGetValue(id, out var history))
                {
                    // grab columns history entry
                    foreach (string key in history.Keys)
                    {
                        columns.Add($"{id}.{key}");
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
                        
                        // was destroyed this step if was NOT destroyed last frame and IS destroyed this frame 
                        bool destroyedThisStep = !history.Get<bool>(i-1, FLAG_DESTROY) && history.Get<bool>(i, FLAG_DESTROY);
                        if (relativeSnapshotIndex >= 0 && (!history.Get<bool>(i, FLAG_DESTROY) || column.Contains(FLAG_DESTROY) || destroyedThisStep))
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

    private void PreSaveValidateTimeAnomalies()
    {
        string symmetryBrokenTitle = "Symmetry Broken!";   
        
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
            if (Vector2.Distance(historyPosition, p.transform.position) > POSITION_ANOMALY_ERROR)
            {
                throw new TimeAnomalyException(symmetryBrokenTitle, "Past player was unable to follow his previous path of motion!");
            }
        }
    }
    private void PostSaveValidateTimeAnomalies()
    {
        // check if past player(s) died
        foreach (PlayerController p in PastPlayers)
        {
            if (GetSnapshotValue<bool>(p, TimeStep, FLAG_DESTROY) && !p.DidTimeTravel)
            {
                // player is destroyed & not timetravelling
                throw new TimeAnomalyException("Oh No!", "Past Player was killed!");
            }
        }

        string symmetryBrokenTitle = "Symmetry Broken!";        
        
        // ensure that time machines are not used in such a way that it prevents a past player from going to the time they intended
        foreach (TimeMachineController timeMachine in timeMachines)
        {
            bool currentActivated = GetSnapshotValue<bool>(timeMachine, TimeStep, timeMachine.Activated.CurrentName);
            int historyCountdown = GetSnapshotValue(timeMachine, TimeStep, timeMachine.Countdown.HistoryName, -1);
            int currentCountdown = GetSnapshotValue(timeMachine, TimeStep, timeMachine.Countdown.CurrentName, -1);
            
            if (currentActivated && historyCountdown != -1)
            {
                throw new TimeAnomalyException(symmetryBrokenTitle, "Doppelganger tried activating an already active Time Machine!");
            }
            if (historyCountdown != -1 && currentCountdown != -1 && currentCountdown != historyCountdown)
            {
                throw new TimeAnomalyException(symmetryBrokenTitle, "Doppelganger tried activating a Time Machine in count-down!");
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
        
        Log("DoTimeTravel");

        AnimateRewind = true;
        AnimateFrame = TimeStep;
        TimeStep = timeTravelStep;
        OccupiedTimeMachine = timeMachine;

        this.player.PlayerInput.enabled = false;
        
        // flag player to destroy
        this.player.FlagDestroy = true;
        this.player.DidTimeTravel = true;
        SaveSnapshot(AnimateFrame-1, this.player);

        // clone the player
        PlayerController newPlayer = AcquireAndInitPooledTimeTracker(TYPE_PLAYER, NextID++) as PlayerController;
        newPlayer.PlayerInput.enabled = true;
        newPlayer.CopyTimeTrackerState(this.player);

        // if the player is holding an item, clone it
        if (this.player.ItemID >= 0 && TimeTrackerObjects.TryGetValue(this.player.ItemID, out var playerItem))
        {
            playerItem.FlagDestroy = true;
            SaveSnapshot(AnimateFrame-1, playerItem);

            ITimeTracker newPlayerItem = AcquireAndInitPooledTimeTracker(ObjectTypeByID[playerItem.ID], NextID++);
            newPlayerItem.CopyTimeTrackerState(playerItem);
            newPlayer.ItemID = newPlayerItem.ID; // update the new player's item id to match the cloned item id
            SaveSnapshot(timeTravelStep, newPlayerItem);
        }
        
        SaveSnapshot(timeTravelStep, newPlayer); // save the spawn position for the new player
        this.player = newPlayer; // update current player to the new one

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
