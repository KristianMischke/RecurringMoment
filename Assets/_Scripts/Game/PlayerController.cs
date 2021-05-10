using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.PlayerLoop;
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
    private Material _material;
	
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
    [SerializeField] private float _jumpVelocityChange;
    [SerializeField] private float _jumpAcceleration;
    [SerializeField] private float movementMultiplier;
    [SerializeField] private bool isGrounded = false;
    [SerializeField] private float _maxJumpTime;

    private float _startJumpTime = 0;

    public int ItemID = -1;
    
    //apply in fixed update
    private float verticalInput, horizontalInput;
    private bool jump;
    private bool isActivating, historyActivating;

    public bool facingRight = true;
    public bool isSpriteOrderForced = false;
    
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
    public void GetItemSpriteProperties(out Sprite sprite, out Color color)
    {
        sprite = null;
        color = Color.magenta;
    }

    public bool IsEquivalentItem(ITimeTracker other) => false;
    
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
        if (gameController.CurrentPlayerID != ID) return;

        Vector2 movementVector = movementValue.Get<Vector2>();

        horizontalInput = movementVector.x;
        verticalInput = movementVector.y;
    }
    public void OnJump(InputValue inputValue)
    {
        if (gameController.CurrentPlayerID != ID) return;

        jump = inputValue.isPressed;
    }
    public void OnActivate(InputValue inputValue)
    {
        if (gameController.CurrentPlayerID != ID) return;

        isActivating = inputValue.isPressed;
		Debug.Log("Activated a time machine"); 
    }
    public void OnSkipTime(InputValue inputValue)
    {
        if (gameController.CurrentPlayerID != ID) return;

        gameController.SkipTime(false);
    }
    public void OnSkipExtraTime(InputValue inputValue)
    {
        if (gameController.CurrentPlayerID != ID) return;

        gameController.SkipTime(true);
    }
    public void OnSaveDebugHistory(InputValue inputValue)
    {
        if (gameController.CurrentPlayerID != ID) return;

        gameController.ExportHistory();
    }

    public void OnRetry(InputValue inputValue)
    {
        if (gameController.CurrentPlayerID != ID) return;

        gameController.RetryLevel();
    }

    public void OnRespawn(InputValue inputValue)
     {
        if (gameController.CurrentPlayerID != ID) return;

        gameController.RespawnLatest();
    }

    private bool queueGrab = false;
    private static readonly int Walking = Animator.StringToHash("Walking");
    private static readonly int Jumping = Animator.StringToHash("Jumping");
    private static readonly int Grounded = Animator.StringToHash("Grounded");

    public void OnGrab(InputValue inputValue)
    {
        if (gameController.CurrentPlayerID != ID) return;
        
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
        if (gameController.CurrentPlayerID != ID) return; // only current player can initiate grab with this method
        
        bool isFound = false;

        if (ItemID != -1) // not -1 means it is a valid item, so we ARE holding something
        {
            if (gameController.DropItem(this, ItemID)) // check to see if we successfully drop the item
            {
                gameController.AddEvent(ID, TimeEvent.EventType.PLAYER_DROP, ItemID);
                gameController.SetItemInUI(-1);
                ItemID = -1;
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
				gameController.SetItemInUI(ItemID);
		    }
        }
    }
    
    public void ExecutePastEvent(TimeEvent timeEvent)
    {
        if(gameController.CurrentPlayerID == ID) gameController.LogError($"ExecutePastEvent on current player!");
        
        if (timeEvent.Type == TimeEvent.EventType.PLAYER_GRAB)
        {
            if (gameController.CurrentPlayerID != ID)
            {
                bool isFound = false;
                ITimeTracker bestMatch = null;
                ITimeTracker originalItem = gameController.GetObjectByID(timeEvent.TargetID) as ITimeTracker;
                // NOTE: There might be a bug if the original item was destroyed before this event occurs...
                //       
                
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
                    if (timeTracker == null) continue;
                    
                    if (timeTracker.ID == timeEvent.TargetID)
                    {
                        isFound = true;
                    }

                    if (isFound)
                    {
                        isFound = timeTracker.SetItemState(true);
                    }

                    // break the loop if we found the object bc we can only pick up one object
                    if (isFound)
                    {
                        ItemID = timeEvent.TargetID;
                        break;
                    }
                    
                    // if object is equivalent enough, save it in case we don't find the actual object we previously picked up
                    if (originalItem != null && originalItem.IsEquivalentItem(timeTracker))
                    {
                        bestMatch = timeTracker;
                    }
                }

                if (bestMatch != null)
                {
                    isFound = bestMatch.SetItemState(true);
                    if (isFound)
                    {
                        ItemID = bestMatch.ID;
                    }
                }

                if (!isFound)
                {
                    gameController.LogError($"Player {ID} could not grab {timeEvent.TargetID}");
                    throw new TimeAnomalyException("Time Anomaly!",
                        $"Doppelganger could not grab the {gameController.GetUserFriendlyName(timeEvent.TargetID)}",
                        this);
                }
            }
        } // end PLAYER_GRAB
        else if (timeEvent.Type == TimeEvent.EventType.PLAYER_DROP)
        {
            if (ItemID != timeEvent.TargetID)
            {
                gameController.Log($"Note: dropping {ItemID} instead of {timeEvent.TargetID}");
            }
            if (gameController.DropItem(this, ItemID)) // check to see if we successfully drop the item
            {
                ItemID = -1;
            }
        } // end PLAYER_DROP
        else if (timeEvent.Type == TimeEvent.EventType.TIME_TRAVEL)
        {
            TimeMachineController timeMachine = gameController.GetTimeTrackerByID(timeEvent.TargetID) as TimeMachineController;

            if (timeMachine == null || !timeMachine.IsTouching(gameObject))
            {
                throw new TimeAnomalyException("Time Anomaly!", "Doppelganger could not use the Time Machine!", this);
            }
        } // end TIME_TRAVEL
        else if (timeEvent.Type == TimeEvent.EventType.ACTIVATE_TIME_MACHINE)
        {
            TimeMachineController timeMachine = gameController.GetTimeTrackerByID(timeEvent.TargetID) as TimeMachineController;

            if (timeMachine == null || !timeMachine.IsTouching(gameObject))
            {
                throw new TimeAnomalyException("Time Anomaly!", "Doppelganger could not activate the Time Machine!", this);
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
        isSpriteOrderForced = false;

        queueGrab = false;
    }

    private void Update()
    {

        Animator.SetBool(Walking, Mathf.Abs(Rigidbody.velocity.x) > 0.001f);
        Animator.SetBool(Grounded, isGrounded);
        Animator.SetBool(Jumping, Rigidbody.velocity.y > 0);

        SpriteRenderer.flipX = facingRight;
        if (!isSpriteOrderForced)
        {
            SpriteRenderer.sortingOrder = gameController.CurrentPlayerID == ID ? 7 : 6; // current player on higher layer than past player
        }
    }

    private bool _alreadyJumping = false;

    void FixedUpdate()
    {
        UpdateIsGrounded();

        if (gameController.CurrentPlayerID != ID) return; // don't update physics from inputs if not main player

        if (jump && !_alreadyJumping && isGrounded)
        {
            _startJumpTime = Time.time;
	        Rigidbody.AddForce(this.transform.up * _jumpVelocityChange, ForceMode2D.Impulse);
	        _alreadyJumping = true;
        }
	    else if(jump && _alreadyJumping && (_startJumpTime + _maxJumpTime > Time.time))
	    {
	        Rigidbody.AddForce(Vector3.up * _jumpAcceleration, ForceMode2D.Force);
	    }

        Rigidbody.AddForce(new Vector2(horizontalInput, 0)*movementMultiplier);
        float updateXVel = Mathf.Clamp(Rigidbody.velocity.x, -maxHorizontalSpeed, maxHorizontalSpeed);
        if (Mathf.Abs(horizontalInput) > 0.1f && Mathf.Abs(Rigidbody.velocity.x) < 4f && isGrounded)
        {
            updateXVel = 4f * Mathf.Sign(horizontalInput);
        }
        Rigidbody.velocity = new Vector2(updateXVel, Rigidbody.velocity.y);
    }

    public void GameUpdate()
    {
        if (Mathf.Abs(Rigidbody.velocity.x) > 0.001f)
        {
            facingRight = Rigidbody.velocity.x > 0;
        }
        
        if (queueGrab) // this is only for the current player
        {
            DoGrab();
            queueGrab = false;
        }
    }

    void UpdateIsGrounded()
    {
        List<ContactPoint2D> contacts = new List<ContactPoint2D>();
        CapsuleCollider.GetContacts(contacts);
        isGrounded = false;
        for (int i = 0; i < contacts.Count; i++)
        {
            if (contacts[i].collider.gameObject.layer == LayerMask.NameToLayer("LevelPlatforms")
                && contacts[i].point.y < transform.position.y - CapsuleCollider.size.y/2 + 0.5f
                && Mathf.Abs(contacts[i].point.x - transform.position.x) < 0.3f
                )
            {
                _alreadyJumping = false;
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
	      EnableShaders();

        Animator.SetFloat("Cycle_Offset", UnityEngine.Random.value);
    }

    public virtual void OnPoolRelease()
    {
        PlayerInput.enabled = false;
        ClearState();
	    DisableShaders();
    }
    
    public void Init(GameController gameController, int id)
    {
        this.gameController = gameController;
        ID = id;
        name = $"Player {id.ToString()}";

        if (id == gameController.CurrentPlayerID)
        {
            PlayerInput.enabled = true;
            DisableShaders();
        }

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
            if (collider.gameObject == gameController.Player.gameObject || collider.gameObject == this.gameObject) continue;
            
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
        snapshotDictionary.Set(nameof(facingRight), facingRight, force);
        snapshotDictionary.Set(nameof(isSpriteOrderForced), isSpriteOrderForced, force);
        //snapshotDictionary[nameof(GetCollisionStateString)] = GetCollisionStateString();
        snapshotDictionary.Set(GameController.FLAG_DESTROY, FlagDestroy, force);
        //NOTE: players should never be in item form, so don't save/load that info here
    }

    // TODO: add fixed frame # associated with snapshot? and Lerp in update loop?!
    public void PreUpdateLoadSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        Position.LoadSnapshot(snapshotDictionary);
        Velocity.LoadSnapshot(snapshotDictionary);

        if (gameController.CurrentPlayerID != ID) // we don't want the current player to revert to their history positions/velocity
        {
            Position.Current = Position.History;
            Velocity.Current = Velocity.History;
        }

        Rigidbody.rotation = snapshotDictionary.Get<float>(nameof(Rigidbody.rotation));
        historyActivating = snapshotDictionary.Get<bool>(nameof(isActivating));
        DidTimeTravel = snapshotDictionary.Get<bool>(nameof(DidTimeTravel));
        facingRight = snapshotDictionary.Get<bool>(nameof(facingRight));

        FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);
    }

    public void ForceRestoreSnapshot(TimeDict.TimeSlice snapshotDictionary)
    {
        ItemID = snapshotDictionary.Get<int>(nameof(ItemID));
        Position.LoadSnapshot(snapshotDictionary);
        Velocity.LoadSnapshot(snapshotDictionary);

        Position.Current = Position.History;
        Velocity.Current = Velocity.History;

        Rigidbody.rotation = snapshotDictionary.Get<float>(nameof(Rigidbody.rotation));
        historyActivating = snapshotDictionary.Get<bool>(nameof(isActivating));
        DidTimeTravel = snapshotDictionary.Get<bool>(nameof(DidTimeTravel));
        facingRight = snapshotDictionary.Get<bool>(nameof(facingRight));
        isSpriteOrderForced = snapshotDictionary.Get<bool>(nameof(isSpriteOrderForced));

        FlagDestroy = snapshotDictionary.Get<bool>(GameController.FLAG_DESTROY);
    }

    public void EnableShaders()
    {
	    this._material.SetFloat("_StaticOpacity", 0.50f);
	    this._material.SetFloat("_DistortIntensity", 0.02f);
    }

    public void DisableShaders()
    {
	    this._material.SetFloat("_StaticOpacity", 0.0f);
	    this._material.SetFloat("_DistortIntensity", 0.0f);
    }

    void Awake()
    {
	    _material = GetComponentInChildren<SpriteRenderer>().material;
    }

    void OnDestroy()
    {
	    Destroy(_material);
    }
}
