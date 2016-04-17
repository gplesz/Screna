![Logo](Screna.png)
# Screna
[![Build status](https://ci.appveyor.com/api/projects/status/nadvi6vf6kl999g5?svg=true)](https://ci.appveyor.com/project/MathewSachin/screna)  
.Net Capture Solution to Capture Screen/Audio/Video/Mouse Cursor/KeyStrokes and more...

Screna provides a highly extensible API to develop Capturing Apps.

# News
* Screna is now composed of a collection of packages, instead of a single one.  
  Main reasons behind this were licensing issues and the mess involved in copying code from other projects into Screna.
  
* Added a BASS audio library extension for Screna.
  
Package             | Feature
--------------------|--------------------------------------------
Screna.Lame         | Supports LAME Mp3 Encoder
Screna.MouseKeyHook | Provides Mouse Click and Keystroke Overlays using [MouseKeyHook](https://github.com/gmamaladze/globalmousekeyhook) by George Mamaladze
Screna.Bass         | Audio Support using BASS audio library
Screna.SharpAvi     | Avi support using [SharpAvi](https://github.com/baSSiLL/SharpAvi) by Vasilli Massilov


# Getting Started

Install the Package from NuGet.
```powershell
Install-Package Screna
```

> [Captura](https://github.com/MathewSachin/Captura) is a Capture application demonstrating all the features of Screna.