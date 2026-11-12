"""Rebuild the original low-poly laundromat asset. Requires Python 3 + Pillow.
Run from any directory: python3 Horror/Tools/generate_laundromat.py
Geometry, weathered textures and signs are procedural; no external art is used.
"""
import io
import json
import math
import random
import struct
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
RNG = random.Random(247)
TAU = math.tau
materials, images, textures, groups, collisions = [], [], [], {}, []
frame = (0, 0, 0, 0)


def texture(base, kind="metal", size=128):
    im = Image.new("RGB", (size, size))
    pixels = im.load()
    for y in range(size):
        for x in range(size):
            noise = RNG.randrange(-13, 14)
            stain = 9 * math.sin(x * .09) * math.sin(y * .06)
            if kind == "metal":
                stain += 5 * math.sin(y * 1.7)
            pixels[x, y] = tuple(max(0, min(255, int(c + noise + stain))) for c in base)
    d = ImageDraw.Draw(im)
    if kind == "tile":
        d.rectangle((0, 0, size-1, size-1), outline=(39, 43, 44), width=3)
        d.line((4, size-5, size-5, size-5), fill=tuple(min(255,c+18) for c in base))
    elif kind == "glass":
        for _ in range(430):
            x, y = RNG.randrange(size), RNG.randrange(size)
            col = RNG.choice([(25, 38, 50), (39, 54, 66), (60, 69, 76)])
            d.line((x,y,x-1,y+RNG.randrange(2,15)), fill=col, width=1)
        for _ in range(9):
            x, y = RNG.randrange(size), RNG.randrange(size)
            d.rectangle((x,y,x+RNG.randrange(1,4),y+2), fill=(132,103,51))
    else:
        for _ in range(95):
            x, y = RNG.randrange(size), RNG.randrange(size)
            d.line((x,y,x+RNG.randrange(1,9),y+RNG.randrange(1,4)), fill=tuple(max(0,c-35) for c in base))
        if kind == "metal":
            for _ in range(30):
                x, y = RNG.randrange(size), RNG.randrange(size)
                d.rectangle((x,y,x+2,y+RNG.randrange(2,10)),fill=(74,55,37))
    return im


def font(size, serif=False):
    names = (["/usr/share/fonts/google-noto-vf/NotoSerif[wght].ttf", "DejaVuSerif.ttf", "C:/Windows/Fonts/georgia.ttf"]
             if serif else ["/usr/share/fonts/google-noto-vf/NotoSansMono[wght].ttf", "DejaVuSansMono.ttf", "C:/Windows/Fonts/consola.ttf"])
    for name in names:
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default(size=size)


def sign(lines, color=(195,189,160), background=(22,29,29), neon=False):
    im = Image.new("RGB", (512,256), background)
    d = ImageDraw.Draw(im)
    d.rectangle((7,7,504,248),outline=color,width=3)
    for text,y,size in lines:
        f = font(size, not neon)
        d.text((256,y), text, anchor="mt", font=f, fill=color)
    if not neon:
        for _ in range(2200):
            x,y=RNG.randrange(512),RNG.randrange(256)
            d.point((x,y),fill=RNG.choice([background,(71,70,58)]))
    return im


def material(name, color, roughness=.8, metallic=0, kind=None, image=None, emissive=None):
    pbr = {"baseColorFactor": [*color,1], "metallicFactor": metallic,"roughnessFactor":roughness}
    if kind or image is not None:
        im = image if image is not None else texture(tuple(int(c*255) for c in color),kind)
        data = io.BytesIO(); im.save(data,format="PNG")
        images.append(data.getvalue()); textures.append({"source":len(images)-1,"sampler":0})
        pbr["baseColorFactor"]=[1,1,1,1]; pbr["baseColorTexture"]={"index":len(textures)-1}
    m={"name":name,"pbrMetallicRoughness":pbr,"doubleSided":True}
    if emissive:
        strength=max(1,max(emissive))
        m["emissiveFactor"]=[v/strength for v in emissive]
        if strength>1: m["extensions"]={"KHR_materials_emissive_strength":{"emissiveStrength":strength}}
    materials.append(m); return len(materials)-1


