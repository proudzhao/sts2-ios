#!/usr/bin/env python3
"""List/extract files in a Godot 4.x .pck (format v2/v3, unencrypted)."""
import struct, sys, os

def u32(f): return struct.unpack("<I", f.read(4))[0]
def u64(f): return struct.unpack("<Q", f.read(8))[0]

def read_dir(f):
    magic = f.read(4)
    assert magic == b"GDPC", f"bad magic {magic}"
    fmt = u32(f)
    ver = (u32(f), u32(f), u32(f))
    flags = u32(f)
    files_base = u64(f)
    dir_offset = u64(f) if fmt >= 3 else 0
    print(f"# pck format v{fmt}, godot {ver[0]}.{ver[1]}.{ver[2]}, flags={flags:#x}, files_base={files_base:#x}, dir_offset={dir_offset:#x}", file=sys.stderr)
    assert not (flags & 1), "directory is encrypted!"
    if fmt >= 3:
        f.seek(dir_offset)
    else:
        f.seek(16 * 4, 1)  # reserved
    count = u32(f)
    entries = []
    for _ in range(count):
        plen = u32(f)
        path = f.read(plen).rstrip(b"\x00").decode("utf-8")
        off = u64(f)
        size = u64(f)
        f.read(16)  # md5
        fflags = u32(f) if fmt >= 2 else 0
        if flags & 2:  # PACK_REL_FILEBASE
            off += files_base
        entries.append((path, off, size, fflags))
    return entries

def main():
    pck = sys.argv[1]
    mode = sys.argv[2] if len(sys.argv) > 2 else "ls"
    with open(pck, "rb") as f:
        entries = read_dir(f)
        if mode == "ls":
            for p, o, s, fl in entries:
                enc = " [ENCRYPTED]" if fl & 1 else ""
                print(f"{s:>12} {p}{enc}")
        elif mode == "x":  # extract paths matching substring argv[3] into argv[4]
            pat, out = sys.argv[3], sys.argv[4]
            out_real = os.path.realpath(out)
            total = 0
            MAX_TOTAL = 4 * 1024 * 1024 * 1024  # 4 GiB 总量上限:防恶意 pck 用虚假 size 撑爆磁盘
            for p, o, s, fl in entries:
                if pat not in p:
                    continue
                if fl & 1:
                    print(f"SKIP encrypted: {p}"); continue
                rel = p.replace("res://", "").replace("user://", "user/")
                # 防路径穿越:pck 条目路径完全可控(恶意 pck 可覆盖任意用户文件)。
                # 拒绝 空路径 / 绝对路径 / 盘符 / .. 段,并要求展开后仍在输出目录内。
                segs = rel.replace("\\", "/").split("/")
                if (not rel or rel.endswith("/") or rel.startswith("/") or ":" in segs[0]
                        or ".." in segs):
                    print(f"SKIP unsafe path: {p}"); continue
                # realpath 同时消解相对路径与符号链接:前缀校验之外再防 symlink 指向目录外
                dst = os.path.realpath(os.path.join(out, rel))
                if not dst.startswith(out_real + os.sep):
                    print(f"SKIP escapes output dir: {p}"); continue
                if os.path.isdir(dst):
                    print(f"SKIP path is a directory: {p}"); continue   # 防 open(dst,"wb") 抛 IsADirectoryError
                total += s
                if total > MAX_TOTAL:
                    print("abort: total extracted size exceeds 4 GiB limit", file=sys.stderr)
                    sys.exit(1)
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                f.seek(o)
                with open(dst, "wb") as w:
                    w.write(f.read(s))
                print(f"extracted {p} -> {dst}")

if __name__ == "__main__":
    main()
