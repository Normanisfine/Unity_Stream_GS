==================================================
AR GAUSSIAN SPLAT IMAGE TRACKING - SETUP GUIDE
==================================================

STEP 1: Create XR Reference Image Library
-----------------------------------------
1. Right-click in Project window → Create → XR → Reference Image Library
2. Name it "TrackingImages"
3. Click "Add Image" 
4. Drag your "track.png" into the Texture slot
5. Set "Specify Size" = ON
6. Set Physical Size (e.g., Width = 0.1 meters for a 10cm marker)
   - This MUST match your printed marker's real-world size!
7. Name it "TrackMarker"


STEP 2: Create AR Scene
-----------------------
1. Create new scene: File → New Scene → Basic (Built-in)
2. Delete the default "Main Camera"
3. Add AR Session: GameObject → XR → AR Session
4. Add XR Origin: GameObject → XR → XR Origin (Mobile AR)
   - This includes AR Camera automatically


STEP 3: Setup AR Tracked Image Manager
--------------------------------------
1. Select "XR Origin" in Hierarchy
2. Add Component → AR Tracked Image Manager
3. In the Inspector:
   - Serialized Library = Your "TrackingImages" library
   - Max Number Of Moving Images = 1


STEP 4: Create GS_Seq Prefab
----------------------------
1. In Hierarchy, create your Gaussian Splat setup:
   - Empty GameObject named "GS_Seq_Prefab"
   - Add Component: Gaussian Splat Renderer
   - Add Component: Gaussian Splat Player
   - Assign your GaussianSequence to the player
2. Drag "GS_Seq_Prefab" from Hierarchy to Project to create prefab
3. Delete from scene (it will be spawned by AR tracker)


STEP 5: Setup AR Tracker
------------------------
1. Create Empty GameObject named "ARGaussianSplatTracker"
2. Add Component: ARGaussianSplatTracker (the script we created)
3. In Inspector, assign:
   - Tracked Image Manager = XR Origin's ARTrackedImageManager
   - Gaussian Splat Prefab = Your GS_Seq_Prefab
   - Adjust Position Offset, Rotation Offset as needed
   - Scale Multiplier: Start with 0.5-1.0, adjust based on your model


STEP 6: Build Settings
----------------------
1. File → Build Settings → iOS
2. Player Settings:
   - Camera Usage Description: "AR camera access required"
   - Target minimum iOS Version: 12.0+
   - Architecture: ARM64
3. XR Plug-in Management → iOS → Enable "ARKit"


STEP 7: Test
------------
1. Build and Run on iOS device
2. Point camera at your printed track.png marker
3. The Gaussian Splat sequence should appear and play!


TROUBLESHOOTING
---------------
- Model appears upside down? Adjust Rotation Offset (try -90, 0, 0)
- Model too big/small? Adjust Scale Multiplier
- Model offset from marker? Adjust Position Offset
- Not detecting? Check marker size matches physical print size
- Check Console for "[ARGaussianSplatTracker]" debug messages


HIERARCHY STRUCTURE (Final)
---------------------------
Scene
├── AR Session
├── XR Origin
│   └── Camera Offset
│       └── Main Camera (AR Camera)
└── ARGaussianSplatTracker
    └── (GS_Seq_Prefab spawns here at runtime)

