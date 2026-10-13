"""Offline archive and behavior validation. Requires Pillow and lupa (LuaJIT 2.1).
It substitutes only the OS/game boundary; it does not run the Windows audio engine.
"""
from pathlib import Path
import struct,json,zipfile,math
from PIL import Image
from lupa.luajit21 import LuaRuntime

ROOT=Path(__file__).resolve().parent.parent
source=(ROOT/'Source/walkman.lua').read_text();results=[]
def passed(message):results.append(message);print('PASS',message)
lua=LuaRuntime(unpack_returned_tuples=True)
lua.eval('function(s)local f,e=loadstring(s);assert(f,e);return true end')(source)
passed('Complete Walkman source compiles under LuaJIT 2.1.')
lua.execute('local f=require(\'ffi\');f.cdef([[int hd2WalkmanTest_abs(int) __asm__("abs");]]);assert(f.C.hd2WalkmanTest_abs(-42)==42)')
passed('LuaJIT private native-symbol aliases parse and resolve without declaring the original symbol name.')
discovery_source=ROOT.parent/'inspect/sharedloader/discover.lua'
if discovery_source.exists():
    loader=LuaRuntime(unpack_returned_tuples=True,encoding=None)
    loader.globals()[b'Discovery']=loader.execute(discovery_source.read_bytes())
    loader.execute(b'Hash=Discovery.hasher(require("ffi"),require("bit"))')
    paths=loader.table_from([str((ROOT/'Mod/9ba626afa44a3aa3.patch_0').resolve()).encode()])
    found,warnings,copies=loader.globals()[b'Discovery'][b'scan'](paths,loader.globals()[b'Hash'],loader.eval(b'io.open'),loader.table())
    assert list(found.values())==[b'mods/julian/walkman'] and not list(warnings.values())
    passed('The upstream Bingus Shared Loader discovery scanner recognizes the real Walkman archive with no warnings.')

patch=next((ROOT/'Mod').glob('*.patch_0'));data=patch.read_bytes();gpu=Path(str(patch)+'.gpu_resources').read_bytes()
assert len(data)==struct.unpack_from('<Q',data,32)[0]
assert struct.unpack_from('<Q',data,8)[0]==3 and struct.unpack_from('<Q',data,40)[0]==len(gpu)==4194304
entries=[struct.unpack_from('<7Q6I',data,168+i*80)for i in range(3)]
for i,e in enumerate(entries):
 assert e[2]%16==0 and e[2]+e[7]<=len(data) and e[5]==e[2]-416
 assert e[12]==i+1 and e[11]==64 and e[10]==16
 if i:assert entries[i-1][2]+entries[i-1][7]<=e[2]
e=entries[0];assert data[e[2]+8:e[2]+e[7]]==(ROOT/'Source/walkman.lua').read_bytes()
e=entries[1];texture=data[e[2]:e[2]+e[7]];assert texture[192:196]==b'DDS ' and struct.unpack_from('<III',texture,204)==(1024,1024,4096)
assert struct.unpack_from('<I',texture,320)[0]==28
e=entries[2];assert struct.unpack_from('<Q',data,e[2]+140)[0]==entries[1][0]
assert gpu==Image.open(ROOT/'Source/WalkmanAtlas.png').convert('RGBA').tobytes()
with zipfile.ZipFile(ROOT/'Walkman_Mod.zip')as z:assert z.testzip()is None and len(z.namelist())==5
passed('Independent three-resource archive: exact Lua payload, aligned records, DDS/RGBA dimensions, material-to-texture reference, GPU bytes, and ZIP CRC.')

