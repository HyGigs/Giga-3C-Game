using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAudioManager : MonoBehaviour
{
    [Header("SFX Source")]
    [SerializeField] private AudioSource _footstepSFX;
    [SerializeField] private AudioSource _landingSFX;
    [SerializeField] private AudioSource _punchSFX;
    [SerializeField] private AudioSource _glideSFX;

    private void PlayFootstepSFX()
    {
        _footstepSFX.volume = Random.Range(0.7f, 1f);
        _footstepSFX.pitch = Random.Range(0.5f, 2.5f);
        _footstepSFX.Play();
    }

    private void PlayLandingSFX()
    {
        _landingSFX.Play();
    }

    private void PlayPunchSFX()
    {
        _punchSFX.volume = Random.Range(0.8f, 1f);
        _punchSFX.pitch = Random.Range(0.2f, 3f);
        _punchSFX.Play();
    }

    public void PlayGlideSFX()
    {
        _glideSFX.Play();
    }

    public void StopGlideSFX()
    {
        _glideSFX.Stop();
    }
}
