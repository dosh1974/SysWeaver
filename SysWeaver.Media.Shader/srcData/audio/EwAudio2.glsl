// =============================================================================
//  EwAudio2 - The rows of the audio texture (512 x 64, the media "audio")
// =============================================================================
//  An audio effect (//  media: audio; was "audio2") gets the sound as a
//  512 x 64 float texture on iChannel0 / tex: r = the mix (mono), g = left,
//  b = right; the rows marked "mix" hold the mix's value in r, g and b.
//  Read it at uv = (u, v): v = the row (the constants below, pixel centers),
//  u = the band / segment / slot. The EwAudioGet... functions read r (the
//  mix); for left / right read .g / .b with texture() and the same constants.
//  (ShaderToy's 512 x 2 texture is the kind "legacyaudio".)
//
//  Timing: the texture is computed at a fixed 60 analysis steps per second of
//  SOUND time (not per frame): the same values at any frame rate, when seeking
//  and in the video export. "A step" below = 1/60 s.
//
//  Rows 0 .. 31 are the values of the moment, rows 32 .. 63 the same values
//  smoothed (add EwAudioDiffuseAddV to a v): a rise is followed by half per
//  step (attack 0.5), a fall by a tenth (release 0.1): punchy but calm.
//  Phases, positions, the tempo, the hue, "since", travel and the history are
//  not smoothed (their smoothed v holds the same value).
//
//  Spectrum (log frequency 20 Hz .. 20 kHz, per 1/3 octave, tilted so typical
//  music reads flat, (dB + 74) / 58 -> 0 .. 1):
//    EwAudioFrequency512v .. EwAudioFrequency1v: 512, 256, ... 1 bands
//  Waveform (the last 512 samples, 0.5 = silence):
//    EwAudioWave512v .. EwAudioWave1v: 512, 256, ... 1 segments
//  Values: energies, hits (pulses, since, strength), loudness, tempo, beat,
//  bar, sections (intensity, kick presence, build-up, drop), auto gain,
//  chroma / hue, travel, history. Each is described at its constants below.
//
//  EwAudioGetBpm(s), EwAudioGetKickPulse(s), EwAudioGetBarPhase(s), ... read
//  a value (s = iChannel0 / tex; many with an overload (s, bool smoothed));
//  EwAudioGetFrequency(s, x, level) / EwAudioGetWave(s, x, level) a row.
//  EwAudio2(row): the v of a row. EwAudioGet<N>u(i): the u of band /
//  segment i of a row of N; EwAudioGet<N>u(x): the band at x in 0..1.
//    float kick = EwAudioGetKickPulse(iChannel0);
//    float level = texture(iChannel0, vec2(EwAudioGet64u(uv.x), EwAudioFrequency64v)).r;
//    float calm = texture(iChannel0, vec2(EwAudioGet64u(uv.x), EwAudioFrequency64v + EwAudioDiffuseAddV)).r;
//
//  Requires: -
//  Docs    : EwAudio2.md
// =============================================================================

