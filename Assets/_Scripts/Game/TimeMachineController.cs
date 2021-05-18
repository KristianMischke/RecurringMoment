using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEditor;
using UnityEngine.PlayerLoop;

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
    public Animator animator;
    public Sprite foldSpriteIcon;

    public bool IsAnimatingOpenClose = false;
    public bool IsAnimatingFold = false;
    public bool IsAnimatingUnfold = false;
    public TimeInt playerID = new TimeInt("PlayerID");
    public int doneTimeTravelPlayerID = -1;
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
    
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int MainColor = Shader.PropertyToID("_MainColor");
    
    public static readonly int AnimateOpen = Animator.StringToHash("AnimateOpen");
    public static readonly int AnimIsFoldable = Animator.StringToHash("IsFoldable");
    public static readonly int AnimIsItem = Animator.StringToHash("IsItem");
    public static readonly int AnimateFolding = Animator.StringToHash("AnimateFolding");
    public static readonly int AnimateUnfolding = Animator.StringToHash("AnimateUnfolding");
    public bool IsAnimClosedState => animator.GetCurrentAnimatorStateInfo(0).IsName("TimeMachineClosed_Temp") || animator.GetCurrentAnimatorStateInfo(0).IsName("TimeMachineFoldClosed_Temp");
    public bool IsAnimFoldedState => animator.GetCurrentAnimatorStateInfo(0).IsName("TimeMachineFolded");
    
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

            playerID = otherTM.playerID;
            doneTimeTravelPlayerID = otherTM.doneTimeTravelPlayerID;
            IsAnimatingOpenClose = otherTM.IsAnimatingOpenClose;
            IsAnimatingFold = otherTM.IsAnimatingFold;
            IsAnimatingUnfold = otherTM.IsAnimatingUnfold;
            
            isFoldable = otherTM.isFoldable;
        }
        else
        {
            gameController.LogError($"Cannot copy state from {other.GetType()} to {nameof(TimeMachineController)}");
        }
    }
    
    //TODO: need way to handle TimeMachine folding before coming item!
    public bool SetItemState(bool state)
    {
        if (IsAnimatingFold || IsAnimatingUnfold) // can't pick up while animating
            return false;
        
        if (state) // trying to turn into an item
        {
            // time machine is occupied or activated (or not foldable), cannot move it
            if (!isFoldable || IsActivatedOrOccupied || Countdown.Current >= 0 || Countdown.History >= 0)
                return false;

            IsAnimatingFold = true;
            animator.SetBool(AnimateFolding, true);
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

                // cannot place time machine on top of ITimeTracker
                if (hitTimeTracker != null)
                {
                    gameObject.SetActive(false);
                    return false;
                }

                // cannot place time machine on top of button 
                if (hitObject.GetComponent<ButtonController>() != null)
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

            IsAnimatingUnfold = true;
            animator.SetBool(AnimateUnfolding, true);
        }
            
        ItemForm = state;
        gameObject.SetActive((!ItemForm && !FlagDestroy) || IsAnimatingFold);
        return true;
    }
    
    public virtual void GetItemSpriteProperties(out Sprite sprite, out Color color)
    {
        sprite = isFoldable ? foldSpriteIcon : renderer.sprite;
        color = isFoldable ? Color.white : renderer.color;
    }
    
    public bool IsEquivalentItem(ITimeTracker other)
    {
        return ID == other.ID;
        /*
         * NOTE: I was going to allow the player to pick up other Time Machines, but realized that the time histories
         *       would not necessarily line up and would cause a lot of potential issues, so opted for this feature
         *       to only be used for non-Time Machine objects for now.
         */
        /*TimeMachineController otherTimeMachine = other as TimeMachineController;
        if (otherTimeMachine == null) return false; // incorrect object type

        if (isFoldable != otherTimeMachine.isFoldable) return false; // must match foldable
        
        // not equivalent item if other machine is active in any way
        if (otherTimeMachine.IsActivatedOrOccupied || otherTimeMachine.Countdown.Current >= 0 || otherTimeMachine.Countdown.History >= 0)
            return false;

        return true;*/
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="playerController"></param>
    /// <returns>true if the machine is now active. false otherwise (i.e. false if fail, and false if now animating to time travel</returns>
    public bool Activate(PlayerController playerController)
    {
        if (Occupied.AnyTrue || Countdown.Current >= 0 || Countdown.History >= 0) // time machine is occupied, cannot use it
            return false;

        if (Activated.AnyTrue) // time machine is active, so  get ready to timetravel
        {
            IsAnimatingOpenClose = true;
            playerID.Current = playerController.ID;
            animator.SetBool(AnimateOpen, true);
            return false;
        }
            
        Countdown.Current = TIME_MACHINE_COUNTDOWN;
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
        animator.SetBool(AnimIsFoldable, isFoldable);
        animator.SetBool(AnimIsItem, ItemForm && !IsAnimatingFold);
        
        if (IsAnimatingOpenClose && IsAnimClosedState)
        {
            // stop animation, could be present or past TimeMachine
            IsAnimatingOpenClose = false;
            animator.SetBool(AnimateOpen, false);

            if (playerID.Current != -1) // execute time travel bc we have a playerID
            {
                int timeTravelDestStep = Activated.Current ? ActivatedTimeStep.Current : ActivatedTimeStep.History;
                ActivatedTimeStep.Current = -1;
                Activated.Current = false;
                gameController.QueueTimeTravel(new TimeEvent(playerID.Current, TimeEvent.EventType.TIME_TRAVEL, ID,
                    timeTravelDestStep.ToString()));
                AudioSource.PlayClipAtPoint(_timeTravelSound, Camera.main.transform.position, 0.6f);
                _source.Stop();
            }
        }
        else if (IsAnimatingOpenClose)
        {
            if (playerID.Current != -1 || playerID.History != -1) // keep player on TimeMachine
            {
                bool isAnimating = isFoldable
                                    ? animator.GetCurrentAnimatorStateInfo(0).IsName("TimeMachineFoldDoorOpenAnim")
                                    : animator.GetCurrentAnimatorStateInfo(0).IsName("Animating");
                PlayerController player = gameController.GetObjectByID(playerID.Current == -1 ? playerID.History : playerID.Current) as PlayerController;
                player.Position.Current = new Vector2(Position.Current.x, player.Position.Current.y);
                player.Velocity.Current = Vector2.zero;
                player.isSpriteOrderForced = true;
                bool isBeginning = !isAnimating || animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.5f;
                player.SpriteRenderer.sortingOrder = isBeginning ? 7 : 2;
            }
        }
        
        if (doneTimeTravelPlayerID != -1) // correct sorting order for player at beginning of replaying
        {
            bool isAnimating = isFoldable
                                ? animator.GetCurrentAnimatorStateInfo(0).IsName("TimeMachineFoldDoorOpenAnim")
                                : animator.GetCurrentAnimatorStateInfo(0).IsName("Animating");
            PlayerController player = gameController.GetObjectByID(doneTimeTravelPlayerID) as PlayerController;
            player.Position.Current = new Vector2(Position.Current.x, player.Position.Current.y);
            player.Velocity.Current = Vector2.zero;
            player.isSpriteOrderForced = true;
            bool isBeginning = !isAnimating || animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.5f;
            player.SpriteRenderer.sortingOrder = isBeginning ? 2 : 7;
            if (IsAnimClosedState && gameController.TimeStep > ActivatedTimeStep.History + 5)
            {
                doneTimeTravelPlayerID = -1;
                player.isSpriteOrderForced = false;
            }
        }

        if (IsAnimClosedState)
        {
            IsAnimatingUnfold = false;
            animator.SetBool(AnimateUnfolding, false);
        }
        
        if (IsAnimFoldedState && IsAnimatingFold)
        {
            gameObject.SetActive(false);
            IsAnimatingFold = false;
            animator.SetBool(AnimateFolding, false);
        }
        
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

    public string GetDisplayString()
    {
        int displayStartStep = ActivatedTimeStep.Current == -1 ? ActivatedTimeStep.History : ActivatedTimeStep.Current;
        int displayCountdown = Countdown.Current == -1 ? Countdown.History : Countdown.Current;
        if (displayCountdown >= 0)
        {
            return (displayCountdown * Time.fixedDeltaTime).ToString("0.0");
        }
        if (displayStartStep >= 0)
        {
            return ((gameController.TimeStep - displayStartStep) * Time.fixedDeltaTime).ToString("0.0");
        }
        return isFoldable ? "FOLD" : "TM";                
    }

    public void Start()
    {
	    _source = GetComponent<AudioSource>();
    }
    public void Update()
    {
        TextBubbleHint.SetActive(isFoldable);

        int displayCountdown = Countdown.Current == -1 ? Countdown.History : Countdown.Current;
        timeText.text = GetDisplayString();
        
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetTexture(MainTex, renderer.sprite.texture);
        
        Color indicatorColor = Color.yellow;
        if (Occupied.AnyTrue)
        {
            indicatorColor = new Color(0f, 1f, 0f);
        }
        else if (Activated.AnyTrue)
        {
            indicatorColor = new Color(1f, 0f, 0f);
        }
        else if (displayCountdown >= 0)
        {
            indicatorColor = new Color(1f, 0.7f, 0f);
        }

        propertyBlock.SetColor(MainColor, indicatorColor);
        renderer.SetPropertyBlock(propertyBlock);
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
        playerID.SaveSnapshot(snapshotDictionary, force);
        
        snapshotDictionary.Set(GameController.FLAG_DESTROY, FlagDestroy, force);
        snapshotDictionary.Set(nameof(ItemForm), ItemForm, force, clearFuture:true);
        snapshotDictionary.Set(nameof(IsAnimatingOpenClose), IsAnimatingOpenClose); // don't force animations?!
        snapshotDictionary.Set(nameof(IsAnimatingFold), IsAnimatingFold);
        snapshotDictionary.Set(nameof(IsAnimatingUnfold), IsAnimatingUnfold);
        snapshotDictionary.Set(nameof(doneTimeTravelPlayerID), doneTimeTravelPlayerID, force);
        snapshotDictionary.Set(nameof(isFoldable), isFoldable, force);
        Position.SaveSnapshot(snapshotDictionary, force);
    }

    public void PreUpdateLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        Activated.LoadSnapshot(snapshotDictionary);
        Occupied.LoadSnapshot(snapshotDictionary);
        ActivatedTimeStep.LoadSnapshot(snapshotDictionary);
        Countdown.LoadSnapshot(snapshotDictionary);
        playerID.LoadSnapshot(snapshotDictionary);
        
        FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);

        gameObject.SetActive((!ItemForm && !FlagDestroy) || IsAnimatingFold);

        IsAnimatingOpenClose = snapshotDictionary.Get<bool>(nameof(IsAnimatingOpenClose));
        animator.SetBool(AnimateOpen, IsAnimatingOpenClose);
        IsAnimatingFold = snapshotDictionary.Get<bool>(nameof(IsAnimatingFold));
        animator.SetBool(AnimateFolding, IsAnimatingFold);
        IsAnimatingUnfold = snapshotDictionary.Get<bool>(nameof(IsAnimatingUnfold));
        animator.SetBool(AnimateUnfolding, IsAnimatingUnfold);
        doneTimeTravelPlayerID = snapshotDictionary.Get<int>(nameof(doneTimeTravelPlayerID));
        
        Occupied.Current &= Activated.History;
    }

    public void ForceRestoreSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        Activated.ForceLoadSnapshot(snapshotDictionary);
        Occupied.ForceLoadSnapshot(snapshotDictionary);
        ActivatedTimeStep.ForceLoadSnapshot(snapshotDictionary);
        Countdown.ForceLoadSnapshot(snapshotDictionary);
        playerID.ForceLoadSnapshot(snapshotDictionary);

        FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);
        ItemForm = snapshotDictionary.Get<bool>(nameof(ItemForm));
        Position.ForceLoadSnapshot(snapshotDictionary);
        Position.Current = Position.History;
        
        IsAnimatingOpenClose = snapshotDictionary.Get<bool>(nameof(IsAnimatingOpenClose));
        animator.SetBool(AnimateOpen, IsAnimatingOpenClose);
        IsAnimatingFold = snapshotDictionary.Get<bool>(nameof(IsAnimatingFold));
        animator.SetBool(AnimateFolding, IsAnimatingFold);
        IsAnimatingUnfold = snapshotDictionary.Get<bool>(nameof(IsAnimatingUnfold));
        animator.SetBool(AnimateUnfolding, IsAnimatingUnfold);
        doneTimeTravelPlayerID = snapshotDictionary.Get<int>(nameof(doneTimeTravelPlayerID));
        isFoldable = snapshotDictionary.Get<bool>(nameof(isFoldable));
        animator.SetBool(AnimIsFoldable, isFoldable);
        animator.SetBool(AnimIsItem, ItemForm);
        
        gameObject.SetActive((!ItemForm && !FlagDestroy) || IsAnimatingFold);
    }
}
