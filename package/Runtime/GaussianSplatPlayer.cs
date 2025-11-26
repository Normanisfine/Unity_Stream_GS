// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.Events;

namespace GaussianSplatting.Runtime
{
    /// <summary>
    /// Component for playing back a sequence of GaussianSplatAssets.
    /// Attach to the same GameObject as GaussianSplatRenderer.
    /// </summary>
    [RequireComponent(typeof(GaussianSplatRenderer))]
    [ExecuteInEditMode]
    public class GaussianSplatPlayer : MonoBehaviour
    {
        public enum PlaybackMode
        {
            /// <summary>Play once and stop at the last frame</summary>
            Once,
            /// <summary>Loop continuously</summary>
            Loop,
            /// <summary>Play forward then backward (ping-pong)</summary>
            PingPong,
        }

        [Header("Sequence")]
        [Tooltip("The GaussianSplatSequence to play")]
        public GaussianSplatSequence sequence;

        [Header("Playback Settings")]
        [Tooltip("Playback speed multiplier (1 = normal speed)")]
        [Range(0.1f, 5f)]
        public float playbackSpeed = 1f;

        [Tooltip("How the sequence should play")]
        public PlaybackMode playbackMode = PlaybackMode.Loop;

        [Tooltip("Start playing automatically on enable")]
        public bool playOnEnable = true;

        [Header("Coordinate System")]
        [Tooltip("Flip Y axis - use when PLY files have inverted Y coordinates")]
        public bool flipY = false;

        [Header("Manual Control")]
        [Tooltip("Current frame index (for manual scrubbing)")]
        [SerializeField] int m_CurrentFrame;

        [Tooltip("Normalized playback position (0-1)")]
        [Range(0f, 1f)]
        [SerializeField] float m_NormalizedTime;

        [Header("Events")]
        public UnityEvent onSequenceComplete;
        public UnityEvent<int> onFrameChanged;

        // Runtime state
        GaussianSplatRenderer m_Renderer;
        float m_PlaybackTime;
        bool m_IsPlaying;
        int m_LastFrame = -1;
        int m_PingPongDirection = 1;
        bool m_LastFlipY;

        /// <summary>Is the player currently playing?</summary>
        public bool isPlaying => m_IsPlaying;

        /// <summary>Current frame index</summary>
        public int currentFrame => m_CurrentFrame;

        /// <summary>Total number of frames</summary>
        public int frameCount => sequence?.frameCount ?? 0;

        /// <summary>Normalized playback position (0-1)</summary>
        public float normalizedTime
        {
            get => m_NormalizedTime;
            set
            {
                m_NormalizedTime = Mathf.Clamp01(value);
                m_CurrentFrame = Mathf.FloorToInt(m_NormalizedTime * Mathf.Max(1, frameCount - 1));
                m_PlaybackTime = m_NormalizedTime * (sequence?.duration ?? 0f);
                ApplyCurrentFrame();
            }
        }

        void OnEnable()
        {
            m_Renderer = GetComponent<GaussianSplatRenderer>();
            m_LastFlipY = flipY;
            ApplyFlipY();
            
            if (playOnEnable && Application.isPlaying)
            {
                Play();
            }
            else
            {
                ApplyCurrentFrame();
            }
        }

        void OnDisable()
        {
            m_IsPlaying = false;
        }

        void Update()
        {
            // Check if flipY changed
            if (m_LastFlipY != flipY)
            {
                m_LastFlipY = flipY;
                ApplyFlipY();
            }

            if (!m_IsPlaying || sequence == null || sequence.frameCount == 0)
                return;

            float fps = sequence.targetFPS * playbackSpeed;
            if (fps <= 0)
                return;

            float frameDuration = 1f / fps;
            m_PlaybackTime += Time.deltaTime;

            // Calculate current frame based on playback mode
            int newFrame = CalculateFrame();

            if (newFrame != m_CurrentFrame)
            {
                m_CurrentFrame = newFrame;
                m_NormalizedTime = (float)m_CurrentFrame / Mathf.Max(1, frameCount - 1);
                ApplyCurrentFrame();
            }
        }

