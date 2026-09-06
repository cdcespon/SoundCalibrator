# Sound Calibrator — Video Editing Instructions & EDL

## Technical Specifications
- **Video Container:** MP4 (`avc1` / H.264 High Profile Level 5.0)
- **Resolution:** 2152 x 1318 (Native screen capture aspect ratio ~16:10)
- **Frame Rate:** 30.000 FPS (Progressive)
- **Audio Codec:** AAC-LC, 320 kbps, 48 kHz, Stereo
- **Total Duration:** 00:00:44.43 (1332 frames)

---

## Edit Decision List (EDL Timeline)

```text
TIMELINE (Total: 44.43 seconds)
========================================================================================
00:00.00 - 00:00.50  |  INTRO LEAD-IN  |  Ambient Music Swell (-14 dB)
00:00.50 - 00:06.60  |  SCENE 1        |  VO: "Sound Calibrator: real-time dual-channel..."
00:06.60 - 00:07.80  |  TRANSITION     |  Voice pause, cursor moves to RTA tab
00:07.80 - 00:14.30  |  SCENE 2        |  VO: "Inspect live spectra with fractional..."
00:14.30 - 00:14.80  |  TRANSITION     |  Voice pause, cursor clicks IMPULSE / ETC
00:14.80 - 00:17.60  |  SCENE 3        |  VO: "Impulse response synthesis with instant..."
00:17.60 - 00:18.20  |  TRANSITION     |  Voice pause, cursor clicks SPECTROGRAM
00:18.20 - 00:24.00  |  SCENE 4        |  VO: "High-speed GPU waterfall spectrogram..."
00:24.00 - 00:25.00  |  TRANSITION     |  Voice pause, cursor moves to theme toggle button
00:25.00 - 00:31.70  |  SCENE 5        |  VO: "Engineered for studio and outdoor..."
00:31.70 - 00:36.00  |  UI INTERACTION |  Voice pause, demonstrating panel folding & themes
00:36.00 - 00:42.50  |  SCENE 6        |  VO: "Automated parametric EQ, delay matrices..."
00:42.50 - 00:44.43  |  OUTRO          |  Music swell to -12 dB, smooth 1.5s fade out to black
========================================================================================
```

---

## Audio Track Layout
1. **Audio Track 1 (Center / Mono):** AI Voiceover (`en-US-ChristopherNeural`, +7% rate, RMS normalized).
2. **Audio Track 2 (Stereo L/R):** Ambient Cyber/Tech Soundtrack (Warm analog pads, syncopated delay arpeggios, sub-bass 55-73 Hz).
3. **Master Processing:** Sidechain compressor/ducker ducking music by 8 dB during speech. Soft peak limiter at -0.5 dBFS.
