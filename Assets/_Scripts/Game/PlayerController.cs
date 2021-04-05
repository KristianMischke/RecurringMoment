using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, ITimeTracker
{
    private Rigidbody2D _rigidbody;
    private CapsuleCollider2D _capsuleCollider;
    private Collider2D _grabCollider;
    private PlayerInput _playerInput;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
	
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

    [SerializeField] private float maxHorizontalSpeed;
    [SerializeField] private float jumpMultiplier;
    [SerializeField] private float movementMultiplier;
    [SerializeField] private bool isGrounded = false;

    public TimeInt ItemID = new TimeInt("itemID");
    
    //apply in fixed update
    private float verticalInput, horizontalInput;
    private bool jump;
    private bool isActivating, historyActivating;

    public bool IsActivating => isActivating;
    public bool HistoryActivating => historyActivating;

    public bool DidTimeTravel { get; set; }
    
    /* TODO: TimeBool to track if the player is grounded (use to determine if past player is in valid location
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

    private bool queueGrab = false;
    public void OnGrab(InputValue inputValue)
    {
        queueGrab = true;
    }
    //------

    private void DoGrab()
    {
        // to get the sprite 
		Sprite itemImage = gameController.tempImage; 
		bool isFound = false;

        if (ItemID.Current != -1)
        {
            gameController.DropItem(ItemID.Current);
            ItemID.Current = -1;
            ItemID.History = -1;
			gameController.playerItem.SetActive(false); 
			gameController.playerItem.GetComponentInChildren<SpriteRenderer>().sprite = itemImage;
        }
        else
        {
            List<Collider2D> contacts = new List<Collider2D>();
            GrabCollider.GetContacts(contacts);
            foreach (var contact in contacts)
            {
                TimeMachineController timeMachine = null;
                
                bool validObj = contact.CompareTag("TriggerObject") || contact.TryGetComponent(out timeMachine);
                if (validObj && contact.gameObject != gameObject)
                {
					if (timeMachine != null)
                    {
                        if (timeMachine.SetItemState(true))
                        {
                            isFound = true;
                            ItemID.Current = timeMachine.ID;
                            itemImage = contact.transform.gameObject.GetComponentInChildren<SpriteRenderer>().sprite;
                            Debug.Log("The name of the sprite is : " + itemImage.name);
                        }
                    }
                    else if (contact.TryGetComponent(out BasicTimeTracker basicTimeTracker))
                    {
                        if (basicTimeTracker.SetItemState(true))
                        {
                            isFound = true;
                            ItemID.Current = basicTimeTracker.ID;
                            itemImage = contact.transform.gameObject.GetComponentInChildren<SpriteRenderer>().sprite;
                            Debug.Log("The name of the sprite is : " + itemImage.name);
                        }
                    }
                }
            }
			
			// this is when he grabs a object and it shows up in the screen 
			if(isFound == true)
			{
				gameController.playerItem.SetActive(true); // shows the screen to the player 
				gameController.playerItem.GetComponentInChildren<SpriteRenderer>().sprite = itemImage; 
				Debug.Log("The name of the sprite is : " + itemImage.name);
			}
        }
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
        ItemID.Current = ItemID.History = -1;
        DidTimeTravel = false;
    }

    private void Update()
    {
        Animator.SetBool("Walking", _rigidbody.velocity != Vector2.zero);
        if (_rigidbody.velocity != Vector2.zero)
        {
            SpriteRenderer.flipX = _rigidbody.velocity.x > 0;
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
        if (queueGrab)
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
        ItemID.SaveSnapshot(snapshotDictionary, force);
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
        ItemID.LoadSnapshot(snapshotDictionary);
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
        ItemID.ForceLoadSnapshot(snapshotDictionary);
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
