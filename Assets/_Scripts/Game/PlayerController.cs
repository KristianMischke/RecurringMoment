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
    public Collider2D GrabCollider
    {
        get
        {
            if (_grabCollider == null)
            {
                _grabCollider = GetComponentInChildren<Collider2D>();
            }
            return _grabCollider;
        }
    }
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

    [SerializeField] private float maxHorizontalSpeed;
    [SerializeField] private float jumpMultiplier;
    [SerializeField] private float movementMultiplier;
    [SerializeField] private bool isGrounded = false;

    private int itemID = -1;
    
    //apply in fixed update
    private float verticalInput, horizontalInput;
    private bool jump;
    private bool isActivating, historyActivating;

    public bool IsActivating => isActivating;
    public bool HistoryActivating => historyActivating;

    private GameController gameController;
    public int ID { get; private set; }
    public Vector2 Position
    {
        get => transform.position;
        set => transform.position = value;
    }
    public bool ItemForm { get => false; set { } }
    public bool FlagDestroy { get; set; }


    //---PlayerInputs---
    private void OnMove(InputValue movementValue)
    {
        if (gameController.player != this) return;

        Vector2 movementVector = movementValue.Get<Vector2>();

        horizontalInput = movementVector.x;
        verticalInput = movementVector.y;
    }
    private void OnJump(InputValue inputValue)
    {
        if (gameController.player != this) return;

        jump |= inputValue.isPressed && isGrounded;
    }
    private void OnActivate(InputValue inputValue)
    {
        if (gameController.player != this) return;

        isActivating = inputValue.isPressed;
    }
    private void OnSkipTime(InputValue inputValue)
    {
        if (gameController.player != this) return;

        gameController.SkipTime();
    }
    private void OnSaveDebugHistory(InputValue inputValue)
    {
        if (gameController.player != this) return;

        gameController.ExportHistory();
    }

    private void OnRetry(InputValue inputValue)
    {
        if (gameController.player != this) return;

        gameController.RetryLevel();
    }

    private void OnGrab(InputValue inputValue)
    {
        if (itemID != -1)
        {
            gameController.DropItem(itemID);
            itemID = -1;
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
                        timeMachine.ItemForm = true;
                        if (timeMachine.ItemForm)
                            itemID = timeMachine.ID;
                    }
                    else if (contact.TryGetComponent(out BasicTimeTracker basicTimeTracker))
                    {
                        basicTimeTracker.ItemForm = true;
                        if (basicTimeTracker.ItemForm)
                            itemID = basicTimeTracker.ID;
                    }
                }
            }
        }
    }
    //------

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
    }

    void FixedUpdate()
    {
        UpdateIsGrounded();
        if (jump)
        {
            Rigidbody.AddForce(Vector2.up * jumpMultiplier, ForceMode2D.Impulse);
            jump = false;
        }
        Rigidbody.AddForce(new Vector2(horizontalInput, 0)*movementMultiplier);
        Rigidbody.velocity = new Vector2(Mathf.Clamp(Rigidbody.velocity.x, -maxHorizontalSpeed, maxHorizontalSpeed), Rigidbody.velocity.y);
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

    public void Init(GameController gameController, int id)
    {
        this.gameController = gameController;
        ID = id;
        name = $"Player {id.ToString()}";
    }

    public void SaveSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        snapshotDictionary[nameof(Rigidbody.position)] = Rigidbody.position;
        snapshotDictionary[nameof(Rigidbody.velocity)] = Rigidbody.velocity;
        snapshotDictionary[nameof(Rigidbody.rotation)] = Rigidbody.rotation;
        snapshotDictionary[nameof(isActivating)] = isActivating;
        if(FlagDestroy)
        {
            snapshotDictionary[GameController.FLAG_DESTROY] = true;
        }
        //NOTE: players should never be in item form, so don't save/load that info here
    }

    // TODO: add fixed frame # associated with snapshot? and Lerp in update loop?!
    public void LoadSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        Rigidbody.position = (Vector2)snapshotDictionary[nameof(Rigidbody.position)];
        Rigidbody.velocity = (Vector2)snapshotDictionary[nameof(Rigidbody.velocity)];
        Rigidbody.rotation = (float)snapshotDictionary[nameof(Rigidbody.rotation)];
        historyActivating = (bool)snapshotDictionary[nameof(isActivating)];
    }
}
