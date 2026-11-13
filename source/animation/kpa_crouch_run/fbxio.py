"""Binary FBX (7.x) reader / writer that keeps every node, property type and raw array bytes,
so a file can be re-written byte-identically and only the arrays we touch change.

Used by kpa_crouch_run.py to replace animation-curve values inside the original Mixamo FBX
without re-exporting (keeps node names, PreRotations, unit 1 cm, axes, take "mixamo.com").
"""
import struct, zlib

HEAD_MAGIC = b'Kaydara FBX Binary  \x00\x1a\x00'


class Prop:
    __slots__ = ('t', 'v', 'raw')

    def __init__(self, t, v, raw=None):
        self.t, self.v, self.raw = t, v, raw   # raw: original encoded bytes for arrays (kept if unchanged)

    def set_array(self, values):
        self.v = list(values)
        self.raw = None


class Node:
    __slots__ = ('name', 'props', 'children', 'had_null')

    def __init__(self, name, props, children, had_null):
        self.name, self.props, self.children, self.had_null = name, props, children, had_null

    def find(self, name):
        return [c for c in self.children if c.name == name]

    def first(self, name):
        for c in self.children:
            if c.name == name:
                return c
        return None


_ARR_FMT = {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'b': '?'}


class FbxFile:
    def __init__(self, path):
        data = open(path, 'rb').read()
        assert data[:23] == HEAD_MAGIC, 'not a binary FBX'
        self.version = struct.unpack('<I', data[23:27])[0]
        self.wide = self.version >= 7500
        self._data = data
        pos = 27
        self.nodes = []
        while True:
            n, pos = self._read_node(pos)
            if n is None:
                break
            self.nodes.append(n)
        self.footer = data[pos:]          # everything after the top-level null record
        self._footer_start = pos
        del self._data

    # ---------- reading
    def _read_prop(self, p):
        d = self._data
        t = chr(d[p]); p += 1
        if t == 'Y': return Prop(t, struct.unpack('<h', d[p:p+2])[0]), p+2
        if t == 'C': return Prop(t, d[p]), p+1
        if t == 'I': return Prop(t, struct.unpack('<i', d[p:p+4])[0]), p+4
        if t == 'F': return Prop(t, struct.unpack('<f', d[p:p+4])[0]), p+4
        if t == 'D': return Prop(t, struct.unpack('<d', d[p:p+8])[0]), p+8
        if t == 'L': return Prop(t, struct.unpack('<q', d[p:p+8])[0]), p+8
        if t in 'SR':
            n = struct.unpack('<I', d[p:p+4])[0]
            return Prop(t, bytes(d[p+4:p+4+n])), p+4+n
        if t in _ARR_FMT:
            n, enc, clen = struct.unpack('<III', d[p:p+12])
            raw = bytes(d[p:p+12+clen])
            body = d[p+12:p+12+clen]
            if enc == 1:
                body = zlib.decompress(body)
            vals = list(struct.unpack('<%d%s' % (n, _ARR_FMT[t]), body))
            return Prop(t, vals, raw), p+12+clen
        raise ValueError('unknown FBX property type %r' % t)

    def _read_node(self, p):
        d = self._data
        if self.wide:
            end, nprops, plen = struct.unpack('<QQQ', d[p:p+24]); p += 24
        else:
            end, nprops, plen = struct.unpack('<III', d[p:p+12]); p += 12
        nl = d[p]
        name = d[p+1:p+1+nl].decode('ascii'); p += 1+nl
        if end == 0:
            return None, p
        props = []
        for _ in range(nprops):
            pr, p = self._read_prop(p)
            props.append(pr)
        children = []
        had_null = False
        null_len = 25 if self.wide else 13
        while p < end:
            if p + null_len == end and d[p:end] == b'\x00' * null_len:
                had_null = True
                p = end
                break
            c, p = self._read_node(p)
            if c is None:
                had_null = True
                break
            children.append(c)
        return Node(name, props, children, had_null), end

    # ---------- writing
    @staticmethod
    def _enc_prop(pr, compress_new=True):
        t = pr.t
        if t == 'Y': return b'Y' + struct.pack('<h', pr.v)
        if t == 'C': return b'C' + bytes([pr.v])
        if t == 'I': return b'I' + struct.pack('<i', pr.v)
        if t == 'F': return b'F' + struct.pack('<f', pr.v)
        if t == 'D': return b'D' + struct.pack('<d', pr.v)
        if t == 'L': return b'L' + struct.pack('<q', pr.v)
        if t in 'SR': return t.encode() + struct.pack('<I', len(pr.v)) + pr.v
        if pr.raw is not None:
            return t.encode() + pr.raw
        body = struct.pack('<%d%s' % (len(pr.v), _ARR_FMT[t]), *pr.v)
        if compress_new and len(body) > 128:
            c = zlib.compress(body, 6)
            return t.encode() + struct.pack('<III', len(pr.v), 1, len(c)) + c
        return t.encode() + struct.pack('<III', len(pr.v), 0, len(body)) + body

    def _enc_node(self, n, offset):
        hdr_len = 24 if self.wide else 12
        name = n.name.encode('ascii')
        pbytes = b''.join(self._enc_prop(p) for p in n.props)
        pos = offset + hdr_len + 1 + len(name) + len(pbytes)
        parts = []
        for c in n.children:
            b = self._enc_node(c, pos)
            parts.append(b); pos += len(b)
        if n.had_null:
            nb = b'\x00' * (25 if self.wide else 13)
            parts.append(nb); pos += len(nb)
        if self.wide:
            hdr = struct.pack('<QQQ', pos, len(n.props), len(pbytes))
        else:
            hdr = struct.pack('<III', pos, len(n.props), len(pbytes))
        return hdr + bytes([len(name)]) + name + pbytes + b''.join(parts)

    def write(self, path):
        out = bytearray(HEAD_MAGIC + struct.pack('<I', self.version))
        for n in self.nodes:
            out += self._enc_node(n, len(out))
        # top-level null record + footer. The footer contains alignment padding that depends on
        # the file length: rebuild it like the FBX SDK does (id, 4 zero, pad to 16, version, 120 zero, magic).
        null_len = 25 if self.wide else 13
        out += b'\x00' * null_len
        foot = self.footer
        foot_id = foot[:16]
        magic = foot[-16:]
        out += foot_id + b'\x00' * 4
        # FBX SDK pads so the whole file length is a multiple of 16 (1..16 bytes)
        pad = (-(len(out) + 4 + 120 + 16)) % 16
        if pad == 0:
            pad = 16
        out += b'\x00' * pad
        out += struct.pack('<I', self.version) + b'\x00' * 120 + magic
        open(path, 'wb').write(bytes(out))
        return len(out)

    # ---------- helpers
    def top(self, name):
        for n in self.nodes:
            if n.name == name:
                return n
        return None


