﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Vector2 = UnityEngine.Vector2;

public class TimeAnomalyException : Exception
{
    public string Title;
    public string Body;
    public ICustomObject Cause;
    public TimeAnomalyException(string title, string reason, ICustomObject cause) : base($"Time Anomaly: {reason}")
    {
        Title = title;
        Body = reason;
        Cause = cause;
    }
}

public class GameController : MonoBehaviour
{
    public const string FLAG_DESTROY = "FLAG_DESTROY";
    public const int TIME_STEP_SKIP_AMOUNT = 100;
    public const int TIME_SKIP_ANIMATE_FPS = 10;
    public const int TIME_TRAVEL_REWIND_MULT = 10;
    public const float POSITION_ANOMALY_ERROR = 0.75f;
    public const float POSITION_CLEAR_FUTURE_THRESHOLD = 0.02f;

    public const string TYPE_BOX = "MoveableBox";
    public const string TYPE_EXPLOAD_BOX = "ExplodeBox";
    public const string TYPE_EXPLOSION = "Explosion";
    public const string TYPE_PLAYER = "Player";
    public const string TYPE_TIME_MACHINE = "TimeMachine";
    public const string TYPE_GUARD = "Guard";

    public const int SCENE_LOCKED = -1;
    public const int SCENE_READY = 0;

    public Dictionary<string, GameObject> timeTrackerPrefabs = new Dictionary<string, GameObject>();
    
    public List<TimeMachineController> timeMachines = new List<TimeMachineController>();
    public List<LevelEnd> LevelEnds = new List<LevelEnd>();

    // visuals
    private Image rewindIndicator;
    private Image fastForwardIndicator;
    public TMP_Text timerText;
    public RetryPopup retryPopupPrefab;
    public Canvas mainUICanvas;
	public GameObject watchTMPrefab; 

    [SerializeField]
    private ScriptableRendererFeature _postProcessRenderer = null;

    private Dictionary<string, Pool<ITimeTracker>> timeTrackerPools = new Dictionary<string, Pool<ITimeTracker>>();
	
	public GameObject playerItem;
	public GameObject playerWatch; 
	public int currTMActive = 0; 
	public List<GameObject> watchShow = new List<GameObject>(); 

	public Sprite tempImage; 
	public bool userPause = false; 
	public GameObject pauseScreen; 
	public float actualTimeChange;
    private AnomalyIndicator instantiatedIndicator;
    private int RewindFrameRate = -1;

    private string sceneName;
	
    public IEnumerable<PlayerController> PastPlayers
    {
        get
        {
            foreach(var kvp in TimeTrackerObjects)
            {
                PlayerController playerController = kvp.Value as PlayerController;
                if (playerController != null && playerController != Player)
                {
                    yield return playerController;
                }
            }
        }
    }