// -----------------------------------------------------------------------------
//  Spectrum rows 0 .. 9: row r has 512 >> r bands (a band spans 2^r pixels).
//  The x axis (u) is LOG frequency: x = 0 .. 1 is 20 Hz .. 20 kHz (capped at
//  half the sample rate), f(x) = 20 * 1000^x Hz, x(f) = log(f / 20) / log(1000):
//  50 Hz = 0.13, 100 Hz = 0.23, 250 Hz = 0.37, 1 kHz = 0.57, 4 kHz = 0.77,
//  10 kHz = 0.90. Each octave gets the same width (~0.1).
//  The value is the band's power (the sum of its FFT bins) per 1/3 octave,
//  +1.5 dB per octave around 1 kHz (pink noise and typical music read flat),
//  (dB + 74) / 58 clamped: loud masters peak around 0.85 .. 0.95, nothing
//  sticks at 1. A coarser row averages the power of 2 bands of the row above.
//  An 8192 sample FFT below 300 Hz (6 Hz bins: separate bass notes) is
//  crossfaded into a 2048 sample one by 600 Hz.
// -----------------------------------------------------------------------------
const float EwAudioFrequency512v = 0.5 / 64.0;      // row 0: 512 bands, ~0.02 octave each
const float EwAudioFrequency256v = 1.5 / 64.0;      // row 1: 256 bands
const float EwAudioFrequency128v = 2.5 / 64.0;      // row 2: 128 bands
const float EwAudioFrequency64v = 3.5 / 64.0;       // row 3: 64 bands, ~1/6 octave each
const float EwAudioFrequency32v = 4.5 / 64.0;       // row 4: 32 bands, ~1/3 octave each (a classic 31 band EQ)
const float EwAudioFrequency16v = 5.5 / 64.0;       // row 5: 16 bands
const float EwAudioFrequency8v = 6.5 / 64.0;        // row 6: 8 bands, ~1.25 octaves each
const float EwAudioFrequency4v = 7.5 / 64.0;        // row 7: 4 bands: 20-112 Hz, 112-632 Hz, 632-3557 Hz, 3.6-20 kHz
const float EwAudioFrequency2v = 8.5 / 64.0;        // row 8: 2 bands: below / above 632 Hz
const float EwAudioFrequency1v = 9.5 / 64.0;        // row 9: 1 band: the power of the whole spectrum

// -----------------------------------------------------------------------------
//  Row 10: bass energy (u < 0.5) | tempo (u >= 0.5, mix)
// -----------------------------------------------------------------------------
// Bass energy, 0 .. 1: the power of 30 .. 250 Hz, mapped like the spectrum. A
// level, not a hit detector: it stays up during a held bass note. For hits use
// the kick pulse; the smoothed bass is a good "pump" for sizes / brightness.
const float EwAudioBassU = 0.25;
const float EwAudioBassV = 10.5 / 64.0;
// Tempo: BPM / 255 (* EwAudioBpmScale = BPM). 0 = no tempo yet (silence, the
// first seconds). Range 60 .. 200 BPM, re-estimated 4 times a second from the
// onsets of the last 6 seconds (kick weighted), held unless another tempo wins
// clearly (a half / double tempo needs a 40 % margin). A 70 or 240 BPM song
// may still read as its double / half. Check the tempo confidence.
const float EwAudioBpmU = 0.75;
const float EwAudioBpmV = 10.5 / 64.0;
const float EwAudioBpmScale = 255.0;               // tempo value * this = BPM

// -----------------------------------------------------------------------------
//  Row 11 (mix): beat phase (u < 0.5) | beat pulse (u >= 0.5)
//  The beat grid: the onsets of the last 6 s combed with the tempo's period,
//  followed by a phase locked loop, so it runs smoothly and keeps going through
//  a quiet beat (it predicts; the kick pulse reacts). Without a tempo (BPM 0)
//  it is a guess: fade beat effects with the tempo confidence.
// -----------------------------------------------------------------------------
// Beat phase, 0 .. 1: a sawtooth, 0 on a beat, rising linearly to 1 just
// before the next. Animate WITH the music:
//   1.0 - phase          the fraction of the beat left (anticipation)
//   fract(phase * 2.0)   eighth notes, fract(phase * 4.0) sixteenths
//   sin(phase * 6.2832)  a swing per beat; exp(-phase * 8.0) a decay per beat
// A display that shows the picture late (LED walls, projectors: 30 .. 60 ms):
// EwAudioGetBeatPhase(s, latency) / EwAudioGetBeatPulse(s, latency).
const float EwAudioBeatPhaseU = 0.25;
const float EwAudioBeatPhaseV = 11.5 / 64.0;
// Beat pulse, 0 .. 1: 1 on a beat of the grid, then * 0.85 per step (half
// after ~4 steps = 70 ms, 0.1 after ~14 steps = 0.24 s).
const float EwAudioBeatPulseU = 0.75;
const float EwAudioBeatPulseV = 11.5 / 64.0;

