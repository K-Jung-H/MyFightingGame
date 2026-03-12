using UnityEngine;
using UnityEngine.UI;

public class HealthBarController : MonoBehaviour
{
    [SerializeField] private Slider foregroundSlider;
    [SerializeField] private Slider backgroundSlider;
    [SerializeField] private float delayBeforeDrain = 1.0f;
    [SerializeField] private float drainSpeed = 5.0f;

    private float targetRatio = 1f;
    private float delayTimer = 0f;

    public void Initialize(PlayerCombat combat, bool isPlayerTwo)
    {
        bool isMirrored = isPlayerTwo;
        
        if (isMirrored)
        {
            foregroundSlider.direction = Slider.Direction.RightToLeft;
            backgroundSlider.direction = Slider.Direction.RightToLeft;
        }
        else
        {
            foregroundSlider.direction = Slider.Direction.LeftToRight;
            backgroundSlider.direction = Slider.Direction.LeftToRight;
        }

        combat.OnHealthChanged += UpdateHealthBar;
    }

    private void OnDestroy()
    {

    }

    private void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        targetRatio = (float)currentHealth / maxHealth;
        foregroundSlider.value = targetRatio;
        delayTimer = delayBeforeDrain;
    }

    private void Update()
    {
        bool isDelaying = delayTimer > 0f;
        if (isDelaying)
        {
            delayTimer -= Time.deltaTime;
        }
        else
        {
            bool isBackgroundGreater = backgroundSlider.value > targetRatio;
            if (isBackgroundGreater)
            {
                backgroundSlider.value = Mathf.Lerp(backgroundSlider.value, targetRatio, Time.deltaTime * drainSpeed);
            }
        }
    }
}