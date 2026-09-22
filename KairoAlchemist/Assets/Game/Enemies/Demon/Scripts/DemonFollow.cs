using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
 
public class DemonFollow : MonoBehaviour
{
    [Header("AI References")]
    public Transform playerTarget;
    private Animator animator;
    private Rigidbody2D rb;

    [Header("AI Behavior")]
    public float chaseRange = 10f;
    public float stopDistance = 1.5f;
    public float moveSpeed = 3f;

    [Header("Flipping Logic")]
    [SerializeField] private Vector3 rightFacingRotation = new Vector3(0, 90, 0);
    [SerializeField] private Vector3 leftFacingRotation = new Vector3(0, -90, 0);
    [SerializeField] private Vector3 rightFacingScale = new Vector3(1, 1, 1);
    [SerializeField] private Vector3 leftFacingScale = new Vector3(1, 1, 1);
    [SerializeField] private GameObject[] objectsToFlip;
    private bool isFacingRight = true;

    // --- State Control ---
    private bool shouldBeWalking = false;
    private bool isFlipLocked = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();

        if (playerTarget == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) playerTarget = playerObject.transform;
        }
    }
    void Start()
    {
        // At the start of the game, check the initial direction.
        if (playerTarget != null && playerTarget.position.x < transform.position.x)
        {
            // If player starts on the left, immediately set to the LEFT-facing transform.
            transform.localRotation = Quaternion.Euler(leftFacingRotation);
            transform.localScale = leftFacingScale;
            isFacingRight = false;
        }
        else
        {
            // Otherwise, ensure we are in the default RIGHT-facing transform.
            transform.localRotation = Quaternion.Euler(rightFacingRotation);
            transform.localScale = rightFacingScale;
            isFacingRight = true;
        }
    }
    void Update()
    {

        if (isFlipLocked)
        {
            // If it is locked, do nothing. Exit the method immediately.
            return;
        }
        float distanceToPlayer = Vector2.Distance(transform.position, playerTarget.position);

        if (distanceToPlayer <= chaseRange && distanceToPlayer > stopDistance)
        {
            StartWalking();
        }
        else
        {
            StopWalking();
        }

        FacePlayer();
    }

    // The FixedUpdate movement logic has been DELETED.

    private void StartWalking()
    {
        if (shouldBeWalking) return;
        shouldBeWalking = true;
        animator.SetBool("isWalking", true);
    }

    private void StopWalking()
    {
        if (!shouldBeWalking) return;
        shouldBeWalking = false;
        animator.SetBool("isWalking", false);
        // We also call StopMovement here as a failsafe to prevent sliding if the animation is interrupted.
        StopMovement();
    }

    // --- NEW PUBLIC METHODS FOR ANIMATION EVENTS ---

    /// <summary>
    /// Called by an Animation Event at the START of the walk cycle.
    /// Applies a continuous velocity.
    /// </summary>
    public void StartMovement()
    {
        // Determine direction towards the player.
        float direction = Mathf.Sign(playerTarget.position.x - transform.position.x);
        // Apply a consistent velocity.
        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        Debug.Log("<color=green>StartMovement Event Called</color>");
    }

    /// <summary>
    /// Called by an Animation Event at the END of the walk cycle.
    /// Stops all horizontal movement.
    /// </summary>
    public void StopMovement()
    {
        // CRITICAL: Set horizontal velocity to zero to prevent sliding.
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        Debug.Log("<color=red>StopMovement Event Called</color>");
    }

    public void FacePlayer()
    {


        // If the player is to the right AND we are currently facing left...
        if (playerTarget.position.x > transform.position.x && !isFacingRight)
        {
            // ...flip to face right INSTANTLY.
            Flip();
        }
        // If the player is to the left AND we are currently facing right...
        else if (playerTarget.position.x < transform.position.x && isFacingRight)
        {
            // ...flip to face left INSTANTLY.
            Flip();
        }
    }
    public void LockFlip()
    {
        isFlipLocked = true;
        Debug.Log("<color=red>--- Knight Flip LOCKED ---</color>");
    }

    /// <summary>
    /// Called by an Animation Event to allow the knight to flip again.
    /// </summary>
    public void UnlockFlip()
    {
        isFlipLocked = false;
        Debug.Log("<color=green>--- Knight Flip UNLOCKED ---</color>");
    }
    private void Flip()
    {
        if (isFacingRight)
        {
            // --- Flip to face LEFT ---
            transform.localRotation = Quaternion.Euler(leftFacingRotation);
            transform.localScale = leftFacingScale;
            isFacingRight = false;
        }
        else
        {
            // --- Flip to face RIGHT ---
            transform.localRotation = Quaternion.Euler(rightFacingRotation);
            transform.localScale = rightFacingScale;
            isFacingRight = true;
        }
    }
    private void FlipChildObjects(float newXScale)
    {
        if (objectsToFlip == null || objectsToFlip.Length == 0) return;
        foreach (GameObject obj in objectsToFlip)
        {
            obj.transform.localScale = new Vector3(newXScale, obj.transform.localScale.y, obj.transform.localScale.z);
        }
    }
    public bool IsFacingRight()
    {
        // We can read this directly from the 'isFacingRight' boolean
        // that the Flip() method already controls.
        return isFacingRight;
    }

  


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);
    }
}

