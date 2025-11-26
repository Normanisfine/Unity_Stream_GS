// AR Image Tracking for Gaussian Splat Sequences
// Detects a tracked image and places/plays a Gaussian Splat sequence on it

using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using GaussianSplatting.Runtime;

public class ARGaussianSplatTracker : MonoBehaviour
{
    [Header("AR Components")]
    [Tooltip("The AR Tracked Image Manager component")]
    public ARTrackedImageManager trackedImageManager;

    [Header("Gaussian Splat")]
    [Tooltip("The GameObject containing GaussianSplatRenderer and GaussianSplatPlayer")]
    public GameObject gaussianSplatPrefab;

    [Tooltip("Offset from the tracked image center")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Additional rotation applied to the splat")]
    public Vector3 rotationOffset = new Vector3(-90f, 0f, 0f);

    [Tooltip("Scale multiplier for the splat")]
    public float scaleMultiplier = 0.1f;

    [Header("Playback")]
    [Tooltip("Auto-play when image is first detected")]
    public bool autoPlayOnDetection = true;

    [Tooltip("Pause playback when tracking is lost")]
    public bool pauseWhenLost = true;

    // Spawned instance
    private GameObject spawnedSplat;
    private GaussianSplatPlayer splatPlayer;
    private bool isTracking = false;

    void OnEnable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        }
    }

    void OnDisable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }
    }

    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        // Handle newly detected images
        foreach (var trackedImage in args.added)
        {
            OnImageDetected(trackedImage);
        }

        // Handle updated images (tracking state changes)
        foreach (var trackedImage in args.updated)
        {
            OnImageUpdated(trackedImage);
        }

        // Handle removed images
        foreach (var trackedImage in args.removed)
        {
            OnImageLost(trackedImage);
        }
    }

    void OnImageDetected(ARTrackedImage trackedImage)
    {
        Debug.Log($"[ARGaussianSplatTracker] Image detected: {trackedImage.referenceImage.name}");

        if (spawnedSplat == null && gaussianSplatPrefab != null)
        {
            // Spawn the Gaussian Splat
            spawnedSplat = Instantiate(gaussianSplatPrefab);
            spawnedSplat.name = "AR_GaussianSplat";

            // Get the player component
            splatPlayer = spawnedSplat.GetComponent<GaussianSplatPlayer>();
            if (splatPlayer == null)
            {
                splatPlayer = spawnedSplat.GetComponentInChildren<GaussianSplatPlayer>();
            }

            Debug.Log($"[ARGaussianSplatTracker] Spawned Gaussian Splat");
        }

        // Update position
        UpdateSplatTransform(trackedImage);

        // Auto-play if enabled
        if (autoPlayOnDetection && splatPlayer != null)
        {
            splatPlayer.Play();
            Debug.Log("[ARGaussianSplatTracker] Started playback");
        }

        isTracking = true;
    }

    void OnImageUpdated(ARTrackedImage trackedImage)
    {
        if (spawnedSplat == null) return;

        // Check tracking state
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            // Update position while tracking
            UpdateSplatTransform(trackedImage);

            if (!isTracking)
            {
                // Tracking resumed
                spawnedSplat.SetActive(true);
                if (splatPlayer != null && pauseWhenLost)
                {
                    splatPlayer.Play();
                }
                isTracking = true;
                Debug.Log("[ARGaussianSplatTracker] Tracking resumed");
            }
        }
        else if (trackedImage.trackingState == TrackingState.Limited)
        {
            // Limited tracking - keep visible but don't update position
            if (isTracking && pauseWhenLost && splatPlayer != null)
            {
                splatPlayer.Pause();
            }
            isTracking = false;
        }
        else
        {
            // No tracking
            if (isTracking)
            {
                if (pauseWhenLost && splatPlayer != null)
                {
                    splatPlayer.Pause();
                }
                spawnedSplat.SetActive(false);
                isTracking = false;
                Debug.Log("[ARGaussianSplatTracker] Tracking lost");
            }
        }
    }

    void OnImageLost(ARTrackedImage trackedImage)
    {
        Debug.Log($"[ARGaussianSplatTracker] Image removed: {trackedImage.referenceImage.name}");

        if (spawnedSplat != null)
        {
            if (pauseWhenLost && splatPlayer != null)
            {
                splatPlayer.Pause();
            }
            spawnedSplat.SetActive(false);
            isTracking = false;
        }
    }

    void UpdateSplatTransform(ARTrackedImage trackedImage)
    {
        if (spawnedSplat == null) return;

        // Position: at tracked image with offset
        spawnedSplat.transform.position = trackedImage.transform.position + 
            trackedImage.transform.TransformDirection(positionOffset);

        // Rotation: match tracked image + offset
        spawnedSplat.transform.rotation = trackedImage.transform.rotation * 
            Quaternion.Euler(rotationOffset);

        // Scale: based on tracked image size
        float imageSize = Mathf.Max(trackedImage.size.x, trackedImage.size.y);
        float scale = imageSize * scaleMultiplier;
        spawnedSplat.transform.localScale = Vector3.one * scale;
    }

    // Public methods for UI control
    public void TogglePlayPause()
    {
        if (splatPlayer != null)
        {
            splatPlayer.TogglePlayPause();
        }
    }

    public void StopPlayback()
    {
        if (splatPlayer != null)
        {
            splatPlayer.Stop();
        }
    }

    public void RestartPlayback()
    {
        if (splatPlayer != null)
        {
            splatPlayer.Stop();
            splatPlayer.Play();
        }
    }
}

