using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] Health target; // whose health to show
    [SerializeField] Image fill;    // the bar image that fills and empties

    void Update()
    {
        if (!target || !fill) return;
        fill.fillAmount = (float)target.CurrentHealth / target.MaxHealth;
    }
}