        void ApplyFlipY()
        {
            var scale = transform.localScale;
            scale.y = flipY ? -Mathf.Abs(scale.y) : Mathf.Abs(scale.y);
            transform.localScale = scale;
        }

        int CalculateFrame()
        {
            if (sequence == null || sequence.frameCount == 0)
                return 0;

            float fps = sequence.targetFPS * playbackSpeed;
            int frame = Mathf.FloorToInt(m_PlaybackTime * fps);

            switch (playbackMode)
            {
                case PlaybackMode.Once:
                    if (frame >= frameCount)
                    {
                        frame = frameCount - 1;
                        if (m_IsPlaying)
                        {
                            m_IsPlaying = false;
                            onSequenceComplete?.Invoke();
                        }
                    }
                    break;

                case PlaybackMode.Loop:
                    frame = frame % frameCount;
                    break;

                case PlaybackMode.PingPong:
                    int cycle = frame / Mathf.Max(1, frameCount - 1);
                    int posInCycle = frame % Mathf.Max(1, frameCount - 1);
                    
                    if (cycle % 2 == 0)
                        frame = posInCycle;
                    else
                        frame = frameCount - 1 - posInCycle;
                    break;
            }

            return Mathf.Clamp(frame, 0, frameCount - 1);
        }

        void ApplyCurrentFrame()
        {
            if (m_Renderer == null || sequence == null)
                return;

            var asset = sequence.GetFrame(m_CurrentFrame, playbackMode == PlaybackMode.Loop);
            if (asset != null && m_Renderer.m_Asset != asset)
            {
                m_Renderer.m_Asset = asset;
                
                if (m_CurrentFrame != m_LastFrame)
                {
                    m_LastFrame = m_CurrentFrame;
                    onFrameChanged?.Invoke(m_CurrentFrame);
                }
            }
        }

        /// <summary>Start or resume playback</summary>
        public void Play()
        {
            m_IsPlaying = true;
            m_PingPongDirection = 1;
        }

        /// <summary>Pause playback</summary>
        public void Pause()
        {
            m_IsPlaying = false;
        }

        /// <summary>Stop playback and reset to beginning</summary>
        public void Stop()
        {
            m_IsPlaying = false;
            m_PlaybackTime = 0f;
            m_CurrentFrame = 0;
            m_NormalizedTime = 0f;
            m_PingPongDirection = 1;
            ApplyCurrentFrame();
        }

        /// <summary>Go to a specific frame</summary>
        public void GoToFrame(int frame)
        {
            if (sequence == null || sequence.frameCount == 0)
                return;

            m_CurrentFrame = Mathf.Clamp(frame, 0, frameCount - 1);
            m_NormalizedTime = (float)m_CurrentFrame / Mathf.Max(1, frameCount - 1);
            m_PlaybackTime = m_NormalizedTime * (sequence?.duration ?? 0f);
            ApplyCurrentFrame();
        }

        /// <summary>Go to the next frame</summary>
        public void NextFrame()
        {
            GoToFrame(m_CurrentFrame + 1);
        }

        /// <summary>Go to the previous frame</summary>
        public void PreviousFrame()
        {
            GoToFrame(m_CurrentFrame - 1);
        }

        /// <summary>Toggle between play and pause</summary>
        public void TogglePlayPause()
        {
            if (m_IsPlaying)
                Pause();
            else
                Play();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Clamp frame index to valid range
            if (sequence != null && sequence.frameCount > 0)
            {
                m_CurrentFrame = Mathf.Clamp(m_CurrentFrame, 0, frameCount - 1);
            }

            // Apply frame when scrubbing in editor
            if (!Application.isPlaying)
            {
                m_Renderer = GetComponent<GaussianSplatRenderer>();
                ApplyCurrentFrame();
            }

            // Apply flip Y when changed in editor
            if (m_LastFlipY != flipY)
            {
                m_LastFlipY = flipY;
                ApplyFlipY();
            }
        }
#endif
    }
}