// -----------------------------------------------------------------------------
//  Row 12: loudness | peak | brightness | onset (mix) (quarters)
// -----------------------------------------------------------------------------
// Loudness ("level"), 0 .. 1: the momentary loudness (400 ms, K-weighted like
// LUFS meters; r = left + right), (LUFS + 45) / 40: mastered music sits around
// 0.75 .. 0.85, -23 LUFS (broadcast) is 0.55.
const float EwAudioLevelU = 0.125;
const float EwAudioLevelV = 12.5 / 64.0;
// Peak, 0 .. 1: the largest |sample| of the last 2048 samples, LINEAR: 1 = full scale.
const float EwAudioPeakU = 0.375;
const float EwAudioPeakV = 12.5 / 64.0;
// Brightness, 0 .. 1: the spectral centroid on the spectrum's log axis (of the
// tilted bands: balanced music ~0.5), frequency = 20 * 1000^brightness Hz.
// Low: dark / bass heavy; high: cymbals, noise, bright. Good for color.
const float EwAudioBrightnessU = 0.625;
const float EwAudioBrightnessV = 12.5 / 64.0;
// Onset, 0 .. 1: how much the sound rises now (kick / snare / hi-hat rises
// and the spectral flux): high on hits and new notes, ~0 on held sounds.
const float EwAudioOnsetU = 0.875;
const float EwAudioOnsetV = 12.5 / 64.0;

// -----------------------------------------------------------------------------
//  Row 13: kick pulse | snare pulse | hi-hat pulse | mid energy (quarters)
//  A hit is found in the time domain: the band's 10 .. 20 ms envelope rising
//  3 dB (kick 45 .. 110 Hz), 2 dB (snare: 2 .. 5 kHz crack with its 150 ..
//  300 Hz body) or 1.5 dB (hi-hat above 8 kHz) and standing out of the last
//  0.5 s; one hit per 100 ms (50 ms hi-hat). The pulse jumps to the hit's
//  strength, then * 0.85 per step (half after 70 ms, 0.1 after 0.24 s).
// -----------------------------------------------------------------------------
const float EwAudioKickPulseU = 0.125;               // kick: jumps on a bass drum hit
const float EwAudioKickPulseV = 13.5 / 64.0;
const float EwAudioSnarePulseU = 0.375;              // snare: jumps on a snare / clap
const float EwAudioSnarePulseV = 13.5 / 64.0;
const float EwAudioHiHatPulseU = 0.625;              // hi-hat: jumps on a hi-hat / cymbal
const float EwAudioHiHatPulseV = 13.5 / 64.0;
// Mid energy, 0 .. 1: the power of 250 Hz .. 2 kHz: vocals, guitars, keys.
const float EwAudioMidU = 0.875;
const float EwAudioMidV = 13.5 / 64.0;

// -----------------------------------------------------------------------------
//  Row 14: kick energy | snare energy | hi-hat energy | tempo confidence (mix)
//  The energies (levels, not hits): kick 45 .. 110 Hz, snare 1.5 .. 6 kHz,
//  hi-hat 8 .. 16 kHz, mapped like the spectrum.
// -----------------------------------------------------------------------------
const float EwAudioKickU = 0.125;
const float EwAudioKickV = 14.5 / 64.0;
const float EwAudioSnareU = 0.375;
const float EwAudioSnareV = 14.5 / 64.0;
const float EwAudioHiHatU = 0.625;
const float EwAudioHiHatV = 14.5 / 64.0;
// Tempo confidence, 0 .. 1: how strongly the onsets repeat at the tempo's
// period. Below ~0.15: no clear beat, above ~0.35: solid. Fade beat effects in
// with e.g. smoothstep(0.15, 0.35, confidence).
const float EwAudioTempoConfidenceU = 0.875;
const float EwAudioTempoConfidenceV = 14.5 / 64.0;