fixture=r'''
ffi=require('ffi');TestMs=2000;Launches=0;Exited=false;Missing=false;Closed={};Options={};Pressed={};Draw={};NextId=0;Width=1920;Height=1080;WorldList={'main','ui'}
CursorShown=false;Protected=false;MouseX=0;MouseY=0;MousePressed={};MouseDown={};Registered={};BitmapCreates=0;BitmapUpdates=0;ProtectFails=false;FrameEpoch=0
function TestProtect(active)if ProtectFails and active then return false end;Protected=active;return true end
Memory=ffi.new('uint8_t[4096]');Words=ffi.cast('uint32_t *',Memory)
function wstring(p)local s='';local i=0;while p[i]~=0 do s=s..string.char(p[i]<128 and p[i]or 63);i=i+1 end;return s end
TestK={GetCurrentProcess=function()return ffi.cast('void *',99)end,GetModuleHandleA=function()return ffi.cast('void *',0)end,ReadProcessMemory=function()NativeReads=(NativeReads or 0)+1;return 0 end,GetCurrentProcessId=function()return 42 end,GetTickCount=function()return TestMs end,
GetEnvironmentVariableW=function(name,out,limit)local s=[[C:\Users\Test\AppData\Local]];for i=1,#s do out[i-1]=s:byte(i)end;out[#s]=0;return #s end,
GetFileAttributesW=function()return Missing and 0xffffffff or 0 end,GetLastError=function()return 2 end,
CreateFileMappingA=function(_,_,_,_,size,name)assert(size==4096 and name==[[Local\HD2Walkman_42]]);return ffi.cast('void *',100)end,
MapViewOfFile=function(_,_,_,_,size)assert(size==4096);return ffi.cast('void *',Memory)end,
UnmapViewOfFile=function()Unmapped=true;return 1 end,
CloseHandle=function(h)Closed[#Closed+1]=tonumber(ffi.cast('uintptr_t',h));return 1 end,
WaitForSingleObject=function(_,timeout)assert(timeout==0);return Exited and 0 or 258 end,
CreateProcessW=function(path,command,_,_,inherit,flags,_,folder,startup,info)
 assert(wstring(path):find('WalkmanAudio.exe',1,true));assert(wstring(command):find('" --game 42',1,true));assert(inherit==0 and flags==0x08000000)
 assert(ffi.cast('uint32_t *',startup)[0]==104);local h=ffi.cast('void **',info);h[0]=ffi.cast('void *',101);h[1]=ffi.cast('void *',102);Launches=Launches+1;return 1 end}
stingray={
Vector2=function(x,y)return {x=x,y=y}end,Vector3=setmetatable({x=function(v)return v.x end,y=function(v)return v.y end},{__call=function(_,x,y,z)return {x=x,y=y,z=z}end}),
Window={show_cursor=function()return CursorShown end,set_show_cursor=function(v)CursorShown=v end},
Mouse={axis_id=function(name)return name end,axis=function()return{x=MouseX,y=MouseY}end,button_id=function(name)return name end,pressed=function(name)return MousePressed[name]or false end,down=function(name)return MouseDown[name]or false end},
Color=function(a,r,g,b)return {a=a,r=r,g=g,b=b,epoch=FrameEpoch}end,
Application={main_world=function()return 'main'end,worlds=function()return WorldList end},
World={create_screen_gui=function(world)if CreationFails then error('UI world is not ready')end;assert(world~='main');CreatedWorld=world;return 'gui'end,destroy_gui=function()Destroyed=true end},
Gui={resolution=function()return Width,Height end,
bitmap_uv=function(gui,material,uv1,uv2,pos,size,color)
 assert(gui=='gui'and material=='mods/julian/walkman/ui/atlas/material'and color.epoch==FrameEpoch,'Expired engine color');NextId=NextId+1;BitmapCreates=BitmapCreates+1;Draw[NextId]={uv1=uv1,uv2=uv2,pos=pos,size=size,color=color};if DrawEvents then DrawEvents[#DrawEvents+1]='create'end;return NextId end,
update_bitmap_uv=function(gui,id,material,uv1,uv2,pos,size,color)assert(Draw[id]and color.epoch==FrameEpoch,'Expired engine color');BitmapUpdates=BitmapUpdates+1;Draw[id]={uv1=uv1,uv2=uv2,pos=pos,size=size,color=color}end,
destroy_bitmap=function(_,id)if DrawEvents then DrawEvents[#DrawEvents+1]='destroy'end;Draw[id]=nil end}}
ModOptionsMenu={api=1,register_option=function(key,o)if Options[key]==nil then Options[key]=o.default end;return true end,get=function(key)return Options[key]end,set=function(key,v)Options[key]=v end}
ModBindingsMenu={api=1,register_binding=function(key,label,default,opt)assert(default==nil and opt.category=='Walkman');Registered[key]=true;return true end,ready=function()return true end,is_down=function(key)return Pressed[key]or false end}
update=function(dt,...)FrameEpoch=FrameEpoch+1;OldCalls=(OldCalls or 0)+1;return 'old-return',nil,dt,... end
shutdown=function(...)OldShutdown=true;return ... end
function advance(n)for i=1,n or 1 do TestMs=TestMs+210;update(.1);TestMs=TestMs+210;update(.1)end end
function publish(count,title)
 Words[11]=Words[11]+1;Words[9]=TestMs;Words[10]=1;Words[12]=count;Words[13]=0;Words[14]=92000;Words[15]=247000;Words[16]=50;Words[17]=0;Words[18]=0;Words[19]=Words[21];Words[23]=7
 ffi.fill(Memory+96,192);ffi.fill(Memory+288,256);ffi.copy(Memory+288,title,#title);ffi.fill(Memory+544,128);ffi.copy(Memory+544,'MISSION MIX',11)
 for i=0,7 do local s=(TrackNames and TrackNames[tonumber(Words[21])+i])or 'Track '..(tonumber(Words[21])+i+1);ffi.fill(Memory+672+i*256,256);if Words[21]+i<count then ffi.copy(Memory+672+i*256,s,#s)end end
 Words[11]=Words[11]+1
end
function fill(count,title)for i=1,4 do ack();publish(count,title);advance(2)end end
function publish_current(count,index,title)publish(count,title);Words[13]=index end
function fill_current(count,index,title)for i=1,4 do ack();publish_current(count,index,title);advance(2)end end
function point(u,t)local x,y,w,h,k=HD2Walkman.layout();MouseX=x+u*k;MouseY=y+h-t*k end
function click(u,t)point(u,t);MousePressed.left=true;update(.016);MousePressed.left=false;ack();advance()end
function ack()Words[8]=Words[5];Words[9]=TestMs end
function press(key)Pressed['julian.walkman.'..key]=true;advance();Pressed['julian.walkman.'..key]=false;ack();advance()end
'''
a=source.index("local kernel=ffi.load('kernel32')");b=source.index('local process=K.GetCurrentProcess()',a)
test_source=source[:a]+'local K=TestK\n'+source[b:]
native_test_source=test_source
a=test_source.index('local function protect_input(active)');b=test_source.index('local function wide(s)',a)
test_source=test_source[:a]+'local protect_input=TestProtect\n'+test_source[b:]
test_source=test_source.replace('api.layout=layout', '''api.inspect=function()local n=0;for _,p in ipairs(title_pages)do n=n+#p.rows end;local v=0;for _ in pairs(visible_rows)do v=v+1 end;return page,snapshot.title,snapshot.state,snapshot.index,n,v,scroll_visual,scroll_first,visible_generation end
api.layout=layout''')
api=lua.execute(fixture+test_source)
lua.execute("local a,b,c,d=update(.1,'arg');assert(a=='old-return'and b==nil and c==.1 and d=='arg');advance()")
passed('Update chaining preserves multiple return values, nil slots, and arguments; key registrations have no forced defaults.')
lua.execute("press('toggle');assert(Launches==1 and #Closed==1 and Closed[1]==102);assert(Words[0]==0x574B4D31 and Words[1]==1 and Words[2]==42);publish(12,'I Really Want to Stay at Your House');advance()")
lua.execute("assert(Options['julian.walkman.mini']==false);click(62,100)")
passed('Real game bridge creates the versioned 4 KiB mapping, launches the exact companion, closes the thread handle, and retains the process handle.')
lua.execute('for i=1,8 do ack();publish(12,"SUPER EARTH NIGHT DRIVE");advance()end')
lua.execute("click(180,480);assert(Words[6]==1);ack();advance();click(250,480);assert(Words[6]==2);ack();advance();click(105,480);assert(Words[6]==3);ack();advance()")
passed('Mouse playback controls publish acknowledged commands without extra bindings.')
lua.execute("local n=0;for _ in pairs(Registered)do n=n+1 end;assert(n==7);for _,key in ipairs({'toggle','compact','play_pause','next','previous','shuffle','repeat'})do assert(Registered['julian.walkman.'..key])end;assert(CursorShown and Protected)")
passed('Seven optional bindings register without default keys; full-player mouse navigation retains guarded input capture.')
lua.execute("publish(1000,'I Really Want to Stay at Your House');advance();click(80,104);point(170,200);MousePressed.wheel_down=true;update(.016);MousePressed.wheel_down=false;advance();assert(Words[21]==8);publish(1000,'I Really Want to Stay at Your House');advance();click(100,155);assert(Words[6]==4 and Words[7]==3 and Words[750]==7)")
passed('Mouse wheel uses cached titles and requests only the missing nearby title window and clicking a row uses its absolute index and library generation.')
lua.execute("ack();publish(1000,'I Really Want to Stay at Your House');advance();click(80,104);point(309,438);MousePressed.left=true;MouseDown.left=true;update(.016);MousePressed.left=false;update(.016);MouseDown.left=false;advance();assert(Words[21]==984);fill(1000,'I Really Want to Stay at Your House');click(100,155);assert(Words[7]==991)")
passed('The draggable scrollbar reaches the last window of a 1,000-song library without crossing its bounds.')
lua.execute("ack();publish(1000,'I Really Want to Stay at Your House');advance();click(180,440);assert(Words[6]==8 and math.abs(tonumber(Words[7])-123500)<500);ack();advance()")
passed('Clicking the seek bar seeks to the matching song position.')
lua.execute("local creates=BitmapCreates;for i=1,60 do update(.016)end;assert(BitmapUpdates>100 and BitmapCreates-creates<40)")
passed('Animation updates reuse existing bitmap objects instead of rebuilding the entire GUI each frame.')
lua.execute("press('compact');assert(not Protected and not CursorShown);press('compact');assert(Protected and CursorShown);press('toggle');assert(Options['julian.walkman.open']==false and not Protected and not CursorShown);local n=0;for _ in pairs(Draw)do n=n+1 end;assert(n==0 and Words[4]==0 and Launches==1);HD2Walkman.play_pause();advance();assert(Words[6]==1)")
passed('Compact and hide release owned input capture/cursor state; hide finishes its exit animation and keeps audio alive.')
lua.execute("for _,test in ipairs({{'play_pause',1},{'next',2},{'previous',3},{'shuffle',6},{'repeat',7}})do ack();press(test[1]);assert(Words[6]==test[2]and not Options['julian.walkman.open']and not CursorShown and not Protected)end;assert(Words[7]==1);press('repeat');assert(Words[6]==7 and Words[7]==2);press('repeat');assert(Words[7]==0);press('shuffle');assert(Words[6]==6 and Words[7]==0)")
passed('Optional playback bindings work while hidden, do not capture the cursor, and shuffle/repeat toggles track acknowledged and pending state.')
lua.execute("press('toggle');for i=1,8 do ack();publish(12,'I Really Want to Stay at Your House');advance()end")
for res in [(1280,720),(1920,1080),(2560,1440),(3840,2160),(3440,1440),(1280,1024)]:
 for size in [70,100,140]:
  for mini in [False,True]:
   lua.globals().Width,lua.globals().Height=res
   opt=lua.globals().Options;opt['julian.walkman.size']=size;opt['julian.walkman.mini']=mini
   for x,y in [(0,0),(100,0),(0,100),(100,100),(50,50)]:
    opt['julian.walkman.x']=x;opt['julian.walkman.y']=y;lua.execute('ack();advance()')
    gx,gy,w,h,k=api.layout();assert gx>=-1e-7 and gy>=-1e-7 and gx+w<=res[0]+1e-7 and gy+h<=res[1]+1e-7
