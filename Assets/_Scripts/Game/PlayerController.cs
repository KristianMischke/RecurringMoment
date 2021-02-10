using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rigidbody;
    private CapsuleCollider2D capsuleCollider;

    [SerializeField] private float maxHorizontalSpeed;
    [SerializeField] private float jumpMultiplier;
    [SerializeField] private float movementMultiplier;
    [SerializeField] private bool isGrounded = false;

    GameController gameController;

    //apply in fixed update
    float verticalInput, horizontalInput;
    private bool jump;

    void Start()
    {
        rigidbody = GetComponent<Rigidbody2D>();
        capsuleCollider = GetComponent<CapsuleCollider2D>();
    }

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
}