// -----------------------------------------------------------------------------
//  Row 15 (mix, eighths): the song
// -----------------------------------------------------------------------------
const float EwAudioSongV = 15.5 / 64.0;
// Short-term loudness (3 s, K-weighted, (LUFS + 45) / 40): calm level changes
const float EwAudioShortTermLoudnessU = 0.0625;
// Stereo width above 200 Hz: side / (mid + side) power: 0 mono, 0.5 wide, 1 out of phase
const float EwAudioWidthU = 0.1875;
// Sub energy (25 .. 55 Hz): the rumble below the kick
const float EwAudioSubU = 0.3125;
// Intensity 0 .. 1: how much is going on (the loudness within the last 16 s,
// the kick presence, the hit rate), slow (attack 0.5 s, release 3 s): a
// "hype" meter for particle counts / post effect amounts
const float EwAudioIntensityU = 0.4375;
// Kick presence 0 .. 1: is the kick in (1) or away (0: a breakdown, an intro):
// the kick band against the loudness, level independent
const float EwAudioKickPresenceU = 0.5625;
// Build-up 0 .. 1: rising snare / hi-hat rolls and risers over 8 s while the
// kick is away; 0 again at the drop
const float EwAudioBuildUpU = 0.6875;
// Drop: 1 on the first strong kick after a breakdown (6 of 8 s without the
// kick), falling linearly to 0 over one bar. The big moment: switch scenes.
const float EwAudioDropU = 0.8125;
// Loudness range position 0 .. 1: the short-term loudness within its range of
// the last 16 s (quiet part = 0, loud part = 1)
const float EwAudioLoudnessRangeU = 0.9375;

// -----------------------------------------------------------------------------
//  Waveform rows 16 .. 25: the last 512 samples (~11 ms) of the step, oldest
//  (u = 0) .. newest (u = 1). Row 16 + l has 512 >> l segments of 2^l samples,
//  each the sample with the largest amplitude (its sign kept). Stored as
//  0.5 + 0.5 * sample; EwAudioGetWave returns -1 .. 1. Free running (not
//  triggered).
// -----------------------------------------------------------------------------
const float EwAudioWave512v = 16.5 / 64.0;          // row 16: every sample
const float EwAudioWave256v = 17.5 / 64.0;          // row 17: 256 segments of 2 samples
const float EwAudioWave128v = 18.5 / 64.0;          // row 18: 128 segments of 4 samples
const float EwAudioWave64v = 19.5 / 64.0;           // row 19: 64 segments of 8 samples
const float EwAudioWave32v = 20.5 / 64.0;           // row 20: 32 segments
const float EwAudioWave16v = 21.5 / 64.0;           // row 21: 16 segments
const float EwAudioWave8v = 22.5 / 64.0;            // row 22: 8 segments
const float EwAudioWave4v = 23.5 / 64.0;            // row 23: 4 segments
const float EwAudioWave2v = 24.5 / 64.0;            // row 24: 2 segments
const float EwAudioWave1v = 25.5 / 64.0;            // row 25: 1 segment: the signed peak sample of the 512

// -----------------------------------------------------------------------------
//  Row 26: the history of the last 512 steps (8.5 s), oldest (u = 0) ..
//  newest (u = 1): r = low (the kick band), g = mid (the snare crack band),
//  b = high (the hi-hat band) envelopes, (dB + 60) / 50. For waterfalls,
//  trails, scrolling ribbons (the texture moves 1/512 per step).
// -----------------------------------------------------------------------------
const float EwAudioHistoryV = 26.5 / 64.0;

// -----------------------------------------------------------------------------
//  Row 27 (mix, quarters): relative (auto gain) bass | mid | high (4 .. 16
//  kHz) | loudness: the value between its 10th and 95th percentile of the last
//  8 s (a span of at least 12 dB, 6 dB for the loudness). Uses the full 0 .. 1
//  on any track and level; a quiet breakdown stays low (the span).
// -----------------------------------------------------------------------------
const float EwAudioRelativeV = 27.5 / 64.0;

