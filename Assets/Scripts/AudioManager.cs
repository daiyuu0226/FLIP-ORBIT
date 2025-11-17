// Assets/Scripts/FlipOrbit/AudioManager.cs
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    public AudioClip pickUp;
    public AudioClip miss;
    public AudioClip flip;

    private AudioSource src;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
    }

    public void PlayPickup(float pitch = 1f) => PlayOneShot(pickUp, pitch);
    public void PlayMiss() => PlayOneShot(miss, 1f);
    public void PlayFlip() => PlayOneShot(flip, 1f);

    private void PlayOneShot(AudioClip clip, float pitch)
    {
        if (!clip) return;
        src.pitch = pitch;
        src.PlayOneShot(clip);
    }
}