passed('36 size/resolution/compact combinations and all five anchor positions keep the device within the render surface (180 placements).')

lua.globals().Width=1920;lua.globals().Height=1080
opt=lua.globals().Options;opt['julian.walkman.size']=140;opt['julian.walkman.mini']=False;opt['julian.walkman.x']=100;opt['julian.walkman.y']=10
lua.execute("ack();publish(12,'I Really Want to Stay at Your House');advance();click(62,100)")
atlas=Image.open(ROOT/'Source/WalkmanAtlas.png').convert('RGBA')
def render(path,engine=None,engine_api=None):
 engine=engine or lua;engine_api=engine_api or api
 x,y,w,h,k=engine_api.layout();out=Image.new('RGBA',(math.ceil(w)+4,math.ceil(h)+4),(0,0,0,0))
 calls=sorted(engine.globals().Draw.values(),key=lambda c:c['pos']['z'])
 for c in calls:
  u,v=c['uv1'],c['uv2'];p,s,col=c['pos'],c['size'],c['color']
  box=tuple(round(n*1024)for n in [u['x'],u['y'],v['x'],v['y']]);tile=atlas.crop(box).resize((max(1,round(s['x'])),max(1,round(s['y']))),Image.Resampling.LANCZOS)
  import numpy as np
  px=np.asarray(tile).astype('uint16');px[:,:,0]=px[:,:,0]*col['r']//255;px[:,:,1]=px[:,:,1]*col['g']//255;px[:,:,2]=px[:,:,2]*col['b']//255;px[:,:,3]=px[:,:,3]*col['a']//255
  out.alpha_composite(Image.fromarray(px.astype('uint8'),'RGBA'),(round(p['x']-x)+2,round(h-(p['y']-y)-s['y'])+2))
 out.save(path)
 matte=Image.new('RGB',out.size,(25,29,34));matte.paste(out,mask=out.getchannel('A'));matte.save(path.with_name(path.stem+'_Opaque.png'))