// -----------------------------------------------------------------------------
//  Row 28 (mix, quarters): bar phase | downbeat pulse | beat in the bar / 4 |
//  kick pulse on the beat grid
//  The bar (4 beats): the downbeat is where the kick, a crash and a loudness
//  rise land and the snare isn't (it takes beats 2 and 4). It keeps counting
//  1 2 3 4 and moves only when another position wins clearly. Fade bar effects
//  with the bar confidence (row 30): without a clear pattern it is a guess.
// -----------------------------------------------------------------------------
const float EwAudioBarV = 28.5 / 64.0;
const float EwAudioBarPhaseU = 0.125;                // 0 on a downbeat .. 1: (beat in the bar + beat phase) / 4
const float EwAudioDownbeatPulseU = 0.375;           // 1 on a downbeat, * 0.85 per step
const float EwAudioBeatInBarU = 0.625;               // 0, 0.25, 0.5, 0.75: beat 1, 2, 3, 4 of the bar
const float EwAudioGridKickU = 0.875;                // the kick pulse only near a beat of the grid (stable flashes)

// -----------------------------------------------------------------------------
//  Row 29 (mix, sixteenths): chroma C .. B (slots 0 .. 11) | tonalness |
//  chroma hue | hue strength
//  Chroma: the tonal peaks of 220 Hz .. 4 kHz by pitch class (C = 0 ..
//  B = 11), max = 1, times the tonalness. Tonalness 0 (noise, drums) .. 1
//  (tones, pads). Hue: the circular mean of the chroma of the last 0.5 s,
//  0 .. 1 around the circle (C = 0): a color for the key / chord, not smoothed
//  (it wraps); hue strength: how clear it is (often 0.05 .. 0.3).
// -----------------------------------------------------------------------------
const float EwAudioChromaV = 29.5 / 64.0;
const float EwAudioTonalnessU = 12.5 / 16.0;
const float EwAudioChromaHueU = 13.5 / 16.0;
const float EwAudioChromaHueStrengthU = 14.5 / 16.0;

// -----------------------------------------------------------------------------
//  Row 30 (mix, eighths): since the last kick | its strength | since the last
//  snare | its strength | since the last hi-hat | its strength | bar
//  confidence | -
//  Since: seconds / 2 (1 = 2 s or more: none). With EwAudioGetHit(s, hit,
//  rate) any decay: strength * exp(-seconds * rate); or ramps (ring radius).
// -----------------------------------------------------------------------------
const float EwAudioHitsV = 30.5 / 64.0;
const float EwAudioBarConfidenceU = 0.8125;          // 0 .. 1: how clear the downbeat is (gate bar effects: smoothstep(0.3, 0.6, c))

// -----------------------------------------------------------------------------
//  Row 31 (mix, quarters): travel of bass | mid | high | loudness, in seconds
//  (0 .. ~1): Σ relative value * (K - age) / K / 60 over the last 2 s. It
//  changes by (value - its recent mean) / 60 per step, so time + travel runs
//  faster when the band is above its recent mean and never backwards: motion
//  (scrolling, rotation) that surges with the music (EwAudioGetTravelTime).
// -----------------------------------------------------------------------------
const float EwAudioTravelV = 31.5 / 64.0;

// The indexes of the bands (EwAudioGetRelative, EwAudioGetTravel, EwAudioGetHistory: low, mid, high) and hits (EwAudioGetSince, ...)
const int EwAudioBandBass = 0;
const int EwAudioBandMid = 1;
const int EwAudioBandHigh = 2;
const int EwAudioBandLoudness = 3;
const int EwAudioHitKick = 0;
const int EwAudioHitSnare = 1;
const int EwAudioHitHiHat = 2;

