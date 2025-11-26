// SPDX-License-Identifier: MIT

using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEngine;

namespace GaussianSplatting.Editor
{
    [CustomEditor(typeof(GaussianSplatPlayer))]
    public class GaussianSplatPlayerEditor : UnityEditor.Editor
    {
        SerializedProperty m_Sequence;
        SerializedProperty m_PlaybackSpeed;
        SerializedProperty m_PlaybackMode;
        SerializedProperty m_PlayOnEnable;
        SerializedProperty m_FlipY;
        SerializedProperty m_CurrentFrame;
        SerializedProperty m_NormalizedTime;
        SerializedProperty m_OnSequenceComplete;
        SerializedProperty m_OnFrameChanged;

        void OnEnable()
        {
            m_Sequence = serializedObject.FindProperty("sequence");
            m_PlaybackSpeed = serializedObject.FindProperty("playbackSpeed");
            m_PlaybackMode = serializedObject.FindProperty("playbackMode");
            m_PlayOnEnable = serializedObject.FindProperty("playOnEnable");
            m_FlipY = serializedObject.FindProperty("flipY");
            m_CurrentFrame = serializedObject.FindProperty("m_CurrentFrame");
            m_NormalizedTime = serializedObject.FindProperty("m_NormalizedTime");
            m_OnSequenceComplete = serializedObject.FindProperty("onSequenceComplete");
            m_OnFrameChanged = serializedObject.FindProperty("onFrameChanged");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var player = (GaussianSplatPlayer)target;

            // Sequence
            EditorGUILayout.PropertyField(m_Sequence);
            
            if (player.sequence == null)
            {
                EditorGUILayout.HelpBox("Assign a GaussianSplatSequence to enable playback.", MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            // Sequence info
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.LabelField("Sequence Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Frame Count", player.frameCount.ToString());
            EditorGUILayout.LabelField("Target FPS", player.sequence.targetFPS.ToString("F1"));
            EditorGUILayout.LabelField("Duration", $"{player.sequence.duration:F2}s");
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Playback settings
            EditorGUILayout.LabelField("Playback Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_PlaybackSpeed);
            EditorGUILayout.PropertyField(m_PlaybackMode);
            EditorGUILayout.PropertyField(m_PlayOnEnable);

            EditorGUILayout.Space();

            // Coordinate system
            EditorGUILayout.LabelField("Coordinate System", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_FlipY, new GUIContent("Flip Y Axis", "Enable if the sequence appears upside down"));

            EditorGUILayout.Space();

            // Playback controls
            EditorGUILayout.LabelField("Playback Controls", EditorStyles.boldLabel);
            
            // Timeline slider
            EditorGUI.BeginChangeCheck();
            int newFrame = EditorGUILayout.IntSlider("Frame", player.currentFrame, 0, Mathf.Max(0, player.frameCount - 1));
            if (EditorGUI.EndChangeCheck())
            {
                player.GoToFrame(newFrame);
                EditorUtility.SetDirty(target);
            }

            // Normalized time slider
            EditorGUI.BeginChangeCheck();
            float newTime = EditorGUILayout.Slider("Time", player.normalizedTime, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                player.normalizedTime = newTime;
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.Space();

            // Transport buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Rewind button
            if (GUILayout.Button("⏮", GUILayout.Width(40), GUILayout.Height(30)))
            {
                player.Stop();
                EditorUtility.SetDirty(target);
            }

            // Previous frame button
            if (GUILayout.Button("◀", GUILayout.Width(40), GUILayout.Height(30)))
            {
                player.PreviousFrame();
                EditorUtility.SetDirty(target);
            }

            // Play/Pause button
            string playButtonText = player.isPlaying ? "⏸" : "▶";
            if (GUILayout.Button(playButtonText, GUILayout.Width(50), GUILayout.Height(30)))
            {
                player.TogglePlayPause();
                EditorUtility.SetDirty(target);
            }

            // Next frame button
            if (GUILayout.Button("▶", GUILayout.Width(40), GUILayout.Height(30)))
            {
                player.NextFrame();
                EditorUtility.SetDirty(target);
            }

            // Go to end button
            if (GUILayout.Button("⏭", GUILayout.Width(40), GUILayout.Height(30)))
            {
                player.GoToFrame(player.frameCount - 1);
                EditorUtility.SetDirty(target);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Status
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            string statusText = player.isPlaying ? "▶ Playing" : "⏸ Paused";
            EditorGUILayout.LabelField(statusText, EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Events
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_OnSequenceComplete);
            EditorGUILayout.PropertyField(m_OnFrameChanged);

            serializedObject.ApplyModifiedProperties();

            // Force repaint while playing
            if (player.isPlaying)
            {
                Repaint();
            }
        }
    }
}

