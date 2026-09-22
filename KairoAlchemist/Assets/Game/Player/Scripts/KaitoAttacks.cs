using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
public class KaitoAttacks : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Combo Settings")]
    [Tooltip("How long the player has to press attack again to continue the combo.")]
    [SerializeField] private float comboResetTime = 1f;

    [Tooltip("Safety timeout — if EndAttack() is never called (missing animation event), the attack force-resets after this long.")]
    [SerializeField] private float attackTimeout = 2f;

    [Header("Lunge Settings")]
    [SerializeField] private float lungeSpeed = 8f;
    [SerializeField] private float lungeDuration = 0.15f;

    private Coroutine lungeCoroutine;

    [Header("Damage Settings")]
    [Tooltip("The AttackData used for each combo step. Index 0 = combo step 1, index 1 = step 2, etc. " +
             "If the array is shorter than the combo, the last entry is reused for the remaining steps.")]
    [SerializeField] private AttackData[] comboAttackData;

    [Tooltip("An empty GameObject marking the center of the player's damage area.")]
    [SerializeField] private Transform attackPoint;

    [Tooltip("The size of the damage area (Width, Height).")]
    [SerializeField] private Vector2 attackAreaSize = new Vector2(1.5f, 1f);

    [Tooltip("The layer the enemies are on, so we know who to damage.")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Collision Settings")]
    [Tooltip("The integer value of the Player's layer.")]
    [SerializeField] private int playerLayerValue = 6;
    [Tooltip("The integer value of the Enemy's layer.")]
    [SerializeField] private int enemyLayerValue = 7;
    // --- State ---
    private int comboStep = 0;
    private bool isAttacking = false;
    private Coroutine comboResetCoroutine;
    private Coroutine attackWatchdogCoroutine;

    // --- Animation Hashes ---
    private readonly int attackStepHash = Animator.StringToHash("attackStep");
    private readonly int isAttackingBoolHash = Animator.StringToHash("isAttacking");
    private readonly int attackTriggerHash = Animator.StringToHash("Attack");

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        Physics2D.IgnoreLayerCollision(playerLayerValue, enemyLayerValue, true);
    }

    private void OnEnable()
    {
        // Add an "Attack" action to your Input Actions asset (Player map) and bind it here.
        PlayerMovement.inputActions.Player.Attack.performed += HandleAttackInput;
    }

    private void OnDisable()
    {
        if (PlayerMovement.inputActions != null)
        {
            PlayerMovement.inputActions.Player.Attack.performed -= HandleAttackInput;
        }
    }

    private void HandleAttackInput(InputAction.CallbackContext context)
    {
        if (isAttacking) return;
        if (playerMovement != null && !playerMovement.CanMove) return;
        if (playerMovement != null && playerMovement.IsRolling()) return;

        if (comboResetCoroutine != null)
        {
            StopCoroutine(comboResetCoroutine);
            comboResetCoroutine = null;
        }

        comboStep++;
        if (comboStep > 3) comboStep = 1;

        PerformAttack(comboStep);
    }

    private void PerformAttack(int step)
    {
        isAttacking = true;
        Physics2D.IgnoreLayerCollision(playerLayerValue, enemyLayerValue, false);
        if (playerMovement != null)
        {
            playerMovement.CanMove = false;
            playerMovement.CanFlip = false;
        }

        // Clear any leftover trigger before arming a new one — this is the key fix.
        animator.ResetTrigger(attackTriggerHash);

        animator.SetBool(isAttackingBoolHash, true);
        animator.SetInteger(attackStepHash, step);
        animator.SetTrigger(attackTriggerHash);

        if (attackWatchdogCoroutine != null) StopCoroutine(attackWatchdogCoroutine);
        attackWatchdogCoroutine = StartCoroutine(AttackWatchdogRoutine());

        Debug.Log($"<color=green>ATTACK {step} TRIGGERED!</color>");
    }

    /// <summary>
    /// Call this from an Animation Event at the end of each attack clip.
    /// </summary>
    public void EndAttack()
    {
        isAttacking = false;
        Physics2D.IgnoreLayerCollision(playerLayerValue, enemyLayerValue, true);
        if (playerMovement != null)
        {
            playerMovement.CanMove = true;
            playerMovement.CanFlip = true;
        }

        animator.SetBool(isAttackingBoolHash, false);
        animator.ResetTrigger(attackTriggerHash); // clear it so nothing carries into the next combo

        if (attackWatchdogCoroutine != null)
        {
            StopCoroutine(attackWatchdogCoroutine);
            attackWatchdogCoroutine = null;
        }

        if (comboResetCoroutine != null) StopCoroutine(comboResetCoroutine);
        comboResetCoroutine = StartCoroutine(ComboResetRoutine());
    }
    private IEnumerator ComboResetRoutine()
    {
        yield return new WaitForSeconds(comboResetTime);
        comboStep = 0;
        comboResetCoroutine = null;
        Debug.Log("<color=orange>Combo Reset.</color>");
    }

    private IEnumerator AttackWatchdogRoutine()
    {
        yield return new WaitForSeconds(attackTimeout);

        if (isAttacking)
        {
            Debug.LogWarning("<color=orange>ATTACK TIMEOUT! Forcibly resetting state.</color>");
            EndAttack();
        }
    }

    public void PerformLunge()
    {
        if (lungeCoroutine != null) StopCoroutine(lungeCoroutine);
        lungeCoroutine = StartCoroutine(LungeRoutine());
    }

    private IEnumerator LungeRoutine()
    {
        if (playerMovement == null) yield break;

        float direction = playerMovement.IsFacingRight() ? 1f : -1f;
        float timer = 0f;

        while (timer < lungeDuration)
        {
            transform.position += new Vector3(direction * lungeSpeed * Time.deltaTime, 0f, 0f);
            timer += Time.deltaTime;
            yield return null;
        }

        lungeCoroutine = null;
    }
    public void CancelAttack()
    {
        if (!isAttacking) return;
        Physics2D.IgnoreLayerCollision(playerLayerValue, enemyLayerValue, true);
        Debug.LogWarning("<color=orange>ATTACK CANCELLED by a higher priority action.</color>");

        animator.ResetTrigger(attackTriggerHash);
        animator.SetBool(isAttackingBoolHash, false);
        animator.SetInteger(attackStepHash, 0);

        if (attackWatchdogCoroutine != null)
        {
            StopCoroutine(attackWatchdogCoroutine);
            attackWatchdogCoroutine = null;
        }
        if (comboResetCoroutine != null)
        {
            StopCoroutine(comboResetCoroutine);
            comboResetCoroutine = null;
        }

        isAttacking = false;
        comboStep = 0;

        if (playerMovement != null)
        {
            playerMovement.CanMove = true;
            playerMovement.CanFlip = true;
        }

        if (lungeCoroutine != null)
        {
            StopCoroutine(lungeCoroutine);
            lungeCoroutine = null;
        }
    }
    public bool IsAttacking() => isAttacking;
    public int CurrentComboStep() => comboStep;

    #region Damage System

    /// <summary>
    /// Call this from an Animation Event on the hit frame of each attack clip.
    /// Automatically grabs the right AttackData for the current combo step and deals damage.
    /// </summary>
    public void EVENT_DealDamage()
    {
        AttackData dataToUse = GetAttackDataForStep(comboStep);
        if (dataToUse == null)
        {
            Debug.LogWarning($"KaitoAttacks: No AttackData assigned for combo step {comboStep}!", this);
            return;
        }

        AttackEnemy(dataToUse);
    }

    /// <summary>
    /// Returns the AttackData assigned to the given combo step (1-based).
    /// If the array is shorter than the step count, the last available entry is reused.
    /// </summary>
    private AttackData GetAttackDataForStep(int step)
    {
        if (comboAttackData == null || comboAttackData.Length == 0) return null;

        int index = Mathf.Clamp(step - 1, 0, comboAttackData.Length - 1);
        return comboAttackData[index];
    }

    /// <summary>
    /// Finds enemies inside the attack box and applies damage + knockback to the first one hit.
    /// Mirrors ZreyAttacks.AttackEnemy, targeting DemonHealth.
    /// </summary>
    public void AttackEnemy(AttackData attackData)
    {
        if (attackData == null) return;

        if (attackPoint == null)
        {
            Debug.LogWarning("KaitoAttacks: Attack Point is not assigned!", this);
            return;
        }

        Collider2D[] enemiesHit = Physics2D.OverlapBoxAll(attackPoint.position, attackAreaSize, 0f, enemyLayer);

        foreach (Collider2D enemy in enemiesHit)
        {
            DemonHealth demonHealth = enemy.GetComponent<DemonHealth>();
            if (demonHealth != null)
            {
                demonHealth.ApplyDamageAndKnockback(attackData);
               
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(attackPoint.position, attackAreaSize);
    }

    #endregion
}