using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, ITimeTracker
{
    private Rigidbody2D rigidbody;
    private CapsuleCollider2D capsuleCollider;
    private PlayerInput playerInput;

    public PlayerInput PlayerInput
    {
        get
        {
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }
            return playerInput;
        }
    }

    [SerializeField] private float maxHorizontalSpeed;
    [SerializeField] private float jumpMultiplier;
    [SerializeField] private float movementMultiplier;
    [SerializeField] private bool isGrounded = false;

    //apply in fixed update
    private float verticalInput, horizontalInput;
    private bool jump;
    private bool isActivating;

    public bool IsActivating => isActivating;

    private GameController gameController;
    public int ID { get; private set; }
    public bool FlagDestroy { get; set; }

    void Start()
    {
        rigidbody = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
    }

    //---PlayerInputs---
    private void OnMove(InputValue movementValue)
    {
        Vector2 movementVector = movementValue.Get<Vector2>();

        horizontalInput = movementVector.x;
        verticalInput = movementVector.y;
    }
    private void OnJump(InputValue inputValue)
    {
        jump |= inputValue.isPressed && isGrounded;
    }
    private void OnActivate(InputValue inputValue)
    {
        isActivating = inputValue.isPressed;
    }
    //------

    public void ClearActivate()
    {
        isActivating = false;
    }

    void FixedUpdate()
    {
        UpdateIsGrounded();
        if (jump)
        {
            rigidbody.AddForce(Vector2.up * jumpMultiplier, ForceMode2D.Impulse);
            jump = false;
        }
        rigidbody.AddForce(new Vector2(horizontalInput, 0)*movementMultiplier);
        rigidbody.velocity = new Vector2(Mathf.Clamp(rigidbody.velocity.x, -maxHorizontalSpeed, maxHorizontalSpeed), rigidbody.velocity.y);
    }

    void UpdateIsGrounded()
    {
        RaycastHit2D[] raycastHits = Physics2D.RaycastAll(transform.position, Vector2.down, capsuleCollider.size.y);//, LayerMask.NameToLayer("LevelPlatforms"));
        isGrounded = false;
        for (int i = 0; i < raycastHits.Length; i++)
        {
            if (raycastHits[i].collider.gameObject == gameObject) continue;

            if (raycastHits[i].collider.gameObject.layer == LayerMask.NameToLayer("LevelPlatforms")
                && raycastHits[i].point.y < transform.position.y - capsuleCollider.size.y/2 + 0.01
                && raycastHits[i].point.y > transform.position.y - capsuleCollider.size.y/2 - 0.01)
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
        name = "Player " + id;
    }

    public void SaveSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        snapshotDictionary[nameof(rigidbody.position)] = rigidbody.position;
        snapshotDictionary[nameof(rigidbody.velocity)] = rigidbody.velocity;
        snapshotDictionary[nameof(rigidbody.rotation)] = rigidbody.rotation;
        snapshotDictionary[nameof(isActivating)] = isActivating;
        if(FlagDestroy)
        {
            snapshotDictionary[GameController.FLAG_DESTROY] = true;
        }
    }

    // TODO: add fixed frame # associated with snapshot? and Lerp in update loop?!
    public void LoadSnapshot(Dictionary<string, object> snapshotDictionary)
    {
        rigidbody.position = (Vector2)snapshotDictionary[nameof(rigidbody.position)];
        rigidbody.velocity = (Vector2)snapshotDictionary[nameof(rigidbody.velocity)];
        rigidbody.rotation = (float)snapshotDictionary[nameof(rigidbody.rotation)];
        isActivating = (bool)snapshotDictionary[nameof(isActivating)];
    }
}