// Add to a v of rows 0 .. 31: the smoothed value (rows 32 .. 63)
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
//  The values (the mix) of an audio texture s (iChannel0 / tex);
//  smoothed = true: the smoothed value (+ EwAudioDiffuseAddV)
// -----------------------------------------------------------------------------
float EwAudioGetValue(sampler2D s, float u, float v, bool smoothed) { return texture(s, vec2(u, v + (smoothed ? EwAudioDiffuseAddV : 0.0))).r; }

// The tempo in BPM (0 = none yet, else 60 .. 200) and how sure it is (0 .. 1, solid above ~0.35)
float EwAudioGetBpm(sampler2D s) { return EwAudioGetValue(s, EwAudioBpmU, EwAudioBpmV, false) * EwAudioBpmScale; }
float EwAudioGetTempoConfidence(sampler2D s) { return EwAudioGetValue(s, EwAudioTempoConfidenceU, EwAudioTempoConfidenceV, false); }
float EwAudioGetTempoConfidence(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioTempoConfidenceU, EwAudioTempoConfidenceV, smoothed); }

// The beat grid: phase 0 (on a beat) .. 1 (never smoothed), pulse 1 on a beat, * 0.85 per step
float EwAudioGetBeatPhase(sampler2D s) { return EwAudioGetValue(s, EwAudioBeatPhaseU, EwAudioBeatPhaseV, false); }
float EwAudioGetBeatPulse(sampler2D s) { return EwAudioGetValue(s, EwAudioBeatPulseU, EwAudioBeatPulseV, false); }
float EwAudioGetBeatPulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioBeatPulseU, EwAudioBeatPulseV, smoothed); }
// The same latency seconds ahead (a display that shows the picture late: LED walls, projectors, ~0.03 .. 0.06 s)
float EwAudioGetBeatPhase(sampler2D s, float latency) { return fract(EwAudioGetBeatPhase(s) + latency * EwAudioGetBpm(s) / 60.0); }
float EwAudioGetBeatPulse(sampler2D s, float latency)
{
    float bpm = EwAudioGetBpm(s);
    return bpm < 1.0 ? EwAudioGetBeatPulse(s) : pow(0.85, EwAudioGetBeatPhase(s, latency) * 3600.0 / bpm);
}

// Band energies 0 .. 1 (levels, mapped like the spectrum): bass 30-250 Hz, mid 250-2000 Hz, kick 45-110 Hz, snare 1.5-6 kHz,
// hi-hat 8-16 kHz, sub 25-55 Hz
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
float EwAudioGetSubEnergy(sampler2D s) { return EwAudioGetValue(s, EwAudioSubU, EwAudioSongV, false); }
float EwAudioGetSubEnergy(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioSubU, EwAudioSongV, smoothed); }

// Hits 0 .. 1: jump on a kick / snare / hi-hat hit (its strength), then * 0.85 per step (half after 70 ms)
float EwAudioGetKickPulse(sampler2D s) { return EwAudioGetValue(s, EwAudioKickPulseU, EwAudioKickPulseV, false); }
float EwAudioGetKickPulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioKickPulseU, EwAudioKickPulseV, smoothed); }
float EwAudioGetSnarePulse(sampler2D s) { return EwAudioGetValue(s, EwAudioSnarePulseU, EwAudioSnarePulseV, false); }
float EwAudioGetSnarePulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioSnarePulseU, EwAudioSnarePulseV, smoothed); }
float EwAudioGetHiHatPulse(sampler2D s) { return EwAudioGetValue(s, EwAudioHiHatPulseU, EwAudioHiHatPulseV, false); }
float EwAudioGetHiHatPulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioHiHatPulseU, EwAudioHiHatPulseV, smoothed); }
// Seconds since the last hit (EwAudioHitKick / Snare / HiHat; 2.0 = 2 s or more: none), its strength, and a hit with any decay
float EwAudioGetSince(sampler2D s, int hit) { return EwAudioGetValue(s, (float(hit) * 2.0 + 0.5) / 8.0, EwAudioHitsV, false) * 2.0; }
float EwAudioGetHitStrength(sampler2D s, int hit) { return EwAudioGetValue(s, (float(hit) * 2.0 + 1.5) / 8.0, EwAudioHitsV, false); }
float EwAudioGetHit(sampler2D s, int hit, float rate) { return EwAudioGetHitStrength(s, hit) * exp(-EwAudioGetSince(s, hit) * rate); }

