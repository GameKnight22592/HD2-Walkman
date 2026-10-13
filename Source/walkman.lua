-- HD2-Addon: mods/julian/walkman
-- Walkman v0.3.7: native game GUI and local audio bridge.
if rawget(_G,'HD2Walkman') and HD2Walkman.api==1 then return HD2Walkman end
local ffi=require('ffi')
local bit=require('bit')
local sr=rawget(_G,'stingray') or _G
local old_update=assert(rawget(_G,'update'),'Walkman requires Bingus Shared Loader')
local api={api=1}
local function log(value)
 local root=os.getenv('LOCALAPPDATA');local f=root and io.open(root..'/CowboyBingus/Helldivers2/Logs/Walkman.log','a')
 if f then f:write(os.date('%H:%M:%S '),tostring(value),'\n');f:close()end
end
local kernel=ffi.load('kernel32')
local K={}
for _,d in ipairs({
 {'GetCurrentProcess','void *','(void)'}, {'GetModuleHandleA','void *','(const char *)'},
 {'ReadProcessMemory','int','(void *,const void *,void *,size_t,size_t *)'},
 {'GetCurrentProcessId','uint32_t','(void)'}, {'GetTickCount','uint32_t','(void)'},
 {'GetEnvironmentVariableW','uint32_t','(const uint16_t *,uint16_t *,uint32_t)'},
 {'GetFileAttributesW','uint32_t','(const uint16_t *)'}, {'GetLastError','uint32_t','(void)'},
 {'CreateFileMappingA','void *','(void *,void *,uint32_t,uint32_t,uint32_t,const char *)'},
 {'MapViewOfFile','void *','(void *,uint32_t,uint32_t,uint32_t,size_t)'},
 {'UnmapViewOfFile','int','(const void *)'}, {'CloseHandle','int','(void *)'},
 {'WaitForSingleObject','uint32_t','(void *,uint32_t)'},
 {'CreateProcessW','int','(const uint16_t *,uint16_t *,void *,void *,int,uint32_t,void *,const uint16_t *,void *,void *)'}
})do
 local private='hd2Walkman1_'..d[1]
 pcall(ffi.cdef,d[2]..' '..private..d[3]..' __asm__("'..d[1]..'");');K[d[1]]=ffi.cast(d[2]..' (*)'..d[3],kernel[private])
end
local process=K.GetCurrentProcess()
local buffer=ffi.new('uint8_t[64]')
local got=ffi.new('size_t[1]')
local game
local function read(at,n)
    if not at or at<65536 or at>=0x800000000000 or n>64 then return end
    got[0]=0
    if K.ReadProcessMemory(process,ffi.cast('const void *',at),buffer,n,got)~=0 and tonumber(got[0])==n then return ffi.string(buffer,n) end
end
local function u32(s,o)
    local a,b,c,d=s:byte(o+1,o+4)
    return a+b*256+c*65536+d*16777216
end
local function ptr(at)
    local s=read(at,8)
    if s then return u32(s,0)+u32(s,4)*4294967296 end
end
local function map_index(owner,id,offset)
    if not owner then return end
    local at=owner+offset
    local b=read(at,20)
    if not b then return end
    local count,empty,mult=u32(b,8),u32(b,12),u32(b,16)
    if count==0 or count>1048576 or bit.band(count,count-1)~=0 or id==empty then return end
    local buckets=ptr(at)
    if not buckets then return end
    local product=((id%65536)*(mult%65536)+((math.floor(id/65536)*(mult%65536)+(id%65536)*math.floor(mult/65536))%65536)*65536)%4294967296
    for i=0,math.min(count,128)-1 do
        local item=read(buckets+((product+i)%count)*8,8)
        if not item then return end
        local key=u32(item,0)
        if key==id then local index=u32(item,4);if index<1048576 then return index end;return end
        if key==empty then return end
    end
end

local function field(at)
    local b=read(at,4)
    return b and u32(b,0)
end

local input_verified
local input_unblock_type=ffi.typeof('void (*)(void *, uint64_t)')
local input_inhibit_type=ffi.typeof('void (*)(void *, uint64_t, uint32_t, float)')
local input_guards={{0x12fd250,'48895c240848896c2410488974241848897c24204154415641574883ec20488bf1458be0488bca488bdae8018928ff448b86489b0a0033ff8bae509b0a008bcf440fb7c88bc3c1e010440bc8458d78ff410fafe94585c0743d4c8b96409b0a00448b9e4c9b0a00660f1f840000000000418bc78d14294823d0418b04d2413bc30f8487000000413bc10f8483000000ffc1413bc872da448b86347d0a00488d8e409b0a00418bd1418d40018986347d0a004b8d2c40e8862443000f57c048899cee3c7d0a000f2fc34489a4ee387d0a007714f30f591d96ab0c01f3480f2cfb4803be287d0a004889bcee487d0a00488b5c2440488b6c2448488b742450488b7c24584883c420415f415e415cc3413bc17584418b44d2040f57c00f2fc3488d0c404489a4ce387d0a007723488b84ce487d0a00f30f591d35ab0c01f3480f2cfb4803be287d0a00483bc7480f47f84889bcce487d0a00eb96','ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffffff'},{0x12fd3b0,'48896c2418564883ec204c8bd94c8bca488bcae8b88728ff418bb3509b0a0041c1e110440fb7d033c0450bd1458b8b489b0a00410faff2418d69ff4585c9746448895c2430498b9b409b0a0048897c2438418bbb4c9b0a000f1f8400000000008bcd8d14304823d18b0cd34c8d04d33bcf740e413bca740effc0413bc172e1eb19413bca7514418b4004488d0c4041c784cb387d0a0000000000488b5c2430488b7c2438488b6c24404883c4205ec3','ffffffffffffffffffffffffffffffffffffffff00000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff'}}
local input_actions={
    {id=ffi.new('uint64_t',38654705666),key=0x20009,group=2,index=9},
    {id=ffi.new('uint64_t',42949672961),key=0x1000a,group=1,index=10}
}
local function input_verify()
    if input_verified then return true end
    if not game then game=tonumber(ffi.cast('uintptr_t',K.GetModuleHandleA('game.dll')))end
    if not game or game<65536 then game=nil;return false end
    for _,g in ipairs(input_guards)do
        local length=#g[2]/2
        for start=0,length-1,32 do
            local bytes=read(game+g[1]+start,math.min(32,length-start))
            if not bytes then return false end
            for i=1,#bytes do
                local p=(start+i-1)*2+1
                if g[3]:sub(p,p+1)=='ff' and bytes:byte(i)~=tonumber(g[2]:sub(p,p+1),16) then return false end
            end
        end
    end
    input_verified=true;return true