plaster=material("Damp nicotine plaster",(.42,.4,.31),kind="plaster")
ceiling=material("Water-stained ceiling",(.24,.25,.23),kind="plaster")
grout=material("Dark tile grout",(.085,.10,.105))
tile_a=material("Blue grey floor tile",(.30,.35,.38),.34,kind="tile")
tile_b=material("Ivory floor tile",(.43,.43,.36),.4,kind="tile")
wall_tile=material("Old ceramic wall tile",(.46,.48,.40),.45,kind="tile")
enamel=material("Scratched washer enamel",(.57,.57,.48),.43,.15,kind="metal")
metal=material("Tarnished steel",(.25,.28,.27),.35,.7,kind="metal")
black=material("Black rubber",(.018,.023,.023),.9)
drum=material("Drum steel",(.22,.25,.26),.27,.75)
glass=material("Dark washer glass",(.025,.045,.05),.12,.55)
window=material("Rain on night windows",(.065,.095,.14),.22,.2,kind="glass")
wood=material("Worn laminate counter",(.26,.18,.12),.4,kind="metal")
phone=material("Old ivory telephone",(.55,.51,.4),.52,kind="metal")
seat=material("Faded waiting chairs",(.26,.29,.24),.62,kind="metal")
paper=material("Paper",(.65,.62,.48),kind="plaster")
red=material("Red indicator",(.65,.025,.016),.4,emissive=[1,.015,.008])
tube=material("Fluorescent tubes",(.86,.89,.67),.6,emissive=[3.4,3.5,2.5])
blue_led=material("Machine screen",(.08,.21,.18),.5,emissive=[.08,.27,.21])
poster=material("Laundry rules",(1,1,1),image=sign([("WASH / DRY",28,48),("NO ATTENDANT ON DUTY",107,24),("DO NOT LEAVE ITEMS",151,25),("AFTER MIDNIGHT",190,27)]))
brand=material("Laundry wall sign",(1,1,1),image=sign([("DELICATE WASH",32,46),("SELF SERVICE LAUNDRY",112,25),("OPEN ALL NIGHT",179,30)]))
neon=material("24 / 7 neon sign",(1,1,1),image=sign([("24/7",9,155)],(235,38,30),(22,9,10),True),emissive=[.035,.001,.001])


def world(p, normal=False):
    x,y,z=p; a,b,c,yaw=frame
    co,si=math.cos(yaw),math.sin(yaw)
    return (co*x+si*z+(0 if normal else a),y+(0 if normal else b),-si*x+co*z+(0 if normal else c))


def quad(mat, points, normal, uvscale=(1,1)):
    g=groups.setdefault(mat, [[],[],[],[]]); start=len(g[0])
    g[0].extend(world(p) for p in points); g[1].extend([world(normal,True)]*4)
    u,v=uvscale; g[2].extend([(0,v),(u,v),(u,0),(0,0)])
    g[3].extend([start,start+1,start+2,start,start+2,start+3])


