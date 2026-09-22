using UnityEngine;

[CreateAssetMenu(fileName = "New AttackData", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("Attack Properties")]
    public int damage = 10;
    public string hitType = ".";

    [Header("Knockback Properties")]
    public float knockbackDistance = 2f;
    public float knockbackDuration = 0.2f;

    [Header("Vertical Knockback Properties")]
    [Tooltip("The upward force to apply to the enemy. A value > 0 will launch them up.")]
    public float upwardForce = 0f;

    [Tooltip("The downward force to apply to the enemy. A value > 0 will slam them down.")]
    public float downwardForce = 0f;

    public float guardDamage = 0f;
}