    public IEnumerable<PlayerController> AllPlayers
    {
        get
        {
            yield return Player;
            foreach(var p in PastPlayers) yield return p;
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
        public Dictionary<int, List<TimeEvent>> eventsByTimeStep = new Dictionary<int, List<TimeEvent>>();
        public Dictionary<int, string> timeMachineLabel = new Dictionary<int, string>();
        public int currentPlayerID = -1;
        public int timeStep = 0;
        public int furthestTimeStep = 0;
        public int skipTimeStep = -1;
        public bool isPresent = true;
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
                if (!(kvp.Value is ITimeTracker tracker) || !tracker.ShouldPoolObject) // skip potentially stale time trackers
                {
                    allReferencedObjects[kvp.Key] = kvp.Value;
                }
            }
            
            timeTrackerObjects.Clear();
            foreach (var kvp in other.timeTrackerObjects)
            {
                if (!kvp.Value.ShouldPoolObject) // skip potentially stale time trackers
                {
                    timeTrackerObjects[kvp.Key] = kvp.Value;
                }
            }

            snapshotHistoryById.Clear();
            foreach (var kvp in other.snapshotHistoryById)
            {
                snapshotHistoryById[kvp.Key] = new TimeDict(kvp.Value);
            }
            
            eventsByTimeStep.Clear();
            foreach (var kvp in other.eventsByTimeStep)
            {
                eventsByTimeStep[kvp.Key] = new List<TimeEvent>(kvp.Value);
            }

            objectTypeByID = new Dictionary<int, string>(other.objectTypeByID);
            historyStartById = new Dictionary<int, int>(other.historyStartById);
            timeMachineLabel = new Dictionary<int, string>(other.timeMachineLabel);

            currentPlayerID = other.currentPlayerID;
            
            timeStep = other.timeStep;
            furthestTimeStep = other.furthestTimeStep;
            skipTimeStep = other.skipTimeStep;
            isPresent = other.isPresent;
            didTimeTravelThisFrame = other.didTimeTravelThisFrame;
            activatedLastFrame = other.activatedLastFrame;

            animateRewind = other.animateRewind;
            animateFrame = other.animateFrame;
            occupiedTimeMachine = other.occupiedTimeMachine;//TODO: this might need to be stored by ID not reference to object
        }
    }

    private GameState spawnState = null;
    private GameState currentState = new GameState();
    private bool paused = false;
    public bool doTimeSkip = false;
    public bool skipExtra = false;

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

    private Dictionary<int, List<TimeEvent>> EventsByTimeStep => currentState.eventsByTimeStep;

    private Dictionary<int, string> TimeMachineLabel => currentState.timeMachineLabel;

    public PlayerController Player
    {
        get => GetTimeTrackerByID(currentState.currentPlayerID) as PlayerController;
        set => currentState.currentPlayerID = value.ID;
    }

    public int CurrentPlayerID => currentState.currentPlayerID;

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
        int sceneNumStarts = PlayerPrefs.GetInt($"{SceneManager.GetActiveScene().name}_starts", defaultValue:0);
        PlayerPrefs.SetInt($"{SceneManager.GetActiveScene().name}_starts", ++sceneNumStarts);

        int sceneIndex = LevelEnd.levels?.IndexOf(SceneManager.GetActiveScene().name) ?? -1;
        if (sceneIndex >= 0 && sceneIndex < LevelEnd.levels.Count)
        {
            sceneName = LevelEnd.levelTitles[sceneIndex];
        }

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
		
		
		// getting the pauseMenu set up here - assuming that it is active when it starts 
		pauseScreen = GameObject.Find("PauseMenu"); 
		pauseScreen.SetActive(false); 
		Button[] pauseButton;
		pauseButton = pauseScreen.GetComponentsInChildren<Button>();
		foreach (Button butt in pauseButton)
		{
			if (butt.gameObject.name == "Resume")
			{
				butt.onClick.AddListener(Resume);
			}
			if (butt.gameObject.name == "Restart")
			{
				butt.onClick.AddListener(Retry);
			}
			if (butt.gameObject.name == "QuitScene")
			{
				butt.onClick.AddListener(QuitScene);
			}
			if (butt.gameObject.name == "QuitDesktop")
			{
				butt.onClick.AddListener(QuitDesktop);
			}
		}			
        
        // Find the player, store and initialize it
        var playersInScene = FindObjectsOfType<PlayerController>();
        Assert.AreEqual(1, playersInScene.Length, "There should be exactly one (1) player in the scene");
        
        PlayerController tempPlayer = playersInScene[0];
        tempPlayer.Init(this, NextID++);
        TimeTrackerObjects[tempPlayer.ID] = tempPlayer;
        AllReferencedObjects[tempPlayer.ID] = tempPlayer;
        Player = tempPlayer;
        
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
		
		
		// sets up the watch setup as well and makes a array to hold the different current watches it has 
        playerWatch = GameObject.Find("PlayerWatch");


        // get all level end transition objects, and make sure they have valid scenes attached
        LevelEnds.AddRange(FindObjectsOfType<LevelEnd>());
        foreach (var levelEnd in LevelEnds)
        {
            Assert.IsFalse(string.IsNullOrEmpty(levelEnd.TransitionToLevel), $"Scene name is empty for LevelEnd object!");
#if UNITY_EDITOR
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>($"Assets/Scenes/{levelEnd.TransitionToLevel}.unity"), $"Scene name {levelEnd.TransitionToLevel} is invalid!");
#endif
        }

        // get rewind indicator object
        rewindIndicator = GameObject.Find("RewindIndicator").GetComponent<Image>();
        Assert.IsNotNull(rewindIndicator);
        // get fast-forward indicator object
        fastForwardIndicator = GameObject.Find("FastForwardIndicator").GetComponent<Image>();
        Assert.IsNotNull(fastForwardIndicator);
        
        Physics2D.simulationMode = SimulationMode2D.Script; // GameController will call Physics2D.Simulate()

	    //Reset the post-processing effect
	    _postProcessRenderer.SetActive(false);
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
        Log($"Releasing {GetTimeTrackerType(obj)} ({obj.ID}) to pool");
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

    public void SaveObjectToPool(ITimeTracker timeTracker)
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

    public string GetTimeMachineLabel(int id)
    {
        if (GetObjectTypeByID(id) != TYPE_TIME_MACHINE) return null;
        
        if (!TimeMachineLabel.TryGetValue(id, out string label))
        {
            label = TimeMachineLabel[id] = (char)('A' + TimeMachineLabel.Count) + "";
        }

        return label;
    }

    /// <summary>
    ///     Adds a new event to the time event "queue". Expected to be called by <see cref="ITimeTracker"/> in their
    ///     <see cref="ITimeTracker.GameUpdate()"/> method.
    /// </summary>
    /// <param name="sourceID">The item that spawned the event</param>
    /// <param name="eventType">The type of event spawned</param>
    /// <param name="targetID">The optional target of the event</param>
    /// <param name="otherData">Any additional data needed for the event</param>
    public void AddEvent(int sourceID, TimeEvent.EventType eventType, int targetID = -1, string otherData = null)
    {
        if (!EventsByTimeStep.TryGetValue(TimeStep, out var events))
        {
            events = EventsByTimeStep[TimeStep] = new List<TimeEvent>();
        }
    
        Log($"AddEvent({sourceID}, {eventType.ToString()}, {targetID}, {otherData})");
        events.Add(new TimeEvent(sourceID, eventType, targetID, otherData));
    }

    /// <summary>
    ///     Method that executes the events stored in the structure. Called in <see cref="DoTimeStep"/>
    /// </summary>
    /// <param name="timeEvent"></param>
    public void ExecuteEvent(TimeEvent timeEvent)
    {
        
        ITimeTracker timeTracker = GetTimeTrackerByID(timeEvent.SourceID);
        if (timeTracker == null) // cannot find source object
        {
            Log($"Failed: ExecuteEvent({timeEvent.SourceID}, {timeEvent.Type.ToString()}, {timeEvent.TargetID}, {timeEvent.OtherData})");
            if (timeEvent.Type == TimeEvent.EventType.TIME_TRAVEL)
            {
                throw new TimeAnomalyException("Time Anomaly!", "Doppelganger was nowhere to be found to enter the Time Machine!", GetObjectByID(timeEvent.TargetID));
            }
            if (timeEvent.Type == TimeEvent.EventType.ACTIVATE_TIME_MACHINE)
            {
                throw new TimeAnomalyException("Time Anomaly!", "Doppelganger was nowhere to be found to activate the Time Machine!", GetObjectByID(timeEvent.TargetID));
            }
        }
        else
        {
            Log($"ExecuteEvent({timeEvent.SourceID}, {timeEvent.Type.ToString()}, {timeEvent.TargetID}, {timeEvent.OtherData})");
            timeTracker.ExecutePastEvent(timeEvent);   
        }
    }
    
    public Explosion CreateExplosion(Vector2 location, float radius)
    {
        int newID = NextID++;
        Explosion explosionObject = AcquireTimeTracker<Explosion>(TYPE_EXPLOSION);
        
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
            TimeSpan span = new TimeSpan(0, 0, (int)(TimeStep * Time.fixedDeltaTime));
            timerText.text = $"{sceneName}\n{span.Minutes:00}:{span.Seconds:00}";
        }