def box(mat, pos, size, collide=False, name="Obstacle", uv=1):
    x,y,z=pos; a,b,c=[s/2 for s in size]
    quad(mat,[(x-a,y-b,z+c),(x+a,y-b,z+c),(x+a,y+b,z+c),(x-a,y+b,z+c)],(0,0,1),(size[0]*uv,size[1]*uv))
    quad(mat,[(x+a,y-b,z-c),(x-a,y-b,z-c),(x-a,y+b,z-c),(x+a,y+b,z-c)],(0,0,-1),(size[0]*uv,size[1]*uv))
    quad(mat,[(x+a,y-b,z+c),(x+a,y-b,z-c),(x+a,y+b,z-c),(x+a,y+b,z+c)],(1,0,0),(size[2]*uv,size[1]*uv))
    quad(mat,[(x-a,y-b,z-c),(x-a,y-b,z+c),(x-a,y+b,z+c),(x-a,y+b,z-c)],(-1,0,0),(size[2]*uv,size[1]*uv))
    quad(mat,[(x-a,y+b,z+c),(x+a,y+b,z+c),(x+a,y+b,z-c),(x-a,y+b,z-c)],(0,1,0),(size[0]*uv,size[2]*uv))
    quad(mat,[(x-a,y-b,z-c),(x+a,y-b,z-c),(x+a,y-b,z+c),(x-a,y-b,z+c)],(0,-1,0),(size[0]*uv,size[2]*uv))
    if collide: collisions.append({"name":name,"position":world(pos),"size":size,"yaw":frame[3]})


def ring(mat, pos, outer, inner, depth=.05, segments=32):
    x,y,z=pos
    for i in range(segments):
        a,b=TAU*i/segments,TAU*(i+1)/segments
        co,si=math.cos,math.sin
        p=[(x+inner*co(a),y+inner*si(a),z+depth/2),(x+outer*co(a),y+outer*si(a),z+depth/2),(x+outer*co(b),y+outer*si(b),z+depth/2),(x+inner*co(b),y+inner*si(b),z+depth/2)]
        quad(mat,p,(0,0,1))
        quad(mat,[(x+outer*co(a),y+outer*si(a),z-depth/2),p[1],p[2],(x+outer*co(b),y+outer*si(b),z-depth/2)],(co((a+b)/2),si((a+b)/2),0))


def disk(mat, pos, radius, segments=32):
    # A tiny centre radius avoids degenerate triangles in the generated GLB.
    ring(mat,pos,radius,.0005,.008,segments)


def rod(mat, start, end, radius=.012, segments=6):
    # Cylinder between arbitrary endpoints, used for baskets and coiled cables.
    p,q=start,end; direction=[q[i]-p[i] for i in range(3)]; length=math.sqrt(sum(v*v for v in direction))
    w=[v/length for v in direction]; ref=(0,1,0) if abs(w[1])<.95 else (1,0,0)
    u=(w[1]*ref[2]-w[2]*ref[1],w[2]*ref[0]-w[0]*ref[2],w[0]*ref[1]-w[1]*ref[0]); n=math.sqrt(sum(v*v for v in u));u=[v/n for v in u]
    v=(w[1]*u[2]-w[2]*u[1],w[2]*u[0]-w[0]*u[2],w[0]*u[1]-w[1]*u[0])
    for j in range(segments):
        def offset(t): return [radius*(math.cos(t)*u[i]+math.sin(t)*v[i]) for i in range(3)]
        a,b=offset(TAU*j/segments),offset(TAU*(j+1)/segments)
        quad(mat,[tuple(p[i]+a[i] for i in range(3)),tuple(q[i]+a[i] for i in range(3)),tuple(q[i]+b[i] for i in range(3)),tuple(p[i]+b[i] for i in range(3))],tuple((a[i]+b[i])/(2*radius) for i in range(3)))


def label(mat, pos, width, height):
    x,y,z=pos
    quad(mat,[(x-width/2,y-height/2,z),(x+width/2,y-height/2,z),(x+width/2,y+height/2,z),(x-width/2,y+height/2,z)],(0,0,1))


# Room envelope: twelve by ten metres, 3.1m ceiling, all dimensions human scale.
box(grout,(0,-.16,0),(12,.32,10),True,"Floor")
for x in range(24):
    for z in range(20):
        box(tile_a if (x+z)%2==0 else tile_b,(-5.75+x*.5,.006,-4.75+z*.5),(.487,.012,.487),uv=2.05)