render(ROOT/'Notes/Player_Preview.png')
lua.execute("HD2Walkman.library();publish(12,'I Really Want to Stay at Your House');advance()")
render(ROOT/'Notes/Library_Preview.png')
passed('Preview images are rendered from the actual Lua bitmap calls and packaged atlas, rather than a separate UI mockup.')

lua.execute("WorldList={game='main',hud='ui'};advance();assert(CreatedWorld=='ui');WorldList={[0]='main',[5]='ui'};advance();assert(CreatedWorld=='ui');WorldList={game='main',hud='replacement'};advance();assert(CreatedWorld=='replacement');WorldList={'main','ui'};advance();assert(CreatedWorld=='ui')")
passed('Keyed, sparse, zero-based and array world lists all render; a live target stays stable and a replaced world rebuilds the owned GUI.')
# Real title glyphs must move while remaining inside the full-player text viewport.
lua.execute("HD2Walkman.toggle();HD2Walkman.toggle();publish(12,'I Really Want to Stay at Your House');advance(2);click(62,100);advance()")
def title_quads():
 x,y,w,h,k=api.layout()
 return sorted([(c['pos']['x'],c['size']['x'])for c in lua.globals().Draw.values() if c['pos']['z']==12004 and abs(c['pos']['y']-(y+h-356*k-16*(20/48*k)))<.01])
first_title=title_quads();lua.execute("for i=1,25 do ack();publish(12,'I Really Want to Stay at Your House');advance()end")
second_title=title_quads();assert first_title and second_title and first_title!=second_title
x,y,w,h,k=api.layout();assert all(qx>=x+49*k-.01 and qx+qw<=x+311*k+.01 for qx,qw in second_title)
passed('Long titles visibly scroll over time and their cropped glyph quads stay within the text viewport.')
lua.execute("Exited=true;advance();Exited=false;press('toggle');press('toggle');assert(Launches==2)")
passed('An exited host can be relaunched by reopening; a live host is reused.')
lua.execute("local n=Launches;TestMs=TestMs+25000;Words[9]=0;advance();assert(Launches==n)")
passed('An unresponsive live host does not cause duplicate process launches.')
lua.execute("shutdown('done');assert(OldShutdown and Unmapped and Words[4]==1 and Destroyed)")
passed('Shutdown signals the companion, unmaps shared memory, disposes the retained GUI and process handle, and chains the prior hook.')

