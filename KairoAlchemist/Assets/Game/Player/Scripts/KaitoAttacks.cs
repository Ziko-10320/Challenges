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
}