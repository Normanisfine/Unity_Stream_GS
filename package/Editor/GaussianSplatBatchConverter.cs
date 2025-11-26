// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GaussianSplatting.Editor.Utils;
using GaussianSplatting.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace GaussianSplatting.Editor
{
    /// <summary>
    /// Batch converter for converting multiple PLY files into GaussianSplatAssets for streaming playback.
    /// Access via Tools -> Gaussian Splats -> Batch Convert PLY Sequence
    /// </summary>
    [BurstCompile]
    public class GaussianSplatBatchConverter : EditorWindow
    {
        const string kProgressTitle = "Batch Converting Gaussian Splats";
        const string kPrefQuality = "nesnausk.GaussianSplatting.BatchQuality";
        const string kPrefOutputFolder = "nesnausk.GaussianSplatting.BatchOutputFolder";

        enum DataQuality
        {
            VeryHigh,
            High,
            Medium,
            Low,
            VeryLow,
        }

        readonly FilePickerControl m_FolderPicker = new();

        [SerializeField] string m_InputFolder;
        [SerializeField] string m_OutputFolder = "Assets/GaussianSequence";
        [SerializeField] DataQuality m_Quality = DataQuality.Medium;
        [SerializeField] string m_FilePattern = "*.ply";
        [SerializeField] bool m_CreateSequenceAsset = true;

        GaussianSplatAsset.VectorFormat m_FormatPos;
        GaussianSplatAsset.VectorFormat m_FormatScale;
        GaussianSplatAsset.ColorFormat m_FormatColor;
        GaussianSplatAsset.SHFormat m_FormatSH;

        string m_ErrorMessage;
        List<string> m_DetectedFiles = new();

        [MenuItem("Tools/Gaussian Splats/Batch Convert PLY Sequence")]
        public static void Init()
        {
            var window = GetWindowWithRect<GaussianSplatBatchConverter>(new Rect(50, 50, 450, 380), false, "Gaussian Splat Batch Converter", true);
            window.minSize = new Vector2(400, 350);
            window.maxSize = new Vector2(800, 600);
            window.Show();
        }

        void Awake()
        {
            m_Quality = (DataQuality)EditorPrefs.GetInt(kPrefQuality, (int)DataQuality.Medium);
            m_OutputFolder = EditorPrefs.GetString(kPrefOutputFolder, "Assets/GaussianSequence");
        }

        void OnEnable()
        {
            ApplyQualityLevel();
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            GUILayout.Label("Input", EditorStyles.boldLabel);
            
            var rect = EditorGUILayout.GetControlRect(true);
            string newInputFolder = m_FolderPicker.PathFieldGUI(rect, new GUIContent("Input Folder"), m_InputFolder, null, "PLYSequenceFolder");
            if (newInputFolder != m_InputFolder)
            {
                m_InputFolder = newInputFolder;
                RefreshFileList();
            }

            m_FilePattern = EditorGUILayout.TextField("File Pattern", m_FilePattern);
            
            if (GUILayout.Button("Refresh File List"))
            {
                RefreshFileList();
            }

            if (m_DetectedFiles.Count > 0)
            {
                EditorGUILayout.HelpBox($"Found {m_DetectedFiles.Count} PLY files", MessageType.Info);
                
                // Show first and last file names
                if (m_DetectedFiles.Count > 0)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("First:", Path.GetFileName(m_DetectedFiles[0]));
                    if (m_DetectedFiles.Count > 1)
                        EditorGUILayout.LabelField("Last:", Path.GetFileName(m_DetectedFiles[m_DetectedFiles.Count - 1]));
                    EditorGUI.indentLevel--;
                }
            }
            else if (!string.IsNullOrEmpty(m_InputFolder))
            {
                EditorGUILayout.HelpBox("No PLY files found in the specified folder", MessageType.Warning);
            }

            EditorGUILayout.Space();
            GUILayout.Label("Output", EditorStyles.boldLabel);
            
            rect = EditorGUILayout.GetControlRect(true);
            string newOutputFolder = m_FolderPicker.PathFieldGUI(rect, new GUIContent("Output Folder"), m_OutputFolder, null, "GaussianSequenceOutputFolder");
            if (newOutputFolder != m_OutputFolder)
            {
                m_OutputFolder = newOutputFolder;
                EditorPrefs.SetString(kPrefOutputFolder, m_OutputFolder);
            }

            var newQuality = (DataQuality)EditorGUILayout.EnumPopup("Quality", m_Quality);
            if (newQuality != m_Quality)
            {
                m_Quality = newQuality;
                EditorPrefs.SetInt(kPrefQuality, (int)m_Quality);
                ApplyQualityLevel();
            }

            // Show format details
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.LabelField("Position", m_FormatPos.ToString());
            EditorGUILayout.LabelField("Scale", m_FormatScale.ToString());
            EditorGUILayout.LabelField("Color", m_FormatColor.ToString());
            EditorGUILayout.LabelField("SH", m_FormatSH.ToString());
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            m_CreateSequenceAsset = EditorGUILayout.Toggle("Create Sequence Asset", m_CreateSequenceAsset);
            EditorGUILayout.HelpBox("Creates a GaussianSplatSequence asset that references all converted frames for easy playback.", MessageType.None);

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(m_DetectedFiles.Count == 0);
            if (GUILayout.Button($"Convert {m_DetectedFiles.Count} Files", GUILayout.Height(30)))
            {
                BatchConvert();
            }
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrWhiteSpace(m_ErrorMessage))
            {
                EditorGUILayout.HelpBox(m_ErrorMessage, MessageType.Error);
            }
        }

        void RefreshFileList()
        {
            m_DetectedFiles.Clear();
            m_ErrorMessage = null;

            if (string.IsNullOrWhiteSpace(m_InputFolder) || !Directory.Exists(m_InputFolder))
                return;

            try
            {
                var files = Directory.GetFiles(m_InputFolder, m_FilePattern)
                    .Where(f => f.EndsWith(".ply", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f)
                    .ToList();
                m_DetectedFiles = files;
            }
            catch (Exception ex)
            {
                m_ErrorMessage = ex.Message;
            }
        }

        void ApplyQualityLevel()
        {
            switch (m_Quality)
            {
                case DataQuality.VeryLow:
                    m_FormatPos = GaussianSplatAsset.VectorFormat.Norm11;
                    m_FormatScale = GaussianSplatAsset.VectorFormat.Norm6;
                    m_FormatColor = GaussianSplatAsset.ColorFormat.BC7;
                    m_FormatSH = GaussianSplatAsset.SHFormat.Cluster4k;
                    break;
                case DataQuality.Low:
                    m_FormatPos = GaussianSplatAsset.VectorFormat.Norm11;
                    m_FormatScale = GaussianSplatAsset.VectorFormat.Norm6;
                    m_FormatColor = GaussianSplatAsset.ColorFormat.Norm8x4;
                    m_FormatSH = GaussianSplatAsset.SHFormat.Cluster16k;
                    break;
                case DataQuality.Medium:
                    m_FormatPos = GaussianSplatAsset.VectorFormat.Norm11;
                    m_FormatScale = GaussianSplatAsset.VectorFormat.Norm11;
                    m_FormatColor = GaussianSplatAsset.ColorFormat.Norm8x4;
                    m_FormatSH = GaussianSplatAsset.SHFormat.Norm6;
                    break;
                case DataQuality.High:
                    m_FormatPos = GaussianSplatAsset.VectorFormat.Norm16;
                    m_FormatScale = GaussianSplatAsset.VectorFormat.Norm16;
                    m_FormatColor = GaussianSplatAsset.ColorFormat.Float16x4;
                    m_FormatSH = GaussianSplatAsset.SHFormat.Norm11;
                    break;
                case DataQuality.VeryHigh:
                    m_FormatPos = GaussianSplatAsset.VectorFormat.Float32;
                    m_FormatScale = GaussianSplatAsset.VectorFormat.Float32;
                    m_FormatColor = GaussianSplatAsset.ColorFormat.Float32x4;
                    m_FormatSH = GaussianSplatAsset.SHFormat.Float32;
                    break;
            }
        }

        void BatchConvert()
        {
            m_ErrorMessage = null;

            if (m_DetectedFiles.Count == 0)
            {
                m_ErrorMessage = "No files to convert";
                return;
            }

            if (string.IsNullOrWhiteSpace(m_OutputFolder) || !m_OutputFolder.StartsWith("Assets/"))
            {
                m_ErrorMessage = $"Output folder must be within project, was '{m_OutputFolder}'";
                return;
            }

            Directory.CreateDirectory(m_OutputFolder);

            var convertedAssets = new List<GaussianSplatAsset>();
            int successCount = 0;
            int failCount = 0;

            try
            {
                for (int i = 0; i < m_DetectedFiles.Count; i++)
                {
                    string filePath = m_DetectedFiles[i];
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    
                    float progress = (float)i / m_DetectedFiles.Count;
                    if (EditorUtility.DisplayCancelableProgressBar(kProgressTitle, 
                        $"Converting {fileName} ({i + 1}/{m_DetectedFiles.Count})", progress))
                    {
                        break;
                    }

                    try
                    {
                        var asset = ConvertSingleFile(filePath, fileName);
                        if (asset != null)
                        {
                            convertedAssets.Add(asset);
                            successCount++;
                        }
                        else
                        {
                            failCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to convert {fileName}: {ex.Message}");
                        failCount++;
                    }
                }

                // Create sequence asset if requested
                if (m_CreateSequenceAsset && convertedAssets.Count > 0)
                {
                    EditorUtility.DisplayProgressBar(kProgressTitle, "Creating sequence asset...", 0.99f);
                    CreateSequenceAsset(convertedAssets);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Batch conversion complete: {successCount} succeeded, {failCount} failed");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        GaussianSplatAsset ConvertSingleFile(string inputPath, string baseName)
        {
            // Read input file
            NativeArray<InputSplatData> inputSplats;
            try
            {
                GaussianFileReader.ReadFile(inputPath, out inputSplats);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read {inputPath}: {ex.Message}");
                return null;
            }

            if (inputSplats.Length == 0)
            {
                Debug.LogError($"No splats in {inputPath}");
                return null;
            }

            // Calculate bounds
            float3 boundsMin, boundsMax;
            unsafe
            {
                var boundsJob = new CalcBoundsJob
                {
                    m_BoundsMin = &boundsMin,
                    m_BoundsMax = &boundsMax,
                    m_SplatData = inputSplats
                };
                boundsJob.Schedule().Complete();
            }

            // Morton reorder
            ReorderMorton(inputSplats, boundsMin, boundsMax);

            // Create asset
            GaussianSplatAsset asset = ScriptableObject.CreateInstance<GaussianSplatAsset>();
            asset.Initialize(inputSplats.Length, m_FormatPos, m_FormatScale, m_FormatColor, m_FormatSH, boundsMin, boundsMax, null);
            asset.name = baseName;

            bool useChunks = m_FormatPos != GaussianSplatAsset.VectorFormat.Float32 ||
                            m_FormatScale != GaussianSplatAsset.VectorFormat.Float32 ||
                            m_FormatColor != GaussianSplatAsset.ColorFormat.Float32x4 ||
                            m_FormatSH != GaussianSplatAsset.SHFormat.Float32;

            var dataHash = new Hash128((uint)asset.splatCount, (uint)asset.formatVersion, 0, 0);

            string pathChunk = $"{m_OutputFolder}/{baseName}_chk.bytes";
            string pathPos = $"{m_OutputFolder}/{baseName}_pos.bytes";
            string pathOther = $"{m_OutputFolder}/{baseName}_oth.bytes";
            string pathCol = $"{m_OutputFolder}/{baseName}_col.bytes";
            string pathSh = $"{m_OutputFolder}/{baseName}_shs.bytes";

            // Create data files (simplified version without SH clustering for speed)
            if (useChunks)
                CreateChunkData(inputSplats, pathChunk, ref dataHash);
            CreatePositionsData(inputSplats, pathPos, ref dataHash);
            CreateOtherData(inputSplats, pathOther, ref dataHash);
            CreateColorData(inputSplats, pathCol, ref dataHash);
            CreateSHData(inputSplats, pathSh, ref dataHash);

            asset.SetDataHash(dataHash);
            inputSplats.Dispose();

            // Import created files
            AssetDatabase.Refresh(ImportAssetOptions.ForceUncompressedImport);

            // Set asset files
            asset.SetAssetFiles(
                useChunks ? AssetDatabase.LoadAssetAtPath<TextAsset>(pathChunk) : null,
                AssetDatabase.LoadAssetAtPath<TextAsset>(pathPos),
                AssetDatabase.LoadAssetAtPath<TextAsset>(pathOther),
                AssetDatabase.LoadAssetAtPath<TextAsset>(pathCol),
                AssetDatabase.LoadAssetAtPath<TextAsset>(pathSh));

            var assetPath = $"{m_OutputFolder}/{baseName}.asset";
            AssetDatabase.CreateAsset(asset, assetPath);

            return asset;
        }

        void CreateSequenceAsset(List<GaussianSplatAsset> assets)
        {
            var sequence = ScriptableObject.CreateInstance<GaussianSplatSequence>();
            sequence.frames = assets.ToArray();
            sequence.name = "GaussianSequence";

            string sequencePath = $"{m_OutputFolder}/GaussianSequence.asset";
            AssetDatabase.CreateAsset(sequence, sequencePath);
            
            Selection.activeObject = sequence;
        }

        #region Data Creation Methods (simplified from GaussianSplatAssetCreator)

        [BurstCompile]
        struct CalcBoundsJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public unsafe float3* m_BoundsMin;
            [NativeDisableUnsafePtrRestriction] public unsafe float3* m_BoundsMax;
            [ReadOnly] public NativeArray<InputSplatData> m_SplatData;

            public unsafe void Execute()
            {
                float3 boundsMin = float.PositiveInfinity;
                float3 boundsMax = float.NegativeInfinity;

                for (int i = 0; i < m_SplatData.Length; ++i)
                {
                    float3 pos = m_SplatData[i].pos;
                    boundsMin = math.min(boundsMin, pos);
                    boundsMax = math.max(boundsMax, pos);
                }
                *m_BoundsMin = boundsMin;
                *m_BoundsMax = boundsMax;
            }
        }

        [BurstCompile]
        struct ReorderMortonJob : IJobParallelFor
        {
            const float kScaler = (float)((1 << 21) - 1);
            public float3 m_BoundsMin;
            public float3 m_InvBoundsSize;
            [ReadOnly] public NativeArray<InputSplatData> m_SplatData;
            public NativeArray<(ulong, int)> m_Order;

            public void Execute(int index)
            {
                float3 pos = ((float3)m_SplatData[index].pos - m_BoundsMin) * m_InvBoundsSize * kScaler;
                uint3 ipos = (uint3)pos;
                ulong code = GaussianUtils.MortonEncode3(ipos);
                m_Order[index] = (code, index);
            }
        }

        struct OrderComparer : IComparer<(ulong, int)>
        {
            public int Compare((ulong, int) a, (ulong, int) b)
            {
                if (a.Item1 < b.Item1) return -1;
                if (a.Item1 > b.Item1) return +1;
                return a.Item2 - b.Item2;
            }
        }

        static void ReorderMorton(NativeArray<InputSplatData> splatData, float3 boundsMin, float3 boundsMax)
        {
            ReorderMortonJob order = new ReorderMortonJob
            {
                m_SplatData = splatData,
                m_BoundsMin = boundsMin,
                m_InvBoundsSize = 1.0f / (boundsMax - boundsMin),
                m_Order = new NativeArray<(ulong, int)>(splatData.Length, Allocator.TempJob)
            };
            order.Schedule(splatData.Length, 4096).Complete();
            order.m_Order.Sort(new OrderComparer());

            NativeArray<InputSplatData> copy = new(order.m_SplatData, Allocator.TempJob);
            for (int i = 0; i < copy.Length; ++i)
                order.m_SplatData[i] = copy[order.m_Order[i].Item2];
            copy.Dispose();
            order.m_Order.Dispose();
        }

        [BurstCompile]
        struct CalcChunkDataJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<InputSplatData> splatData;
            public NativeArray<GaussianSplatAsset.ChunkInfo> chunks;

            public void Execute(int chunkIdx)
            {
                float3 chunkMinpos = float.PositiveInfinity;
                float3 chunkMinscl = float.PositiveInfinity;
                float4 chunkMincol = float.PositiveInfinity;
                float3 chunkMinshs = float.PositiveInfinity;
                float3 chunkMaxpos = float.NegativeInfinity;
                float3 chunkMaxscl = float.NegativeInfinity;
                float4 chunkMaxcol = float.NegativeInfinity;
                float3 chunkMaxshs = float.NegativeInfinity;

                int splatBegin = math.min(chunkIdx * GaussianSplatAsset.kChunkSize, splatData.Length);
                int splatEnd = math.min((chunkIdx + 1) * GaussianSplatAsset.kChunkSize, splatData.Length);

                for (int i = splatBegin; i < splatEnd; ++i)
                {
                    InputSplatData s = splatData[i];
                    s.scale = math.pow(s.scale, 1.0f / 8.0f);
                    s.opacity = GaussianUtils.SquareCentered01(s.opacity);
                    splatData[i] = s;

                    chunkMinpos = math.min(chunkMinpos, s.pos);
                    chunkMinscl = math.min(chunkMinscl, s.scale);
                    chunkMincol = math.min(chunkMincol, new float4(s.dc0, s.opacity));
                    UpdateSHBounds(s, ref chunkMinshs, ref chunkMaxshs, true);
                    chunkMaxpos = math.max(chunkMaxpos, s.pos);
                    chunkMaxscl = math.max(chunkMaxscl, s.scale);
                    chunkMaxcol = math.max(chunkMaxcol, new float4(s.dc0, s.opacity));
                }

                chunkMaxpos = math.max(chunkMaxpos, chunkMinpos + 1.0e-5f);
                chunkMaxscl = math.max(chunkMaxscl, chunkMinscl + 1.0e-5f);
                chunkMaxcol = math.max(chunkMaxcol, chunkMincol + 1.0e-5f);
                chunkMaxshs = math.max(chunkMaxshs, chunkMinshs + 1.0e-5f);

                GaussianSplatAsset.ChunkInfo info = default;
                info.posX = new float2(chunkMinpos.x, chunkMaxpos.x);
                info.posY = new float2(chunkMinpos.y, chunkMaxpos.y);
                info.posZ = new float2(chunkMinpos.z, chunkMaxpos.z);
                info.sclX = math.f32tof16(chunkMinscl.x) | (math.f32tof16(chunkMaxscl.x) << 16);
                info.sclY = math.f32tof16(chunkMinscl.y) | (math.f32tof16(chunkMaxscl.y) << 16);
                info.sclZ = math.f32tof16(chunkMinscl.z) | (math.f32tof16(chunkMaxscl.z) << 16);
                info.colR = math.f32tof16(chunkMincol.x) | (math.f32tof16(chunkMaxcol.x) << 16);
                info.colG = math.f32tof16(chunkMincol.y) | (math.f32tof16(chunkMaxcol.y) << 16);
                info.colB = math.f32tof16(chunkMincol.z) | (math.f32tof16(chunkMaxcol.z) << 16);
                info.colA = math.f32tof16(chunkMincol.w) | (math.f32tof16(chunkMaxcol.w) << 16);
                info.shR = math.f32tof16(chunkMinshs.x) | (math.f32tof16(chunkMaxshs.x) << 16);
                info.shG = math.f32tof16(chunkMinshs.y) | (math.f32tof16(chunkMaxshs.y) << 16);
                info.shB = math.f32tof16(chunkMinshs.z) | (math.f32tof16(chunkMaxshs.z) << 16);
                chunks[chunkIdx] = info;

                for (int i = splatBegin; i < splatEnd; ++i)
                {
                    InputSplatData s = splatData[i];
                    s.pos = ((float3)s.pos - chunkMinpos) / (chunkMaxpos - chunkMinpos);
                    s.scale = ((float3)s.scale - chunkMinscl) / (chunkMaxscl - chunkMinscl);
                    s.dc0 = ((float3)s.dc0 - chunkMincol.xyz) / (chunkMaxcol.xyz - chunkMincol.xyz);
                    s.opacity = (s.opacity - chunkMincol.w) / (chunkMaxcol.w - chunkMincol.w);
                    NormalizeSHs(ref s, chunkMinshs, chunkMaxshs);
                    splatData[i] = s;
                }
            }

            static void UpdateSHBounds(InputSplatData s, ref float3 minSh, ref float3 maxSh, bool isMin)
            {
                if (isMin)
                {
                    minSh = math.min(minSh, s.sh1); minSh = math.min(minSh, s.sh2);
                    minSh = math.min(minSh, s.sh3); minSh = math.min(minSh, s.sh4);
                    minSh = math.min(minSh, s.sh5); minSh = math.min(minSh, s.sh6);
                    minSh = math.min(minSh, s.sh7); minSh = math.min(minSh, s.sh8);
                    minSh = math.min(minSh, s.sh9); minSh = math.min(minSh, s.shA);
                    minSh = math.min(minSh, s.shB); minSh = math.min(minSh, s.shC);
                    minSh = math.min(minSh, s.shD); minSh = math.min(minSh, s.shE);
                    minSh = math.min(minSh, s.shF);
                }
                maxSh = math.max(maxSh, s.sh1); maxSh = math.max(maxSh, s.sh2);
                maxSh = math.max(maxSh, s.sh3); maxSh = math.max(maxSh, s.sh4);
                maxSh = math.max(maxSh, s.sh5); maxSh = math.max(maxSh, s.sh6);
                maxSh = math.max(maxSh, s.sh7); maxSh = math.max(maxSh, s.sh8);
                maxSh = math.max(maxSh, s.sh9); maxSh = math.max(maxSh, s.shA);
                maxSh = math.max(maxSh, s.shB); maxSh = math.max(maxSh, s.shC);
                maxSh = math.max(maxSh, s.shD); maxSh = math.max(maxSh, s.shE);
                maxSh = math.max(maxSh, s.shF);
            }

            static void NormalizeSHs(ref InputSplatData s, float3 minSh, float3 maxSh)
            {
                float3 range = maxSh - minSh;
                s.sh1 = ((float3)s.sh1 - minSh) / range;
                s.sh2 = ((float3)s.sh2 - minSh) / range;
                s.sh3 = ((float3)s.sh3 - minSh) / range;
                s.sh4 = ((float3)s.sh4 - minSh) / range;
                s.sh5 = ((float3)s.sh5 - minSh) / range;
                s.sh6 = ((float3)s.sh6 - minSh) / range;
                s.sh7 = ((float3)s.sh7 - minSh) / range;
                s.sh8 = ((float3)s.sh8 - minSh) / range;
                s.sh9 = ((float3)s.sh9 - minSh) / range;
                s.shA = ((float3)s.shA - minSh) / range;
                s.shB = ((float3)s.shB - minSh) / range;
                s.shC = ((float3)s.shC - minSh) / range;
                s.shD = ((float3)s.shD - minSh) / range;
                s.shE = ((float3)s.shE - minSh) / range;
                s.shF = ((float3)s.shF - minSh) / range;
            }
        }

        void CreateChunkData(NativeArray<InputSplatData> splatData, string filePath, ref Hash128 dataHash)
        {
            int chunkCount = (splatData.Length + GaussianSplatAsset.kChunkSize - 1) / GaussianSplatAsset.kChunkSize;
            CalcChunkDataJob job = new CalcChunkDataJob
            {
                splatData = splatData,
                chunks = new NativeArray<GaussianSplatAsset.ChunkInfo>(chunkCount, Allocator.TempJob),
            };
            job.Schedule(chunkCount, 8).Complete();
            dataHash.Append(ref job.chunks);

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            fs.Write(job.chunks.Reinterpret<byte>(UnsafeUtility.SizeOf<GaussianSplatAsset.ChunkInfo>()));
            job.chunks.Dispose();
        }

        static ulong EncodeFloat3ToNorm16(float3 v) => (ulong)(v.x * 65535.5f) | ((ulong)(v.y * 65535.5f) << 16) | ((ulong)(v.z * 65535.5f) << 32);
        static uint EncodeFloat3ToNorm11(float3 v) => (uint)(v.x * 2047.5f) | ((uint)(v.y * 1023.5f) << 11) | ((uint)(v.z * 2047.5f) << 21);
        static ushort EncodeFloat3ToNorm655(float3 v) => (ushort)((uint)(v.x * 63.5f) | ((uint)(v.y * 31.5f) << 6) | ((uint)(v.z * 31.5f) << 11));
        static ushort EncodeFloat3ToNorm565(float3 v) => (ushort)((uint)(v.x * 31.5f) | ((uint)(v.y * 63.5f) << 5) | ((uint)(v.z * 31.5f) << 11));
        static uint EncodeQuatToNorm10(float4 v) => (uint)(v.x * 1023.5f) | ((uint)(v.y * 1023.5f) << 10) | ((uint)(v.z * 1023.5f) << 20) | ((uint)(v.w * 3.5f) << 30);

        static unsafe void EmitEncodedVector(float3 v, byte* outputPtr, GaussianSplatAsset.VectorFormat format)
        {
            switch (format)
            {
                case GaussianSplatAsset.VectorFormat.Float32:
                    *(float*)outputPtr = v.x;
                    *(float*)(outputPtr + 4) = v.y;
                    *(float*)(outputPtr + 8) = v.z;
                    break;
                case GaussianSplatAsset.VectorFormat.Norm16:
                    ulong enc16 = EncodeFloat3ToNorm16(math.saturate(v));
                    *(uint*)outputPtr = (uint)enc16;
                    *(ushort*)(outputPtr + 4) = (ushort)(enc16 >> 32);
                    break;
                case GaussianSplatAsset.VectorFormat.Norm11:
                    *(uint*)outputPtr = EncodeFloat3ToNorm11(math.saturate(v));
                    break;
                case GaussianSplatAsset.VectorFormat.Norm6:
                    *(ushort*)outputPtr = EncodeFloat3ToNorm655(math.saturate(v));
                    break;
            }
        }

        [BurstCompile]
        struct CreatePositionsDataJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<InputSplatData> m_Input;
            public GaussianSplatAsset.VectorFormat m_Format;
            public int m_FormatSize;
            [NativeDisableParallelForRestriction] public NativeArray<byte> m_Output;

            public unsafe void Execute(int index)
            {
                byte* outputPtr = (byte*)m_Output.GetUnsafePtr() + index * m_FormatSize;
                EmitEncodedVector(m_Input[index].pos, outputPtr, m_Format);
            }
        }

        void CreatePositionsData(NativeArray<InputSplatData> inputSplats, string filePath, ref Hash128 dataHash)
        {
            int formatSize = GaussianSplatAsset.GetVectorSize(m_FormatPos);
            int dataLen = ((inputSplats.Length * formatSize + 7) / 8) * 8;
            NativeArray<byte> data = new(dataLen, Allocator.TempJob);

            var job = new CreatePositionsDataJob
            {
                m_Input = inputSplats,
                m_Format = m_FormatPos,
                m_FormatSize = formatSize,
                m_Output = data
            };
            job.Schedule(inputSplats.Length, 8192).Complete();
            dataHash.Append(data);

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            fs.Write(data);
            data.Dispose();
        }

        [BurstCompile]
        struct CreateOtherDataJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<InputSplatData> m_Input;
            public GaussianSplatAsset.VectorFormat m_ScaleFormat;
            public int m_FormatSize;
            [NativeDisableParallelForRestriction] public NativeArray<byte> m_Output;

            public unsafe void Execute(int index)
            {
                byte* outputPtr = (byte*)m_Output.GetUnsafePtr() + index * m_FormatSize;
                Quaternion rotQ = m_Input[index].rot;
                float4 rot = new float4(rotQ.x, rotQ.y, rotQ.z, rotQ.w);
                *(uint*)outputPtr = EncodeQuatToNorm10(rot);
                outputPtr += 4;
                EmitEncodedVector(m_Input[index].scale, outputPtr, m_ScaleFormat);
            }
        }

        void CreateOtherData(NativeArray<InputSplatData> inputSplats, string filePath, ref Hash128 dataHash)
        {
            int formatSize = GaussianSplatAsset.GetOtherSizeNoSHIndex(m_FormatScale);
            int dataLen = ((inputSplats.Length * formatSize + 7) / 8) * 8;
            NativeArray<byte> data = new(dataLen, Allocator.TempJob);

            var job = new CreateOtherDataJob
            {
                m_Input = inputSplats,
                m_ScaleFormat = m_FormatScale,
                m_FormatSize = formatSize,
                m_Output = data
            };
            job.Schedule(inputSplats.Length, 8192).Complete();
            dataHash.Append(data);

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            fs.Write(data);
            data.Dispose();
        }

        static int SplatIndexToTextureIndex(uint idx)
        {
            uint2 xy = GaussianUtils.DecodeMorton2D_16x16(idx);
            uint width = GaussianSplatAsset.kTextureWidth / 16;
            idx >>= 8;
            uint x = (idx % width) * 16 + xy.x;
            uint y = (idx / width) * 16 + xy.y;
            return (int)(y * GaussianSplatAsset.kTextureWidth + x);
        }

        [BurstCompile]
        struct CreateColorDataJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<InputSplatData> m_Input;
            [NativeDisableParallelForRestriction] public NativeArray<float4> m_Output;

            public void Execute(int index)
            {
                var splat = m_Input[index];
                int i = SplatIndexToTextureIndex((uint)index);
                m_Output[i] = new float4(splat.dc0.x, splat.dc0.y, splat.dc0.z, splat.opacity);
            }
        }

        [BurstCompile]
        struct ConvertColorJob : IJobParallelFor
        {
            public int width, height;
            [ReadOnly] public NativeArray<float4> inputData;
            [NativeDisableParallelForRestriction] public NativeArray<byte> outputData;
            public GaussianSplatAsset.ColorFormat format;
            public int formatBytesPerPixel;

            public unsafe void Execute(int y)
            {
                int srcIdx = y * width;
                byte* dstPtr = (byte*)outputData.GetUnsafePtr() + y * width * formatBytesPerPixel;
                for (int x = 0; x < width; ++x)
                {
                    float4 pix = inputData[srcIdx];
                    switch (format)
                    {
                        case GaussianSplatAsset.ColorFormat.Float32x4:
                            *(float4*)dstPtr = pix;
                            break;
                        case GaussianSplatAsset.ColorFormat.Float16x4:
                            *(half4*)dstPtr = new half4(pix);
                            break;
                        case GaussianSplatAsset.ColorFormat.Norm8x4:
                            pix = math.saturate(pix);
                            uint enc = (uint)(pix.x * 255.5f) | ((uint)(pix.y * 255.5f) << 8) | ((uint)(pix.z * 255.5f) << 16) | ((uint)(pix.w * 255.5f) << 24);
                            *(uint*)dstPtr = enc;
                            break;
                    }
                    srcIdx++;
                    dstPtr += formatBytesPerPixel;
                }
            }
        }

        void CreateColorData(NativeArray<InputSplatData> inputSplats, string filePath, ref Hash128 dataHash)
        {
            var (width, height) = GaussianSplatAsset.CalcTextureSize(inputSplats.Length);
            NativeArray<float4> data = new(width * height, Allocator.TempJob);

            var job = new CreateColorDataJob { m_Input = inputSplats, m_Output = data };
            job.Schedule(inputSplats.Length, 8192).Complete();

            dataHash.Append(data);
            dataHash.Append((int)m_FormatColor);

            GraphicsFormat gfxFormat = GaussianSplatAsset.ColorFormatToGraphics(m_FormatColor);
            int dstSize = (int)GraphicsFormatUtility.ComputeMipmapSize(width, height, gfxFormat);

            if (GraphicsFormatUtility.IsCompressedFormat(gfxFormat))
            {
                Texture2D tex = new Texture2D(width, height, GraphicsFormat.R32G32B32A32_SFloat, TextureCreationFlags.DontInitializePixels | TextureCreationFlags.DontUploadUponCreate);
                tex.SetPixelData(data, 0);
                EditorUtility.CompressTexture(tex, GraphicsFormatUtility.GetTextureFormat(gfxFormat), 100);
                NativeArray<byte> cmpData = tex.GetPixelData<byte>(0);
                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                fs.Write(cmpData);
                DestroyImmediate(tex);
            }
            else
            {
                var jobConvert = new ConvertColorJob
                {
                    width = width,
                    height = height,
                    inputData = data,
                    format = m_FormatColor,
                    outputData = new NativeArray<byte>(dstSize, Allocator.TempJob),
                    formatBytesPerPixel = dstSize / width / height
                };
                jobConvert.Schedule(height, 1).Complete();
                using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                fs.Write(jobConvert.outputData);
                jobConvert.outputData.Dispose();
            }
            data.Dispose();
        }

        [BurstCompile]
        struct CreateSHDataJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<InputSplatData> m_Input;
            public GaussianSplatAsset.SHFormat m_Format;
            public NativeArray<byte> m_Output;

            public unsafe void Execute(int index)
            {
                var splat = m_Input[index];
                switch (m_Format)
                {
                    case GaussianSplatAsset.SHFormat.Float32:
                    {
                        GaussianSplatAsset.SHTableItemFloat32 res;
                        res.sh1 = splat.sh1; res.sh2 = splat.sh2; res.sh3 = splat.sh3;
                        res.sh4 = splat.sh4; res.sh5 = splat.sh5; res.sh6 = splat.sh6;
                        res.sh7 = splat.sh7; res.sh8 = splat.sh8; res.sh9 = splat.sh9;
                        res.shA = splat.shA; res.shB = splat.shB; res.shC = splat.shC;
                        res.shD = splat.shD; res.shE = splat.shE; res.shF = splat.shF;
                        res.shPadding = default;
                        ((GaussianSplatAsset.SHTableItemFloat32*)m_Output.GetUnsafePtr())[index] = res;
                        break;
                    }
                    case GaussianSplatAsset.SHFormat.Float16:
                    {
                        GaussianSplatAsset.SHTableItemFloat16 res;
                        res.sh1 = new half3(splat.sh1); res.sh2 = new half3(splat.sh2); res.sh3 = new half3(splat.sh3);
                        res.sh4 = new half3(splat.sh4); res.sh5 = new half3(splat.sh5); res.sh6 = new half3(splat.sh6);
                        res.sh7 = new half3(splat.sh7); res.sh8 = new half3(splat.sh8); res.sh9 = new half3(splat.sh9);
                        res.shA = new half3(splat.shA); res.shB = new half3(splat.shB); res.shC = new half3(splat.shC);
                        res.shD = new half3(splat.shD); res.shE = new half3(splat.shE); res.shF = new half3(splat.shF);
                        res.shPadding = default;
                        ((GaussianSplatAsset.SHTableItemFloat16*)m_Output.GetUnsafePtr())[index] = res;
                        break;
                    }
                    case GaussianSplatAsset.SHFormat.Norm11:
                    {
                        GaussianSplatAsset.SHTableItemNorm11 res;
                        res.sh1 = EncodeFloat3ToNorm11(splat.sh1); res.sh2 = EncodeFloat3ToNorm11(splat.sh2);
                        res.sh3 = EncodeFloat3ToNorm11(splat.sh3); res.sh4 = EncodeFloat3ToNorm11(splat.sh4);
                        res.sh5 = EncodeFloat3ToNorm11(splat.sh5); res.sh6 = EncodeFloat3ToNorm11(splat.sh6);
                        res.sh7 = EncodeFloat3ToNorm11(splat.sh7); res.sh8 = EncodeFloat3ToNorm11(splat.sh8);
                        res.sh9 = EncodeFloat3ToNorm11(splat.sh9); res.shA = EncodeFloat3ToNorm11(splat.shA);
                        res.shB = EncodeFloat3ToNorm11(splat.shB); res.shC = EncodeFloat3ToNorm11(splat.shC);
                        res.shD = EncodeFloat3ToNorm11(splat.shD); res.shE = EncodeFloat3ToNorm11(splat.shE);
                        res.shF = EncodeFloat3ToNorm11(splat.shF);
                        ((GaussianSplatAsset.SHTableItemNorm11*)m_Output.GetUnsafePtr())[index] = res;
                        break;
                    }
                    case GaussianSplatAsset.SHFormat.Norm6:
                    {
                        GaussianSplatAsset.SHTableItemNorm6 res;
                        res.sh1 = EncodeFloat3ToNorm565(splat.sh1); res.sh2 = EncodeFloat3ToNorm565(splat.sh2);
                        res.sh3 = EncodeFloat3ToNorm565(splat.sh3); res.sh4 = EncodeFloat3ToNorm565(splat.sh4);
                        res.sh5 = EncodeFloat3ToNorm565(splat.sh5); res.sh6 = EncodeFloat3ToNorm565(splat.sh6);
                        res.sh7 = EncodeFloat3ToNorm565(splat.sh7); res.sh8 = EncodeFloat3ToNorm565(splat.sh8);
                        res.sh9 = EncodeFloat3ToNorm565(splat.sh9); res.shA = EncodeFloat3ToNorm565(splat.shA);
                        res.shB = EncodeFloat3ToNorm565(splat.shB); res.shC = EncodeFloat3ToNorm565(splat.shC);
                        res.shD = EncodeFloat3ToNorm565(splat.shD); res.shE = EncodeFloat3ToNorm565(splat.shE);
                        res.shF = EncodeFloat3ToNorm565(splat.shF);
                        res.shPadding = default;
                        ((GaussianSplatAsset.SHTableItemNorm6*)m_Output.GetUnsafePtr())[index] = res;
                        break;
                    }
                }
            }
        }

        void CreateSHData(NativeArray<InputSplatData> inputSplats, string filePath, ref Hash128 dataHash)
        {
            int dataLen = (int)GaussianSplatAsset.CalcSHDataSize(inputSplats.Length, m_FormatSH);
            NativeArray<byte> data = new(dataLen, Allocator.TempJob);

            var job = new CreateSHDataJob
            {
                m_Input = inputSplats,
                m_Format = m_FormatSH,
                m_Output = data
            };
            job.Schedule(inputSplats.Length, 8192).Complete();

            dataHash.Append(data);
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            fs.Write(data);
            data.Dispose();
        }

        #endregion
    }
}