fresh=LuaRuntime(unpack_returned_tuples=True);fresh.execute(fixture+test_source);fresh.execute("advance();Missing=true;press('toggle');assert(Launches==0 and Words[0]==0)")
passed('A missing companion leaves the player open with installation guidance, and does not create/launch a broken audio bridge.')
guarded=LuaRuntime(unpack_returned_tuples=True);guarded.execute(fixture+native_test_source);guarded.execute("advance();press('toggle');assert(not CursorShown and not Protected and NativeReads==nil);shutdown()")
passed('An unsupported game module fails cursor capture closed before any native memory read or function call.')
failed=LuaRuntime(unpack_returned_tuples=True);failed.execute(fixture+test_source);failed.execute("advance();ProtectFails=true;press('toggle');assert(not CursorShown and not Protected);shutdown()")
passed('Failed input capture keeps the player read-only and restores cursor ownership.')
no_world=LuaRuntime(unpack_returned_tuples=True);no_world.execute(fixture+test_source);no_world.execute("WorldList={game='main'};advance();press('toggle');assert(not CursorShown and not Protected and next(Draw)==nil);WorldList={game='main',hud='ui'};advance();assert(CreatedWorld=='ui'and next(Draw)~=nil and Protected);shutdown()")
passed('No UI world means no cursor capture; the player recovers automatically when a UI world becomes available.')
keyed=LuaRuntime(unpack_returned_tuples=True);keyed.execute(fixture+test_source);keyed.execute("WorldList={game='main',hud='ui'};advance();press('toggle');assert(next(Draw)~=nil and CursorShown and Protected);shutdown()")
passed('Cold-start keyed-world regression: the player visibly renders instead of launching audio into an invisible GUI.')
creation=LuaRuntime(unpack_returned_tuples=True);creation.execute(fixture+test_source);creation.execute("CreationFails=true;advance();press('toggle');assert(next(Draw)==nil and not Protected and Launches==1 and Words[4]==0);CreationFails=false;advance();assert(next(Draw)~=nil and Protected);shutdown()")
passed('A transient GUI creation error does not disable the mod or stop audio; rendering recovers when creation succeeds.')
passed('Every submitted color is created in the current frame; the fixture rejects engine colors retained across frame boundaries.')
def dots(engine):
 return [c for c in engine.globals().Draw.values()if abs(c['uv1']['x']*1024-784)<.01 and abs(c['uv1']['y']*1024-620)<.01]
ring=LuaRuntime(unpack_returned_tuples=True);ring_api=ring.execute(fixture+test_source)
ring.execute("advance();press('toggle');publish(2,'Super Earth National Anthem');advance(3);click(62,100);advance()")
gx,gy,w,h,k=ring_api.layout();before=dots(ring);assert len(before)==32
centers=lambda calls:sorted(((c['pos']['x']+c['size']['x']/2-gx)/k,(gy+h-c['pos']['y']-c['size']['y']/2)/k)for c in calls)
baseline=centers(before)
assert all(abs(math.hypot(cx-180,cy-217)-75)<1e-6 for cx,cy in baseline)
ring.execute("Words[756]=2;Words[757]=1;for i=758,769 do Words[i]=(i%3)*450 end;for i=1,40 do ack();publish(2,'Super Earth National Anthem');update(.016)end")
assert centers(dots(ring))==baseline
cover=Image.open(ROOT/'Source/WalkmanAtlas.png').crop((384,620,580,816))
assert cover.getpixel((0,0))[3]==0 and cover.getpixel((98,98))[3]==0
dot_image=Image.open(ROOT/'Source/WalkmanAtlas.png').crop((784,620,800,636))
assert dot_image.getpixel((0,0))[3]==0 and dot_image.getpixel((8,8))[3]==255
passed('All 32 round dots share a fixed 75-unit circle during changing spectrum levels; ring and dot tiles have transparent backgrounds.')
fallback=LuaRuntime(unpack_returned_tuples=True);fallback.execute(fixture+"stingray.Gui.update_bitmap_uv=nil\n"+test_source)
fallback.execute("advance();press('toggle');publish(12,'I Really Want to Stay at Your House');advance(4);Housing=nil;for id,c in pairs(Draw)do if c.uv1.x==0 and c.uv1.y==400/1024 then Housing=id end end;assert(Housing);local created=BitmapCreates;for i=1,90 do DrawEvents={};ack();publish(12,'I Really Want to Stay at Your House');update(.05);local retired=false;for _,event in ipairs(DrawEvents)do if event=='destroy'then retired=true elseif event=='create'then assert(not retired,'Destroyed old artwork before replacement frame completed')end end;assert(Draw[Housing])end;assert(BitmapCreates-created<3000);shutdown()")
passed('Without bitmap updates, unchanged housing stays alive and replacement artwork is created before old artwork is retired during title scrolling.')
holding=LuaRuntime(unpack_returned_tuples=True);holding.execute(fixture+test_source)
holding.execute("advance();Pressed['julian.walkman.play_pause']=true;advance();for i=1,10 do ack();publish(2,'Track');advance()end;local stable=Words[5];for i=1,20 do ack();publish(2,'Track');advance()end;assert(Words[5]==stable and Launches==1 and not Protected and not CursorShown);Pressed['julian.walkman.play_pause']=false;advance();Pressed['julian.walkman.play_pause']=true;advance();assert(Words[6]==1 and Words[5]==stable+1);shutdown()")
passed('Holding a playback binding triggers once per press, with no repeated toggles or duplicate companion launches.')
assert Image.open(ROOT/'Source/WalkmanAtlas.png').getpixel((180,617))[3]==255
passed('The glass, housing interior, and compact backing are opaque; atlas size remains 4 MiB.')

