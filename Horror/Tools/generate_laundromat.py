"""Rebuild the original low-poly laundromat asset. Requires Python 3 + Pillow + NumPy.
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
window=material("Clear shopfront glass",(.16,.23,.28),.08,.1)
materials[window]["pbrMetallicRoughness"]["baseColorFactor"][3]=.08
materials[window]["alphaMode"]="BLEND"
wood=material("Worn laminate counter",(.26,.18,.12),.4,kind="metal")
phone=material("Old ivory telephone",(.55,.51,.4),.52,kind="metal")
seat=material("Faded waiting chairs",(.26,.29,.24),.62,kind="metal")
paper=material("Paper",(.65,.62,.48),kind="plaster")
red=material("Red indicator",(.65,.025,.016),.4,emissive=[1,.015,.008])
tube=material("Fluorescent tubes",(.86,.89,.67),.6,emissive=[3.4,3.5,2.5])
blue_led=material("Machine screen",(.08,.21,.18),.5,emissive=[.08,.27,.21])
poster=material("Laundry rules",(1,1,1),image=sign([("WASH / DRY",28,48),("NO ATTENDANT ON DUTY",107,24),("DO NOT LEAVE ITEMS",151,25),("AFTER MIDNIGHT",190,27)]))
brand=material("Laundry wall sign",(1,1,1),image=sign([("ЧИСТЫЙ ЛИСТ",32,48),("КРУГЛОСУТОЧНАЯ ПРАЧЕЧНАЯ",112,21),("ПОСЛЕ 23:00 — КАРТОЙ",179,25)]))
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


def write_model(filename):
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
        centre=[(min(p[i] for p in positions)+max(p[i] for p in positions))*.5 for i in range(3)]
        positions=[tuple(p[i]-centre[i] for i in range(3)) for p in positions]
        attrs={"POSITION":accessor(positions,3,"VEC3",5126),"NORMAL":accessor(normals,3,"VEC3",5126),"TEXCOORD_0":accessor(uvs,2,"VEC2",5126)}
        meshes.append({"name":materials[mat]["name"],"primitives":[{"attributes":attrs,"indices":accessor(indices,1,"SCALAR",5125),"material":mat}]})
        nodes.append({"name":materials[mat]["name"],"mesh":len(meshes)-1,"translation":centre})
    image_defs=[{"bufferView":view(data),"mimeType":"image/png"} for data in images]
    gltf={"asset":{"version":"2.0","generator":"Horror procedural laundromat"},"extensionsUsed":["KHR_materials_emissive_strength"],"scene":0,"scenes":[{"nodes":list(range(len(nodes)))}],"nodes":nodes,"meshes":meshes,"materials":materials,"textures":textures,"images":image_defs,"samplers":[{"magFilter":9728,"minFilter":9728,"wrapS":10497,"wrapT":10497}],"buffers":[{"byteLength":len(binary)}],"bufferViews":views,"accessors":accessors}
    js=json.dumps(gltf,separators=(',',':')).encode(); js+=b' '*((-len(js))%4);binary.extend(b'\0'*((-len(binary))%4))
    out=ROOT/'Assets/Models'/filename
    out.write_bytes(struct.pack('<III',0x46546C67,2,12+8+len(js)+8+len(binary))+struct.pack('<II',len(js),0x4E4F534A)+js+struct.pack('<II',len(binary),0x004E4942)+binary)
    print(f"{filename}: {len(binary):,} geometry bytes, {len(meshes)} batches")

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

# Real clear panes: only frames and glass, with exterior geometry beyond them.
for z in (-4,-2,0,2):
    frame=(-5.975,1.62,z,math.pi/2)
    label(window,(0,0,0),1.88,2.72)
    frame=(0,0,0,0)
    collisions.append({"name":"Shopfront glass","position":[-6.01,1.55,z],"size":[.14,3.1,2],"yaw":0})
for z in (-5,-3,-1,1,3,5):
    box(metal,(-5.94,1.55,z),(.12,3.1,.08))
for z in (-4,-2,0,2): box(metal,(-5.94,1.0,z),(.08,.055,2))
box(metal,(-5.94,2.99,0),(.12,.12,10)); box(metal,(-5.94,.15,-1),(.12,.14,8))
# The last shopfront bay is a separate hinged, interactive entrance door.
box(metal,(-5.94,2.86,4),(.12,.08,2))
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

# The street is modelled at world scale, with a bus stop and a closed florist
# across the road as described in the story. It remains visible from every pane.
asphalt=material("Wet asphalt",(.105,.12,.14),.19,.18,kind="plaster")
concrete=material("Wet sidewalk",(.31,.32,.31),.33,kind="tile")
brick=material("Old street brick",(.27,.22,.20),kind="plaster")
shutter=material("Closed steel shutters",(.22,.25,.25),.42,.5,kind="metal")
car_grey=material("Grey estate car paint",(.27,.3,.32),.22,.45)
car_blue=material("Dark blue saloon paint",(.07,.12,.16),.25,.4)
car_window=material("Dark car windows",(.025,.055,.075),.09,.55)
streetlamp=material("Sodium street lamps",(.95,.7,.32),emissive=[2.7,1.8,.65])
window_light=material("Distant apartment windows",(.44,.37,.21),emissive=[.48,.35,.16])
road_paint=material("Worn road paint",(.55,.54,.43),.42)
florist_sign=material("Closed florist sign",(1,1,1),image=sign([("ЦВЕТЫ",28,94),("МАГАЗИН ЗАКРЫТ",157,35)],(184,170,125),(21,37,31)))
bus_sign=material("Bus stop sign",(1,1,1),image=sign([("АВТОБУС",22,60),("24   36   81",117,38),("ПРАЧЕЧНАЯ",184,27)],(207,209,190),(24,47,68)))
address_sign=material("Laundry entrance sign",(1,1,1),image=sign([("ЧИСТЫЙ ЛИСТ",24,45),("24 ЧАСА",101,55),("ВХОД / E",189,27)]))
box(asphalt,(-14,-.34,0),(9,.24,48),True,"Road")
box(concrete,(-8.05,-.17,0),(4.1,.2,44),True,"Laundry sidewalk")
box(concrete,(-19.65,-.17,0),(2.7,.2,44),True,"Opposite sidewalk")
for z in range(-21,22,2):
    box(concrete,(-10.2,-.16,z),(.18,.22,1.97))
    box(concrete,(-18.25,-.16,z),(.18,.22,1.97))
for z in range(-21,22,5): box(road_paint,(-14.3,-.215,z),(.09,.008,2.3))
for z in (-7.5,-2.5,2.5,7.5):
    box(road_paint,(-16.9,-.214,z),(.055,.008,3.8))
for x in range(6):
    box(road_paint,(-10.7-x*1.35,-.213,13.0),(.75,.008,2.5))
# Shop canopy keeps a strip of the pavement dry; rain shaders use the same bounds.
box(metal,(-6.7,3.12,0),(1.6,.15,11.2))
for z in (-4.8,4.8): rod(metal,(-6.1,2.55,z),(-7.4,3.04,z),.025)
frame=(-6.12,2.45,4.0,-math.pi/2)
label(address_sign,(0,0,0),1.4,.55)
frame=(0,0,0,0)
# Neighbouring buildings continue the shopfront rather than leaving a floating room.
for z in (-12.3,12.3):
    box(brick,(.0,3.6,z),(12.2,7.4,14.3),True,"Neighbouring building")
    frame=(-6.12,0,z,-math.pi/2)
    for xx in (-4,-1.5,1.5,4):
        box(shutter,(xx,1.5,0),(2.1,2.9,.12))
        for y in range(15): box(metal,(xx,.18+y*.18,.08),(2.05,.02,.04))
    for xx in (-4,-1.5,1.5,4):
        for y in (4.2,6.15): box(car_window,(xx,y,.08),(1.25,1.3,.06))
    frame=(0,0,0,0)
# Opposite row: florist is to the left from the service counter's window view.
for z in (-13,-3,9):
    box(brick,(-23.1,4.2,z),(4.2,8.7,9.8),True,"Opposite shops")
    frame=(-20.94,0,z,math.pi/2)
    for xx in (-3,0,3):
        box(shutter,(xx,1.45,.02),(2.7,2.9,.09))
        for y in range(17): box(metal,(xx,.1+y*.17,.075),(2.68,.025,.045))
    for xx in (-3,0,3):
        for yi,y in enumerate((4.5,6.6)):
            box(metal,(xx,y,0),(1.4,1.65,.12))
            box(window_light if (int(z)+int(xx)+yi)%4==0 else car_window,(xx,y,.09),(1.19,1.44,.04))
    if z==-3:
        label(florist_sign,(0,3.35,.15),5.6,.95)
        box(seat,(0,3.92,.5),(8.6,.15,1.3))
        for xx in (-3.9,3.9): rod(metal,(xx,3.8,.9),(xx,3.05,.05),.025)
        # Empty flower buckets visible under the shutter, shop is closed for the night.
        for xx in (-3.8,-3.1,3.5): box(black,(xx,.18,.8),(.37,.5,.37))
    frame=(0,0,0,0)
# Bus shelter across the street, with its own transparent glass and a bench.
box(metal,(-19.5,2.66,3.2),(2.05,.16,4.4))
for x in (-20.4,-18.65):
    for z in (1.1,5.3): rod(metal,(x,-.06,z),(x,2.59,z),.035)
frame=(-20.4,1.32,3.2,math.pi/2)
label(window,(0,0,0),4.1,2.45)
frame=(0,0,0,0)
box(wood,(-19.93,.46,3.2),(.48,.11,3.1),True,"Bus stop bench")
box(wood,(-20.12,.85,3.2),(.075,.68,3.1))
for z in (1.9,4.4): box(metal,(-19.95,.2,z),(.3,.5,.12))
rod(metal,(-18.8,-.1,6.2),(-18.8,2.9,6.2),.035)
frame=(-18.76,2.3,6.2,math.pi/2)
label(bus_sign,(0,0,0),.65,.8)
frame=(0,0,0,0)
# Street furniture and warm pools of light in otherwise empty late-night streets.
for x,z in [(-10.05,-5.5),(-18.55,8.0)]:
    rod(metal,(x,-.1,z),(x,5.4,z),.055,8)
    reach=1 if x>-14 else -1
    rod(metal,(x,5.4,z),(x+reach,5.4,z),.035,8)
    box(metal,(x+reach,5.35,z),(.7,.16,.35))
    box(streetlamp,(x+reach,5.25,z),(.52,.035,.25))
for z in (-10,11):
    box(black,(-8.7,.38,z),(.48,.9,.48),True,"Street litter bin")
    box(metal,(-8.7,.85,z),(.52,.06,.52))
# Closed drains and puddle depressions along both gutters.
for z in (-16,-8,0,8,16):
    box(metal,(-10.45,-.207,z),(.45,.02,.65))
    for k in range(6): box(black,(-10.6+k*.06,-.193,z),(.025,.008,.57))


def car(x,z,yaw,paint,estate=False):
    global frame
    frame=(x,-.13,z,yaw)
    length=4.5 if estate else 4.2
    box(paint,(0,.48,0),(1.78,.68,length),True,"Parked grey estate" if estate else "Parked dark car")
    box(paint,(0,.99,-.22),(1.64,.63,2.65 if estate else 2.25))
    # Flat low-poly glass remains dark: the interior and occupants cannot be seen.
    box(car_window,(0,1.13,-.22),(1.675,.39,2.28 if estate else 1.91))
    box(paint,(0,1.34,-.22),(1.72,.08,2.6 if estate else 2.22))
    for zz in (-.9,.25): box(paint,(0,1.13,zz),(1.72,.43,.07))
    box(paint,(0,.72,0),(1.83,.065,length+.025))
    for xx in (-.94,.94):
        for zz in (-1.43,1.43):
            saved=frame
            wheel=world((xx,.33,zz)); frame=(*wheel,yaw+math.pi/2)
            disk(black,(0,0,0),.33,20);disk(metal,(0,0,.008 if xx>0 else -.008),.18,16)
            frame=saved
    for xx in (-.58,.58):
        box(paper,(xx,.57,length/2+.012),(.43,.18,.03))
        box(red,(xx,.57,-length/2-.012),(.4,.16,.03))
        box(metal,(xx*.98,.9,.12),(.14,.04,.025))
    box(black,(0,.34,length/2+.03),(1.7,.15,.05))
    box(paper,(0,.4,-length/2-.04),(.46,.1,.015))
    frame=(0,0,0,0)

car(-17.0,-6.5,0,car_grey,True)
car(-11.35,7.5,math.pi,car_blue)
# Concrete end barriers are physical map limits, softened by distant fog.
for z in (-22,22):
    box(concrete,(-14,-.02,z),(8.0,.4,.5),True,"Street end barrier")
for x in (-8.1,-19.7):
    for z in (-21.8,21.8): box(brick,(x,1,z),(3.8,2.2,.4),True,"Pavement end wall")


write_model("Laundromat.glb")
layout={"spawnPosition":[-3.65,.025,4.1],"spawnYaw":-.36,"colliders":collisions}
(ROOT/'Assets/Configs/Laundromat.yaml').write_text(json.dumps(layout,indent=2)+'\n')

# Separate leaf: its model origin is the hinge, not the centre of the glass.
groups = {}
frame=(0,0,0,0)
for z in (0,1.9): box(metal,(0,1.43,z),(.09,2.86,.075))
for y in (.07,1.0,2.82): box(metal,(0,y,.95),(.09,.07,1.9))
box(metal,(0,.25,.95),(.07,.3,1.85))
quad(window,[(0,.41,.055),(0,.41,1.845),(0,2.77,1.845),(0,2.77,.055)],(1,0,0))
for x in (-.095,.095):
    rod(metal,(x,.98,1.66),(x,1.27,1.66),.024)
    box(metal,(x*.6,1.1,1.8),(.055,.24,.08))
write_model("EntranceDoor.glb")

# GPU-animated world-space rain. Two crossed quads per streak keep drops visible
# from the pavement and through the windows without a camera-facing overlay.
groups = {}
frame=(0,0,0,0)
rain_mat=material("Weather Rain",(.6,.7,.8))
splash_mat=material("Weather Splashes",(.6,.7,.8))
weather_rng=random.Random(206)

def weather_quad(mat,points,seed):
    g=groups.setdefault(mat,[[],[],[],[]]); start=len(g[0])
    g[0].extend(points)
    # Unit normals carry a stable per-particle phase for the custom weather shader.
    g[1].extend([(seed,math.sqrt(1-seed*seed),0)]*4)
    g[2].extend([(0,0),(1,0),(1,1),(0,1)])
    g[3].extend([start,start+1,start+2,start,start+2,start+3])

for _ in range(6500):
    x=weather_rng.uniform(-20.7,-6.28);z=weather_rng.uniform(-21.5,21.5)
    seed=weather_rng.random();y=seed*8
    w=.009;h=.11
    weather_quad(rain_mat,[(x-w,y-h,z),(x+w,y-h,z),(x+w,y+h,z),(x-w,y+h,z)],seed)
    weather_quad(rain_mat,[(x,y-h,z-w),(x,y-h,z+w),(x,y+h,z+w),(x,y+h,z-w)],seed)
for _ in range(1100):
    x=weather_rng.uniform(-20.7,-6.28);z=weather_rng.uniform(-21.5,21.5)
    # No rain beneath the shop canopy, bus shelter or parked vehicles.
    if x>-7.5 and abs(z)<5.6: continue
    if -20.6<x<-18.4 and 1.0<z<5.5: continue
    if abs(x+17)<1.0 and abs(z+6.5)<2.4: continue
    if abs(x+11.35)<1.0 and abs(z-7.5)<2.3: continue
    y=-.059 if x>-10.1 or x<-18.3 else -.208
    w=.16
    weather_quad(splash_mat,[(x-w,y,z-w),(x+w,y,z-w),(x+w,y,z+w),(x-w,y,z+w)],weather_rng.random())
write_model("Weather.glb")

# Original procedural rain ambience, seamless by construction (circular filtering).
# The moving rain is visible independently of audio backend availability.
import wave
import numpy as np
sound_rng=np.random.default_rng(206)
rate=22050; count=rate*8
noise=sound_rng.standard_normal((count,2))
low=sum(np.roll(noise,k,axis=0) for k in range(12))/12
rain_sound=noise*.08+low*.5
# Sparse soft taps, wrapped at the loop seam.
for _ in range(420):
    start=int(sound_rng.integers(count)); length=int(sound_rng.integers(35,180))
    t=np.arange(length)
    tap=np.sin(t*sound_rng.uniform(.18,.7))*np.exp(-t/(length*.22))*sound_rng.uniform(.025,.12)
    channel=int(sound_rng.integers(2))
    rain_sound[(start+t)%count,channel]+=tap
rain_sound=np.clip(rain_sound,-.9,.9)
(ROOT/'Assets/Audio').mkdir(exist_ok=True)
with wave.open(str(ROOT/'Assets/Audio/Rain.wav'),'wb') as wav:
    wav.setnchannels(2);wav.setsampwidth(2);wav.setframerate(rate)
    wav.writeframes((rain_sound*32767).astype('<i2').tobytes())
