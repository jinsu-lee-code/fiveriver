# Cut VARCO Sound's "Player footsteps" / "Player rifle handling" tracks (generated from the game's own running clip)
# into single-step one-shots for the step-driven footstep system: strongest onsets at least 0.3 s apart, 0.32 s each
# (footsteps) / 0.36 s (gear), 5 ms fade-in, 120 ms fade-out, peak normalised to -3 dBFS.
import wave, struct, math, os
SRC = 'C:/sk_assets/varco_run/zip/'
OUT = 'C:/sk/five_river/Assets/_Project/Audio/SFX/Foley/'

def load(name):
    w = wave.open(SRC + name); fr = w.getframerate(); d = w.readframes(w.getnframes())
    return fr, [x / 32768 for x in struct.unpack('<%dh' % (len(d) // 2), d)]

def onsets(s, fr, gap):
    win = fr // 100; env = [math.sqrt(sum(x * x for x in s[i:i + win]) / win) for i in range(0, len(s) - win, win)]
    cand = []
    for i in range(3, len(env) - 3):
        prev = max(env[i - 3:i]) + 1e-5
        if env[i] > 0.02 and env[i] / prev > 1.8 and env[i] >= max(env[i:i + 3]): cand.append((env[i] / prev * env[i], i))
    cand.sort(reverse=True); picked = []
    for score, i in cand:
        if all(abs(i - j) * 0.01 >= gap for j in picked): picked.append(i)
    return sorted(picked), env

def cut(s, fr, start, length):
    a = max(0, int((start - 0.01) * fr)); b = min(len(s), a + int(length * fr)); seg = s[a:b]
    n = len(seg); fi, fo = int(.005 * fr), int(.12 * fr)
    seg = [x * min(1, i / fi) * min(1, (n - 1 - i) / fo) for i, x in enumerate(seg)]
    pk = max(abs(x) for x in seg) or 1; g = 10 ** (-3 / 20) / pk
    return [x * g for x in seg]

def save(path, s, fr):
    w = wave.open(path, 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(fr)
    w.writeframes(struct.pack('<%dh' % len(s), *[max(-32767, min(32767, int(x * 32767))) for x in s])); w.close()

for src, prefix, count, length in (('Player footsteps_1_cbfd19c0.wav', 'SFX_Foot_Run_Varco', 8, .32),
                                   ('Player rifle handling_1_36b34364.wav', 'SFX_Gear_Run_Varco', 6, .36)):
    fr, s = load(src)
    ons, env = onsets(s, fr, .3)
    # keep the strongest `count` hits
    ranked = sorted(ons, key=lambda i: -env[i])[:count]
    for k, i in enumerate(sorted(ranked)):
        save(OUT + '%s_%02d.wav' % (prefix, k + 1), cut(s, fr, i * 0.01, length), fr)
    print(prefix, 'hits', len(ons), 'kept at', [round(i * 0.01, 2) for i in sorted(ranked)])
