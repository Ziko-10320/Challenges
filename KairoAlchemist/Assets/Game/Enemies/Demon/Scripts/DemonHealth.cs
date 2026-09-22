using FirstGearGames.SmoothCameraShaker;
using System.Collections;
using UnityEngine;

/// <summary>
/// Lean enemy health script for the Demon: takes damage via AttackData,
/// plays directional hit reactions (up/down/back), spawns one blood VFX,
/// shakes the camera on hit, and dies by spawning a death explosion and
/// destroying itself. No block/guard/counter/finisher system — intentionally simple.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class DemonHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    [Header("Knockback Settings")]
    [Tooltip("Fallback knockback distance, used by the simple TakeDamage() entry point.")]
    [SerializeField] private float knockbackDistance = 1.5f;
    [Tooltip("Fallback knockback duration, used by the simple TakeDamage() entry point.")]
    [SerializeField] private float knockbackDuration = 0.2f;

    [Header("Blood VFX")]
    [Tooltip("The single blood particle effect prefab spawned on hit.")]
    [SerializeField] private GameObject bloodVFXPrefab;
    [Tooltip("The specific point on the demon's body where blood VFX will spawn.")]
    [SerializeField] private Transform bloodSpawnPoint;

    [Header("Camera Shake")]
    [Tooltip("The shake data played whenever the demon takes a hit.")]
    public ShakeData CameraShakeHit;
    public ShakeData CameraShakeDeath;

    [Header("Death Settings")]
    [Tooltip("The explosion prefab spawned when the demon dies.")]
    [SerializeField] private GameObject deathExplosionPrefab;
    [Tooltip("The specific point where the death explosion will spawn. Defaults to the demon's own position if left empty.")]
    [SerializeField] private Transform deathExplosionSpawnPoint;
    [Tooltip("Delay before the GameObject is destroyed after death (lets the explosion/animation play out).")]
    [SerializeField] private float destroyDelay = 0.1f;

    [Header("Hit Sounds")]
    [Range(0f, 1f)][SerializeField] private float hitSfxVolume = 1f;
    [SerializeField] private AudioClip[] hitSoundClips;
    private AudioSource hitSfxSource;

    // --- Components ---
    private Rigidbody2D rb;
    private Animator animator;
    private Coroutine knockbackCoroutine;
    private bool isBeingKnockedBack = false;
    private bool isDying = false;

    // --- Animation Hashes (match the trigger names used on Spear/Knight so the same Animator setup works) ---
    private readonly int getHitUpTriggerHash = Animator.StringToHash("getHitUp");
    private readonly int getHitDownTriggerHash = Animator.StringToHash("getHitDown");
    private readonly int getHitBackTriggerHash = Animator.StringToHash("getHitBack");

    void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        hitSfxSource = gameObject.AddComponent<AudioSource>();
        hitSfxSource.playOnAwake = false;
        hitSfxSource.spatialBlend = 0f;
    }

    public void UpdateVolume(float masterVolume)
    {
        hitSfxVolume = masterVolume;
    }

    /// <summary>
    /// Simple damage entry point (fixed knockback values, no AttackData needed).
    /// Mirrors SpearHealth.TakeDamage for cases where you just want to poke the demon directly.
    /// </summary>
    public void TakeDamage(int damage, Transform attacker, string hitType)
    {
        if (isDying) return;

        PlayHitReaction(hitType);
        currentHealth -= damage;
        SpawnBloodVFX();
        PlayRandomHitSound();
        CameraShake();

        Debug.Log(transform.name + " took " + damage + " damage. Health is now: " + currentHealth);

        if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(attacker, knockbackDistance, knockbackDuration, 0f, 0f));

        if (currentHealth <= 0) Die();
    }

    /// <summary>
    /// Main damage entry point, driven by an AttackData Scriptable Object.
    /// Call this from the player's attack script (e.g. KaitoAttacks.AttackEnemy).
    /// </summary>
    public void ApplyDamageAndKnockback(AttackData attackData)
    {
        if (isDying) return;
        if (attackData == null) return;

        int damage = attackData.damage;
        string hitType = attackData.hitType;
        float distance = attackData.knockbackDistance;
        float duration = attackData.knockbackDuration;
        float upward = attackData.upwardForce;
        float downward = attackData.downwardForce;

        Transform attacker = GameObject.FindGameObjectWithTag("Player")?.transform;

        PlayHitReaction(hitType);
        currentHealth -= damage;
        SpawnBloodVFX();
        PlayRandomHitSound();
        CameraShake();

        Debug.Log($"<color=red>{transform.name} took {damage} damage via AttackData. Health is now: {currentHealth}</color>");

        if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(attacker, distance, duration, upward, downward));

        if (currentHealth <= 0) Die();
    }

    /// <summary>
    /// Plays the correct directional hit-reaction animation and resets the others
    /// so the animator never gets stuck waiting on a stale trigger.
    /// </summary>
    public void PlayHitReaction(string hitType)
    {
        if (isDying) return;

        // A new hit interrupts any ongoing knockback.
        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
            isBeingKnockedBack = false;
        }

        animator.ResetTrigger(getHitUpTriggerHash);
        animator.ResetTrigger(getHitDownTriggerHash);
        animator.ResetTrigger(getHitBackTriggerHash);

        switch (hitType.ToLower())
        {
            case "up":
                animator.SetTrigger(getHitUpTriggerHash);
                break;
            case "down":
                animator.SetTrigger(getHitDownTriggerHash);
                break;
            case "back":
                animator.SetTrigger(getHitBackTriggerHash);
                break;
            default:
                // Unknown hit type — fall back to a safe default.
                animator.SetTrigger(getHitBackTriggerHash);
                break;
        }
    }

    /// <summary>
    /// Fires the assigned ShakeData through FirstGearGames' CameraShakerHandler.
    /// Public so it can also be triggered directly from an Animation Event if you want
    /// the shake to line up with a specific frame of the hit-reaction clip.
    /// </summary>
    public void CameraShake()
    {
        if (CameraShakeHit == null) return;
        CameraShakerHandler.Shake(CameraShakeHit);
    }

    private void SpawnBloodVFX()
    {
        if (bloodVFXPrefab == null) return;

        if (bloodSpawnPoint == null)
        {
            Debug.LogError("Blood Spawn Point is not assigned on DemonHealth! Using demon's own position as a fallback.", this);
            bloodSpawnPoint = transform;
        }

        Instantiate(bloodVFXPrefab, bloodSpawnPoint.position, bloodVFXPrefab.transform.rotation);
    }

    private void PlayRandomHitSound()
    {
        if (hitSoundClips == null || hitSoundClips.Length == 0 || hitSfxSource == null) return;
        AudioClip clip = hitSoundClips[Random.Range(0, hitSoundClips.Length)];
        if (clip != null) hitSfxSource.PlayOneShot(clip, hitSfxVolume);
    }

    private IEnumerator KnockbackRoutine(Transform attacker, float distance, float duration, float upwardForce, float downwardForce)
    {
        isBeingKnockedBack = true;

        if (attacker == null || rb == null || duration <= 0f)
        {
            isBeingKnockedBack = false;
            yield break;
        }

        // Knockback direction is simply "away from the attacker" on the X axis.
        Vector2 diff = (Vector2)(transform.position - attacker.position);
        float knockbackDirectionX = diff.x >= 0f ? 1f : -1f;

        float horizontalVelocity = (distance / duration) * knockbackDirectionX;

        float initialYVelocity = 0f;
        if (upwardForce > 0f) initialYVelocity = upwardForce;
        if (downwardForce > 0f) initialYVelocity = -downwardForce;

        rb.linearVelocity = new Vector2(horizontalVelocity, initialYVelocity);

        float timer = 0f;
        while (timer < duration)
        {
            rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);
            timer += Time.deltaTime;
            yield return null;
        }

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        knockbackCoroutine = null;
        isBeingKnockedBack = false;
    }

    private void Die()
    {
        if (isDying) return;
        isDying = true;
        currentHealth = 0;

        Debug.LogWarning($"--- {transform.name} has been defeated! ---");

        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
            knockbackCoroutine = null;
        }
        if (rb != null) rb.linearVelocity = Vector2.zero;

        CameraShakerHandler.Shake(CameraShakeHit);
        SpawnDeathExplosion();

        Destroy(gameObject, destroyDelay);
    }

    private void SpawnDeathExplosion()
    {
        if (deathExplosionPrefab == null) return;

        Transform spawnPoint = deathExplosionSpawnPoint != null ? deathExplosionSpawnPoint : transform;
        Instantiate(deathExplosionPrefab, spawnPoint.position, deathExplosionPrefab.transform.rotation);
    }

    public bool IsStunned()
    {
        return isBeingKnockedBack;
    }

    public bool IsDying()
    {
        return isDying;
    }

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
}