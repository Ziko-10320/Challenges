using System.Collections;
using UnityEngine;
using FirstGearGames.SmoothCameraShaker;

[RequireComponent(typeof(Animator))]
public class KaitoHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    [Header("Knockback")]
    private PlayerMovement kaitoMovement; // swap this type if your movement script is named differently

    [Header("Camera Shake")]
    public ShakeData hitShake;

    [Header("Blood VFX")]
    public GameObject bloodVFX;
    public Transform bloodSpawnPoint;

    private Animator animator;
    private Rigidbody2D rb;
    private bool isDead = false;
    private Coroutine knockbackCoroutine;
    private float knockbackVelocityRef;

    private readonly int hitBackTriggerHash = Animator.StringToHash("HitBack");
    private readonly int deathTriggerHash = Animator.StringToHash("death");

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;

        kaitoMovement = GetComponent<PlayerMovement>();
    }

    /// <summary>
    /// Applies damage and plays the matching hit reaction on the Animator.
    /// hitReactionType should match an Animator trigger name (e.g. "back", "down", "upward").
    /// </summary>
    public void TakeDamage(int damageAmount, Transform attacker, EnemyAttackData attackData)
    {
        if (isDead || (kaitoMovement != null && kaitoMovement.IsInvincible)) return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"<color=orange>Kaito took {damageAmount} damage. Health: {currentHealth}/{maxHealth}</color>");

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        PlayHitReaction(attackData.hitReactionType);
        CameraShakerHandler.Shake(hitShake);
        SpawnBlood();

        if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(attacker, attackData));
    }
    private void SpawnBlood()
    {
        if (bloodVFX != null && bloodSpawnPoint != null)
            Instantiate(bloodVFX, bloodSpawnPoint.position, bloodVFX.transform.rotation);
    }
    private IEnumerator KnockbackRoutine(Transform attacker, EnemyAttackData attackData)
    {
        if (attackData.knockbackDistance <= 0 || attackData.knockbackDuration <= 0) yield break;

        if (kaitoMovement != null) kaitoMovement.CanMove = false;

        float direction = Mathf.Sign(transform.position.x - attacker.position.x);
        float targetVelocity = (attackData.knockbackDistance / attackData.knockbackDuration) * direction;
        float smoothTime = attackData.knockbackDuration * 0.25f;

        float verticalVelocity = 0f;
        if (attackData.upwardForce > 0) verticalVelocity = attackData.upwardForce;
        else if (attackData.downwardForce > 0) verticalVelocity = -attackData.downwardForce;

        if (verticalVelocity != 0)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, verticalVelocity);

        float timer = 0f;
        while (timer < attackData.knockbackDuration)
        {
            float currentX = Mathf.SmoothDamp(rb.linearVelocity.x, targetVelocity, ref knockbackVelocityRef, smoothTime);
            rb.linearVelocity = new Vector2(currentX, rb.linearVelocity.y);
            timer += Time.deltaTime;
            yield return null;
        }

        float decelTimer = 0f;
        while (decelTimer < smoothTime)
        {
            float currentX = Mathf.SmoothDamp(rb.linearVelocity.x, 0f, ref knockbackVelocityRef, smoothTime);
            rb.linearVelocity = new Vector2(currentX, rb.linearVelocity.y);
            decelTimer += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        if (kaitoMovement != null) kaitoMovement.CanMove = true;
        knockbackCoroutine = null;
    }

    private void PlayHitReaction(string hitReactionType)
    {
        if (string.IsNullOrEmpty(hitReactionType)) return;

        switch (hitReactionType.ToLower())
        {
            case "back":
                animator.SetTrigger(hitBackTriggerHash);
                break;
        }
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("<color=black>KAITO IS DEAD.</color>");
        animator.SetTrigger(deathTriggerHash);
    }

    public bool IsDead()
    {
        return isDead;
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }
}