using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour, ITimeTracker
{
    // NOTE: don't access these directly, use the properties that start with uppercase letters
    private Rigidbody2D _rigidbody;
    private CapsuleCollider2D _capsuleCollider;
    private Collider2D _grabCollider;
    private PlayerInput _playerInput;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
	
    #region EasyAccessProperties
    public Rigidbody2D Rigidbody
    {
        get
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody2D>();
            }
            return _rigidbody;
        }
    }
    public CapsuleCollider2D CapsuleCollider
    {
        get
        {
            if (_capsuleCollider == null)
            {
                _capsuleCollider = GetComponent<CapsuleCollider2D>();
            }
            return _capsuleCollider;
        }
    }

    public Collider2D GrabCollider;
    public PlayerInput PlayerInput
    {
        get
        {
            if (_playerInput == null)
            {
                _playerInput = GetComponent<PlayerInput>();
            }
            return _playerInput;
        }
    }
    public Animator Animator
    {
        get
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }
            return _animator;
        }
    }
    public SpriteRenderer SpriteRenderer //TODO: tidy up art related code
    {
        get
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            return _spriteRenderer;
        }
    }
    #endregion

    [SerializeField] private float maxHorizontalSpeed;
    [SerializeField] private float jumpMultiplier;
    [SerializeField] private float movementMultiplier;
    [SerializeField] private bool isGrounded = false;

    public int ItemID = -1;
    
    //apply in fixed update
    private float verticalInput, horizontalInput;
    private bool jump;
    private bool isActivating, historyActivating;

    public bool facingRight = true; 
    
    public bool IsActivating => isActivating;
    public bool HistoryActivating => historyActivating;

    public bool DidTimeTravel { get; set; }
    
    /* TODO: TimeBool to track if the player is grounded (use to determine if Doppelganger is in valid location
     *       This is important in case player is standing on ground that moves/is destroyed
    */
    
    private GameController gameController;
    public int ID { get; private set; }
    public TimeVector Position { get; private set; }
    public TimeVector Velocity { get; private set; }
    public TimeBool ItemForm { get; } = null;
    public bool FlagDestroy { get; set; }
    public bool ShouldPoolObject => true;
    
    public bool SetItemState(bool state) => false;
    
    public void CopyTimeTrackerState(ITimeTracker other)
    {
        PlayerController otherPlayer = other as PlayerController;
        if (otherPlayer != null)
        {
            Position.Copy(otherPlayer.Position);
            Velocity.Copy(otherPlayer.Velocity);
        }
        else
        {
            gameController.LogError($"Cannot copy state from {other.GetType()} to {nameof(PlayerController)}");
        }
    }

    //---PlayerInputs---
    public void OnMove(InputValue movementValue)
    {
        if (gameController.player != this) return;

        Vector2 movementVector = movementValue.Get<Vector2>();

        horizontalInput = movementVector.x;
        verticalInput = movementVector.y;
    }
    public void OnJump(InputValue inputValue)
    {
        if (gameController.player != this) return;

        jump |= inputValue.isPressed && isGrounded;
    }
    public void OnActivate(InputValue inputValue)
    {
        if (gameController.player != this) return;

        isActivating = inputValue.isPressed;
    }
    public void OnSkipTime(InputValue inputValue)
    {
        if (gameController.player != this) return;

        gameController.SkipTime();
    }
    public void OnSaveDebugHistory(InputValue inputValue)
    {
        if (gameController.player != this) return;

        gameController.ExportHistory();
    }

    public void OnRetry(InputValue inputValue)
    {
        if (gameController.player != this) return;

        gameController.RetryLevel();
    }

    public void OnRespawn(InputValue inputValue)
    {
        if (gameController.player != this) return;

        gameController.RespawnLatest();
    }

    private bool queueGrab = false;
    private static readonly int Walking = Animator.StringToHash("Walking");

    public void OnGrab(InputValue inputValue)
    {
        if (gameController.player != this) return;
        
        queueGrab = true;
    }
	
	
	public void OnPause(InputValue inputValue)
	{
		Debug.Log("Pressed the escape key"); 
		gameController.ToggleUserPause(); 
	}
	
    //------

    private void DoGrab()
    {
        if (gameController.player != this) return; // only current player can initiate grab with this method
        
        // to get the sprite 
        Sprite itemImage = gameController.tempImage;
        Color itemColor = Color.white;
        string itemLabel = "";
        bool isFound = false;

        if (ItemID != -1) // not -1 means it is a valid item, so we ARE holding something
        {
            if (gameController.DropItem(this, ItemID)) // check to see if we successfully drop the item
            {
                gameController.AddEvent(ID, TimeEvent.EventType.PLAYER_DROP, ItemID);
                ItemID = -1;
                gameController.playerItem.SetActive(false);
                gameController.playerItem.GetComponentInChildren<Image>().sprite = itemImage;
            }
        }
        else
        {
            List<Collider2D> contacts = new List<Collider2D>();
            GrabCollider.GetContacts(contacts);
            foreach (var contact in contacts)
            {
                if (contact.gameObject == gameObject) continue;
                
                ITimeTracker timeTracker = GameController.GetTimeTrackerComponent(contact.gameObject, true);
				if (timeTracker != null)
                {
                    if (timeTracker.SetItemState(true))
                    {
                        isFound = true;
                        ItemID = timeTracker.ID;
                        var sr = timeTracker.gameObject.GetComponentInChildren<SpriteRenderer>(); 
                        itemImage = sr.sprite;
                        itemColor = sr.color;
                        Debug.Log("The name of the sprite is : " + itemImage.name);
                        
                        ExplodeBox explodeBox = timeTracker as ExplodeBox;
                        if (explodeBox != null)
                        {
                            itemLabel = explodeBox.label;
                        }
                    }
                }

                // break the loop if we found an object bc we can only pick up one object
                if (isFound)
                {
                    break;
                }
            }
			
			// this is when he grabs a object and it shows up in the screen 
			if(isFound == true)
			{
                gameController.AddEvent(ID, TimeEvent.EventType.PLAYER_GRAB, ItemID);
				gameController.playerItem.SetActive(true); // shows the screen to the player 
                Image playerItemImage = gameController.playerItem.GetComponentInChildren<Image>(); 
                playerItemImage.sprite = itemImage;
                playerItemImage.color = itemColor;
                TMP_Text playerItemLabel = playerItemImage.gameObject.GetComponentInChildren<TMP_Text>();
                playerItemLabel.text = itemLabel ?? "";
				Debug.Log("The name of the sprite is : " + itemImage.name);
			}
        }
    }
    
    public void ExecutePastEvent(TimeEvent timeEvent)
    {
        if(gameController.player == this) gameController.LogError($"ExecutePastEvent on current player!");
        
        if (timeEvent.Type == TimeEvent.EventType.PLAYER_GRAB)
        {
            bool isFound = false;
            if (gameController.player != this)
            {
                if (ItemID != -1)
                {
                    gameController.LogError($"Trying to grab {timeEvent.TargetID} when already holding {ItemID}!");
                }
            
                List<Collider2D> contacts = new List<Collider2D>();
                GrabCollider.GetContacts(contacts);
                foreach (var contact in contacts)
                {
                    if (contact.gameObject == gameObject) continue;

                    ITimeTracker timeTracker = GameController.GetTimeTrackerComponent(contact.gameObject, true);
                    if (timeTracker != null && timeTracker.ID == timeEvent.TargetID)
                    {
                        isFound = true;
                    }

                    // break the loop if we found the object bc we can only pick up one object
                    if (isFound)
                    {
                        timeTracker.SetItemState(true);
                        ItemID = timeEvent.TargetID;
                        break;
                    }
                }

                if (!isFound)
                {
                    gameController.LogError($"Player {ID} could not grab {timeEvent.TargetID}");
                    throw new TimeAnomalyException("Time Anomaly!",
                        $"Doppelganger could not grab the {gameController.GetUserFriendlyName(timeEvent.TargetID)}");
                }
            }
        } // end PLAYER_GRAB
        else if (timeEvent.Type == TimeEvent.EventType.PLAYER_DROP)
        {
            if (gameController.DropItem(this, timeEvent.TargetID)) // check to see if we successfully drop the item
            {
                ItemID = -1;
            }
        } // end PLAYER_DROP
        else if (timeEvent.Type == TimeEvent.EventType.TIME_TRAVEL)
        {
            TimeMachineController timeMachine = gameController.GetTimeTrackerByID(timeEvent.TargetID) as TimeMachineController;

            if (timeMachine == null || !timeMachine.IsTouching(gameObject))
            {
                throw new TimeAnomalyException("Time Anomaly!", "Doppelganger could not use the Time Machine!");
            }
        } // end TIME_TRAVEL
        else if (timeEvent.Type == TimeEvent.EventType.ACTIVATE_TIME_MACHINE)
        {
            TimeMachineController timeMachine = gameController.GetTimeTrackerByID(timeEvent.TargetID) as TimeMachineController;

            if (timeMachine == null || !timeMachine.IsTouching(gameObject))
            {
                throw new TimeAnomalyException("Time Anomaly!", "Doppelganger could not activate the Time Machine!");
            }
        } // end ACTIVATE_TIME_MACHINE
    }

    public void ClearActivate()
    {
        isActivating = false;
    }

    public void ClearState()
    {
        gameController = null;
        ID = -1;
        FlagDestroy = false;
        
        verticalInput = 0;
        horizontalInput = 0;
        jump = false;
        isActivating = false;
        historyActivating = false;
        ItemID = ItemID = -1;
        DidTimeTravel = false;

        queueGrab = false;
    }

    private void Update()
    {
        Animator.SetBool(Walking, Rigidbody.velocity != Vector2.zero);
        
        if (Rigidbody.velocity.x != 0)
        {
            facingRight = Rigidbody.velocity.x > 0;
            SpriteRenderer.flipX = facingRight;
        }
        if (gameController.player != this)
        {
            Color temp = SpriteRenderer.color;
            temp.r = 0.5f;
            temp.g = 0.5f;
            temp.b = 0.5f;
            SpriteRenderer.color = temp;
        }
        else
        {
            Color temp = SpriteRenderer.color;
            temp.r = 1.0f;
            temp.g = 1.0f;
            temp.b = 1.0f;
            SpriteRenderer.color = temp;
        }

    }

    void FixedUpdate()
    {
        if (this != gameController.player) return; // don't update physics from inputs if not main player
        
        UpdateIsGrounded();
        if (jump)
        {
            Rigidbody.AddForce(Vector2.up * jumpMultiplier, ForceMode2D.Impulse);
            jump = false;
        }
        Rigidbody.AddForce(new Vector2(horizontalInput, 0)*movementMultiplier);
        Rigidbody.velocity = new Vector2(Mathf.Clamp(Rigidbody.velocity.x, -maxHorizontalSpeed, maxHorizontalSpeed), Rigidbody.velocity.y);
    }

    public void GameUpdate()
    {
        if (queueGrab) // this is only for the current player
        {
            DoGrab();
            queueGrab = false;
        }
    }

    void UpdateIsGrounded()
    {
        RaycastHit2D[] raycastHits = Physics2D.RaycastAll(transform.position, Vector2.down, CapsuleCollider.size.y);//, LayerMask.NameToLayer("LevelPlatforms"));
        isGrounded = false;
        for (int i = 0; i < raycastHits.Length; i++)
        {
            if (raycastHits[i].collider.gameObject == gameObject) continue;

            if (raycastHits[i].collider.gameObject.layer == LayerMask.NameToLayer("LevelPlatforms")
                && raycastHits[i].point.y < transform.position.y - CapsuleCollider.size.y/2 + 0.01
                && raycastHits[i].point.y > transform.position.y - CapsuleCollider.size.y/2 - 0.01)
            {
                isGrounded = true;
                return;
            }
        }
    }

    public virtual void OnPoolInstantiate()
    {
        PlayerInput.enabled = false;
    }

    public virtual void OnPoolInit()
    {
        PlayerInput.enabled = false;
    }

    public virtual void OnPoolRelease()
    {
        PlayerInput.enabled = false;
        ClearState();
    }
    
    public void Init(GameController gameController, int id)
    {
        this.gameController = gameController;
        ID = id;
        name = $"Player {id.ToString()}";

        Position = new TimeVector("Position", x => Rigidbody.position = x, () => Rigidbody.position);
        Velocity = new TimeVector("Velocity", x => Rigidbody.velocity = x, () => Rigidbody.velocity);
    }

    public string GetCollisionStateString()
    {
        List<Collider2D> contactColliders = new List<Collider2D>();
        _capsuleCollider.GetContacts(contactColliders);
        
        List<string> colliderStrings = new List<string>();
        foreach (var collider in contactColliders)
        {
            if (collider.gameObject == gameController.player.gameObject || collider.gameObject == this.gameObject) continue;
            
            ITimeTracker timeTracker = GameController.GetTimeTrackerComponent(collider.gameObject);
            if (timeTracker != null)
            {
                colliderStrings.Add(timeTracker.ID.ToString());
            }
            else
            {
                colliderStrings.Add($"U{collider.GetInstanceID().ToString()}");   
            }
        }
        
        colliderStrings.Sort();

        return string.Join(",", colliderStrings);
    }

    public void SaveSnapshot(TimeDict.TimeSlice snapshotDictionary, bool force=false)
    {
        Position.SaveSnapshot(snapshotDictionary, force);
        Velocity.SaveSnapshot(snapshotDictionary, force);
        snapshotDictionary.Set(nameof(ItemID), ItemID, force, clearFuture:true);
        snapshotDictionary.Set(nameof(Rigidbody.rotation), Rigidbody.rotation, force);
        snapshotDictionary.Set(nameof(isActivating), isActivating, force);
        snapshotDictionary.Set(nameof(DidTimeTravel), DidTimeTravel, force);
        //snapshotDictionary[nameof(GetCollisionStateString)] = GetCollisionStateString();
        snapshotDictionary.Set(GameController.FLAG_DESTROY, FlagDestroy, force);
        //NOTE: players should never be in item form, so don't save/load that info here
    }

    // TODO: add fixed frame # associated with snapshot? and Lerp in update loop?!
    public void LoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        Position.LoadSnapshot(snapshotDictionary);
        Velocity.LoadSnapshot(snapshotDictionary);

        if (gameController.player != this) // we don't want the current player to revert to their history positions/velocity
        {
            Position.Current = Position.History;
            Velocity.Current = Velocity.History;
        }

        Rigidbody.rotation = snapshotDictionary.Get<float>(nameof(Rigidbody.rotation));
        historyActivating = snapshotDictionary.Get<bool>(nameof(isActivating));
        DidTimeTravel = snapshotDictionary.Get<bool>(nameof(DidTimeTravel));

        FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);
    }

    public void ForceLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        ItemID = snapshotDictionary.Get<int>(nameof(ItemID));
        Position.LoadSnapshot(snapshotDictionary);
        Velocity.LoadSnapshot(snapshotDictionary);

        Position.Current = Position.History;
        Velocity.Current = Velocity.History;

        Rigidbody.rotation = snapshotDictionary.Get<float>(nameof(Rigidbody.rotation));
        historyActivating = snapshotDictionary.Get<bool>(nameof(isActivating));
        DidTimeTravel = snapshotDictionary.Get<bool>(nameof(DidTimeTravel));
        
        FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);
    }
}