box(plaster,(0,1.55,-5.1),(12.4,3.1,.2),True,"Back wall")
box(plaster,(6.1,1.55,0),(.2,3.1,10.4),True,"Right wall")
box(plaster,(0,1.55,5.1),(12.4,3.1,.2),True,"Front wall")
box(ceiling,(0,3.18,0),(12.4,.16,10.4),True,"Ceiling")
for z in range(5):
    box(ceiling,(0,3.08,-4+z*2),(12,.025,.025))
for x in range(7): box(ceiling,(-6+x*2,3.08,0),(.025,.025,10))
for x in range(24):
    for y in range(3): box(wall_tile,(-5.75+x*.5,.25+y*.5,-4.98),(.487,.487,.025),uv=2)
for z in range(20):
    for y in range(3): box(wall_tile,(5.98,.25+y*.5,-4.75+z*.5),(.025,.487,.487),uv=2)
box(black,(0,.09,-4.96),(12,.18,.06)); box(black,(5.96,.09,0),(.06,.18,10))

# Floor-to-ceiling wet shopfront down the left wall. Opaque textured panes show rainy night.
box(metal,(-6.07,1.55,0),(.14,3.1,10.2),True,"Shopfront glass")
for z in (-4,-2,0,2,4):
    box(window,(-5.975,1.62,z),(.028,2.72,1.88))
    box(metal,(-5.94,1.55,z-1),(.12,3.1,.08))
    box(metal,(-5.94,1.0,z),(.08,.055,2))
box(metal,(-5.94,2.99,0),(.12,.12,10)); box(metal,(-5.94,.22,0),(.12,.14,10))
# Door handle and kick plate on the nearest window bay.
box(metal,(-5.89,.41,4),(.1,.3,1.8))
rod(metal,(-5.82,.95,3.27),(-5.82,1.3,3.27),.025)
# 24/7 panel faces into the room from the window.
frame=(-5.86,2.05,.0,math.pi/2)
label(neon,(0,0,0),1.35,.7)
frame=(0,0,0,0)


def washer(x,z,yaw,index):
    global frame
    frame=(x,0,z,yaw)
    box(enamel,(0,.71,0),(1.17,1.4,.94),True,"Washing machine %02d"%index)
    box(black,(0,.08,.01),(1.18,.16,.95))
    box(metal,(0,1.38,0),(1.2,.05,.97))
    box(metal,(0,1.19,.482),(1.11,.26,.025))
    box(black,(-.25,1.2,.504),(.42,.13,.018))
    box(blue_led,(-.28,1.22,.518),(.22,.045,.012))
    for k in range(3): box(phone,(-.4+k*.09,1.15,.52),(.045,.018,.01))
    disk(black,(.28,1.2,.526),.065,16)
    box(phone,(.28,1.2,.542),(.015,.075,.02))
    box(black,(.46,1.21,.52),(.042,.11,.015))
    box(red,(.46,1.27,.531),(.027,.018,.012))
    # Large recessed drum and heavy concentric door seal.
    disk(black,(0,.64,.484),.43)
    ring(metal,(0,.64,.52),.427,.355,.06)
    ring(black,(0,.64,.56),.36,.306,.055)
    disk(glass,(0,.64,.574),.307)
    ring(drum,(0,.64,.581),.26,.237,.006)
    for a in range(0,360,30):
        t=math.radians(a)
        disk(black,(.216*math.cos(t),.64+.216*math.sin(t),.589),.011,6)
    box(metal,(.34,.68,.609),(.06,.21,.065))
    box(paper,(-.4,.96,.49),(.13,.055,.008))
    box(metal,(0,.2,.483),(.59,.018,.012))
    for k in range(8): box(black,(-.28+k*.08,.19,.494),(.035,.012,.008))
    frame=(0,0,0,0)

for i in range(7): washer(-3.7+i*1.29,-4.41,0,i+1)
for i in range(5): washer(5.4,-2.9+i*1.29,-math.pi/2,i+8)