refined=LuaRuntime(unpack_returned_tuples=True);refined_api=refined.execute(fixture+test_source)
refined.execute("advance();press('toggle');for i=1,8 do ack();publish(9,'Current track');advance()end;HD2Walkman.library();publish(9,'Current track');advance(3)")
def icon_calls(engine,ax,ay):
 return [c for c in engine.globals().Draw.values()if abs(c['uv1']['x']*1024-ax)<.01 and abs(c['uv1']['y']*1024-ay)<.01]
def rendered_line(engine,engine_api,baseline,points):
 x,y,w,h,k=engine_api.layout();target=y+h-baseline*k-16*(points/48*k)
 calls=[c for c in engine.globals().Draw.values()if c['pos']['z']==12004 and abs(c['pos']['y']-target)<.01 and c['uv1']['y']*1024<384]
 return ''.join(chr(32+round(c['uv1']['y']*1024/64)*16+round(c['uv1']['x']*1024/64))for c in sorted(calls,key=lambda c:c['pos']['x']))
assert len(icon_calls(refined,832,620))==1 and len(icon_calls(refined,896,620))==1
assert rendered_line(refined,refined_api,434,13)==''
refined.execute("click(296,104);assert(Words[6]==9);click(62,100)")
assert not icon_calls(refined,832,620) and not icon_calls(refined,960,620)
assert rendered_line(refined,refined_api,536,13)=='PLAYING  /  VOL 50'
passed('The circular refresh control rescans the library, the musical note marks its current track, and the top-left player icon returns to playback without footer text.')

def rgb(c):return tuple(c['color'][key]for key in ('r','g','b'))
refined.execute("point(60,490);update(.016);update(.016)")
assert rgb(icon_calls(refined,704,530)[0])==(238,240,244)
refined.execute("MousePressed.left=true;update(.016);MousePressed.left=false;update(.016)")
assert refined.globals().Words[17]==0 # Host has not published the new shuffle value.
assert rgb(icon_calls(refined,704,530)[0])==(242,211,155)
def selected_dots(engine,engine_api):
 x,y,w,h,k=engine_api.layout()
 return [c for c in icon_calls(engine,784,620)if abs(c['pos']['y']-(y+h-514*k))<.01]
assert len(selected_dots(refined,refined_api))==1
refined.execute("point(180,400);update(.016);update(.016)")
assert rgb(icon_calls(refined,704,530)[0])==(216,180,112)
refined.execute("ack();advance();click(60,490)")
assert rgb(icon_calls(refined,704,530)[0])==(238,240,244) and not selected_dots(refined,refined_api)
passed('Hover, enabled and enabled-hover colors are distinct; clicking shuffle redraws its pending state before host acknowledgement and its persistent dot clears when disabled.')
for mode in (1,2,0):
 refined.execute("ack();advance();click(297,490)")
 assert len(icon_calls(refined,832 if mode==2 else 768,530))==1
 assert len(selected_dots(refined,refined_api))==(1 if mode else 0)
