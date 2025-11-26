// SPDX-License-Identifier: MIT

using UnityEngine;

namespace GaussianSplatting.Runtime
{
    /// <summary>
    /// A ScriptableObject that holds a sequence of GaussianSplatAssets for animated playback.
    /// Created by the Batch Converter tool.
    /// </summary>
    [CreateAssetMenu(fileName = "GaussianSequence", menuName = "Gaussian Splatting/Gaussian Splat Sequence")]
    public class GaussianSplatSequence : ScriptableObject
    {
        [Tooltip("Array of GaussianSplatAsset frames in playback order")]
        public GaussianSplatAsset[] frames;

        [Tooltip("Target playback framerate")]
        public float targetFPS = 30f;

        /// <summary>
        /// Number of frames in this sequence
        /// </summary>
        public int frameCount => frames?.Length ?? 0;

        /// <summary>
        /// Duration of the sequence in seconds
        /// </summary>
        public float duration => frameCount > 0 && targetFPS > 0 ? frameCount / targetFPS : 0f;

        /// <summary>
        /// Get a frame by index, with optional clamping or looping
        /// </summary>
        public GaussianSplatAsset GetFrame(int index, bool loop = true)
        {
            if (frames == null || frames.Length == 0)
                return null;

            if (loop)
                index = ((index % frames.Length) + frames.Length) % frames.Length;
            else
                index = Mathf.Clamp(index, 0, frames.Length - 1);

            return frames[index];
        }

        /// <summary>
        /// Get a frame by normalized time (0-1)
        /// </summary>
        public GaussianSplatAsset GetFrameAtTime(float normalizedTime, bool loop = true)
        {
            if (frames == null || frames.Length == 0)
                return null;

            int index = Mathf.FloorToInt(normalizedTime * frames.Length);
            return GetFrame(index, loop);
        }
    }
}