# Posters on the back wall, above machine height.
label(brand,(1.25,2.35,-4.978),3.35,1.12)
label(poster,(-3.15,2.28,-4.974),.8,1.05)
label(poster,(4.23,2.28,-4.974),.65,.9)

# Waiting chairs along the storefront, facing the machines.
for i in range(3):
    frame=(-5.1,0,-2.55+i*.72,math.pi/2)
    box(seat,(0,.47,0),(.61,.13,.55),True,"Waiting chair")
    box(seat,(0,.79,-.24),(.61,.57,.075))
    for x in (-.23,.23):
        for z in (-.2,.2): rod(metal,(x,.08,z),(x,.44,z),.022)
    frame=(0,0,0,0)

# Wire laundry trolley, in the open aisle.
frame=(-1.9,0,-.35,-.2)
box(metal,(0,.53,0),(.82,.045,.61),True,"Laundry trolley basket")
# Collider fills the basket volume without generating hundreds of tiny physics bodies.
collisions.append({"name":"Laundry trolley","position":world((0,.66,0)),"size":[.88,1.08,.66],"yaw":frame[3]})
for y in (.55,.72,.9,1.05):
    for z in (-.3,.3): rod(metal,(-.42,y,z),(.42,y,z),.012)
    for x in (-.42,.42): rod(metal,(x,y,-.3),(x,y,.3),.012)
for i in range(9):
    x=-.4+i*.1
    for z in (-.3,.3): rod(metal,(x,.54,z),(x,1.05,z),.009)
for i in range(7):
    z=-.3+i*.1
    for x in (-.42,.42): rod(metal,(x,.54,z),(x,1.05,z),.009)
for x in (-.34,.34):
    for z in (-.24,.24):
        rod(metal,(x,.13,z),(x,.53,z),.015)
        disk(black,(x,.1,z+.03),.075,12)
rod(metal,(-.42,1.05,.3),(-.42,1.13,.49)); rod(metal,(.42,1.05,.3),(.42,1.13,.49))
rod(black,(-.42,1.13,.49),(.42,1.13,.49),.021)
frame=(0,0,0,0)

# Foreground service counter, telephone, paper ledger and coiled cord.
box(wood,(-3.9,.46,2.7),(3.9,.92,.88),True,"Service counter")
box(wood,(-3.9,.97,2.7),(4.05,.1,1.02))
box(metal,(-3.9,.92,3.22),(4.05,.035,.02))
for x in (-5.4,-4.7,-4,-3.3,-2.6): box(black,(x,.42,3.145),(.018,.77,.012))
frame=(-3.55,1.04,2.65,-.12)
box(phone,(0,.09,0),(.5,.18,.42))
box(black,(0,.185,-.08),(.3,.01,.085))
for x in range(3):
    for z in range(4): box(black,(-.115+x*.11,.19,.01+z*.055),(.066,.025,.04))
box(phone,(-.34,.13,0),(.13,.16,.43))
box(phone,(-.30,.13,-.17),(.17,.15,.13)); box(phone,(-.30,.13,.17),(.17,.15,.13))
box(red,(.18,.195,.13),(.045,.02,.045))
for i in range(80):
    t=i/79; angle=t*TAU*13
    a=(-.34-t*.63,.06+math.cos(angle)*.025,.19+math.sin(angle)*.025+t*.4)
    angle2=(i+1)/79*TAU*13; t2=(i+1)/79
    b=(-.34-t2*.63,.06+math.cos(angle2)*.025,.19+math.sin(angle2)*.025+t2*.4)
    rod(black,a,b,.009,5)
frame=(0,0,0,0)
box(paper,(-2.75,1.027,2.7),(.62,.014,.41))
for i in range(9): box(metal,(-2.75,1.036,2.56+i*.034),(.48,.002,.003))
box(metal,(-2.35,1.04,2.8),(.015,.015,.28))
# An old monitor at the left end of the desk.
box(phone,(-5.25,1.36,2.67),(.61,.57,.41))
box(black,(-5.25,1.39,2.889),(.48,.38,.025))
box(phone,(-5.25,1.07,2.67),(.45,.1,.3))