passed('Repeat cycles through library, track and off with the matching icon and a persistent selected indicator, even while the pointer remains over the control.')
refined.execute("HD2Walkman.library();publish(0,'No tracks');advance(3)")
assert rendered_line(refined,refined_api,434,13)=='' and not icon_calls(refined,896,620)
assert rendered_line(refined,refined_api,536,13)=='PLAYING  /  VOL 50'
refined.execute('shutdown()')
passed('The empty library retains a clear return control; playback status sits six design units lower than before.')
assert 'YOUR MUSIC. YOUR MISSION.' not in source and 'LOCAL MUSIC PLAYER' not in source
startup=LuaRuntime(unpack_returned_tuples=True);startup_api=startup.execute(fixture+test_source)
startup.execute("advance();press('toggle')")
assert startup_api.inspect()[0:4]==('library','Super Earth National Anthem',2,0)
startup.execute("publish(9,'Super Earth National Anthem');Words[10]=2;Words[14]=0;advance(3);click(62,100);advance()")
assert startup_api.inspect()[0:4]==('player','Super Earth National Anthem',2,0)
assert rendered_line(startup,startup_api,536,13)=='PAUSED  /  VOL 50'
assert startup.globals().Words[6] != 1
startup.execute("press('toggle');press('toggle');advance()")
assert startup_api.inspect()[0]=='library'
passed('Startup and reopening show the library; returning immediately shows the selected paused anthem, without a splash screen or a play command.')
scroll=LuaRuntime(unpack_returned_tuples=True);scroll_api=scroll.execute(fixture+test_source)
scroll.execute("advance();press('toggle');publish(1000,'Super Earth National Anthem');advance(3);publish(1000,'Super Earth National Anthem');advance(3);point(170,200);MousePressed.wheel_down=true;update(.016);MousePressed.wheel_down=false;update(.03);update(.03)")
state=scroll_api.inspect();assert 0<state[6]<3 and state[7]==3 and state[4]<=24 and state[5]<=19
assert rendered_line(scroll,scroll_api,260,16) not in ('Loading tracks...','Preparing library...')
scroll.execute('advance()');assert abs(scroll_api.inspect()[6]-3)<1e-9
passed('Cached wheel scrolling interpolates row positions, remains populated during movement, and settles exactly on the requested window.')
# Far jumps retain the last complete window until its destination arrives.
scroll.execute("point(309,438);MousePressed.left=true;MouseDown.left=true;update(.016);MousePressed.left=false;update(.016);MouseDown.left=false;advance()")
state=scroll_api.inspect();assert state[7]==991 and state[6]==3 and state[5]>=9
assert rendered_line(scroll,scroll_api,260,16) not in ('Loading tracks...','Preparing library...')
scroll.execute("fill(1000,'Super Earth National Anthem')")
assert scroll_api.inspect()[6]==991
for step in range(30):
 scroll.execute("HD2Walkman.library();point(170,200);MousePressed.wheel_down=true;update(.016);MousePressed.wheel_down=false;advance();publish(1000,'Super Earth National Anthem');advance(3);publish(1000,'Super Earth National Anthem');advance(3)")
 assert scroll_api.inspect()[4]<=24 and scroll_api.inspect()[5]<=19
passed('Large scrollbar jumps retain existing rows while waiting; repeated navigation keeps the cache at 24 titles and transition rows at 19 or fewer.')
scroll.execute("publish(1000,'Super Earth National Anthem');Words[23]=8;advance(3)")
if scroll_api.inspect()[8] != 8:
 scroll.execute('for i=1,8 do ack();advance()end');before=int(scroll.globals().Words[5]);scroll.execute('click(100,155)');assert int(scroll.globals().Words[5])==before
passed('Library generations invalidate cached identities, so retained rows cannot issue stale song-selection commands after a refresh.')
# Header controls mirror one another at equal size and vertical alignment.
startup.execute("fill(9,'Super Earth National Anthem');advance(3)")
icon=icon_calls(startup,960,620)[0];refresh=icon_calls(startup,832,620)[0]
x,y,w,h,k=startup_api.layout()
assert abs((icon['pos']['x']+icon['size']['x']/2-x)/k-62)<1e-6
assert abs((refresh['pos']['x']+refresh['size']['x']/2-x)/k-298)<1e-6
assert abs(icon['pos']['y']-refresh['pos']['y'])<1e-6
assert abs(icon['size']['x']/k-22)<1e-6 and abs(refresh['size']['x']/k-22)<1e-6
startup.execute('point(62,100);update(.016);update(.016)')
assert rendered_line(startup,startup_api,126,10)=='PLAYER'
startup.execute('point(180,112);update(.016);update(.016)')
assert rendered_line(startup,startup_api,126,10)==''
passed('The player and refresh icons mirror each other in the header; a Player hover label identifies the icon without occupying a song row.')
rows=LuaRuntime(unpack_returned_tuples=True);rows_api=rows.execute(fixture+test_source)
rows.execute("advance();press('toggle');fill(9,'Super Earth National Anthem')")
assert rows_api.inspect()[7]==0
for index in range(9):assert rendered_line(rows,rows_api,155+index*34,16)=='Track '+str(index+1)
rows.execute('click(100,427)');assert rows.globals().Words[6]==4 and rows.globals().Words[7]==8
passed('All nine songs fit at their original text size; clicking the ninth row selects absolute track index eight through the unchanged audio bridge.')
rows.execute("HD2Walkman.library();fill(1,'Super Earth National Anthem');point(170,427);MousePressed.wheel_down=true;update(.016);MousePressed.wheel_down=false;advance()")
assert rows_api.inspect()[7]==0
# Moving back must prefetch behind the viewport and then settle its requests.
backward=LuaRuntime(unpack_returned_tuples=True);backward_api=backward.execute(fixture+test_source)
backward.execute("advance();press('toggle');fill(1000,'Song');point(170,200)")
for step in range(6):backward.execute("MousePressed.wheel_down=true;update(.016);MousePressed.wheel_down=false;fill(1000,'Song')")
assert backward_api.inspect()[7]==18
backward.execute("MousePressed.wheel_up=true;update(.016);MousePressed.wheel_up=false;fill(1000,'Song')")
assert backward_api.inspect()[7]==15 and backward_api.inspect()[6]==15
request=int(backward.globals().Words[21]);backward.execute("fill(1000,'Song')")
assert int(backward.globals().Words[21])==request and backward_api.inspect()[4]<=24
backward.execute('shutdown()')
passed('Forward and backward scrolling prefetch in the direction of travel and settle without cycling between unnecessary page requests.')
rows.execute('shutdown()')
passed('Libraries smaller than nine rows stay at the top; scrollbar and wheel bounds use the new visible capacity.')
startup.execute("Words[21]=0;publish(9,'Super Earth National Anthem');Words[23]=11;Words[10]=2;Words[14]=0;ffi.fill(Memory+544,128);ffi.copy(Memory+544,'Mandated Track',14);ffi.fill(Memory+672,256);ffi.copy(Memory+672,'Super Earth National Anthem',27);ffi.fill(Memory+928,256);ffi.copy(Memory+928,'I Really Want to Stay at Your House',35);advance(3);Words[21]=1;publish(9,'Super Earth National Anthem');Words[23]=11;Words[10]=2;Words[14]=0;ffi.fill(Memory+544,128);ffi.copy(Memory+544,'Mandated Track',14);ffi.fill(Memory+672,256);ffi.copy(Memory+672,'I Really Want to Stay at Your House',35);advance(3)")
render(ROOT/'Notes/Startup_Library_Preview.png',startup,startup_api)
startup.execute('click(62,100);advance()')
render(ROOT/'Notes/Paused_Player_Preview.png',startup,startup_api)
startup.execute('shutdown()');scroll.execute('shutdown()')
# Reproduce a library left open while playback advances without a row click.
playing=LuaRuntime(unpack_returned_tuples=True);playing_api=playing.execute(fixture+test_source)
playing.execute("TrackNames={[3]='An older song with a title long enough to scroll in the library',[5]='The current shuffled song also has a long scrolling title'};advance();press('toggle');fill_current(9,3,TrackNames[3]);HD2Walkman.library();advance(3);point(180,112);update(.016);update(.016)")
def highlights(engine,engine_api,color):
 x,y,w,h,k=engine_api.layout()
 return [round((y+h-c['pos']['y']-c['size']['y'])/k,6) for c in engine.globals().Draw.values()if c['uv1']['x']*1024==1021 and c['uv1']['y']*1024==391 and rgb(c)==color]
