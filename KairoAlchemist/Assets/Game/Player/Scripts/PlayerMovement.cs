using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float runSpeed = 5f;

    [Header("Jumping Settings")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.1f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float jumpBufferTime = 0.2f;

    [Header("Flipping Logic")]
    [SerializeField] private Vector3 rightFacingRotation = new Vector3(0, 90, 0);
    [SerializeField] private Vector3 leftFacingRotation = new Vector3(0, -90, 0);
    [SerializeField] private Vector3 rightFacingScale = new Vector3(1, 1, 1);
    [SerializeField] private Vector3 leftFacingScale = new Vector3(1, -1, 1);
    [SerializeField] private GameObject[] objectsToFlip;

    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;

    [Header("Roll Settings")]
    [SerializeField] private float rollDistance = 4f;
    [SerializeField] private float rollDuration = 0.4f;
    [Tooltip("X = time (0-1), Y = speed multiplier. Leave a curve that starts high and eases out.")]
    [SerializeField] private AnimationCurve rollSpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f);
    [SerializeField] private float rollCooldown = 0.5f;
    [SerializeField] private bool groundOnlyRoll = true;
    [Tooltip("Keeps the player from being pushed off ledges mid-roll.")]
    [SerializeField] private bool zeroGravityDuringRoll = true;

    [SerializeField] private KaitoAttacks playerAttacks;

    // --- Input ---
    public static InputSystem_Actions inputActions;
    private Vector2 moveInput;

    // --- State ---
    public bool isFacingRight = true;
    private bool isGrounded;
    private float jumpBufferCounter;

    private bool isRolling = false;
    private float rollCooldownCounter = 0f;
    private float originalGravityScale;
    private Coroutine rollCoroutine;

   
    // External scripts can switch these off to freeze the player
    public bool CanMove { get; set; } = true;
    public bool CanFlip { get; set; } = true;

    // --- Animation Hashes ---
    private readonly int isRunningHash = Animator.StringToHash("isRunning");
    private readonly int isGroundedHash = Animator.StringToHash("isGrounded");
    private readonly int jumpTriggerHash = Animator.StringToHash("jump");
    private readonly int isFallingHash = Animator.StringToHash("isFalling");
    private readonly int rollTriggerHash = Animator.StringToHash("roll");

    private void Awake()
    {
        // Create the input actions once and keep them enabled for the whole game.
        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
            inputActions.Player.Enable();
        }
        else if (!inputActions.Player.enabled)
        {
            inputActions.Player.Enable();
        }

        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        originalGravityScale = rb.gravityScale;

        if (playerAttacks == null) playerAttacks = GetComponent<KaitoAttacks>();
    }

    private void OnEnable()
    {
        inputActions.Player.Jump.performed += HandleJump;
        inputActions.Player.Dash.performed += HandleRoll;

    }

    private void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Jump.performed -= HandleJump;
            inputActions.Player.Dash.performed -= HandleRoll;
        }
    }

    private void Update()
    {
        // --- Read Input ---
        moveInput = CanMove ? inputActions.Player.Move.ReadValue<Vector2>() : Vector2.zero;


        if (rollCooldownCounter > 0f) 
            rollCooldownCounter -= Time.deltaTime;

        // --- Ground Check ---
        bool wasGrounded = isGrounded;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded != wasGrounded)
        {
            animator.SetBool(isGroundedHash, isGrounded);
        }

        // --- Jump Buffer ---
        if (jumpBufferCounter > 0f) jumpBufferCounter -= Time.deltaTime;
        if (!wasGrounded && isGrounded && jumpBufferCounter > 0f) PerformJump();

        // --- Landing ---
        if (!wasGrounded && isGrounded)
        {
            OnLanded();
        }

        // --- Animation ---
        HandleMovementAnimation();
        HandleAirborneAnimation();

        // --- Flip ---
        if (CanFlip && moveInput.x != 0f)
        {
            if (moveInput.x < 0f && isFacingRight) Flip();
            else if (moveInput.x > 0f && !isFacingRight) Flip();
        }
        // --- Roll ---
        if (!isRolling)
        {
            HandleMovementAnimation();
            HandleAirborneAnimation();

            if (CanFlip && moveInput.x != 0f)
            {
                if (moveInput.x < 0f && isFacingRight) Flip();
                else if (moveInput.x > 0f && !isFacingRight) Flip();
            }
        }
    }

    private void FixedUpdate()
    {
        if (isRolling) return;
        if (!CanMove) return;

        rb.linearVelocity = new Vector2(moveInput.x * runSpeed, rb.linearVelocity.y);
    }

    // --- JUMPING ---

    private void HandleJump(InputAction.CallbackContext context)
    {
        if (!CanMove) return;

        if (isGrounded)
        {
            PerformJump();
        }
        else
        {
            // Buffer the input so it fires the moment we touch the ground.
            jumpBufferCounter = jumpBufferTime;
        }
    }

    private void PerformJump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        animator.SetTrigger(jumpTriggerHash);
        jumpBufferCounter = 0f;
    }

    private void OnLanded()
    {
        // Hook for landing effects (sound, particles, screen shake...).
        animator.SetBool(isFallingHash, false);
    }

    // --- ANIMATION ---

    private void HandleMovementAnimation()
    {
        animator.SetBool(isRunningHash, moveInput.x != 0f && isGrounded);
    }

    private void HandleAirborneAnimation()
    {
        if (isGrounded)
        {
            animator.SetBool(isFallingHash, false);
        }
        else if (rb.linearVelocity.y < 0f)
        {
            animator.SetBool(isFallingHash, true);
        }
    }

    // --- Roll ---
    private void HandleRoll(InputAction.CallbackContext context)
    {
        if (!CanMove) return;
        if (isRolling) return;
        if (rollCooldownCounter > 0f) return;
        if (groundOnlyRoll && !isGrounded) return;

       

        if (rollCoroutine != null) StopCoroutine(rollCoroutine);
        rollCoroutine = StartCoroutine(RollRoutine());
    }

    private IEnumerator RollRoutine()
    {
        isRolling = true;
        rollCooldownCounter = rollCooldown;

        animator.SetTrigger(rollTriggerHash);
        animator.SetBool(isRunningHash, false);

        float direction = isFacingRight ? 1f : -1f;
        float baseSpeed = rollDistance / rollDuration;

        if (zeroGravityDuringRoll)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }

        float timer = 0f;
        while (timer < rollDuration)
        {
            float progress = timer / rollDuration;
            float speed = baseSpeed * rollSpeedCurve.Evaluate(progress);

            rb.linearVelocity = new Vector2(
                speed * direction,
                zeroGravityDuringRoll ? 0f : rb.linearVelocity.y);

            timer += Time.deltaTime;
            yield return null;
        }

        // Cleanup
        rb.gravityScale = originalGravityScale;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        isRolling = false;
        rollCoroutine = null;
    }

    public bool IsRolling() => isRolling;


    // --- FLIPPING ---

    private void Flip()
    {
        if (!isFacingRight) // currently facing left -> face right
        {
            transform.localRotation = Quaternion.Euler(rightFacingRotation);
            transform.localScale = rightFacingScale;
            isFacingRight = true;
            FlipChildObjects(1f);
        }
        else // currently facing right -> face left
        {
            transform.localRotation = Quaternion.Euler(leftFacingRotation);
            transform.localScale = leftFacingScale;
            isFacingRight = false;
            FlipChildObjects(-1f);
        }
    }

    private void FlipChildObjects(float newXScale)
    {
        if (objectsToFlip == null || objectsToFlip.Length == 0) return;

        foreach (GameObject obj in objectsToFlip)
        {
            if (obj == null) continue;
            obj.transform.localScale = new Vector3(newXScale, obj.transform.localScale.y, obj.transform.localScale.z);
        }
    }

    public void ForceFaceDirection(bool shouldFaceRight)
    {
        if (shouldFaceRight != isFacingRight) Flip();
    }

    // --- PUBLIC GETTERS ---

    public bool IsGrounded() => isGrounded;
    public bool IsFacingRight() => isFacingRight;

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}