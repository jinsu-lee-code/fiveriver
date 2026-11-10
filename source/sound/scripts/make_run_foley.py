# Builds the running foley layers from the game's own recordings (no new outside sounds):
#   SFX_Gear_Run_01..06  - one step's worth of rifle/sling rattle + webbing/cloth shuffle (~0.32 s)
#   SFX_Player_Breath_Run_Loop - the supplemental heavy-breathing take with soft loop edges
import random, struct, wave, math, os

A = 'C:/sk/five_river/Assets/_Project/Audio/'
RATE = 48000

def load(p):
    w = wave.open(A + p)
    assert w.getnchannels() == 1 and w.getframerate() == RATE and w.getsampwidth() == 2
    d = w.readframes(w.getnframes())
    return [x / 32768 for x in struct.unpack('<%dh' % (len(d) // 2), d)]

def save(p, s):
    w = wave.open(A + p, 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
    w.writeframes(struct.pack('<%dh' % len(s), *[max(-32767, min(32767, int(x * 32767))) for x in s])); w.close()

def fade(s, a, r):
    n = len(s); a = int(a * RATE); r = int(r * RATE)
    return [x * min(1, i / max(1, a)) * min(1, (n - 1 - i) / max(1, r)) for i, x in enumerate(s)]

def norm(s, peak_db):
    pk = max(abs(x) for x in s) or 1
    g = 10 ** (peak_db / 20) / pk
    return [x * g for x in s]

def highpass(s, hz):
    # gentle one-pole: takes the boom out of the handling takes so the rattle sits above the footstep
    rc = 1 / (2 * math.pi * hz); dt = 1 / RATE; k = rc / (rc + dt); out = []; py = px = 0
    for x in s:
        py = k * (py + x - px); px = x; out.append(py)
    return out

metal = [load('SFX/Weapon/SFX_Weapon_Switch_01.wav'), load('SFX/Weapon/SFX_Weapon_Switch_02.wav'), load('SFX/Weapon/SFX_M1_ClipInsert.wav')]
cloth = [load('SFX/Foley/SFX_Stance_Change_01.wav'), load('SFX/Foley/SFX_Stance_Change_02.wav')]
rng = random.Random(1950)
L = int(.32 * RATE)
for v in range(1, 7):
    m = metal[rng.randrange(len(metal))]; c = cloth[rng.randrange(len(cloth))]
    mo = rng.randint(0, len(m) - L - 1); co = rng.randint(0, int(.15 * RATE))
    lag = int(rng.uniform(.02, .07) * RATE)  # the gear lands a beat after the foot
    out = [0.0] * (L + lag)
    cs = fade(c[co:co + L], .01, .12)
    ms = fade(highpass(m[mo:mo + L], 300), .004, .15)
    for i in range(L):
        out[i] += cs[i] * .7
        out[i + lag] += ms[i] * rng.uniform(.45, .7)
    save('SFX/Foley/SFX_Gear_Run_%02d.wav' % v, norm(fade(out, .003, .08), -6))

b = load('Supplemental/SFX/SFX_Player_Breath_01.wav')
save('SFX/Foley/SFX_Player_Breath_Run_Loop.wav', norm(fade(b, .08, .25), -6))
print('ok')
