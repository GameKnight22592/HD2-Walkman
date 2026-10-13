"""Emit a standard .res file for compilers that do not embed /win32manifest.
Windows build.ps1 uses Microsoft's compiler and the XML manifest directly.
"""
from pathlib import Path
import struct
root=Path(__file__).resolve().parent.parent
manifest=(root/'Companion/app.manifest').read_bytes()
def header(size,kind,name,flags=0,language=0):
    return struct.pack('<IIHHHHIHHII',size,32,65535,kind,65535,name,0,flags,language,0,0)
resource=header(0,0,0)+header(len(manifest),24,1,0x30,0x409)+manifest+bytes((-len(manifest))%4)
(root/'Companion/app.res').write_bytes(resource)
print('Built manifest resource:',len(resource),'bytes')
