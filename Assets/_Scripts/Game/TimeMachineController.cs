using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEditor;

[ExecuteInEditMode]
public class TimeMachineController : MonoBehaviour, ITimeTracker
{
    public const int TIME_MACHINE_COUNTDOWN = 200;

    private HashSet<GameObject> triggeringObjects = new HashSet<GameObject>();
    private GameController gameController;

    [SerializeField] private AudioClip _timeTravelSound;
    [SerializeField] private AudioClip _startupSound;
    [SerializeField] private AudioClip _activeSound;

    private AudioSource _source;

    // art related
    public SpriteRenderer renderer;
    public TMP_Text timeText;
    public GameObject TextBubbleHint;

    public TimeBool Activated = new TimeBool("Activated");
    public TimeBool Occupied = new TimeBool("Occupied");
    public TimeInt ActivatedTimeStep = new TimeInt("ActivatedTimeStep");
    public TimeInt Countdown = new TimeInt("Countdown");

    public bool IsActivatedOrOccupied => Activated.AnyTrue || Occupied.AnyTrue;

    public int ID { get; private set; }
    public TimeVector Position { get; private set; }
    private bool ItemForm = false;

    public bool FlagDestroy { get; set; }
    public bool ShouldPoolObject => true;


    //Whether or not a machine is able to be converted into item form
    public bool isFoldable = false;

    public void CopyTimeTrackerState(ITimeTracker other)
    {
        TimeMachineController otherTM = other as TimeMachineController;
        if (otherTM != null)
        {
            Activated.Copy(otherTM.Activated);
            Occupied.Copy(otherTM.Occupied);
            ActivatedTimeStep.Copy(otherTM.ActivatedTimeStep);
            Countdown.Copy(otherTM.Countdown);
            
            Position.Copy(otherTM.Position);
            ItemForm = otherTM.ItemForm;
            
            isFoldable = otherTM.isFoldable;
        }
        else
        {
            gameController.LogError($"Cannot copy state from {other.GetType()} to {nameof(TimeMachineController)}");
        }
    }
    
    public bool SetItemState(bool state)
    {
        if (state) // trying to turn into an item
        {
            // time machine is occupied or activated (or not foldable), cannot move it
            if (!isFoldable || IsActivatedOrOccupied || Countdown.Current >= 0 || Countdown.History >= 0)
                return false;
        }
        else // trying to turn back into time machine
        {
            gameObject.SetActive(true);
            // ensure the time machine is touching the level platform
            BoxCollider2D collider = GetComponent<BoxCollider2D>();
            RaycastHit2D[] raycastHits = Physics2D.RaycastAll((Vector2)transform.position + collider.offset, Vector2.down);
            
            for (int i = 0; i < raycastHits.Length; i++)
            {
                GameObject hitObject = raycastHits[i].collider.gameObject;
                ITimeTracker hitTimeTracker = GameController.GetTimeTrackerComponent(hitObject, true);
                if (hitObject == gameObject || hitTimeTracker is PlayerController || hitTimeTracker == this) continue;

                // cannot place time machine ontop of ITimeTracker
                if (hitTimeTracker != null)
                {
                    gameObject.SetActive(false);
                    return false;
                }

                // cannot place time machine further than collider distance (e.g. when jumping)
                if (Mathf.Abs(raycastHits[i].point.y - transform.position.y) > collider.size.y)
                {
                    gameObject.SetActive(false);
                    return false;
                }

                bool CheckSides(RaycastHit2D[] sideHits)
                {
                    foreach (var sideHit in sideHits)
                    {
                        if (sideHit.collider.gameObject.layer == LayerMask.NameToLayer("LevelPlatforms"))
                        {
                            // if we hit a platform to the side that is inside our collider, fail placement
                            if (Mathf.Abs(sideHit.point.x - transform.position.x) < collider.size.x)
                            {
                                return false;
                            }
                            break;
                        }
                    }

                    return true;
                }
                
                // ensure the time machine is not obstructed on either side
                if (!CheckSides(Physics2D.RaycastAll((Vector2)transform.position + collider.offset, Vector2.left)))
                {
                    gameObject.SetActive(false);
                    return false;
                }
                if (!CheckSides(Physics2D.RaycastAll((Vector2)transform.position + collider.offset, Vector2.right)))
                {
                    gameObject.SetActive(false);
                    return false;
                }
                
                // Machine is placed on the ground, so align to the ground platform
                if (hitObject.layer == LayerMask.NameToLayer("LevelPlatforms"))
                {
                    Position.Current = raycastHits[i].point;
                    break;
                }
            }
        }
            
        ItemForm = state;
        gameObject.SetActive(!ItemForm && !FlagDestroy);
        return true;
    }