# Fluorescent fixtures and hanging conduit.
for x,z in [(-2,-1.2),(2,-2.4),(2,2.2)]:
    box(metal,(x,3.005,z),(1.72,.12,.34))
    for dz in (-.095,.095): box(tube,(x,2.925,z+dz),(1.55,.045,.044))
    rod(metal,(x-.86,3.03,z),(x-.86,3.1,z),.015)
# Side utility pipe, drain and wall sockets.
rod(metal,(5.92,2.8,-4.8),(5.92,2.8,4.8),.035)
for x in (-2.5,0,2.5):
    box(metal,(x,1.63,-4.92),(.15,.2,.09))
    rod(black,(x,1.55,-4.84),(x,1.4,-4.5),.018)

# Export one mesh per material: many visual details, few draw calls.
binary=bytearray(); views=[]; accessors=[]; meshes=[]; nodes=[]
def view(data,target=None):
    while len(binary)%4: binary.append(0)
    offset=len(binary);binary.extend(data)
    item={"buffer":0,"byteOffset":offset,"byteLength":len(data)}
    if target: item["target"]=target
    views.append(item);return len(views)-1

def accessor(data,components,typ,ctype):
    fmt='f' if ctype==5126 else 'I'
    flat=[n for p in data for n in p] if components>1 else data
    v=view(struct.pack('<'+fmt*len(flat),*flat),34963 if ctype==5125 else 34962)
    a={"bufferView":v,"componentType":ctype,"count":len(data),"type":typ}
    if typ=="VEC3":
        a["min"]=[min(p[i] for p in data) for i in range(3)];a["max"]=[max(p[i] for p in data) for i in range(3)]
    accessors.append(a);return len(accessors)-1

for mat,(positions,normals,uvs,indices) in groups.items():
    attrs={"POSITION":accessor(positions,3,"VEC3",5126),"NORMAL":accessor(normals,3,"VEC3",5126),"TEXCOORD_0":accessor(uvs,2,"VEC2",5126)}
    meshes.append({"name":materials[mat]["name"],"primitives":[{"attributes":attrs,"indices":accessor(indices,1,"SCALAR",5125),"material":mat}]})
    nodes.append({"name":materials[mat]["name"],"mesh":len(meshes)-1})
image_defs=[{"bufferView":view(data),"mimeType":"image/png"} for data in images]
gltf={"asset":{"version":"2.0","generator":"Horror procedural laundromat"},"extensionsUsed":["KHR_materials_emissive_strength"],"scene":0,"scenes":[{"nodes":list(range(len(nodes)))}],"nodes":nodes,"meshes":meshes,"materials":materials,"textures":textures,"images":image_defs,"samplers":[{"magFilter":9728,"minFilter":9728,"wrapS":10497,"wrapT":10497}],"buffers":[{"byteLength":len(binary)}],"bufferViews":views,"accessors":accessors}
js=json.dumps(gltf,separators=(',',':')).encode(); js+=b' '*((-len(js))%4);binary.extend(b'\0'*((-len(binary))%4))
out=ROOT/'Assets/Models/Laundromat.glb'
out.write_bytes(struct.pack('<III',0x46546C67,2,12+8+len(js)+8+len(binary))+struct.pack('<II',len(js),0x4E4F534A)+js+struct.pack('<II',len(binary),0x004E4942)+binary)
layout={"spawnPosition":[-3.65,.025,4.1],"spawnYaw":-.36,"colliders":collisions}
(ROOT/'Assets/Configs/Laundromat.yaml').write_text(json.dumps(layout,indent=2)+'\n')
print(f'{out.name}: {len(binary):,} bytes, {sum(len(g[3])//3 for g in groups.values()):,} triangles, {len(meshes)} material batches, {len(collisions)} colliders')
