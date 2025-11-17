// Assets/Scripts/FlipOrbit/CameraShake.cs
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    private float timeLeft = 0f;
    private float magnitude = 0f;
    private Vector3 basePos;

    void Awake() => basePos = transform.localPosition;

    public void Shake(float duration, float magnitude)
    {
        this.timeLeft = Mathf.Max(timeLeft, duration);
        this.magnitude = Mathf.Max(this.magnitude, magnitude);
    }

    void LateUpdate()
    {
        if (timeLeft > 0f)
        {
            timeLeft -= Time.deltaTime;
            Vector2 r = Random.insideUnitCircle * magnitude;
            transform.localPosition = basePos + new Vector3(r.x, r.y, 0f);
            if (timeLeft <= 0f)
            {
                transform.localPosition = basePos;
                magnitude = 0f;
            }
        }
    }
}
