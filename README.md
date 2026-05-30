# Unity AssetBundle Helper Pro

A Unity Editor tool that automates and simplifies multi-platform AssetBundle generation, reducing build time, eliminating manual configuration errors, and streamlining deployment workflows.

## Overview

Building AssetBundles across multiple platforms often requires repetitive manual steps such as switching color spaces, configuring compression settings, managing output folders, and building each platform individually. This tool automates the entire workflow through a single editor window.

## Key Features

### Multi-Platform AssetBundle Builds

Build AssetBundles for multiple platforms in a single operation:

* Windows
* macOS
* iOS
* HoloLens (UWP)
* Android / Meta Quest

### Automatic Color Space Management

The tool automatically applies the correct rendering color space based on the target platform:

| Platform        | Color Space |
| --------------- | ----------- |
| Android / Quest | Linear      |
| Windows         | Gamma       |
| macOS           | Gamma       |
| iOS             | Gamma       |
| HoloLens        | Gamma       |

The original project settings are restored automatically after the build completes.

### Selective Bundle Building

* Build only the AssetBundles you need.
* Search and filter large bundle lists.
* Reduce unnecessary build time.

### Organized Output Structure

Automatically creates platform-specific output directories:

* StandaloneWindows
* StandaloneOSX
* iOS
* WSAPlayer
* Android

Optional cleanup removes previous build artifacts before generating new bundles.

### Optimized Compression

Uses Unity's LZ4 chunk-based compression to provide a balanced tradeoff between build size and loading performance.

### Error Prevention

* Validates build paths before execution.
* Prevents incorrect platform settings.
* Displays clear build progress and status updates.
* Generates build summaries with success/failure reporting.

## Results

### Before

* Manual platform switching
* Repeated color space configuration
* Multiple build operations
* High risk of configuration mistakes

### After

* One-click build workflow
* Automatic platform configuration
* Consistent output structure
* Significantly reduced build time

## Technologies

* Unity Editor Scripting
* C#
* AssetBundle Pipeline
* Build Automation
* Editor UI Development

## Future Improvements

* CI/CD integration
* Automated versioning
* Build reports and analytics
* Cloud storage upload support
* Build profile presets

## Screenshots
<img width="1135" height="1251" alt="Unity_Bmfr1V9s0g" src="https://github.com/user-attachments/assets/3bb4ff89-c04d-4222-a061-e1bf67a2612c" />


MIT License
