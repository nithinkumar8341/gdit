import math
import os
import random
import struct
import wave


OUTPUT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "Audio"))
RATE = 22050
os.makedirs(OUTPUT, exist_ok=True)


def write_wave(filename, seconds, sample):
    count = int(RATE * seconds)
    with wave.open(os.path.join(OUTPUT, filename), "wb") as target:
        target.setnchannels(1)
        target.setsampwidth(2)
        target.setframerate(RATE)
        frames = bytearray()
        for index in range(count):
            time_value = index / RATE
            amplitude = max(-1.0, min(1.0, sample(time_value, index, count)))
            frames.extend(struct.pack("<h", int(amplitude * 32767)))
        target.writeframes(frames)
    print("WROTE", filename, count, "samples")


def envelope(time_value, duration, attack=0.01, release=0.06):
    return min(1.0, time_value / attack, (duration - time_value) / release)


write_wave(
    "UI_Click.wav", 0.09,
    lambda t, i, n: math.sin(2 * math.pi * 880 * t) *
                    math.exp(-t * 42) * 0.28)
write_wave(
    "Patient_Interaction.wav", 0.14,
    lambda t, i, n: math.sin(2 * math.pi * (620 - 250 * t) * t) *
                    math.exp(-t * 16) * 0.32)
write_wave(
    "Kit_Open.wav", 0.42,
    lambda t, i, n: (math.sin(2 * math.pi * 180 * t) * 0.38 +
                     math.sin(2 * math.pi * 620 * t) * 0.1) *
                    envelope(t, 0.42, 0.025, 0.16))
write_wave(
    "Objective_Complete.wav", 0.62,
    lambda t, i, n: (math.sin(2 * math.pi * 660 * t) * 0.32 +
                     math.sin(2 * math.pi * 880 * t) * 0.24) *
                    envelope(t, 0.62, 0.02, 0.18))

random.seed(26)
write_wave(
    "Roadside_Ambience.wav", 6.0,
    lambda t, i, n: (math.sin(2 * math.pi * 78 * t) * 0.055 +
                     math.sin(2 * math.pi * 117 * t) * 0.025 +
                     random.uniform(-0.45, 0.45) * 0.012) *
                    (0.7 + 0.3 * math.sin(2 * math.pi * t / 6.0)))
random.seed(31)
write_wave(
    "Footstep.wav", 0.16,
    lambda t, i, n: (random.uniform(-1.0, 1.0) * 0.5 +
                     math.sin(2 * math.pi * 95 * t) * 0.6) *
                    math.exp(-t * 34) * 0.42)
write_wave(
    "UI_Hover.wav", 0.05,
    lambda t, i, n: math.sin(2 * math.pi * 1320 * t) *
                    math.exp(-t * 70) * 0.12)
# --- v2 clips -----------------------------------------------------------------
random.seed(41)


def lowpass_noise(state, amount):
    state[0] = state[0] + (random.uniform(-1.0, 1.0) - state[0]) * amount
    return state[0]


def siren(t, i, n):
    # Two-tone ambulance siren, 0.5 s per tone, loops every 2 s.
    freq = 960 if int(t / 0.5) % 2 == 0 else 770
    tone = math.sin(2 * math.pi * freq * t)
    return (0.6 * tone + 0.25 * (1 if tone > 0 else -1)) * 0.32


write_wave("Siren.wav", 2.0, siren)

static_state = [0.0]
write_wave(
    "Radio_Static.wav", 0.35,
    lambda t, i, n: (lowpass_noise(static_state, 0.6) * 0.5 + math.sin(2 * math.pi * 1700 * t) * 0.04) *
                    envelope(t, 0.35, 0.01, 0.08))


def voice(t, i, n):
    pitch = 150 + 30 * math.sin(2 * math.pi * 3.0 * t)
    base = sum(math.sin(2 * math.pi * pitch * k * t) / k for k in range(1, 6))
    formant = 0.6 + 0.4 * math.sin(2 * math.pi * 7.0 * t)
    return base * formant * 0.12 * envelope(t, 0.32, 0.03, 0.1)


write_wave("Voice_Murmur.wav", 0.32, voice)


def heartbeat(t, i, n):
    def thump(t0):
        d = t - t0
        return math.sin(2 * math.pi * 55 * d) * math.exp(-d * 18) if d >= 0 else 0.0
    return (thump(0.0) + 0.7 * thump(0.28)) * 0.7


write_wave("Heartbeat.wav", 0.9, heartbeat)

breath_state = [0.0]
write_wave(
    "Breathing.wav", 2.4,
    lambda t, i, n: lowpass_noise(breath_state, 0.25) * 0.35 *
                    (math.sin(math.pi * min(t, 1.0)) ** 2 if t < 1.0 else 0.6 * math.sin(math.pi * (t - 1.0) / 1.4) ** 2))

rain_state = [0.0]
write_wave(
    "Rain_Loop.wav", 4.0,
    lambda t, i, n: (lowpass_noise(rain_state, 0.85) * 0.22 +
                     (random.uniform(-1, 1) * 0.5 if random.random() < 0.002 else 0.0)) *
                    min(1.0, t / 0.05, (4.0 - t) / 0.05))

wind_state = [0.0]
write_wave(
    "Wind_Loop.wav", 6.0,
    lambda t, i, n: lowpass_noise(wind_state, 0.03) * 0.9 * (0.6 + 0.4 * math.sin(2 * math.pi * t / 6.0)) *
                    min(1.0, t / 0.1, (6.0 - t) / 0.1))


def pad(t, i, n):
    # Slow minor chord (A minor) with gentle tremolo, 8 s loop.
    chord = (220.0, 261.63, 329.63)
    value = sum(math.sin(2 * math.pi * f * t) + 0.3 * math.sin(2 * math.pi * f * 2 * t) for f in chord)
    return value * 0.05 * (0.85 + 0.15 * math.sin(2 * math.pi * t / 4.0)) * min(1.0, t / 0.2, (8.0 - t) / 0.2)


write_wave("Music_Pad.wav", 8.0, pad)


def pulse(t, i, n):
    beat = (t * 2.0) % 1.0
    note = 110.0 if int(t * 2.0) % 4 != 3 else 98.0
    return math.sin(2 * math.pi * note * t) * math.exp(-beat * 6) * 0.35


write_wave("Music_Pulse.wav", 8.0, pulse)


def drums(t, i, n):
    beat = (t * 2.0) % 1.0
    kick = math.sin(2 * math.pi * (60 + 80 * math.exp(-beat * 30)) * t) * math.exp(-beat * 10)
    half = (t * 4.0) % 1.0
    hat = random.uniform(-1, 1) * math.exp(-half * 40) * 0.25
    return (kick * 0.5 + hat) * 0.6


write_wave("Music_Drums.wav", 8.0, drums)
write_wave("Metronome.wav", 0.06, lambda t, i, n: math.sin(2 * math.pi * 1800 * t) * math.exp(-t * 80) * 0.5)
write_wave("Compression.wav", 0.12, lambda t, i, n: math.sin(2 * math.pi * 90 * t) * math.exp(-t * 35) * 0.6)
print("GOLDEN_HOUR_AUDIO_OK")