end
local function input_state(action)
    local owner=game and ptr(game+0x347cf18)
    local count=owner and field(owner+0xa7d34)
    local header=owner and read(owner+0xa9b40,20)
    if not count or count>320 or not header or u32(header,8)>1024 then return end
    local index=map_index(owner,action.key,0xa9b40)
    if index and index>=count then return end
    local bytes=index and read(owner+0xa7d38+index*24,24)
    if index and not bytes then return end
    if bytes and (u32(bytes,4)~=action.group or u32(bytes,8)~=action.index) then return end
    if ptr(game+0x347cf18)~=owner then return end
    return owner,count,bytes
end
local function input_action(active,action)
    if not active then
        if not action.lease then return true end
        local owner,_,bytes=input_state(action)
        if owner==action.lease.owner and bytes==action.lease.bytes then
            if not input_verify() then return false end
            local unblock=ffi.cast(input_unblock_type,game+0x12fd3b0)
            unblock(ffi.cast('void *',owner),action.id)
            local after,_,state=input_state(action)
            if after~=owner or not state or u32(state,0)~=0 then return false end
        end
        action.lease=nil;return true
    end
    if not input_verify() then return false end
    local owner,count,bytes=input_state(action)
    if not owner then return false end
    if action.lease then
        if owner==action.lease.owner and bytes==action.lease.bytes then return true end
        if not input_action(false,action) then return false end
        owner,count,bytes=input_state(action)
        if not owner then return false end
    end
    if bytes and u32(bytes,0)~=0 then return true end
    if not bytes and count>=320 then return false end
    local inhibit=ffi.cast(input_inhibit_type,game+0x12fd250)
    inhibit(ffi.cast('void *',owner),action.id,1,-1.0)
    local after,_,state=input_state(action)
    if after~=owner or not state or u32(state,0)~=1 or state:sub(17,24)~=string.rep('\0',8) then return false end
    action.lease={owner=owner,bytes=state}
    return true
end

local function protect_input(active)
    local ok=true
    for _,action in ipairs(input_actions)do
        if not input_action(active,action) then ok=false end
    end
    if not ok and active then
        for _,action in ipairs(input_actions)do input_action(false,action)end
    end
    return ok
end

