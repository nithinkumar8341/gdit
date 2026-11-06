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
print("GOLDEN_HOUR_AUDIO_OK")
