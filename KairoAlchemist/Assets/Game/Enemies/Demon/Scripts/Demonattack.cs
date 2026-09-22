using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class DemonAttack : MonoBehaviour
{
    [Header("References")]
    private Animator animator;
    private Rigidbody2D rb;
    private DemonFollow followAI;

    [Header("Range Detection")]
    [Tooltip("How close the player needs to be for the demon to trigger a DashAttack.")]
    public float attackRange = 3f;
    [Tooltip("Minimum time between DashAttacks.")]
    public float attackCooldown = 2f;
    private float lastAttackTime = -10f;

    [Header("Damage Settings")]
    [Tooltip("An empty GameObject marking the center of the demon's damage area.")]
    public Transform attackPoint;
    [Tooltip("The size of the damage area (Width, Height).")]
    public Vector2 attackAreaSize = new Vector2(1.5f, 2f);
    [Tooltip("The layer the player is on, so we know who to damage.")]
    public LayerMask playerLayer;

    [Header("Lunge Settings")]
    [Tooltip("The force of the lunge during the dash attack.")]
    public float lungeForce = 6f;
    [Tooltip("How long the lunge lasts (in seconds).")]
    public float lungeDuration = 0.2f;
    private Coroutine lungeCoroutine;

    // --- State Control ---
    private bool isAttacking = false;
    private EnemyAttackData currentAttackData;
    private bool isDamageWindowOpen = false;
    private Transform playerTarget;
    private float lungeVelocityRef;

    private readonly int dashAttackTriggerHash = Animator.StringToHash("dashAttack");

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        followAI = GetComponent<DemonFollow>();

        if (followAI != null && followAI.playerTarget != null)
            playerTarget = followAI.playerTarget;
        else
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) playerTarget = playerObject.transform;
        }
    }

    void Update()
    {
        if (isDamageWindowOpen)
        {
            Collider2D[] hitPlayers = Physics2D.OverlapBoxAll(attackPoint.position, attackAreaSize, 0f, playerLayer);
            foreach (Collider2D player in hitPlayers)
            {
                KaitoHealth kaitoHealth = player.GetComponent<KaitoHealth>();
                if (kaitoHealth != null)
                {
                    Debug.Log("<color=red>Demon hit Player with DashAttack!</color>");
                    kaitoHealth.TakeDamage(currentAttackData.damage, transform, currentAttackData);
                    isDamageWindowOpen = false; // one hit only per window
                    break;
                }
            }
        }

        if (playerTarget == null || isAttacking) return;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTarget.position);

        if (distanceToPlayer <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            TriggerDashAttack();
        }

      
    }

    private void TriggerDashAttack()
    {
        isAttacking = true;
        followAI?.LockFlip();
        lastAttackTime = Time.time;
        animator.SetTrigger(dashAttackTriggerHash);
    }

    /// <summary>
    /// Called by an Animation Event once the DashAttack animation finishes.
    /// </summary>
    public void FinishAttack()
    {
        isAttacking = false;
        followAI?.UnlockFlip();
    }

    /// <summary>
    /// Called by an Animation Event to lunge the demon forward during the dash.
    /// </summary>
    public void Lunge()
    {
        if (lungeCoroutine != null) StopCoroutine(lungeCoroutine);
        lungeCoroutine = StartCoroutine(LungeCoroutine());
    }

    private IEnumerator LungeCoroutine()
    {
        if (followAI == null) yield break;
        float direction = followAI.IsFacingRight() ? 1f : -1f;
        float targetVelocity = direction * lungeForce;
        float smoothTime = lungeDuration * 0.25f;

        float timer = 0f;
        while (timer < lungeDuration)
        {
            float currentX = Mathf.SmoothDamp(rb.linearVelocity.x, targetVelocity, ref lungeVelocityRef, smoothTime);
            rb.linearVelocity = new Vector2(currentX, rb.linearVelocity.y);
            timer += Time.deltaTime;
            yield return null;
        }

        float decelTimer = 0f;
        while (decelTimer < smoothTime)
        {
            float currentX = Mathf.SmoothDamp(rb.linearVelocity.x, 0f, ref lungeVelocityRef, smoothTime);
            rb.linearVelocity = new Vector2(currentX, rb.linearVelocity.y);
            decelTimer += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
    }

    /// <summary>
    /// Called by an Animation Event to deal damage. Drag the EnemyAttackData
    /// asset for this attack directly onto the event's Object field.
    /// </summary>
    public void StartDamage(EnemyAttackData attackData)
    {
        currentAttackData = attackData;
        isDamageWindowOpen = true;
    }

    public void StopDamage()
    {
        isDamageWindowOpen = false;
        currentAttackData = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(attackPoint.position, attackAreaSize);
        }
    }
}