local function wide(s)local out=ffi.new('uint16_t[?]',#s+1);for i=1,#s do out[i-1]=s:byte(i)end;return out end
local function append(folder,s)
 local n=0;while n<32767 and folder[n]~=0 do n=n+1 end
 assert(n+#s<32768,'Walkman path is too long');local out=ffi.new('uint16_t[?]',n+#s+1);ffi.copy(out,folder,n*2)
 for i=1,#s do out[n+i-1]=s:byte(i)end;return out
end
local function ms()return tonumber(K.GetTickCount())end
do
 local a=sr.Application
 log('Walkman v0.3.7 audio API: user_setting='..tostring(type(a.user_setting))..', set_user_setting='..tostring(type(a.set_user_setting))..', Wwise='..tostring(type(sr.Wwise))..', WwiseWorld='..tostring(type(sr.WwiseWorld)))
 if type(a.user_setting)=='function'then local ok,value=pcall(a.user_setting,'music_volume');log('Game music_volume setting: '..(ok and tostring(value)or 'unavailable'))end
end
local function elapsed(now,then_)return (now-then_)%4294967296 end
local pid=tonumber(K.GetCurrentProcessId())
local folder=ffi.new('uint16_t[32768]')
local folder_length=tonumber(K.GetEnvironmentVariableW(wide('LOCALAPPDATA'),folder,32768))
if folder_length>0 and folder_length<32768 then folder=append(folder,'\\HD2Walkman')else folder=nil end
local map,memory,words,host,launched_at
local last_volume,last_shuffle,last_repeat
local pending,command_seq={},0
local snapshot={count=0,index=0,position=0,duration=0,volume=50,shuffle=0,repeat_mode=0,state=2,title='Super Earth National Anthem',folder='Mandated Track',error='',first=0,generation=0,rows={}}
local error_text,next_sync= nil,0
local function read_text(offset,length)
 local s=ffi.string(memory+offset,length);local zero=s:find('\0',1,true);return zero and s:sub(1,zero-1)or s
end
local function create_map()
 if map then return true end
 map=K.CreateFileMappingA(ffi.cast('void *',-1),nil,4,0,4096,'Local\\HD2Walkman_'..pid)
 if map==nil then map=nil;error_text='Could not create audio bridge';return false end
 memory=K.MapViewOfFile(map,0xf001f,0,0,4096)
 if memory==nil then K.CloseHandle(map);map=nil;memory=nil;error_text='Could not map audio bridge';return false end
 memory=ffi.cast('uint8_t *',memory);ffi.fill(memory,4096);words=ffi.cast('uint32_t *',memory)
 words[0]=0x574B4D31;words[1]=1;words[2]=pid;words[3]=ms();return true
end
local function launch()
 if not folder then error_text='Windows user data folder is unavailable';return false end
 if host then
  if K.WaitForSingleObject(host,0)~=0 then return true end
  K.CloseHandle(host);host=nil;error_text='Audio companion exited. Reopen to retry.'
 end
 local path=append(folder,'\\WalkmanAudio.exe');local attr=tonumber(K.GetFileAttributesW(path))
 if attr==0xffffffff or bit.band(attr,16)~=0 then error_text='Install Companion, then reopen Walkman';return false end
 if not create_map()then return false end
 words[9]=0;words[8]=words[5];words[4]=0
 local n=0;while path[n]~=0 do n=n+1 end
 local suffix='" --game '..pid;local cmd=ffi.new('uint16_t[?]',n+#suffix+2);cmd[0]=34;ffi.copy(cmd+1,path,n*2);ffi.copy(cmd+n+1,wide(suffix),(#suffix+1)*2)
 local startup=ffi.new('uint8_t[104]');ffi.cast('uint32_t *',startup)[0]=104;ffi.cast('uint32_t *',startup+60)[0]=1
 local info=ffi.new('uint8_t[24]')
 if K.CreateProcessW(path,cmd,nil,nil,0,0x08000000,nil,folder,startup,info)==0 then error_text='Audio launch failed: '..tonumber(K.GetLastError());return false end
 local handles=ffi.cast('void **',info);host=handles[0];if handles[1]~=nil then K.CloseHandle(handles[1])end
 last_volume=nil;last_shuffle=nil;last_repeat=nil;launched_at=ms();error_text='Starting audio companion...';log('Audio companion launched');return true
end
local function send(op,arg)
 if #pending>=16 then error_text='Audio controls busy; try again';return false end
 if not launch()then return false end
 if op==5 or op==6 or op==7 or op==8 then for i=#pending,1,-1 do if pending[i][1]==op then table.remove(pending,i)end end end
 pending[#pending+1]={op,arg or 0,op==4 and snapshot.generation or 0};return true
end
local opened,page,selection=false,'library',0
local size,xpos,ypos,mini=1,100,10,false
local visualizer=true
local now,next_menu=0,0
local reveal,screen_motion,scroll_first=0,1,0
local gui,gui_world,ids,used,draw_key=nil,nil,{},{},nil
local retired,serial={},0
local cursor_before,captured,input_warning
local hover,drag,mouse_x,mouse_y=nil,nil,-1,-1
local levels={0,0,0,0,0,0,0,0,0,0,0,0}
local spectrum_ready=0
local marquee_starts={}
local function cursor_restore()
 if cursor_before~=nil and sr.Window and sr.Window.set_show_cursor then pcall(sr.Window.set_show_cursor,cursor_before);cursor_before=nil end
end
local function capture(active)
 if active then
  if not gui then protect_input(false);captured=false;cursor_restore();return false end
  local ok=protect_input(true)
  if not ok then protect_input(false);captured=false;cursor_restore();input_warning='Cursor unavailable on this game build';return false end
  captured=true;input_warning=nil
  local W=sr.Window
  if cursor_before==nil and W and W.show_cursor and W.set_show_cursor then
   local success,value=pcall(W.show_cursor)
   if success and type(value)=='boolean'then cursor_before=value;if not value then pcall(W.set_show_cursor,true)end end
  end
  return true
 end
 protect_input(false);captured=false;cursor_restore();drag=nil
end
local function clear()
 if gui then for _,record in pairs(ids)do pcall(sr.Gui.destroy_bitmap,gui,record.id)end;for _,id in ipairs(retired)do pcall(sr.Gui.destroy_bitmap,gui,id)end end
 retired={};used={}
 ids={};draw_key=nil
end
local function reset_gui()
 local alive=false;if gui_world then for _,w in pairs(sr.Application.worlds())do if w==gui_world then alive=true end end end
 if not alive then gui=nil end
 clear();if gui and sr.World.destroy_gui then pcall(sr.World.destroy_gui,gui_world,gui)end;gui=nil;gui_world=nil
end
local function layout()
 local sw,sh=sr.Gui.resolution();local rh=mini and page~='library'and 112 or 620
 local k=math.min(sh*.78/rh,sw*.38/360,size*.80*sh/1080)
 local w,h=360*k,rh*k;local margin=math.min(16,sw*.02,sh*.02)
 local x=margin+(sw-w-2*margin)*xpos/100
 local y=margin+(sh-h-2*margin)*(1-ypos/100)
 -- The device slides in from the nearest horizontal edge, without fading its housing.
 local eased=1-(1-reveal)^3
 if xpos>=50 then x=x+(1-eased)*(sw+margin-x)else x=x-(1-eased)*(x+w+margin)end
 return x,y,w,h,k
end
local material='mods/julian/walkman/ui/atlas/material'
local glyph_advance={15.266,19.250,22.078,40.219,30.547,45.609,37.438,13.203,18.734,18.734,24.000,40.219,15.266,17.328,15.266,16.172,30.547,30.547,30.547,30.547,30.547,30.547,30.547,30.547,30.547,30.547,16.172,16.172,40.219,40.219,40.219,25.484,48.000,32.844,32.938,33.516,36.969,30.328,27.609,37.203,36.094,14.156,14.156,31.484,26.750,41.422,35.906,37.781,28.953,37.781,33.359,30.469,29.328,35.141,32.844,47.469,32.891,29.328,32.891,18.734,16.172,18.734,40.219,24.000,24.000,29.422,30.469,26.391,30.469,29.531,16.906,30.469,30.422,13.344,13.344,27.797,13.344,46.766,30.422,29.375,30.469,30.469,19.734,25.016,18.828,30.422,28.406,39.266,28.406,28.406,25.203,30.547,16.172,30.547,40.219,28.812}
-- Engine colors/vectors are frame-local temporaries. Persist plain Lua numbers only.
local function rgba(a,r,g,b)return{a,r,g,b}end
local white=rgba(255,238,240,244)
local gold=rgba(255,216,180,112)
local muted=rgba(255,161,170,181)
local neutral=rgba(255,255,255,255)
local dot_dim=rgba(255,88,75,51)
local update_bitmaps=type(sr.Gui.update_bitmap_uv)=='function'
local function tile(ax,ay,aw,ah,x,y,w,h,z,color,key)
 if w<=0 or h<=0 then return end
 serial=serial+1;key=key or 'shape:'..serial;used[key]=true;color=color or neutral
 local values={ax,ay,aw,ah,x,y,w,h,z,color[1],color[2],color[3],color[4]}
 local record=ids[key];local same=record~=nil
 if same then for i=1,13 do if values[i]~=record.values[i]then same=false;break end end end
 if same then return end
 local uv0,uv1=sr.Vector2(ax/1024,ay/1024),sr.Vector2((ax+aw)/1024,(ay+ah)/1024)
 local pos,sz=sr.Vector3(x,y,z),sr.Vector2(w,h)
 local native_color=sr.Color(color[1],color[2],color[3],color[4])
 if record and update_bitmaps then
  local ok=pcall(sr.Gui.update_bitmap_uv,gui,record.id,material,uv0,uv1,pos,sz,native_color)
  if ok then record.values=values;return end
  update_bitmaps=false;log('GUI bitmap updates unavailable; using bounded redraw fallback')
 end
 -- Build replacements before retiring the previous frame; unchanged artwork stays alive.
 local id=sr.Gui.bitmap_uv(gui,material,uv0,uv1,pos,sz,native_color)
 if record then retired[#retired+1]=record.id end
 ids[key]={id=id,values=values}
end
local function width(value,points)
 local n=0;for i=1,#value do n=n+(glyph_advance[value:byte(i)-31]or 29)*points/48 end;return n
end
local function text(value,x,y,points,k,z,color,clip,key)
 value=tostring(value or ''):gsub('[^\32-\126]','?')
 local scale=points/48*k
 for i=1,#value do
  local c=value:byte(i)-32
  if c>=0 and c<96 then
   local gx,gy,gw,gh=x,y-16*scale,64*scale,64*scale
   local l,r,b,t=0,0,0,0
   if clip then l=math.max(0,clip[1]-gx);r=math.max(0,gx+gw-clip[3]);b=math.max(0,clip[2]-gy);t=math.max(0,gy+gh-clip[4])end
   if l+r<gw and b+t<gh then
    tile((c%16)*64+l/scale,math.floor(c/16)*64+t/scale,64-(l+r)/scale,64-(b+t)/scale,gx+l,gy+b,gw-l-r,gh-b-t,z,color or white,key..':'..i)
   end
   x=x+(glyph_advance[c+1]or 29)*scale
  end
 end
end
local function marquee_offset(key,value,points,available)
 local overflow=math.max(0,width(value,points)-available)
 if overflow==0 then return 0 end
 local record=marquee_starts[key]
 if not record or record.value~=value then record={value=value,time=now};marquee_starts[key]=record end
 local travel=overflow/27;local phase=(now-record.time)%(travel*2+2.4)
 if phase<1.2 then return 0 end
 if phase<1.2+travel then return (phase-1.2)*27 end
 if phase<2.4+travel then return overflow end
 return overflow-(phase-2.4-travel)*27
end
local function clock(value)local seconds=math.floor(value/1000);return math.floor(seconds/60)..':'..string.format('%02d',seconds%60)end
local boxes={}
local function in_box(box,u,t)return u>=box.x and u<=box.x+box.w and t>=box.y and t<=box.y+box.h end
local function clamp(v,lo,hi)return math.max(lo,math.min(hi,v))end
-- Nine displayed rows share the existing eight-title bridge pages.
local PAGE_ROWS,LIBRARY_ROWS,LIBRARY_TOP,LIBRARY_HEIGHT=8,9,130,314
local LIBRARY_BOTTOM=LIBRARY_TOP+LIBRARY_HEIGHT
-- Three cached pages keep scroll work independent of library size.
local title_pages,cache_generation={},-1
local visible_rows,visible_generation={},-1
local scroll_visual,scroll_from,scroll_goal,scroll_started=0,0,0,-1
local library_direction=1
local function cached_title(index)
 for _,p in ipairs(title_pages)do
  local row=index-p.first+1
  if row>=1 and row<=PAGE_ROWS and p.rows[row] and p.rows[row]~=''then return p.rows[row]end
 end
end
local function cache_snapshot(s)
 if cache_generation~=s.generation then title_pages={};cache_generation=s.generation end
 if s.count==0 then visible_rows={};visible_generation=s.generation;scroll_visual=0;scroll_goal=0;return end
 if not s.rows[1]or s.rows[1]==''then return end
 for i=#title_pages,1,-1 do if title_pages[i].first==s.first then table.remove(title_pages,i)end end
 title_pages[#title_pages+1]={first=s.first,rows=s.rows}
 if #title_pages>3 then
  local farthest,distance=1,-1
  for i,p in ipairs(title_pages)do local d=math.abs(p.first-scroll_first);if i<#title_pages and d>distance then farthest=i;distance=d end end
  table.remove(title_pages,farthest)
 end
end
local function page_first(index)return clamp(math.floor(index/PAGE_ROWS)*PAGE_ROWS,0,math.max(0,snapshot.count-PAGE_ROWS))end
local function title_request()
 if page~='library'then return 0 end
 for index=scroll_first,math.min(scroll_first+LIBRARY_ROWS-1,snapshot.count-1)do if not cached_title(index)then return page_first(index)end end
 -- Prefetch one page in the direction of travel, without oscillating requests.
 local edge=library_direction<0 and math.floor(scroll_first/PAGE_ROWS)*PAGE_ROWS-PAGE_ROWS or math.floor((scroll_first+LIBRARY_ROWS-1)/PAGE_ROWS)*PAGE_ROWS+PAGE_ROWS
 local first=clamp(edge,0,math.max(0,snapshot.count-PAGE_ROWS))
 for index=first,math.min(first+PAGE_ROWS-1,snapshot.count-1)do if not cached_title(index)then return first end end
 return snapshot.first
end
local function prepare_rows()
 local target={}
 for index=scroll_first,math.min(scroll_first+LIBRARY_ROWS-1,snapshot.count-1)do
  local value=cached_title(index)
  if not value then
   if visible_generation~=snapshot.generation and next(target)then visible_rows=target;visible_generation=snapshot.generation;scroll_visual=scroll_first;scroll_goal=scroll_first;scroll_started=-1 end
   return
  end
  target[index]=value
 end
 if visible_generation~=snapshot.generation then
  visible_rows=target;visible_generation=snapshot.generation;scroll_visual=scroll_first;scroll_goal=scroll_first;scroll_started=-1
 elseif scroll_goal~=scroll_first then
  local from=scroll_visual
  if math.abs(scroll_first-from)<=LIBRARY_ROWS then
   local lo,hi=math.floor(from),math.floor(from)+LIBRARY_ROWS
   for index=lo,hi do if visible_rows[index]then target[index]=target[index]or visible_rows[index]end end
   scroll_from=from;scroll_started=now
  else scroll_from=scroll_first;scroll_started=-1 end
  visible_rows=target;scroll_goal=scroll_first
 end
 if scroll_started>=0 then
  local fraction=clamp((now-scroll_started)/.18,0,1)
  scroll_visual=scroll_from+(scroll_goal-scroll_from)*(1-(1-fraction)^3)
  if fraction==1 then scroll_started=-1;visible_rows=target end
 else scroll_visual=scroll_goal;visible_rows=target end
end
local function option(key,value)
 local m=rawget(_G,'ModOptionsMenu');if m and m.set then pcall(m.set,'julian.walkman.'..key,value)end
end
local function change_page(value)
 if page~=value then page=value;screen_motion=0;marquee_starts={};drag=nil end
end
function api.toggle()
 opened=not opened;hover=nil;drag=nil
 if opened then launch();api.library();screen_motion=0;marquee_starts={} else capture(false)end
 option('open',opened)
end
function api.compact()
 mini=not mini;change_page('player');screen_motion=0;option('mini',mini)
 if opened then capture(not mini)end
end
function api.play_pause()return send(1)end
function api.next()return send(2)end
function api.previous()return send(3)end
function api.shuffle()
 local current=last_shuffle~=nil and last_shuffle or snapshot.shuffle
 local value=current==0 and 1 or 0
 if not send(6,value)then return false end
 last_shuffle=value;option('shuffle',value==1);return true
end
function api.repeat_mode()
 local current=last_repeat~=nil and last_repeat or snapshot.repeat_mode
 local value=(current+1)%3
 if not send(7,value)then return false end
 last_repeat=value;option('repeat',value~=0);return true
end
function api.seek(seconds)if type(seconds)~='number'or seconds~=seconds then return false end;return send(8,math.floor(math.max(0,math.min(snapshot.duration,snapshot.position+seconds*1000))))end
function api.library()
 mini=false;option('mini',false);selection=clamp(snapshot.index<10000 and snapshot.index or 0,0,math.max(0,snapshot.count-1));scroll_first=clamp(selection-3,0,math.max(0,snapshot.count-LIBRARY_ROWS));library_direction=1;change_page('library')
 if opened then capture(true)end
end
function api.refresh()return send(9)end
local function draw()
 if not opened and reveal==0 then if next(ids)then clear()end;return end
 local main=sr.Application.main_world();local worlds=sr.Application.worlds();local target,slot,count,alive=nil,nil,0,false
 -- Engine world tables can be keyed or sparse. Keep a live target stable across enumeration changes.
 for key,world in pairs(worlds)do
  count=count+1
  if world==gui_world then alive=true end
  if world~=main and not target then target=world;slot=key end
 end
 if gui_world and alive and gui_world~=main then target=gui_world end
 if gui_world and target~=gui_world then
  if alive then clear();if sr.World.destroy_gui then pcall(sr.World.destroy_gui,gui_world,gui)end else ids={};draw_key=nil end
  gui=nil;gui_world=nil
 end
 if not gui then
  if not target then return end
  local ok,result=pcall(sr.World.create_screen_gui,target,'scale',1,1)
  if not ok or not result then
   if error_text~='Display unavailable; reopen Walkman' then log('Display creation failed: '..tostring(result))end
   error_text='Display unavailable; reopen Walkman';return
  end
  gui_world=target;gui=result
  log('Display UI world key '..tostring(slot)..' from '..count..' worlds')
 end
 if page=='library'then prepare_rows()end
 local x,y,w,h,k=layout();local s=snapshot
 local motion=reveal<1 or screen_motion<1 or page~='library'and(width(s.title,mini and 19 or 20)>(mini and 318 or 262)or not mini and width(s.folder,14)>262)
 if page=='library'then if scroll_started>=0 then motion=true end;for index,v in pairs(visible_rows)do if(index==s.index or hover=='row'..index)and width(v,16)>225 then motion=true;break end end end
 for _,v in ipairs(levels)do if v>.003 then motion=true;break end end
 if input_warning or error_text or s.error~=''then motion=true end
 local animation=motion and math.floor(now*30)or 0
 local shuffle_state=last_shuffle~=nil and last_shuffle or s.shuffle
 local repeat_state=last_repeat~=nil and last_repeat or s.repeat_mode
 local key=table.concat({x,y,w,h,page,selection,scroll_first,mini and 1 or 0,animation,os.date('%H:%M'),hover or'',s.count,s.index,s.position,s.volume,s.state,shuffle_state,repeat_state,s.title,s.folder,error_text or s.error,visible_generation,scroll_goal,s.generation,s.first,table.concat(s.rows,'|')},':')
 if key==draw_key then return end;draw_key=key;used={};serial=0;boxes={}
 local label_serial=0
 local shift=(1-screen_motion)^3*10
 local function rect(u,t,ww,hh,color,z,id)tile(1021,391,1,1,x+u*k,y+h-(t+hh)*k,ww*k,hh*k,z or 12003,color,id)end
 local function clip(u,t,ww,hh)return{x+u*k,y+h-(t+hh)*k,x+(u+ww)*k,y+h-t*k}end
 local function label(v,u,t,p,color,crop)
  label_serial=label_serial+1;text(v,x+u*k,y+h-t*k,p,k,12004,color,crop,'text:'..label_serial)
 end
 local function centered(v,t,p,color)local value=tostring(v or'');label(value,180-width(value,p)/2,t,p,color)end
 local function running(v,u,t,p,ww,id,color,center)
  v=tostring(v or'');local offset=marquee_offset(id,v,p,ww)
  local at=u-offset;if center and width(v,p)<=ww then at=u+(ww-width(v,p))/2 end
  label(v,at,t,p,color,clip(u,t-p,ww,p+10))
 end
 local function hit(id,u,t,ww,hh,action)
  boxes[#boxes+1]={id=id,x=u,y=t,w=ww,h=hh,act=action}
 end
 local function button(id,title,u,t,ww,act,active)
  label(title,u,t,13,(hover==id or active)and gold or muted);hit(id,u-5,t-20,ww+10,30,act)
 end
 if mini and page~='library'then
  tile(384,400,360,112,x,y,w,h,12000,nil,'housing')
  label(s.state==1 and 'PLAYING' or s.state==2 and 'PAUSED' or 'WALKMAN',18,25,14,gold)
  label('SONY',291,25,14,muted)
  running(s.title,18,55,19,318,'compact',white,false)
  label(clock(s.position)..' / -'..clock(math.max(0,s.duration-s.position))..'    VOL '..s.volume,18,84,13,muted)
  
 else
  tile(0,400,360,620,x,y,w,h,12000,nil,'housing')
  -- A solid glass panel prevents the game scene leaking through the display.
  rect(41,56,278,494,rgba(255,22,23,26),12001,'glass')
  label(os.date('%H:%M'),49,77,13,white)
  local function window_control(id,at,u,act)
   tile(at,530,64,64,x+u*k,y+h-82*k,20*k,20*k,12004,hover==id and gold or white,'window:'..id)
   hit(id,u-3,59,26,26,act)
  end
  window_control('compact',896,266,api.compact);window_control('hide',960,296,api.toggle)
  if page=='library'then
   centered('Music library',110,20,white)
   tile(832,620,64,64,x+287*k,y+h-111*k,22*k,22*k,12004,hover=='refresh'and gold or white,'library-refresh')
   hit('refresh',281,84,34,32,api.refresh)
   tile(960,620,64,64,x+51*k,y+h-111*k,22*k,22*k,12004,hover=='back'and gold or white,'return-to-player')
   hit('back',45,84,34,32,function()change_page('player')end)
   if hover=='back'then label('PLAYER',47,126,10,muted)elseif hover=='refresh'then label('REFRESH',265,126,10,muted)end
   local function row_rect(u,t,ww,hh,color,id)
    local top,bottom=math.max(LIBRARY_TOP,t),math.min(LIBRARY_BOTTOM,t+hh)
    if bottom>top then rect(u,top,ww,bottom-top,color,12002,id)end
   end
   if not next(visible_rows) and s.count>0 then centered('Preparing library...',260,16,muted)end
   for index=math.floor(scroll_visual),math.floor(scroll_visual)+LIBRARY_ROWS do
    local value=visible_rows[index];local t=155+(index-scroll_visual)*34+shift
    if value and t>LIBRARY_TOP and t-25<LIBRARY_BOTTOM then
     local active=index==s.index;local hovered=hover=='row'..index;local focused=active or hovered
     if focused then row_rect(48,t-23,253,32,active and rgba(255,55,47,32)or rgba(255,38,40,44),'row-highlight:'..index)end
     if index==s.index then
      local top,bottom=math.max(LIBRARY_TOP,t-17),math.min(LIBRARY_BOTTOM,t+2)
      if bottom>top then tile(896,620+(top-(t-17))*64/19,64,(bottom-top)*64/19,x+49*k,y+h-bottom*k,19*k,(bottom-top)*k,12004,gold,'current-track')end
     end
     local offset=focused and marquee_offset('row'..index,value,16,225)or 0
     label(value,71-offset,t,16,active and gold or white,clip(70,LIBRARY_TOP,229,LIBRARY_HEIGHT))
     local top,bottom=math.max(LIBRARY_TOP,t-25),math.min(LIBRARY_BOTTOM,t+9)
     hit('row'..index,48,top,254,bottom-top,function()
      if visible_generation~=snapshot.generation or index>=snapshot.count then return end
      selection=index;if send(4,index)then change_page('player')end
     end)
    end
   end
   local total=math.max(0,s.count-LIBRARY_ROWS);local thumb=math.max(28,LIBRARY_HEIGHT*math.min(1,LIBRARY_ROWS/math.max(1,s.count)))
   local top=LIBRARY_TOP+(LIBRARY_HEIGHT-thumb)*(total>0 and scroll_first/total or 0)
   if total>0 then
    rect(307,LIBRARY_TOP,4,LIBRARY_HEIGHT,rgba(255,50,52,56),12002,'scroll-track');rect(306,top,6,thumb,gold,12003,'scroll-thumb')
    hit('scrollbar',302,LIBRARY_TOP,15,LIBRARY_HEIGHT,function(_,t)drag={kind='scroll',offset=(t>=top and t<=top+thumb)and t-top or thumb/2,thumb=thumb}end)
   end

  else
   button('library','LIBRARY',49,109,68,api.library)
   label('WALKMAN',224,109,14,gold)
   tile(384,620,196,196,x+82*k,y+h-(315+shift)*k,196*k,196*k,12002,nil,'visualizer-ring')
   -- Twelve logarithmic frequency bands decoded from the current song.
   for i=0,31 do
    local angle=i*math.pi/16;local band=math.floor(i*12/32)+1
    local u=180+math.cos(angle)*75-2;local t=217+shift+math.sin(angle)*75-2
    tile(784,620,16,16,x+u*k,y+h-(t+4)*k,4*k,4*k,12003,levels[band]>.08 and gold or dot_dim,'dot:'..i)
   end
   for i=1,12 do
    local bar=4+levels[i]*80
    rect(128+(i-1)*9,217+shift-bar/2,5,bar,gold,12003,'band:'..i)
   end
   centered(not visualizer and 'VISUALIZER OFF' or s.state==2 and 'PAUSED' or spectrum_ready==2 and 'VISUALIZER UNAVAILABLE' or 'AUDIO SPECTRUM',331+shift,11,muted)
   running(s.title,49,356+shift,20,262,'title',white,true)
   running(s.folder,49,384+shift,14,262,'folder',muted,true)
   label(clock(s.position),49,424,13,white);local remaining='-'..clock(math.max(0,s.duration-s.position));label(remaining,311-width(remaining,13),424,13,white)
   centered((s.index>=0 and s.index<10000 and s.index+1 or 0)..' / '..s.count,424,12,muted)
   local progress=s.duration>0 and clamp(s.position/s.duration,0,1)or 0
   rect(49,440,262,3,rgba(255,66,68,72),12002,'seek-track');rect(49,440,262*progress,3,gold,12003,'seek-progress')
   rect(46+262*progress,436,6,11,gold,12003,'seek-thumb')
   hit('seek',46,431,268,24,function()drag={kind='seek'}end)
  end
  local function control(at,u,t,sz,id,act)
   tile(704,620,64,64,x+u*k,y+h-(t+sz)*k,sz*k,sz*k,12002,hover==id and gold or rgba(0,216,180,112),'hover:'..id)
   tile(at,530,64,64,x+u*k,y+h-(t+sz)*k,sz*k,sz*k,12003,hover==id and gold or neutral,'control:'..id)
   hit(id,u,t,sz,sz,act)
  end
  control(384,84,462,47,'previous',api.previous);control(s.state==1 and 448 or 512,151,458,58,'play',api.play_pause);control(576,229,462,47,'next',api.next)
  local function transport_option(id,at,u,act,active)
   local hovered=hover==id
   local color=active and(hovered and rgba(255,242,211,155)or gold)or(hovered and white or muted)
   if hovered then tile(704,620,64,64,x+(u-4)*k,y+h-508*k,38*k,38*k,12002,white,'option-hover:'..id)end
   tile(at,530,64,64,x+u*k,y+h-504*k,30*k,30*k,12003,color,'option:'..id)
   if active then tile(784,620,16,16,x+(u+13)*k,y+h-514*k,4*k,4*k,12003,gold,'option-selected:'..id)end
   hit(id,u-4,470,38,38,act)
  end
  transport_option('shuffle',704,46,api.shuffle,shuffle_state==1)
  transport_option('repeat',repeat_state==2 and 832 or 768,282,api.repeat_mode,repeat_state~=0)
  local state=({[0]='STOPPED',[1]='PLAYING',[2]='PAUSED',[3]='LOADING',[4]='AUDIO ERROR'})[s.state]or'WAITING'
  centered(state..'  /  VOL '..s.volume,536,13,white)
  local msg=input_warning or error_text or s.error
  if msg and msg~=''then running(msg,48,550,11,264,'error',gold,true)end
 end
 for key,record in pairs(ids)do if not used[key]then pcall(sr.Gui.destroy_bitmap,gui,record.id);ids[key]=nil end end
 for _,id in ipairs(retired)do pcall(sr.Gui.destroy_bitmap,gui,id)end;retired={}
end
local function mouse_button(method,name)
 local M=sr.Mouse;if not M or not M[method]then return false end
 local lookup=M.button_id or M.button_index;if not lookup then return false end
 local ok,id=pcall(lookup,name);if not ok or id==nil then return false end
 local success,value=pcall(M[method],id);return success and(value==true or type(value)=='number'and value>0)
end
local function mouse_tick()
 hover=nil
 if not opened or mini or not captured or reveal<.98 then return end
 local M=sr.Mouse;local lookup=M and(M.axis_id or M.axis_index)
 if not lookup or not M.axis then return end
 local ok,id=pcall(lookup,'cursor');if not ok or id==nil then return end
 local good,value=pcall(M.axis,id);if not good or not value then return end
 local x,y,w,h,k=layout()
 mouse_x=(sr.Vector3.x(value)-x)/k;mouse_y=(y+h-sr.Vector3.y(value))/k
 for i=#boxes,1,-1 do if in_box(boxes[i],mouse_x,mouse_y)then hover=boxes[i].id;break end end
 if page=='library'and mouse_x>=41 and mouse_x<=319 and mouse_y>=120 and mouse_y<=LIBRARY_BOTTOM then
  local delta=mouse_button('pressed','wheel_down')and 3 or mouse_button('pressed','wheel_up')and-3 or 0
  if delta~=0 then library_direction=delta>0 and 1 or-1;scroll_first=clamp(scroll_first+delta,0,math.max(0,snapshot.count-LIBRARY_ROWS));marquee_starts={};drag=nil end
 end
 if mouse_button('pressed','left')then
  for i=#boxes,1,-1 do local box=boxes[i];if in_box(box,mouse_x,mouse_y)then box.act(mouse_x,mouse_y);break end end
 end
 if drag then
  if drag.kind=='scroll'then
   local span=LIBRARY_HEIGHT-drag.thumb;local target=math.floor(clamp((mouse_y-LIBRARY_TOP-drag.offset)/math.max(1,span),0,1)*math.max(0,snapshot.count-LIBRARY_ROWS)+.5);if target~=scroll_first then library_direction=target>scroll_first and 1 or-1 end;scroll_first=target
  elseif drag.kind=='seek'and snapshot.duration>0 then
   local target=math.floor(clamp((mouse_x-49)/262,0,1)*snapshot.duration)
   if not drag.last or math.abs(target-drag.last)>250 then send(8,target);drag.last=target end
  end
  if not mouse_button('down','left')then drag=nil end
 end
end
local options={
 {'open',{type='toggle',label='Show Walkman',default=false,description='Show or hide the player. Music continues while hidden.'}},
 {'size',{type='slider',label='Player size',min=70,max=140,step=5,default=100}},
 {'x',{type='slider',label='Horizontal position',min=0,max=100,step=1,default=100}},
 {'y',{type='slider',label='Vertical position (top to bottom)',min=0,max=100,step=1,default=10}},
 {'mini',{type='toggle',label='Compact display',default=false,description='Small passive display. Return to the full player for mouse controls.'}},
 {'visualizer',{type='toggle',label='Song visualizer',default=true,description='Animate frequency bands from the Walkman song. Analysis pauses while hidden or compact.'}},
 {'volume',{type='slider',label='Music volume',min=0,max=100,step=5,default=50}},
 {'shuffle',{type='toggle',label='Shuffle',default=false}},
 {'repeat',{type='toggle',label='Repeat library',default=false}},
}
local bindings={
 {name='toggle',label='Show / hide Walkman',act=api.toggle},
 {name='compact',label='Toggle compact display',act=api.compact},
 {name='play_pause',label='Play / pause',act=api.play_pause},
 {name='next',label='Next track',act=api.next},
 {name='previous',label='Previous track',act=api.previous},
 {name='shuffle',label='Toggle shuffle',act=api.shuffle},
 {name='repeat',label='Cycle repeat: off / library / track',act=api.repeat_mode},
}
local disabled=false
local function accept_snapshot(s)
 local previous=snapshot.index
 local changed=s.index~=previous or s.title~=snapshot.title
 snapshot=s
 if s.index>=0 and s.index<s.count then
  selection=s.index
  if changed then
   marquee_starts['row'..previous]=nil;marquee_starts['row'..s.index]=nil
   marquee_starts.title=nil;marquee_starts.folder=nil;marquee_starts.compact=nil
   -- Follow a new song once; ordinary heartbeats leave manual browsing alone.
   if page=='library'and not(drag and drag.kind=='scroll')and(s.index<scroll_first or s.index>=scroll_first+LIBRARY_ROWS)then
    local first=clamp(s.index-3,0,math.max(0,s.count-LIBRARY_ROWS))
    library_direction=first>scroll_first and 1 or-1;scroll_first=first
   end
  end
 else selection=clamp(selection,0,math.max(0,s.count-1))end
 scroll_first=clamp(scroll_first,0,math.max(0,s.count-LIBRARY_ROWS))
 cache_snapshot(s)
end
local function sync()
 if not words then return end
 local t=ms();words[3]=t
 if host and K.WaitForSingleObject(host,0)==0 then K.CloseHandle(host);host=nil;error_text='Audio companion exited. Reopen to retry.';pending={}end
 if host and words[9]==0 and launched_at and elapsed(t,launched_at)>20000 then error_text='Audio did not start. Open library log.'
 elseif host and words[9]~=0 and elapsed(t,tonumber(words[9]))>3000 then error_text='Audio companion is not responding'
 elseif host and words[9]~=0 then error_text=nil end
 if not host then return end
 if words[8]==words[5] and #pending>0 then
  local c=table.remove(pending,1);command_seq=(tonumber(words[5])+1)%4294967296
  words[750]=c[3];words[7]=c[2];words[6]=c[1];words[5]=command_seq
 end
 local seq=tonumber(words[11]);if seq%2~=0 then return end
 local s={state=tonumber(words[10]),count=tonumber(words[12]),index=tonumber(words[13]),position=tonumber(words[14]),duration=tonumber(words[15]),volume=tonumber(words[16]),shuffle=tonumber(words[17]),repeat_mode=tonumber(words[18]),first=tonumber(words[19]),generation=tonumber(words[23]),error=read_text(96,192),title=read_text(288,256),folder=read_text(544,128),rows={}}
 for i=1,8 do s.rows[i]=read_text(672+(i-1)*256,256)end
 if seq==tonumber(words[11])and seq~=0 and s.count<=10000 and s.first<=s.count and s.volume<=100 and s.repeat_mode<=2 then accept_snapshot(s)end
 words[21]=title_request()
end
local function tick(dt)
 now=now+math.max(0,math.min(type(dt)=='number'and dt or 0,.1))
 if words then words[3]=ms()end
 if now>=next_menu then
  next_menu=now+.2
  local b=rawget(_G,'ModBindingsMenu')
  if b and b.api==1 then for _,v in ipairs(bindings)do if not v.registered then local ok,why=b.register_binding('julian.walkman.'..v.name,v.label,nil,{category='Walkman'});v.registered=ok==true;if not v.registered and why~=v.reason then log('Binding '..v.name..': '..tostring(why));v.reason=why end end end end
  local m=rawget(_G,'ModOptionsMenu')
  if m and m.api==1 then
   for _,v in ipairs(options)do if not v.registered then v[2].mod='Walkman';local ok,why=m.register_option('julian.walkman.'..v[1],v[2]);v.registered=ok==true;if not v.registered and why~=v.reason then log('Option '..v[1]..': '..tostring(why));v.reason=why end;if v[1]=='open'and v.registered and m.set then m.set('julian.walkman.open',false)end end end
   local value=m.get('julian.walkman.open');if type(value)=='boolean'and value~=opened then api.toggle()end
   local function bounded(key,lo,hi,default)local n=m.get('julian.walkman.'..key);return type(n)=='number'and n==n and math.max(lo,math.min(hi,n))or default end
   size=bounded('size',70,140,100)/100;xpos=bounded('x',0,100,100);ypos=bounded('y',0,100,10)
   local desired=m.get('julian.walkman.mini')==true
   if mini~=desired then mini=desired;change_page('player');if opened then capture(not mini)end end
   visualizer=m.get('julian.walkman.visualizer')~=false
   local v=bounded('volume',0,100,50);local s=m.get('julian.walkman.shuffle')==true and 1 or 0;local repeat_setting=m.get('julian.walkman.repeat')==true;local r=repeat_setting and(last_repeat==2 and 2 or 1)or 0
   if host then if last_volume~=v then if send(5,v)then last_volume=v end end;if last_shuffle~=s then if send(6,s)then last_shuffle=s end end;if last_repeat~=r then if send(7,r)then last_repeat=r end end end
  end
 end
 local b=rawget(_G,'ModBindingsMenu')
 if b and b.ready and b.ready()==true then
  for _,v in ipairs(bindings)do
   local down=v.registered and b.is_down('julian.walkman.'..v.name)==true
   if down and not v.down then v.act()end;v.down=down
  end
 end
 if now>=next_sync then next_sync=now+.1;sync()end
 local frame_dt=math.max(0,math.min(type(dt)=='number'and dt or 0,.1))
 reveal=clamp(reveal+(opened and 1 or-1)*frame_dt/.26,0,1)
 screen_motion=math.min(1,screen_motion+frame_dt/.24)
 if words then
  words[751]=opened and not mini and page=='player'and visualizer and 1 or 0
  local seq=tonumber(words[756]);local ready=tonumber(words[757]);local measured={}
  for i=1,12 do measured[i]=clamp(tonumber(words[757+i])/1000,0,1)end
  if seq%2==0 and seq==tonumber(words[756])then
   spectrum_ready=ready
   for i=1,12 do local target=snapshot.state==1 and ready==1 and measured[i]or 0;local speed=target>levels[i]and 22 or 6;levels[i]=levels[i]+(target-levels[i])*(1-math.exp(-frame_dt*speed))end
  end
 end
 draw()
 if opened and not mini and gui then capture(true)else capture(false)end
 mouse_tick()
end
local function cleanup()
 opened=false;capture(false);pcall(reset_gui)
 if words then words[4]=1 end
 if host then K.CloseHandle(host);host=nil end
 if memory then K.UnmapViewOfFile(memory);memory=nil;words=nil end
 if map then K.CloseHandle(map);map=nil end
end
local function after(dt,...)
 if not disabled then local ok,why=xpcall(function()tick(dt)end,debug.traceback);if not ok then disabled=true;pcall(cleanup);log('Disabled: '..tostring(why))end end
 return ...
end
update=function(dt,...)return after(dt,old_update(dt,...))end
local old_shutdown=rawget(_G,'shutdown')
shutdown=function(...)pcall(cleanup);if old_shutdown then return old_shutdown(...)end end
api.layout=layout
_G.HD2Walkman=api
log('Walkman v0.3.7 loaded; library highlight, marquee and viewport follow confirmed playback; cursor input uses verified game routines')
return api
