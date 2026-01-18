<div align="center">

<!--[![Discord](https://img.shields.io/discord/dicord_id?label=Discord&logo=discord&style=flat-square&color=5865F2)](link)
Join our [Discord](YOUR_DISCORD_LINK) for early access and updates.-->
# ⚡ KeyASIO.Net
**The Ultimate Low-Latency Audio Middleware for osu!**

[![Release](https://img.shields.io/github/v/release/Milkitic/KeyASIO.Net?style=flat-square&color=56b6c2)](https://github.com/Milkitic/KeyASIO.Net/releases/latest)
[![Platform](https://img.shields.io/badge/platform-Windows-blue?style=flat-square)](https://github.com/Milkitic/KeyASIO.Net)
[![License](https://img.shields.io/github/license/Milkitic/KeyASIO.Net?style=flat-square)](LICENSE)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/Milkitic/KeyASIO.Net)

<p align="center">
  <b>Experience audio latency as low as 0.6ms.</b><br>
  <i>"I was hearing clicks before I even tapped." — Top Player Feedback</i>
</p>

</div>

---

## 🚀 Why KeyASIO?

While osu!'s audio system suffers from a typical latency of **30~40ms**, KeyASIO bypasses the Windows mixer entirely using an external **ASIO / WASAPI Exclusive** backend.

* **⚡ Extreme Performance:** Achieves **~0.6ms** latency on high-end hardware (Verified by [EmertxE](https://osu.ppy.sh/users/954557)). Even on generic hardware, expect **8-15ms**.
* **🎮 True Game Integration:** Uses [ReadProcessMemory](https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-readprocessmemory) to read memory safely. It's not just "pressing a key makes a sound" — it's real-time game state synchronization.
* **🎧 Rich Audio Support:** Fully supports custom hitsounds, storyboard samples, dynamic volume, and skin overrides.
* **🎹 Mania Optimization:** Per-key sound processing, identical to native game behavior.

## 📸 Screenshots

<p align="center">
  <img src="docs/overview.png" width="45%" alt="Overview">
  <img src="docs/realtimeoptions.png" width="45%" alt="Options">
</p>

---

## ⚙️ Configuration Guide

KeyASIO offers two main modes. Choose the one that fits your hardware.

### 🚩 Which mode should I choose?

* **I have a dedicated Soundcard (Audio Interface) / Mixer:** 👉 **Hardware Mix (Recommended)**. Lowest latency, requires hardware setup.
* **I use a Laptop / Integrated Audio / No Mixer:** 👉 **Software Mix (FullMode)**. Easier setup, requires [VB-CABLE](https://vb-audio.com/Cable/).

<details>
<summary><h3>🔧 Option A: Hardware Mix (FullMode DISABLED) - Recommended</h3></summary>

**Target:** Users with >1 audio outputs (e.g., PC Line-out + dedicated soundcard) AND a physical mixer.

1.  **Prerequisites:**
    * Install your Soundcard's ASIO driver or [ASIO4ALL](https://www.asio4all.org/).
    * A physical mixer to combine audio from osu! (Music) and KeyASIO (Hitsounds).
2.  **Setup:**
    * Route osu! music to `Device A` (e.g., Motherboard Line-out).
    * Route KeyASIO hitsounds to `Device B` (e.g., External Soundcard).
    * Combine A and B into your mixer, then to your headphones.
3.  **In KeyASIO:**
    * Select your ASIO device in the GUI.
    * Set **Realtime Mode** to `TRUE`.
    * Set **FullMode (EnableMusicFunctions)** to `FALSE`.
4.  **In osu!:**
    * Set `Effect Volume` to 0.
    * Adjust your audio offset (likely below -40ms) to sync the audio.

</details>
<details>
<summary><h3>💻 Option B: Software Mix (FullMode ENABLED)</h3></summary>

**Target:** Users with a single soundcard (Laptops/Desktops without mixer).

1.  **Prerequisites:**
    * Install [VB-CABLE](https://vb-audio.com/Cable/).
2.  **Setup:**
    * Set osu! output device to `VB-Cable Input`.
    * (Optional) If you stream, capture `VB-Cable Output` in OBS.
3.  **In KeyASIO:**
    * Select your actual output device (Headphones/Speakers) in the GUI with **WASAPI Exclusive ON**.
    * Set **FullMode (EnableMusicFunctions)** to `TRUE`.
    * *Note: This mode completely replaces osu!'s audio engine.*

</details>

---
**Full options in `appsettings.yaml`:** (Modify after program closed) 
| Item                                    | Description                                                                                                                                                                  |
| --------------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Debugging                               | Show debug logs.                                                                                                                                                             |
| Device                                  | Device configuration (Recommend to configure in GUI).                                                                                                                        |
| HitsoundPath                            | Default hitsound path (relative or absolute) for playing.                                                                                                                    |
| Keys                                    | Triggering keys. See https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.keys?view=windowsdesktop-6.0 for more inforamtion.                                     |
| RealtimeMode                            | If true, the software will enable memory scanning and play the right hitsounds of beatmaps.                                                                                  |
| RealtimeMode.AudioOffset                 | The offset when `RealtimeMode` is true (allow adjusting in GUI).                                                                                                             |
| RealtimeMode.BalanceFactor              | Balance factor.                                                                                                                                                              |
| RealtimeMode.IgnoreBeatmapHitsound      | Ignore beatmap's hitsound and force using user skin instead.                                                                                                                 |
| RealtimeMode.IgnoreComboBreak           | Ignore combo break sound.                                                                                                                                                    |
| RealtimeMode.IgnoreSliderTicksAndSlides | Ignore slider's ticks and slides.                                                                                                                                            |
| RealtimeMode.IgnoreStoryboardSamples    | Ignore beatmap's storyboard samples.                                                                                                                                         |
| RealtimeMode.SliderTailPlaybackBehavior | Slider tail's playback behavior. Normal: Force to play slider tail's sounds; KeepReverse: Play only if a slider with multiple reverses; Ignore: Ignore slider tail's sounds. |
| SampleRate                              | Device's sample rate (allow adjusting in GUI).                                                                                                                               |
| SkinFolder                              | The skin folder when `RealtimeMode` is true.                                                                                                                                 |
| VolumeEnabled                           | Software volume control. Disable for extremely low latency when `RealtimeMode` is false                                                                                      |
| Volume                                  | Configured device volume.                                                                                                                                                    |
## ❓ FAQ

<details>
<summary><b>Is this safe for my osu! account?</b></summary>
KeyASIO uses a passive memory reader (similar to StreamCompanion or Gosumemory) which is generally considered safe and approved by peppy for other tools. However, <b>use at your own risk</b>.
</details>

<details>
<summary><b>Why do I hear no sound in Auto mod?</b></summary>
If you are using Exclusive Mode, osu!'s internal clock might desync because it loses control of the audio device. This is normal behavior when bypassing the Windows Mixer. So you need to select another audio device in osu!
</details>

## 🚧 Roadmap (v4 Preview)

We are currently working on a complete rewrite (**KeyASIO v4**).

---

<p align="center">
  Made with ❤️ for the osu! community.
</p>
