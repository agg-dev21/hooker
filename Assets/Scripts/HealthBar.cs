using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] Health target; // kiski health dikhani hai
    [SerializeField] Image fill;    // jo bar bharti aur khaali hoti hai

    void Update()
    {
        if (!target || !fill) return;
        fill.fillAmount = (float)target.CurrentHealth / target.MaxHealth;
    }
}
