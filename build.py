# -*- coding: utf-8 -*-
import os, re, subprocess, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
FX = r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
MSBUILD = os.path.join(FX, 'M' + 'SBuild.exe')
PROJ = os.path.join(HERE, 'WorkstationStudio.csproj')
OUTDIR = os.path.join(HERE, 'bin')

# 版本号从 AssemblyInfo.cs 读，避免两处写得不一样
VER = '1.2'
try:
    _s = open(os.path.join(HERE, 'AssemblyInfo.cs'), encoding='utf-8').read()
    _m = re.search(r'AssemblyFileVersion\("([\d.]+)"\)', _s)
    if _m:
        VER = '.'.join(_m.group(1).split('.')[:2])
except Exception:
    pass


def main():
    if not os.path.exists(MSBUILD):
        print('BUILD TOOL NOT FOUND'); return 1
    r = subprocess.run(
        [MSBUILD, PROJ, '/t:Rebuild', '/p:Configuration=Release', '/v:m', '/nologo'],
        capture_output=True, timeout=300)
    out = r.stdout.decode('gbk', errors='replace')
    err = r.stderr.decode('gbk', errors='replace')
    print(out[-5000:])
    if err.strip():
        print('STDERR:', err[-1500:])
    print('RC', r.returncode)
    exe = os.path.join(OUTDIR, 'Zogrid.exe')
    if r.returncode == 0 and os.path.exists(exe):
        # 交付目录：文件名带版本与架构，方便发版对号入座
        deliver = os.path.join(HERE, '交付')
        os.makedirs(deliver, exist_ok=True)
        dst = os.path.join(deliver, 'Zogrid_%s_x64.exe' % VER)
        print('EXE:', exe, os.path.getsize(exe), 'bytes')
        try:
            shutil.copy2(exe, dst)
            print('DELIVERED:', dst)
        except Exception as ex:
            print('DELIVER FAILED (程序可能在运行):', ex)
        return 0
    return 2


if __name__ == '__main__':
    raise SystemExit(main())
