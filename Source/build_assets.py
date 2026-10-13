"""Build original Walkman UI art and a three-resource Stingray patch.
Requires Python 3 and Pillow. ArchiveTemplates.json contains format templates only.
"""
from pathlib import Path
import struct, json, hashlib, zipfile
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parent.parent
SOURCE=ROOT/'Source'; MOD=ROOT/'Mod'
MOD.mkdir(parents=True,exist_ok=True)
def hash64(text):
    data=text.encode();m=0xc6a4a7935bd1e995;mask=(1<<64)-1;h=(len(data)*m)&mask
    for i in range(0,len(data)//8*8,8):
        k=int.from_bytes(data[i:i+8],'little');k=k*m&mask;k^=k>>47;k=k*m&mask;h^=k;h=h*m&mask
    tail=data[len(data)//8*8:]
    if tail:h^=int.from_bytes(tail,'little');h=h*m&mask
    h^=h>>47;h=h*m&mask;h^=h>>47;return h
FONT=SOURCE/'DejaVuSans.ttf'
if not FONT.exists(): FONT=Path('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf')
if not FONT.exists(): raise SystemExit('Place DejaVuSansMono.ttf beside this script; see FontLicense.txt.')
atlas=Image.new('RGBA',(1024,1024));d=ImageDraw.Draw(atlas);font=ImageFont.truetype(str(FONT),48)
for code in range(32,128):
    c=code-32;x=(c%16)*64;y=(c//16)*64
    d.text((x+4,y-6),chr(code),font=font,fill='white')
import re
lua_source=SOURCE/'walkman.lua'
lua_text=lua_source.read_text(encoding='utf-8')
metrics=','.join(format(font.getlength(chr(code)),'.3f')for code in range(32,128))
lua_text=re.sub(r'local glyph_advance=\{[^\n]*\}', 'local glyph_advance={'+metrics+'}',lua_text)
lua_source.write_text(lua_text,encoding='utf-8',newline='\n')
d.rectangle((1020,390,1023,393),fill='white')
S=3;body=Image.new('RGBA',(360*S,620*S));bd=ImageDraw.Draw(body)
def box(rect,fill,outline=None,radius=0,width=1):bd.rounded_rectangle(tuple(int(v*S)for v in rect),radius=int(radius*S),fill=fill,outline=outline,width=width*S)
def line(points,fill,width=1):bd.line([(int(x*S),int(y*S))for x,y in points],fill=fill,width=width*S)
def center(text,y,size,color,fontname='DejaVuSansMono-Bold.ttf'):
    fontpath=SOURCE/fontname
    if not fontpath.exists():fontpath=Path('/usr/share/fonts/truetype/dejavu')/fontname
    f=ImageFont.truetype(str(fontpath),size*S);bbox=bd.textbbox((0,0),text,font=f);bd.text(((360*S-(bbox[2]-bbox[0]))/2,y*S),text,font=f,fill=color)
box((2,2,358,618),(51,54,58,255),(154,145,122,255),23,2)
box((8,8,352,612),(9,11,13,255),(70,72,75,255),18,1)
box((44,0,77,5),(216,180,112,255),radius=2);box((283,0,316,5),(216,180,112,255),radius=2)
box((24,64,336,562),(19,23,28,255),radius=4)
center('SONY',23,22,(196,199,203,255));center('W A L K M A N',590,10,(206,174,111,255))
# Original abstract album placeholder; no supplied album art or songs are bundled.
box((72,126,288,292),(35,40,47,255),(62,70,79,255),4)
for i in range(20):
    height=20+((i*37)%99);x=86+i*9
    box((x,210-height/2,x+4,210+height/2),(125+i*4,111+i*2,82+i,255),radius=2)
line([(96,265),(264,265)],(85,88,92,255))
for x,r in [(96,26),(180,32),(264,26)]:
    bd.ellipse(((x-r)*S,(472-r)*S,(x+r)*S,(472+r)*S),outline=(210,214,218,255),width=1*S)
frame=SOURCE/'SonyFrame.png'
if frame.exists():
    im=Image.open(frame).convert('RGBA')
    # Trim transparent canvas padding only, then fit the native UI tile.
    bounds=im.getchannel('A').point(lambda a:255 if a>=20 else 0).getbbox()
    bezel=im.crop(bounds).resize((360,620),Image.Resampling.LANCZOS)
    backing=Image.new('RGBA',(360,620));ImageDraw.Draw(backing).rounded_rectangle((3,5,357,617),radius=18,fill=(8,9,11,255))
    backing.alpha_composite(bezel);atlas.alpha_composite(backing,(0,400))
else: atlas.alpha_composite(body.resize((360,620),Image.Resampling.LANCZOS),(0,400))
# Supersampled, transparent playback symbols.
symbols=Image.new('RGBA',(320*S,80*S));sd=ImageDraw.Draw(symbols)
for base,kind in [(0,'prev'),(64,'pause'),(128,'play'),(192,'next'),(256,'stop')]:
    def xy(points): return [((base+x)*S,y*S)for x,y in points]
    sd.ellipse(((base+4)*S,4*S,(base+60)*S,60*S),outline=(245,245,245,255),width=S)
    if kind=='pause':
        for xx in (24,35): sd.rectangle(((base+xx)*S,21*S,(base+xx+5)*S,43*S),fill='white')
    elif kind=='play': sd.polygon(xy([(26,20),(26,44),(43,32)]),fill='white')
    elif kind=='stop': sd.rectangle(((base+23)*S,23*S,(base+41)*S,41*S),fill='white')
    elif kind=='prev':
        sd.polygon(xy([(44,21),(44,43),(30,32)]),fill='white');sd.polygon(xy([(31,21),(31,43),(17,32)]),fill='white');sd.rectangle(((base+16)*S,21*S,(base+19)*S,43*S),fill='white')
    else:
        sd.polygon(xy([(20,21),(20,43),(34,32)]),fill='white');sd.polygon(xy([(33,21),(33,43),(47,32)]),fill='white');sd.rectangle(((base+45)*S,21*S,(base+48)*S,43*S),fill='white')
atlas.alpha_composite(symbols.resize((320,80),Image.Resampling.LANCZOS),(384,530))
# Transparent concentric rings; the Walkman glass is the only background.
cover=Image.new('RGBA',(196*S,196*S));cd=ImageDraw.Draw(cover)
cd.ellipse((10*S,10*S,186*S,186*S),outline=(103,85,55,255),width=S)
cd.ellipse((18*S,18*S,178*S,178*S),outline=(48,44,37,255),width=S)
atlas.alpha_composite(cover.resize((196,196),Image.Resampling.LANCZOS),(384,620))
# Transport options and window controls: white artwork allows tinting in-game.
for at,kind in [(704,'shuffle'),(768,'repeat'),(832,'repeat_one'),(896,'minimize'),(960,'close')]:
    icon=Image.new('RGBA',(64*S,64*S));p=ImageDraw.Draw(icon)
    def stroke(points,width=4):p.line([(round(x*S),round(y*S))for x,y in points],fill='white',width=width*S,joint='curve')
    def arrow(points):p.polygon([(round(x*S),round(y*S))for x,y in points],fill='white')
    if kind=='shuffle':
        stroke([(9,18),(18,18),(44,46),(53,46)])
        stroke([(9,46),(18,46),(27,36)])
        stroke([(36,27),(44,18),(53,18)])
        arrow([(49,11),(59,18),(49,25)]);arrow([(49,39),(59,46),(49,53)])
    elif kind.startswith('repeat'):
        p.arc((8*S,14*S,56*S,50*S),180,270,fill='white',width=4*S)
        p.arc((8*S,14*S,56*S,50*S),0,90,fill='white',width=4*S)
        stroke([(32,14),(52,14)]);stroke([(32,50),(12,50)])
        arrow([(47,7),(58,14),(47,21)]);arrow([(17,43),(6,50),(17,57)])
        if kind=='repeat_one':
            f=ImageFont.truetype(str(FONT),25*S)
            p.text((32*S,32*S),'1',font=f,anchor='mm',fill='white')
    elif kind=='minimize':stroke([(17,32),(47,32)],5)
    else:stroke([(19,19),(45,45)],4);stroke([(45,19),(19,45)],4)
    atlas.alpha_composite(icon.resize((64,64),Image.Resampling.LANCZOS),(at,530))
# Library controls use the same supersampled, transparent icon style.
for at,kind in [(832,'refresh'),(896,'current_track'),(960,'player')]:
    icon=Image.new('RGBA',(64*S,64*S));p=ImageDraw.Draw(icon)
    if kind=='refresh':
        p.arc((13*S,13*S,51*S,51*S),45,315,fill='white',width=4*S)
        p.polygon([(39*S,10*S),(52*S,23*S),(36*S,25*S)],fill='white')
    elif kind=='current_track':
        # Musical note: identify the current song without another navigation arrow.
        p.line([(27*S,44*S),(27*S,18*S),(48*S,13*S),(48*S,39*S)],fill='white',width=5*S)
        p.ellipse((12*S,38*S,29*S,51*S),fill='white')
        p.ellipse((33*S,33*S,50*S,46*S),fill='white')
    else:
        # A miniature player makes the destination of "Back to player" explicit.
        p.rounded_rectangle((18*S,7*S,46*S,57*S),radius=5*S,outline='white',width=3*S)
        p.rounded_rectangle((24*S,15*S,40*S,31*S),radius=2*S,fill='white')
        p.ellipse((27*S,39*S,37*S,49*S),outline='white',width=2*S)
    atlas.alpha_composite(icon.resize((64,64),Image.Resampling.LANCZOS),(at,620))
# A soft circular hover fill follows the round playback controls exactly.
halo=Image.new('RGBA',(64*S,64*S));hd=ImageDraw.Draw(halo)
hd.ellipse((4*S,4*S,60*S,60*S),fill=(255,255,255,36))
atlas.alpha_composite(halo.resize((64,64),Image.Resampling.LANCZOS),(704,620))
# Fixed outer-ring dots use a round alpha mask rather than square primitives.
dot=Image.new('RGBA',(16*S,16*S));ImageDraw.Draw(dot).ellipse((2*S,2*S,14*S,14*S),fill='white')
atlas.alpha_composite(dot.resize((16,16),Image.Resampling.LANCZOS),(784,620))
mini=Image.new('RGBA',(360*S,112*S));md=ImageDraw.Draw(mini)
md.rounded_rectangle((2*S,2*S,358*S,110*S),radius=12*S,fill=(17,21,26,255),outline=(177,147,97,255),width=2*S)
atlas.alpha_composite(mini.resize((360,112),Image.Resampling.LANCZOS),(384,400))
atlas.save(SOURCE/'WalkmanAtlas.png')
reference=SOURCE/'WalkmanReference.png'
if reference.exists(): (MOD/'Icon.png').write_bytes(reference.read_bytes())
else: body.resize((149,256),Image.Resampling.LANCZOS).save(MOD/'Icon.png')

templates=json.loads((SOURCE/'ArchiveTemplates.json').read_text())
header=bytearray.fromhex(templates['header']);texture=bytearray.fromhex(templates['texture']);material=bytearray.fromhex(templates['material'])
lua=(SOURCE/'walkman.lua').read_bytes();payload=struct.pack('<II',len(lua),2)+lua
texture_path='mods/julian/walkman/ui/atlas/texture';material_path='mods/julian/walkman/ui/atlas/material'
struct.pack_into('<III',texture,204,1024,1024,4096)
struct.pack_into('<Q',material,140,hash64(texture_path))
gpu=atlas.tobytes()
for at in [88,120,152]:struct.pack_into('<Q',header,at,1)
struct.pack_into('<Q',header,8,3);struct.pack_into('<Q',header,40,len(gpu))
base=(168+3*80+15)//16*16
bodies=[payload,bytes(texture),bytes(material)];names=['mods/julian/walkman',texture_path,material_path];types=[0xa14e8dfa2cd117e2,0xcd4238c6a0c69e32,0xeac0b497876adedf]
entries=[];data=bytearray();offset=base
for i,(name,kind,body_bytes)in enumerate(zip(names,types,bodies)):
    entries.append(struct.pack('<7Q6I',hash64(name),kind,offset,0,0,offset-base,0,len(body_bytes),0,len(gpu)if i==1 else 0,16,64,i+1))
    data+=body_bytes;data+=bytes((-len(data))%16);offset=base+len(data)
patch=header+b''.join(entries)+bytes(base-168-3*80)+data
struct.pack_into('<Q',patch,32,len(patch))
archive='9ba626afa44a3aa3.patch_0'
(MOD/archive).write_bytes(patch);(MOD/(archive+'.gpu_resources')).write_bytes(gpu);(MOD/(archive+'.stream')).write_bytes(b'')
(MOD/'manifest.json').write_text(json.dumps({'Guid':'ee14f7bd-4281-4f6b-ac3e-a258153b7482','Name':'Walkman','Description':'Pop some tunes like the modern day Star Lord','IconPath':'Icon.png'},indent=2)+'\n',encoding='utf-8',newline='\n')
with zipfile.ZipFile(ROOT/'Walkman_Mod.zip','w',zipfile.ZIP_DEFLATED,compresslevel=9)as z:
    for p in sorted(MOD.iterdir()):z.write(p,p.name)
print(json.dumps({'archive':archive,'lua_bytes':len(lua),'main_bytes':len(patch),'gpu_bytes':len(gpu),'zip_bytes':(ROOT/'Walkman_Mod.zip').stat().st_size,'sha256':hashlib.sha256(patch).hexdigest()},indent=2))
