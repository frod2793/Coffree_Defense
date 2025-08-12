using System;
using UnityEngine;

public class CaffeWallHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 1000f;
    private float currentHealth;

    public event Action OnDied;
    public event Action<float, float> OnHealthChanged;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        OnDied?.Invoke();
        Debug.Log("Wall has been destroyed.");
    }
    
    public void Heal(float amount)
    { 
        if (currentHealth <= 0) return; // Can't heal a dead wall

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
