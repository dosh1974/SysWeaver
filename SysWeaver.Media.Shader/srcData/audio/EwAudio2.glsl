// =============================================================================
//  EwAudio2 - The rows of the audio2 texture (512 x 64, the media "audio2")
// =============================================================================
//  An audio2 effect (//  media: audio2) gets the sound as a 512 x 64 texture on
//  iChannel0 / tex: r = mono, g = left, b = right. Read it at uv = (u, v):
//  v = the row (the constants below, pixel centers), u = the band / segment.
//
//  Rows 0 .. 31 are the values of the moment, rows 32 .. 63 the same values
//  smoothed (add EwAudioDiffuseAddV to a v).
//
//  Spectrum (log frequency 20 Hz .. 20 kHz, -100 .. -30 dB -> 0 .. 1):
//    EwAudioFrequency512v .. EwAudioFrequency1v: 512, 256, ... 1 bands
//  Waveform (the last 512 samples, 0.5 = silence; a segment = the sample with
//  the largest amplitude, sign kept):
//    EwAudioWave512v .. EwAudioWave1v: 512, 256, ... 1 segments
//  Values (u, v): bass / mid / kick / snare / hi-hat energies, kick / snare /
//  hi-hat pulses, level (RMS), peak, brightness, onset, tempo (BPM / 255,
//  * EwAudioBpmScale = BPM), tempo confidence, beat phase (0 on a beat .. 1),
//  beat pulse (1 on a beat, decays); EwAudioHiHatRowV: hi-hat energy, all u.
//
//  EwAudioGetBpm(s), EwAudioGetBassEnergy(s), EwAudioGetBeatPhase(s), ... read
//  a value (s = iChannel0 / tex; an overload with bool smoothed);
//  EwAudioGetFrequency(s, x, level) / EwAudioGetWave(s, x, level) a row.
//  EwAudio2(row): the v of a row. EwAudioGet<N>u(i): the u of band /
//  segment i of a row of N; EwAudioGet<N>u(x): the band at x in 0..1.
//    float bass = texture(iChannel0, vec2(EwAudioBassU, EwAudioBassV)).r;
//    float level = texture(iChannel0, vec2(EwAudioGet64u(uv.x), EwAudioFrequency64v)).r;
//    float calm = texture(iChannel0, vec2(EwAudioGet64u(uv.x), EwAudioFrequency64v + EwAudioDiffuseAddV)).r;
//
//  Requires: -
//  Docs    : EwAudio2.md
// =============================================================================

// Spectrum rows (log frequency): 512 .. 1 bands
const float EwAudioFrequency512v = 0.5 / 64.0;
const float EwAudioFrequency256v = 1.5 / 64.0;
const float EwAudioFrequency128v = 2.5 / 64.0;
const float EwAudioFrequency64v = 3.5 / 64.0;
const float EwAudioFrequency32v = 4.5 / 64.0;
const float EwAudioFrequency16v = 5.5 / 64.0;
const float EwAudioFrequency8v = 6.5 / 64.0;
const float EwAudioFrequency4v = 7.5 / 64.0;
const float EwAudioFrequency2v = 8.5 / 64.0;
const float EwAudioFrequency1v = 9.5 / 64.0;

// Row 10: bass energy | tempo
const float EwAudioBassU = 0.25;
const float EwAudioBassV = 10.5 / 64.0;
const float EwAudioBpmU = 0.75;
const float EwAudioBpmV = 10.5 / 64.0;
const float EwAudioBpmScale = 255.0;

// Row 11: beat phase | beat pulse
const float EwAudioBeatPhaseU = 0.25;
const float EwAudioBeatPhaseV = 11.5 / 64.0;
const float EwAudioBeatPulseU = 0.75;
const float EwAudioBeatPulseV = 11.5 / 64.0;

// Row 12: level (RMS) | peak | brightness | onset
const float EwAudioLevelU = 0.125;
const float EwAudioLevelV = 12.5 / 64.0;
const float EwAudioPeakU = 0.375;
const float EwAudioPeakV = 12.5 / 64.0;
const float EwAudioBrightnessU = 0.625;
const float EwAudioBrightnessV = 12.5 / 64.0;
const float EwAudioOnsetU = 0.875;
const float EwAudioOnsetV = 12.5 / 64.0;

// Row 13: kick pulse | snare pulse | hi-hat pulse | mid energy
const float EwAudioKickPulseU = 0.125;
const float EwAudioKickPulseV = 13.5 / 64.0;
const float EwAudioSnarePulseU = 0.375;
const float EwAudioSnarePulseV = 13.5 / 64.0;
const float EwAudioHiHatPulseU = 0.625;
const float EwAudioHiHatPulseV = 13.5 / 64.0;
const float EwAudioMidU = 0.875;
const float EwAudioMidV = 13.5 / 64.0;

