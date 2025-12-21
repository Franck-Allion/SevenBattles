using UnityEngine;

namespace SevenBattles.UI
{
    public interface IUiSfxPlayer
    {
        void PlayOneShot(AudioClip clip, float volume);
    }
}