#if DEBUG
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            SceneManager.LoadScene(LevelEnd.levels[9]);
        }
        for (int i = 0; i <= 8; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && i+10 < LevelEnd.levels.Count)
                {
                    SceneManager.LoadScene(LevelEnd.levels[i+10]);
                }
                else
                {
                    SceneManager.LoadScene(LevelEnd.levels[i]);   
                }
            }
        }
#endif
    }


    public void ToggleUserPause()
    {
        if(userPause)
        {
            Resume();
        }
        else
        {
            pauseScreen.SetActive(true); 
            actualTimeChange = Time.timeScale; 
            Time.timeScale = 0f; // stops the time (I think... hopefully) 
            userPause = true;
        }
    }
    
	public void Resume()
    {
        userPause = false;
		pauseScreen.SetActive(false);
		Time.timeScale = actualTimeChange;
	}

	public void Retry()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
		Time.timeScale = actualTimeChange; // this is for the menu whenever it gets changed (also since I didn't want to make retry level has the same thing and potentially break something
    }
	
	public void QuitScene()
    {
		Debug.Log("This is quit");
		Time.timeScale = actualTimeChange; 
        SceneManager.LoadScene(0); // the current starting scene so this might change if the scenes are altered... sry about that 
		
    }

	public void QuitDesktop()
	{
		Debug.Log("This is quit-Desktop");
		Time.timeScale = actualTimeChange;
		Application.Quit(); 
	}


    private void FixedUpdate()
    {
        if (paused || userPause)
        {
            return;
        }

        rewindIndicator.enabled = AnimateRewind;

        if (AnimateRewind)
        {
            fastForwardIndicator.enabled = false;
            
            Player.gameObject.SetActive(false);
            AnimateFrame -= Mathf.Max(RewindFrameRate, TIME_TRAVEL_REWIND_MULT);
            AnimateFrame = Math.Max(AnimateFrame, TimeStep);  
            LoadSnapshotFull(AnimateFrame, true, forceLoad:true);
            Physics2D.Simulate(Time.fixedDeltaTime); // needed to update rigidbodies after loading

            if (AnimateFrame == TimeStep) // we are done rewinding
            {
                Log($"Finish Rewind Animation");
                
		        _postProcessRenderer.SetActive(false);

                AnimateFrame = -1;
                AnimateRewind = false;
                
                // show player
                Player.gameObject.SetActive(true);

                OccupiedTimeMachine.Occupied.Current = true;
                OccupiedTimeMachine.Occupied.SaveSnapshot(SnapshotHistoryById[OccupiedTimeMachine.ID][TimeStep], force:true);
                OccupiedTimeMachine.IsAnimatingOpenClose = true;
                SnapshotHistoryById[OccupiedTimeMachine.ID][TimeStep].Set(nameof(OccupiedTimeMachine.IsAnimatingOpenClose), true);
                OccupiedTimeMachine.animator.SetBool(TimeMachineController.AnimateOpen, true);
                OccupiedTimeMachine.doneTimeTravelPlayerID = CurrentPlayerID;
                SnapshotHistoryById[OccupiedTimeMachine.ID][TimeStep].Set(nameof(OccupiedTimeMachine.doneTimeTravelPlayerID), CurrentPlayerID);
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

                if (doTimeSkip)
                {
                    if (skipExtra)
                    {
                        SkipTimeStep = TimeStep + TIME_STEP_SKIP_AMOUNT * 10;                        
                    }
                    else
                    {
                        SkipTimeStep = TimeStep + TIME_STEP_SKIP_AMOUNT;
                    }
                    doTimeSkip = false;
                }
                if (TimeStep < SkipTimeStep && SkipTimeStep != -1)
                {
                    fastForwardIndicator.enabled = true;
                    int numFrames = skipExtra ? TIME_SKIP_ANIMATE_FPS*10 : TIME_SKIP_ANIMATE_FPS;
                    for (int i = 0; i < numFrames-1; i++)
                    {
                        DoTimeStep();
                    }
                }
                if (TimeStep >= SkipTimeStep)
                {
                    fastForwardIndicator.enabled = false;
                    SkipTimeStep = -1;
                    skipExtra = false;
                }
            }
            catch (TimeAnomalyException e)
            {
                instantiatedIndicator = e.Cause.gameObject.AddComponent<AnomalyIndicator>();
                instantiatedIndicator.tint = new Color(1f, 0.5f, 0.5f, 1f);
                instantiatedIndicator.Apply();
                SetPause(true);
                ShowRetryPopup(e);
            }
        }
		int totalTM = currTMActive; 
		currTMActive = 0;
        foreach (var watchInterface in watchShow) // hide all interfaces
        {
            watchInterface.gameObject.SetActive(false);
        }

        /*
         *  Get all TimeMachines that are counting down
         *  Sort by their countdown (less time remaining at top of list)
         * 
         *  Get all TimeMachines that are activated or occupied
         *  Order by ActivatedTimeStep
         *  Concat counting down machines to end of list
         */
        IEnumerable<TimeMachineController> sortedCountdownTimeMachines = timeMachines
            .Where(tm => tm.Countdown.Current >= 0 || tm.Countdown.History >= 0)
            .OrderBy(tm => tm.Countdown.Current == -1 ? tm.Countdown.History : tm.Countdown.Current);
        IEnumerable<TimeMachineController> sortedTimeMachines = timeMachines
            .Where(tm => tm.IsActivatedOrOccupied)
            .OrderBy(tm => tm.ActivatedTimeStep.Current == -1 ? tm.ActivatedTimeStep.History : tm.ActivatedTimeStep.Current)
            .Concat(sortedCountdownTimeMachines);
        
		foreach (var tm in sortedTimeMachines)
		{
            if(currTMActive == totalTM)
			{
				// if there is a total tm active that equals the total that I had made then add a new one to watchShow
				totalTM += 1;
                RectTransform watchParent = playerWatch.GetComponent<RectTransform>();
				watchShow.Add(Instantiate(watchTMPrefab, watchParent));
				watchShow[currTMActive].gameObject.SetActive(false);
			}
            
            var watchUI = watchShow[currTMActive].GetComponent<WatchUI>();
            watchUI.LabelText.text = GetTimeMachineLabel(tm.ID);
            watchUI.ClockText.text = tm.GetDisplayString();
            watchShow[currTMActive].gameObject.SetActive(true);
            currTMActive = currTMActive + 1;
            
            int displayCountdown = tm.Countdown.Current == -1 ? tm.Countdown.History : tm.Countdown.Current;
            if (tm.Occupied.AnyTrue)
            {
                watchUI.LabelText.color = watchUI.ClockText.color = new Color(0f, 1f, 0f);
            }
            else if (tm.Activated.AnyTrue)
            {
                watchUI.LabelText.color = watchUI.ClockText.color =  new Color(1f, 0f, 0f);
            }
            else if (displayCountdown >= 0)
            {
                watchUI.LabelText.color = watchUI.ClockText.color =  new Color(1f, 0.7f, 0f);
            }
            else
            {
                watchUI.LabelText.color = watchUI.ClockText.color =  new Color(1f, 1f, 0f);
            }
		}
    }





    private void SetPause(bool newPaused)
    {
        paused = newPaused;
    }

    private TimeEvent timeTravelQueueEvent = new TimeEvent();
    public void DoTimeStep()
    {
        timeTravelQueueEvent = new TimeEvent(); // clear time travel event, we want to receive it in GameUpdate()
        
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

        // check if players exit the level
        foreach(var levelEnd in LevelEnds)
        {
            foreach (var player in AllPlayers)
            {
                if (levelEnd.BoxCollider2D.IsTouching(player.CapsuleCollider))
                {
                    // store level stats for scene select screen
                    float prevBest = PlayerPrefs.GetFloat($"{SceneManager.GetActiveScene().name}_time", defaultValue:float.PositiveInfinity);
                    float thisTime = TimeStep * Time.fixedDeltaTime;
                    if (thisTime < prevBest)
                    {
                        PlayerPrefs.SetFloat($"{SceneManager.GetActiveScene().name}_time", thisTime);
                    }

                    int numPlays = PlayerPrefs.GetInt($"{SceneManager.GetActiveScene().name}");
                    PlayerPrefs.SetInt($"{SceneManager.GetActiveScene().name}", numPlays + 1);
                    
                    int nextSceneNumPlays = PlayerPrefs.GetInt($"{levelEnd.TransitionToLevel}", defaultValue:SCENE_LOCKED);
                    if (nextSceneNumPlays == SCENE_LOCKED)
                    {
                        PlayerPrefs.SetInt($"{levelEnd.TransitionToLevel}", SCENE_READY);
                    }
                    SceneManager.LoadScene(levelEnd.TransitionToLevel);
                }
            }
        }

        if (EventsByTimeStep.TryGetValue(TimeStep, out var events))
        {
            foreach (var timeEvent in events)
            {
                ExecuteEvent(timeEvent);
            }
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

        bool didActivate = false;
        TimeMachineController targetTimeMachine = null;
        if (Player.IsActivating && !ActivatedLastFrame)
        {
            foreach (var timeMachine in timeMachines)
            {
                if (timeMachine.IsTouching(Player.gameObject))
                {
                    targetTimeMachine = timeMachine;
                    if (timeMachine.Activate(Player))
                    {
                        AddEvent(Player.ID, TimeEvent.EventType.ACTIVATE_TIME_MACHINE, timeMachine.ID);
                    }
                    break;
                }
            }
        }
        ActivatedLastFrame = Player.IsActivating;

        if (Player.FlagDestroy)
        {
	    this.Player.Animator.Play("Base Layer.Player_Death", 0, 0);
            throw new TimeAnomalyException("Oh no!", "You died!", Player);
        }

        int thisTimeStep = TimeStep;
        PreSaveValidateTimeAnomalies();
        SaveSnapshotFull(TimeStep);
        PostSaveValidateTimeAnomalies();
        TimeStep++;
        FurthestTimeStep = Mathf.Max(TimeStep, FurthestTimeStep);
        Player.ClearActivate();

        if (timeTravelQueueEvent.Type == TimeEvent.EventType.TIME_TRAVEL)
        {
            int timeTravelStep = int.Parse(timeTravelQueueEvent.OtherData);
            targetTimeMachine = GetObjectByID(timeTravelQueueEvent.TargetID) as TimeMachineController;
            DoTimeTravel(timeTravelStep, targetTimeMachine, Player);
        }
    }

    public void SkipTime(bool skipExtra)
    {
        doTimeSkip = true;
        this.skipExtra = skipExtra;
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
            if (timeTracker != null && timeTracker.ID != id) // ID mismatch, remove here, and potentially recreate below
            {
                SaveObjectToPool(timeTracker);
                TimeTrackerObjects.Remove(id);
                AllReferencedObjects.Remove(id);
                if (timeTracker is TimeMachineController timeMachine)
                {
                    timeMachines.Remove(timeMachine);
                }
                timeTracker = null;
            }
            if (!alreadyDestroyed
                && timeTracker != null
                && !timeTracker.ShouldPoolObject)
            {
                // FLAG_DESTROY is false this timeStep AND this object is not pooled
                timeTracker.FlagDestroy = false; // reset current FlagDestroy value
                timeTracker.gameObject.SetActive(true);
            }
            
            if (timeTracker != null) continue; // object already exists, so continue 

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
                if (delete && timeTracker.ID != Player.ID)
                {
                    if (timeTracker.ShouldPoolObject)
                    {
                        SaveObjectToPool(timeTracker);
                        TimeTrackerObjects.Remove(i);
                        AllReferencedObjects.Remove(i);
                        if (timeTracker is TimeMachineController timeMachine)
                        {
                            timeMachines.Remove(timeMachine);
                        }
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
                    timeTracker.ForceRestoreSnapshot(history[timeStep]);
                }
                else
                {
                    timeTracker.PreUpdateLoadSnapshot(history[timeStep]);
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
    public void SetSnapshotValue<T>(ITimeTracker timeTracker, int timeStep, string parameter, T value, bool force=false, bool clearFuture=false) where T : IEquatable<T>
    {
        if (SnapshotHistoryById.TryGetValue(timeTracker.ID, out var history))
        {
            history.Set<T>(timeStep, parameter, value, force, clearFuture);
        }
    }

    public string GetUserFriendlyName(int id)
    {
        string objectType = ObjectTypeByID.TryGetValue(id, out var result) ? result : null;

        switch (objectType)
        {
            case TYPE_TIME_MACHINE: return "Time Machine";
            case TYPE_BOX: return "Crate";
            case TYPE_EXPLOAD_BOX: return "Explosives";
            
            case TYPE_GUARD: return "Guard";
            case TYPE_PLAYER: return "Player";
            case TYPE_EXPLOSION: return "Explosion";
            default: return "Unknown Object";
        }
    }
    public string GetObjectTypeByID(int id) => ObjectTypeByID.TryGetValue(id, out var result) ? result : null;
    public ICustomObject GetObjectByID(int id) => AllReferencedObjects.TryGetValue(id, out var result) ? result : null;
    public ITimeTracker GetTimeTrackerByID(int id) => TimeTrackerObjects.TryGetValue(id, out var result) ? result : null;
    
    public bool DropItem(PlayerController droppingPlayer, int targetID)
    {
        if (TimeTrackerObjects.TryGetValue(targetID, out var timeTracker))
        {
            Log($"Drop Item {targetID.ToString()}");
            Vector2 dropPos = droppingPlayer.Position.Get + new Vector2(droppingPlayer.facingRight ? 1.2f : -1.2f, 0);
            
            RaycastHit2D[] raycastHits = Physics2D.RaycastAll(droppingPlayer.Position.Get, droppingPlayer.facingRight ? Vector2.right : Vector2.left, 1.2f);
            foreach (var hit in raycastHits)
            {
                if (hit.collider.gameObject.layer != LayerMask.NameToLayer("LevelPlatforms")) continue;
                // set drop position to halfway between player and collision
                dropPos = (hit.point + droppingPlayer.Position.Get) / 2;
                break;
            }

            Vector2 prevPosition = timeTracker.Position.Current;
            timeTracker.Position.Current = dropPos; // move item to drop position

            if (timeTracker.SetItemState(false))
            {
                // copy player velocity when dropping
                if (GetObjectTypeByID(timeTracker.ID) == TYPE_BOX ||
                    GetObjectTypeByID(timeTracker.ID) == TYPE_EXPLOAD_BOX)
                {
                    timeTracker.gameObject.GetComponent<Rigidbody2D>().velocity = droppingPlayer.Velocity.Current;
                }

                return true;
            }
            
            timeTracker.Position.Current = prevPosition; // restore position if fail to drop
            return false;
        }
        else
        {
            LogError($"could not drop item {targetID.ToString()}");
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
                if (timeTracker.FlagDestroy && timeTracker.ID != Player.ID)
                {
                    if (timeTracker.ShouldPoolObject)
                    {
                        SaveObjectToPool(timeTracker);
                        TimeTrackerObjects.Remove(i);
                        AllReferencedObjects.Remove(i);
                        if (timeTracker is TimeMachineController timeMachine)
                        {
                            timeMachines.Remove(timeMachine);
                        }
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
        if(userPause) Resume();
        
        Debug.Log("---Retry---");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
	}

    public void RespawnLatest()
    {
        if(userPause) Resume();
        
        if (spawnState == null)
        {
            RetryLevel();
            return;
        }
        Debug.Log("---RespawnLatest---");
        
        // Remove popup if it exists
        var retryPopups = FindObjectsOfType<RetryPopup>();
        foreach (var popup in retryPopups)
        {
            Destroy(popup.gameObject); //TODO: destroying and recreating not the best idea long term... pool popup? make generic popup class?
        }

        // load player snapshot from current state (at the timestep of the spawnState)
        Player.ForceRestoreSnapshot(currentState.snapshotHistoryById[Player.ID][spawnState.timeStep]);

        // pool all objects
        for(int id = 0; id < NextID; id++)
        {
            if (TimeTrackerObjects.TryGetValue(id, out var timeTracker))
            {
                if(timeTracker.ShouldPoolObject)
                {
                    SaveObjectToPool(timeTracker);
                    TimeTrackerObjects.Remove(id);
                    AllReferencedObjects.Remove(id);
                    if (timeTracker is TimeMachineController timeMachine)
                    {
                        timeMachines.Remove(timeMachine);
                    }
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
        
        SetItemInUI(Player.ItemID); // reset UI for player's item
        
        // delete anomaly error
        if (instantiatedIndicator != null)
        {
            instantiatedIndicator.Remove();
        }
    }

    public void SetItemInUI(int id)
    {
        Sprite itemImage = tempImage;
        Color itemColor = Color.white;
        string itemLabel = "";
        var timeTracker = GetTimeTrackerByID(id);
        if (timeTracker != null)
        {
            timeTracker.GetItemSpriteProperties(out itemImage, out itemColor);
        }
        playerItem.SetActive(timeTracker != null); 
        Image playerItemImage = playerItem.GetComponentInChildren<Image>();
        playerItemImage.sprite = itemImage;
        playerItemImage.color = itemColor;
        TMP_Text playerItemLabel = playerItemImage.gameObject.GetComponentInChildren<TMP_Text>();
        playerItemLabel.text = itemLabel ?? "";
    }

    public void ShowRetryPopup(TimeAnomalyException e)
    {
        var popup = Instantiate(retryPopupPrefab, mainUICanvas.transform);
        popup.Init(e.Title, e.Body, RetryLevel, RespawnLatest);
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
        
        // ensure that Doppelganger's paths of motion are uninterrupted
        foreach (PlayerController p in PastPlayers)
        {
            //string historyColliderState = GetSnapshotValue<string>(p, TimeStep, nameof(PlayerController.GetCollisionStateString));
            //string currentColliderState = p.GetCollisionStateString(); 
            //if (historyColliderState != currentColliderState)
            //{
            //    Debug.Log($"{historyColliderState}\n{currentColliderState}");
            //    throw new TimeAnomalyException("Doppelganger was unable to follow his previous path of motion!");
            //}
            Vector2 historyPosition = GetSnapshotValue(p, TimeStep, p.Position.HistoryName, Vector2.positiveInfinity);
            if (Vector2.Distance(historyPosition, p.transform.position) > POSITION_ANOMALY_ERROR)
            {
                throw new TimeAnomalyException(symmetryBrokenTitle, "Doppelganger was unable to follow his previous path of motion!", p);
            }
        }
    }
    private void PostSaveValidateTimeAnomalies()
    {
        string symmetryBrokenTitle = "Symmetry Broken!";        
        
        // ensure that time machines are not used in such a way that it prevents a past player from going to the time they intended
        foreach (TimeMachineController timeMachine in timeMachines)
        {
            bool currentActivated = GetSnapshotValue<bool>(timeMachine, TimeStep, timeMachine.Activated.CurrentName);
            int historyCountdown = GetSnapshotValue(timeMachine, TimeStep, timeMachine.Countdown.HistoryName, -1);
            int currentCountdown = GetSnapshotValue(timeMachine, TimeStep, timeMachine.Countdown.CurrentName, -1);
            
            if (currentActivated && historyCountdown != -1)
            {
                throw new TimeAnomalyException(symmetryBrokenTitle, "Doppelganger tried activating an already active Time Machine!", timeMachine);
            }
            if (historyCountdown != -1 && currentCountdown != -1 && currentCountdown != historyCountdown)
            {
                throw new TimeAnomalyException(symmetryBrokenTitle, "Doppelganger tried activating a Time Machine in count-down!", timeMachine);
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

    public void QueueTimeTravel(TimeEvent timeEvent)
    {
        timeTravelQueueEvent = timeEvent;
        AddEvent(timeEvent.SourceID, timeEvent.Type, timeEvent.TargetID, timeEvent.OtherData);
    }
    
    public void DoTimeTravel(int timeTravelStep, TimeMachineController timeMachine, PlayerController player)
    {
        // TODO: if/when adding lerping to updates need to force no lerp when travelling in time
        IsPresent = false;
        
        Log("DoTimeTravel");

        _postProcessRenderer.SetActive(true);

        AnimateRewind = true;
        AnimateFrame = TimeStep;
        TimeStep = timeTravelStep;
        OccupiedTimeMachine = timeMachine;
        RewindFrameRate = (AnimateFrame - TimeStep) / 60;

        this.Player.PlayerInput.enabled = false;
        
        // flag player to destroy
        this.Player.FlagDestroy = true;
        this.Player.DidTimeTravel = true;
        SaveSnapshot(AnimateFrame-1, this.Player);

        // clone the player
        PlayerController newPlayer = AcquireAndInitPooledTimeTracker(TYPE_PLAYER, NextID++) as PlayerController;
        newPlayer.PlayerInput.enabled = true;
        newPlayer.CopyTimeTrackerState(this.Player);

        // if the player is holding an item, clone it
        if (this.Player.ItemID >= 0 && TimeTrackerObjects.TryGetValue(this.Player.ItemID, out var playerItem))
        {
            playerItem.FlagDestroy = true;
            SaveSnapshot(AnimateFrame-1, playerItem);

            ITimeTracker newPlayerItem = AcquireAndInitPooledTimeTracker(ObjectTypeByID[playerItem.ID], NextID++);
            newPlayerItem.CopyTimeTrackerState(playerItem);
            newPlayer.ItemID = newPlayerItem.ID; // update the new player's item id to match the cloned item id
            SaveSnapshot(timeTravelStep, newPlayerItem);
        }
        
        SaveSnapshot(timeTravelStep, newPlayer); // save the spawn position for the new player

	    this.Player.EnableShaders();
        this.Player = newPlayer; // update current player to the new one

	    this.Player.DisableShaders();
	
        { // set some initial values for when the player spawns in
            newPlayer.isSpriteOrderForced = true;
            newPlayer.SpriteRenderer.sortingOrder = 2;
            newPlayer.facingRight = false;
                
            SaveSnapshot(timeTravelStep, newPlayer, force:true);
        }
        
        { // clear 'history' values on the time machine for the frame this was activated
            timeMachine.Countdown.History = -1;
            timeMachine.ActivatedTimeStep.History = -1;
            timeMachine.Activated.History = false;
            timeMachine.Occupied.History = false;
            timeMachine.playerID.History = -1;
            
            timeMachine.Countdown.Current = -1;
            timeMachine.ActivatedTimeStep.Current = -1;
            timeMachine.Activated.Current = false;
            timeMachine.Occupied.Current = false;
            timeMachine.playerID.Current = -1;
            
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
            otherTimeMachine.playerID.Current = -1;
            SaveSnapshot(timeTravelStep, otherTimeMachine, force:true);
        }

    }
}