def sval(prop):
    """decode an S property, cutting the '\\x00\\x01Class' suffix"""
    return prop.v.split(b'\x00\x01')[0].decode('utf8', 'replace')


def props70(node):
    out = {}
    p70 = node.first('Properties70')
    if p70 is None:
        return out
    for p in p70.children:
        out[sval(p.props[0])] = [q.v for q in p.props[4:]]
    return out


def anim_index(fbx):
    """returns (models{id:(name,node)}, curves{(model_name, 'T'|'R'|'S', axis 0..2): AnimationCurve node})"""
    objs = fbx.top('Objects').children
    by_id = {o.props[0].v: o for o in objs}
    models = {o.props[0].v: sval(o.props[1]) for o in objs if o.name == 'Model'}
    cn_target = {}
    curve_of = {}
    for c in fbx.top('Connections').children:
        kind = sval(c.props[0])
        src, dst = c.props[1].v, c.props[2].v
        if kind == 'OP':
            prop = sval(c.props[3])
            s = by_id.get(src)
            if s is None:
                continue
            if s.name == 'AnimationCurveNode' and dst in models:
                ch = {'Lcl Translation': 'T', 'Lcl Rotation': 'R', 'Lcl Scaling': 'S'}.get(prop)
                if ch:
                    cn_target[src] = (models[dst], ch)
            elif s.name == 'AnimationCurve':
                curve_of.setdefault(dst, {})[prop] = s
    curves = {}
    for cn, (mname, ch) in cn_target.items():
        for prop, cnode in curve_of.get(cn, {}).items():
            axis = {'d|X': 0, 'd|Y': 1, 'd|Z': 2}[prop]
            curves[(mname, ch, axis)] = cnode
    return models, curves


def curve_arrays(cnode):
    kt = cnode.first('KeyTime').props[0]
    kv = cnode.first('KeyValueFloat').props[0]
    return kt, kv
