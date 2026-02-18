# Unity Gaussian Splatting Streaming

Real-time playback of animated 3D Gaussian Splatting sequences in Unity. This project extends the original [UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting) by Aras Pranckevičius with streaming and sequence playback capabilities.

<!-- ![Gaussian Splatting in Unity](/docs/Images/shotOverview.jpg) -->

## AR Demo

![AR Display Demo](/docs/Images/AR_demo.gif)

## Features

### 🎬 Sequence Playback
- **GaussianSplatSequence**: ScriptableObject that holds multiple GaussianSplatAssets for animated playback
- **GaussianSplatPlayer**: Component for playing sequences with full transport controls
  - Play, Pause, Stop functionality
  - Loop, Once, and PingPong playback modes
  - Adjustable playback speed (0.1x - 5x)
  - Frame-by-frame scrubbing
  - Unity Events for sequence completion and frame changes

### 🔄 Batch Conversion
- **Batch Convert PLY Sequence**: Convert folders of PLY files into optimized GaussianSplatAssets
- Automatic sequence asset creation
- Multiple quality presets (Very Low to Very High)
- Progress tracking with cancel support

### 🎮 Editor Controls
- Interactive timeline with frame slider
- Transport buttons (⏮ ◀ ▶/⏸ ▶ ⏭)
- Real-time preview in edit mode
- Flip Y axis option for coordinate system compatibility

## Quick Start

### 1. Install the Package
Add the `package` folder to your Unity project, or reference it via the Package Manager.

### 2. Convert Your PLY Sequence
1. Open **Tools → Gaussian Splats → Batch Convert PLY Sequence**
2. Select your input folder containing PLY files
3. Choose output folder and quality settings
4. Click **Convert** to create assets and a `GaussianSplatSequence`

### 3. Setup Playback
1. Add a GameObject with `GaussianSplatRenderer` component
2. Add the `GaussianSplatPlayer` component to the same object
3. Assign your `GaussianSplatSequence` to the player
4. Press Play!

## Demo Project

The `projects/URP_Stream` folder contains a demo Unity project using the Universal Render Pipeline (URP).

### Setting Up the Demo

1. Open `projects/URP_Stream` in Unity 2022.3+
2. The demo scene is at `Assets/GSTestScene.unity`
3. Use **Tools → Gaussian Splats → Batch Convert PLY Sequence** to convert your own PLY files
4. Save converted data to `Assets/GaussianSequence/`
5. Create or assign a `GaussianSplatSequence` to the player in the scene

> **Note**: The `GaussianSequence/` folder containing converted splat data is not included in this repository due to size. You'll need to generate your own data from PLY files.

## Requirements

- Unity 2022.3 or later
- D3D12, Metal, or Vulkan graphics API
- URP, HDRP, or Built-in Render Pipeline

## Platform Support

| Platform | Status |
|----------|--------|
| Windows (D3D12/Vulkan) | ✅ Supported |
| macOS (Metal) | ✅ Supported |
| Linux (Vulkan) | ✅ Supported |
| VR (Quest 3, Vive, Varjo) | ✅ Supported |
| WebGL/OpenGL | ❌ Not supported |

## Credits

This project is built on top of:
- [UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting) by Aras Pranckevičius
- Based on the paper [3D Gaussian Splatting for Real-Time Radiance Field Rendering](https://repo-sam.inria.fr/fungraph/3d-gaussian-splatting/) (SIGGRAPH 2023)

## License

MIT License - see [LICENSE.md](LICENSE.md)

**Note**: The original Gaussian Splatting training software has specific licensing terms for commercial use. This viewer is MIT licensed, but please review how your Gaussian Splat PLY files were created.

