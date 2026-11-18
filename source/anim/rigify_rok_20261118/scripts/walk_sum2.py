# walk_10_make.py 의 SUMMARY 줄 요약 (표준 입력)
import json, sys
for l in sys.stdin:
    if not l.startswith('SUMMARY'):
        print(l.rstrip()); continue
    d = json.loads(l.split(' ', 1)[1])
    print('reach', d['reach_drop_max'], 'hips', d['hips_z'], 'mesh', d['mesh_low'], 'hands', d['hand_z'], 'loop', d['loop_pos_m'])
    for k in ('knee_heelstrike', 'knee_stance_max', 'knee_midstance', 'knee_stance_min', 'knee_swing_max', 'dorsi_range', 'ankle_z_max',
              'ball_lift_max', 'tip_z_swing_min', 'tip_z_swing_max', 'heel_z_max', 'toe_above_heel_swing_max', 'contact_speed', 'contact_gap_mm'):
        print('  ', k, d['L'][k], d['R'][k])
