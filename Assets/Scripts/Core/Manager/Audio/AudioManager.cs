using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;

namespace AssemblyLine.Core.Manager.Audio
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Mixer 引用")]
        public AudioMixer MainMixer;
        
        [Header("快照名称")]
        public string NormalSnapshotName = "Normal";
        public string PausedSnapshotName = "Paused";
        
        [Header("参数控制")]
        public float TransitionDuration = 0.4f;

        private AudioMixerSnapshot _normalSnapshot;
        private AudioMixerSnapshot _pausedSnapshot;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (MainMixer != null)
            {
                _normalSnapshot = MainMixer.FindSnapshot(NormalSnapshotName);
                _pausedSnapshot = MainMixer.FindSnapshot(PausedSnapshotName);
            }
        }

        /// <summary>
        /// 切换全局音频状态
        /// </summary>
        /// <param name="isPaused">是否进入暂停模式</param>
        public void SetAudioPauseState(bool isPaused)
        {
            if (MainMixer == null) return;

            if (isPaused)
            {
                _pausedSnapshot?.TransitionTo(TransitionDuration);
            }
            else
            {
                _normalSnapshot?.TransitionTo(TransitionDuration);
            }
        }
    }
}