// Loudness (momentary 400 ms / short-term 3 s, K-weighted, (LUFS + 45) / 40), peak (largest |sample|, linear), brightness
// (spectral centroid on the log axis: 20 * 1000^b Hz), onset (how much the sound rises now)
float EwAudioGetLevel(sampler2D s) { return EwAudioGetValue(s, EwAudioLevelU, EwAudioLevelV, false); }
float EwAudioGetLevel(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioLevelU, EwAudioLevelV, smoothed); }
float EwAudioGetLoudness(sampler2D s, bool shortTerm) { return shortTerm ? EwAudioGetValue(s, EwAudioShortTermLoudnessU, EwAudioSongV, false) : EwAudioGetLevel(s); }
float EwAudioGetPeak(sampler2D s) { return EwAudioGetValue(s, EwAudioPeakU, EwAudioPeakV, false); }
float EwAudioGetPeak(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioPeakU, EwAudioPeakV, smoothed); }
float EwAudioGetBrightness(sampler2D s) { return EwAudioGetValue(s, EwAudioBrightnessU, EwAudioBrightnessV, false); }
float EwAudioGetBrightness(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioBrightnessU, EwAudioBrightnessV, smoothed); }
float EwAudioGetOnset(sampler2D s) { return EwAudioGetValue(s, EwAudioOnsetU, EwAudioOnsetV, false); }
float EwAudioGetOnset(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioOnsetU, EwAudioOnsetV, smoothed); }

// The song (row 15): width, intensity, kick presence, build-up, drop, loudness range position
float EwAudioGetWidth(sampler2D s) { return EwAudioGetValue(s, EwAudioWidthU, EwAudioSongV, false); }
float EwAudioGetWidth(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioWidthU, EwAudioSongV, smoothed); }
float EwAudioGetIntensity(sampler2D s) { return EwAudioGetValue(s, EwAudioIntensityU, EwAudioSongV, false); }
float EwAudioGetKickPresence(sampler2D s) { return EwAudioGetValue(s, EwAudioKickPresenceU, EwAudioSongV, false); }
float EwAudioGetKickPresence(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioKickPresenceU, EwAudioSongV, smoothed); }
float EwAudioGetBuildUp(sampler2D s) { return EwAudioGetValue(s, EwAudioBuildUpU, EwAudioSongV, false); }
float EwAudioGetBuildUp(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioBuildUpU, EwAudioSongV, smoothed); }
float EwAudioGetDrop(sampler2D s) { return EwAudioGetValue(s, EwAudioDropU, EwAudioSongV, false); }
float EwAudioGetLoudnessRange(sampler2D s) { return EwAudioGetValue(s, EwAudioLoudnessRangeU, EwAudioSongV, false); }

// The history (row 26) at x (0 oldest .. 1 newest, 8.5 s): low / mid / high band envelopes, or one band (0 .. 2)
vec3 EwAudioGetHistory(sampler2D s, float x) { return texture(s, vec2(EwAudioGet512u(x), EwAudioHistoryV)).rgb; }
float EwAudioGetHistory(sampler2D s, float x, int band)
{
    vec3 h = EwAudioGetHistory(s, x);
    return band == 0 ? h.r : band == 1 ? h.g : h.b;
}

// Relative (auto gain, row 27) bass / mid / high / loudness (EwAudioBand...)
float EwAudioGetRelative(sampler2D s, int band, bool smoothed) { return EwAudioGetValue(s, (float(band) + 0.5) / 4.0, EwAudioRelativeV, smoothed); }
float EwAudioGetRelative(sampler2D s, int band) { return EwAudioGetRelative(s, band, false); }