assert highlights(playing,playing_api,(55,47,32))==[155+3*34-23]
playing.execute("fill_current(9,5,TrackNames[5]);point(180,112);advance(3)")
assert highlights(playing,playing_api,(55,47,32))==[155+5*34-23]
passed('A confirmed queue/shuffle advance moves the gold highlight to the current song, without needing a row click or reopening the library.')
def row_glyphs(engine,engine_api,index):
 x,y,w,h,k=engine_api.layout();t=155+index*34;baseline=y+h-t*k-16*(16/48*k)
 return sorted((c['pos']['x'],c['size']['x'],rgb(c))for c in engine.globals().Draw.values()if c['pos']['z']==12004 and c['uv1']['y']*1024<384 and abs(c['pos']['y']-baseline)<.01)
start=row_glyphs(playing,playing_api,5)
playing.execute("for i=1,6 do ack();publish_current(9,5,TrackNames[5]);advance(2)end")
finish=row_glyphs(playing,playing_api,5)
assert start and finish and start!=finish and all(c[2]==(216,180,112) for c in finish)
assert all(c[2]==(238,240,244) for c in row_glyphs(playing,playing_api,3))
passed('The new current song starts its own gold title marquee; the previously selected row stops scrolling and returns to its normal color.')
render(ROOT/'Notes/Playback_Follow_Preview.png',playing,playing_api)
playing.execute('point(170,257);update(.016);update(.016)')
assert highlights(playing,playing_api,(55,47,32))==[155+5*34-23]
assert highlights(playing,playing_api,(38,40,44))
passed('Hovering a different row uses a neutral highlight, so gold identifies the current song consistently.')
playing.execute("point(180,112);fill_current(1000,500,'Another current song');advance(3)")
assert playing_api.inspect()[7]==497 and playing_api.inspect()[6]==497
playing.execute("point(170,200);MousePressed.wheel_down=true;update(.016);MousePressed.wheel_down=false;fill_current(1000,500,'Another current song');MousePressed.wheel_down=true;update(.016);MousePressed.wheel_down=false;fill_current(1000,500,'Another current song')")
assert playing_api.inspect()[7]==503
playing.execute("fill_current(1000,500,'Another current song')")
assert playing_api.inspect()[7]==503
passed('A song outside the nine visible rows is brought into view once; unchanged playback snapshots preserve subsequent manual library scrolling.')
playing.execute("fill_current(1000,25,'Another current song')")
assert playing_api.inspect()[7]==22
passed('Track changes are identified by their index as well as title, so songs with matching display names still move the library focus.')
playing.execute('shutdown()')
(ROOT/'Notes/Validation.json').write_text(json.dumps({'passed':results,'not_executed':['Live Helldivers 2 loader discovery, UI world ordering, keybinding behavior','Windows Media Engine COM/runtime and actual codec playback','Windows installer and removal scripts','Lossless Scaling capture, frame generation and pacing','CPU, GPU, RAM, VRAM and FPS benchmarks'],'test_boundary':'Lua runs in LuaJIT with substituted Win32/game APIs; archive and UI bitmap data are real.'},indent=2)+'\n')