// Row 14: kick energy | snare energy | hi-hat energy | tempo confidence
const float EwAudioKickU = 0.125;
const float EwAudioKickV = 14.5 / 64.0;
const float EwAudioSnareU = 0.375;
const float EwAudioSnareV = 14.5 / 64.0;
const float EwAudioHiHatU = 0.625;
const float EwAudioHiHatV = 14.5 / 64.0;
const float EwAudioTempoConfidenceU = 0.875;
const float EwAudioTempoConfidenceV = 14.5 / 64.0;

// Waveform rows: 512 .. 1 segments
const float EwAudioWave512v = 16.5 / 64.0;
const float EwAudioWave256v = 17.5 / 64.0;
const float EwAudioWave128v = 18.5 / 64.0;
const float EwAudioWave64v = 19.5 / 64.0;
const float EwAudioWave32v = 20.5 / 64.0;
const float EwAudioWave16v = 21.5 / 64.0;
const float EwAudioWave8v = 22.5 / 64.0;
const float EwAudioWave4v = 23.5 / 64.0;
const float EwAudioWave2v = 24.5 / 64.0;
const float EwAudioWave1v = 25.5 / 64.0;

// Row 26: hi-hat energy (all u)
const float EwAudioHiHatRowV = 26.5 / 64.0;

// Add to a v: the smoothed value (rows 32 .. 63)
const float EwAudioDiffuseAddV = 0.5;

// The v of a row 0 .. 63
float EwAudio2(int row)
{
    return (float(row) + 0.5) * (1.0 / 64.0);
}

// The u of band / segment i of a row of N (0 .. N - 1)
float EwAudioGet512u(int i) { return (float(i) + 0.5) * (1.0 / 512.0); }
float EwAudioGet256u(int i) { return (float(i) + 0.5) * (1.0 / 256.0); }
float EwAudioGet128u(int i) { return (float(i) + 0.5) * (1.0 / 128.0); }
float EwAudioGet64u(int i) { return (float(i) + 0.5) * (1.0 / 64.0); }
float EwAudioGet32u(int i) { return (float(i) + 0.5) * (1.0 / 32.0); }
float EwAudioGet16u(int i) { return (float(i) + 0.5) * (1.0 / 16.0); }
float EwAudioGet8u(int i) { return (float(i) + 0.5) * (1.0 / 8.0); }
float EwAudioGet4u(int i) { return (float(i) + 0.5) * (1.0 / 4.0); }
float EwAudioGet2u(int i) { return (float(i) + 0.5) * (1.0 / 2.0); }
float EwAudioGet1u(int i) { return 0.5; }

// The u of the band / segment at x (0 .. 1) of a row of N: its center (no blend with the neighbours)
float EwAudioGet512u(float x) { return (floor(clamp(x, 0.0, 1.0) * 511.999) + 0.5) * (1.0 / 512.0); }
float EwAudioGet256u(float x) { return (floor(clamp(x, 0.0, 1.0) * 255.999) + 0.5) * (1.0 / 256.0); }
float EwAudioGet128u(float x) { return (floor(clamp(x, 0.0, 1.0) * 127.999) + 0.5) * (1.0 / 128.0); }
float EwAudioGet64u(float x) { return (floor(clamp(x, 0.0, 1.0) * 63.999) + 0.5) * (1.0 / 64.0); }
float EwAudioGet32u(float x) { return (floor(clamp(x, 0.0, 1.0) * 31.999) + 0.5) * (1.0 / 32.0); }
float EwAudioGet16u(float x) { return (floor(clamp(x, 0.0, 1.0) * 15.999) + 0.5) * (1.0 / 16.0); }
float EwAudioGet8u(float x) { return (floor(clamp(x, 0.0, 1.0) * 7.999) + 0.5) * (1.0 / 8.0); }
float EwAudioGet4u(float x) { return (floor(clamp(x, 0.0, 1.0) * 3.999) + 0.5) * (1.0 / 4.0); }
float EwAudioGet2u(float x) { return (floor(clamp(x, 0.0, 1.0) * 1.999) + 0.5) * (1.0 / 2.0); }
float EwAudioGet1u(float x) { return 0.5; }

// -----------------------------------------------------------------------------
//  The values (the mono mix) of an audio2 texture s (iChannel0 / tex);
//  smoothed = true: the smoothed value (+ EwAudioDiffuseAddV)
// -----------------------------------------------------------------------------
float EwAudioGetValue(sampler2D s, float u, float v, bool smoothed) { return texture(s, vec2(u, v + (smoothed ? EwAudioDiffuseAddV : 0.0))).r; }

// The tempo in BPM (0 = none yet) and how sure it is (0 .. 1)
float EwAudioGetBpm(sampler2D s) { return EwAudioGetValue(s, EwAudioBpmU, EwAudioBpmV, false) * EwAudioBpmScale; }
float EwAudioGetTempoConfidence(sampler2D s) { return EwAudioGetValue(s, EwAudioTempoConfidenceU, EwAudioTempoConfidenceV, false); }
// The beat: phase 0 (on a beat) .. 1, pulse 1 on a beat, decaying
float EwAudioGetBeatPhase(sampler2D s) { return EwAudioGetValue(s, EwAudioBeatPhaseU, EwAudioBeatPhaseV, false); }
float EwAudioGetBeatPulse(sampler2D s) { return EwAudioGetValue(s, EwAudioBeatPulseU, EwAudioBeatPulseV, false); }
float EwAudioGetBeatPulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioBeatPulseU, EwAudioBeatPulseV, smoothed); }