// The bar (row 28): phase 0 (downbeat) .. 1, downbeat pulse, the beat of the bar (0 .. 3), the kick on the grid, the bar confidence
float EwAudioGetBarPhase(sampler2D s) { return EwAudioGetValue(s, EwAudioBarPhaseU, EwAudioBarV, false); }
float EwAudioGetDownbeatPulse(sampler2D s) { return EwAudioGetValue(s, EwAudioDownbeatPulseU, EwAudioBarV, false); }
float EwAudioGetDownbeatPulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioDownbeatPulseU, EwAudioBarV, smoothed); }
float EwAudioGetBeatInBar(sampler2D s) { return floor(EwAudioGetValue(s, EwAudioBeatInBarU, EwAudioBarV, false) * 4.0 + 0.5); }
float EwAudioGetGridKickPulse(sampler2D s) { return EwAudioGetValue(s, EwAudioGridKickU, EwAudioBarV, false); }
float EwAudioGetGridKickPulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioGridKickU, EwAudioBarV, smoothed); }
float EwAudioGetBarConfidence(sampler2D s) { return EwAudioGetValue(s, EwAudioBarConfidenceU, EwAudioHitsV, false); }
// The same latency seconds ahead (see EwAudioGetBeatPhase(s, latency))
float EwAudioGetBarPhase(sampler2D s, float latency) { return fract(EwAudioGetBarPhase(s) + latency * EwAudioGetBpm(s) / 240.0); }
float EwAudioGetDownbeatPulse(sampler2D s, float latency)
{
    float bpm = EwAudioGetBpm(s);
    return bpm < 1.0 ? EwAudioGetDownbeatPulse(s) : pow(0.85, EwAudioGetBarPhase(s, latency) * 4.0 * 3600.0 / bpm);
}

// Chroma (row 29): a pitch class (0 = C .. 11 = B, max = 1), the tonalness, the chroma hue (0 .. 1, C = 0) and its strength
float EwAudioGetChroma(sampler2D s, int pitchClass, bool smoothed) { return EwAudioGetValue(s, (float(pitchClass) + 0.5) / 16.0, EwAudioChromaV, smoothed); }
float EwAudioGetChroma(sampler2D s, int pitchClass) { return EwAudioGetChroma(s, pitchClass, false); }
float EwAudioGetTonalness(sampler2D s) { return EwAudioGetValue(s, EwAudioTonalnessU, EwAudioChromaV, false); }
float EwAudioGetTonalness(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioTonalnessU, EwAudioChromaV, smoothed); }
float EwAudioGetChromaHue(sampler2D s) { return EwAudioGetValue(s, EwAudioChromaHueU, EwAudioChromaV, false); }
float EwAudioGetChromaHueStrength(sampler2D s) { return EwAudioGetValue(s, EwAudioChromaHueStrengthU, EwAudioChromaV, false); }
float EwAudioGetChromaHueStrength(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioChromaHueStrengthU, EwAudioChromaV, smoothed); }

// Travel (row 31) of bass / mid / high / loudness (EwAudioBand...) in seconds, and a time that surges with it: time + gain *
// travel (gain 0 .. 1: never backwards; time = the sound's time, iTime)
float EwAudioGetTravel(sampler2D s, int band) { return EwAudioGetValue(s, (float(band) + 0.5) / 4.0, EwAudioTravelV, false); }
float EwAudioGetTravelTime(sampler2D s, int band, float time, float gain) { return time + clamp(gain, 0.0, 1.0) * EwAudioGetTravel(s, band); }

// The spectrum (0 .. 1) at x (0 .. 1: 20 Hz .. 20 kHz, log frequency: f = 20 * 1000^x) of a level: 0 = 512 bands,
// 1 = 256 ... 9 = 1 band (the band's center, no blend with the neighbours)
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