    public bool Activate(out int timeTravelDestStep)
    {
        timeTravelDestStep = -1;

        if (Occupied.AnyTrue || Countdown.Current >= 0 || Countdown.History >= 0) // time machine is occupied, cannot use it
            return false;

        if (Activated.AnyTrue) // time machine is active, so deactivate and do timetravel
        {
            timeTravelDestStep = Activated.Current ? ActivatedTimeStep.Current : ActivatedTimeStep.History;
            ActivatedTimeStep.Current = -1;
            Activated.Current = false;
	    AudioSource.PlayClipAtPoint(_timeTravelSound, Camera.main.transform.position, 0.6f);
	    _source.Stop();
        }
        else
        {
            Countdown.Current = TIME_MACHINE_COUNTDOWN;
        }

        return true;
    }

    public void ExecutePastEvent(TimeEvent timeEvent)
    {
    }

    public void BackToPresent()
    {
        Activated.Current |= Activated.History;
        Occupied.Current |= Occupied.History;
        if (ActivatedTimeStep.Current == -1) ActivatedTimeStep.Current = ActivatedTimeStep.History;
        if (Countdown.Current == -1) Countdown.Current = Countdown.History;

        Activated.History = false;
        Occupied.History = false;
        ActivatedTimeStep.History = -1;
        Countdown.History = -1;
    }

    public bool IsTouching(GameObject other)
    {
        return triggeringObjects.Contains(other);
    }


    public void GameUpdate()
    {   
        if (Countdown.Current > 0)
        {
	    if(!(_source.isPlaying))
	    {
		_source.Stop();
		_source.clip = _startupSound;
		_source.Play();
	    }
            Countdown.Current--;
        }

        if (Countdown.Current == 0)
        {
            Countdown.Current = -1;
            Countdown.History = -1;

            Activated.Current = true;
            ActivatedTimeStep.Current = gameController.TimeStep;

	    _source.Stop();
	    _source.clip = _activeSound;
	    _source.Play();
        }
    }

    public void Start()
    {
        renderer = GetComponentInChildren<SpriteRenderer>();
	_source = GetComponent<AudioSource>();
    }
    public void Update()
    {
        if (Occupied.AnyTrue)
        {
            renderer.color = new Color(0.8f, 0.8f, 1f);
        }
        else if (Activated.AnyTrue)
        {
            renderer.color = new Color(1f, 1f, 0.8f);
        }
        else
        {
            renderer.color = new Color(1f, 1f, 1f);
        }

        TextBubbleHint.SetActive(isFoldable);

        int displayStartStep = ActivatedTimeStep.Current == -1 ? ActivatedTimeStep.History : ActivatedTimeStep.Current;
        int displayCountdown = Countdown.Current == -1 ? Countdown.History : Countdown.Current;
        if (displayCountdown >= 0)
        {
            timeText.text = (displayCountdown * Time.fixedDeltaTime).ToString("0.0");
        }
        else if (displayStartStep >= 0)
        {
            timeText.text = ((gameController.TimeStep - displayStartStep) * Time.fixedDeltaTime).ToString("0.0");
        }
        else
        {
            timeText.text = isFoldable ? "FOLD" : "TM";                
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            triggeringObjects.Add(collision.gameObject);
        }
    }
    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            triggeringObjects.Remove(collision.gameObject);
        }
    }

    public virtual void OnPoolInstantiate() { }
    public virtual void OnPoolInit() { }
    public virtual void OnPoolRelease() { }
    public void Init(GameController gameController, int id)
    {
        this.gameController = gameController;
        ID = id;
        
        Position = new TimeVector("Position", x => transform.position = x, () => transform.position, true);
    }

    public void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false)
    {
        Activated.SaveSnapshot(snapshotDictionary, force);
        Occupied.SaveSnapshot(snapshotDictionary, force);
        ActivatedTimeStep.SaveSnapshot(snapshotDictionary, force);
        Countdown.SaveSnapshot(snapshotDictionary, force);
        
        snapshotDictionary.Set(GameController.FLAG_DESTROY, FlagDestroy, force);
        snapshotDictionary.Set(nameof(ItemForm), ItemForm, force, clearFuture:true);
        Position.SaveSnapshot(snapshotDictionary, force);
    }

    public void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        Activated.LoadSnapshot(snapshotDictionary);
        Occupied.LoadSnapshot(snapshotDictionary);
        ActivatedTimeStep.LoadSnapshot(snapshotDictionary);
        Countdown.LoadSnapshot(snapshotDictionary);
        
        FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);

        gameObject.SetActive(!ItemForm && !FlagDestroy);
        
        Occupied.Current &= Activated.History;
    }

    public void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        Activated.ForceLoadSnapshot(snapshotDictionary);
        Occupied.ForceLoadSnapshot(snapshotDictionary);
        ActivatedTimeStep.ForceLoadSnapshot(snapshotDictionary);
        Countdown.ForceLoadSnapshot(snapshotDictionary);

        FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);
        ItemForm = snapshotDictionary.Get<bool>(nameof(ItemForm));
        Position.ForceLoadSnapshot(snapshotDictionary);
        Position.Current = Position.History;
        
        gameObject.SetActive(!ItemForm && !FlagDestroy);
    }
}