// Band energies 0 .. 1 (the spectrum's dB mapping)
float EwAudioGetBassEnergy(sampler2D s) { return EwAudioGetValue(s, EwAudioBassU, EwAudioBassV, false); }
float EwAudioGetBassEnergy(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioBassU, EwAudioBassV, smoothed); }
float EwAudioGetMidEnergy(sampler2D s) { return EwAudioGetValue(s, EwAudioMidU, EwAudioMidV, false); }
float EwAudioGetMidEnergy(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioMidU, EwAudioMidV, smoothed); }
float EwAudioGetKickEnergy(sampler2D s) { return EwAudioGetValue(s, EwAudioKickU, EwAudioKickV, false); }
float EwAudioGetKickEnergy(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioKickU, EwAudioKickV, smoothed); }
float EwAudioGetSnareEnergy(sampler2D s) { return EwAudioGetValue(s, EwAudioSnareU, EwAudioSnareV, false); }
float EwAudioGetSnareEnergy(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioSnareU, EwAudioSnareV, smoothed); }
float EwAudioGetHiHatEnergy(sampler2D s) { return EwAudioGetValue(s, EwAudioHiHatU, EwAudioHiHatV, false); }
float EwAudioGetHiHatEnergy(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioHiHatU, EwAudioHiHatV, smoothed); }

// Hits: jump on a kick / snare / hi-hat, decaying
float EwAudioGetKickPulse(sampler2D s) { return EwAudioGetValue(s, EwAudioKickPulseU, EwAudioKickPulseV, false); }
float EwAudioGetKickPulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioKickPulseU, EwAudioKickPulseV, smoothed); }
float EwAudioGetSnarePulse(sampler2D s) { return EwAudioGetValue(s, EwAudioSnarePulseU, EwAudioSnarePulseV, false); }
float EwAudioGetSnarePulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioSnarePulseU, EwAudioSnarePulseV, smoothed); }
float EwAudioGetHiHatPulse(sampler2D s) { return EwAudioGetValue(s, EwAudioHiHatPulseU, EwAudioHiHatPulseV, false); }
float EwAudioGetHiHatPulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioHiHatPulseU, EwAudioHiHatPulseV, smoothed); }

// Level (RMS, -60 .. 0 dB as 0 .. 1), peak amplitude, brightness (spectral centroid 0 .. 1), onset (how much the spectrum rises now)
float EwAudioGetLevel(sampler2D s) { return EwAudioGetValue(s, EwAudioLevelU, EwAudioLevelV, false); }
float EwAudioGetLevel(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioLevelU, EwAudioLevelV, smoothed); }
float EwAudioGetPeak(sampler2D s) { return EwAudioGetValue(s, EwAudioPeakU, EwAudioPeakV, false); }
float EwAudioGetPeak(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioPeakU, EwAudioPeakV, smoothed); }
float EwAudioGetBrightness(sampler2D s) { return EwAudioGetValue(s, EwAudioBrightnessU, EwAudioBrightnessV, false); }
float EwAudioGetBrightness(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioBrightnessU, EwAudioBrightnessV, smoothed); }
float EwAudioGetOnset(sampler2D s) { return EwAudioGetValue(s, EwAudioOnsetU, EwAudioOnsetV, false); }
float EwAudioGetOnset(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioOnsetU, EwAudioOnsetV, smoothed); }

// The spectrum (0 .. 1) at x (0 .. 1: low .. high, log frequency) of a level: 0 = 512 bands, 1 = 256 ... 9 = 1 band (the band's center)
float EwAudioGetFrequency(sampler2D s, float x, int level, bool smoothed)
{
    float n = exp2(9.0 - float(level));
    float u = (floor(clamp(x, 0.0, 1.0) * (n - 0.001)) + 0.5) / n;
    return EwAudioGetValue(s, u, EwAudio2(level), smoothed);
}
float EwAudioGetFrequency(sampler2D s, float x, int level) { return EwAudioGetFrequency(s, x, level, false); }

// The waveform (-1 .. 1) at x (0 .. 1: the last 512 samples, oldest .. newest) of a level: 0 = 512 samples, 1 = 256 ... 9 = 1 segment
float EwAudioGetWave(sampler2D s, float x, int level, bool smoothed)
{
    float n = exp2(9.0 - float(level));
    float u = (floor(clamp(x, 0.0, 1.0) * (n - 0.001)) + 0.5) / n;
    return EwAudioGetValue(s, u, EwAudio2(16 + level), smoothed) * 2.0 - 1.0;
}
float EwAudioGetWave(sampler2D s, float x, int level) { return EwAudioGetWave(s, x, level, false); }
