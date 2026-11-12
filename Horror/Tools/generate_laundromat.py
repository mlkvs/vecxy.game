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
glass=material("Domed washer glass",(.10,.17,.20),.12,.35)
materials[glass]["pbrMetallicRoughness"]["baseColorFactor"][3]=.32
materials[glass]["alphaMode"]="BLEND"
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


fabric_blue=material("Folded blue laundry",(.19,.29,.34),.93)
fabric_cream=material("Folded cream laundry",(.6,.56,.44),.96)
machine_numbers=material("Machine cycle plate",(1,1,1),image=sign([("WASH / DRY",78,52)],(90,100,88),(170,173,149)))
key_atlas=Image.new("RGB",(192,256),(32,33,29))
key_draw=ImageDraw.Draw(key_atlas)
for j,ch in enumerate("123456789*0#"):
    key_draw.text((j%3*64+32,j//3*64+4),ch,anchor="mt",font=font(39),fill=(192,188,158))
keypad=material("Telephone numbered keys",(1,1,1),image=key_atlas)
crt_glass=material("Curved CRT glass",(.035,.065,.055),.16,.2,emissive=[.008,.018,.012])


def world(p, normal=False):
    x,y,z=p; a,b,c,yaw=frame
    co,si=math.cos(yaw),math.sin(yaw)
    return (co*x+si*z+(0 if normal else a),y+(0 if normal else b),-si*x+co*z+(0 if normal else c))


def quad(mat, points, normal, uvscale=(1,1)):
    normal=unit(normal)
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


def rod(mat, start, end, radius=.012, segments=8):
    tube_path(mat,[start,end],radius,segments)


def label(mat, pos, width, height):
    x,y,z=pos
    quad(mat,[(x-width/2,y-height/2,z),(x+width/2,y-height/2,z),(x+width/2,y+height/2,z),(x-width/2,y+height/2,z)],(0,0,1))


def surface(mat, points, normals, uvs):
    """A shaded quad; normals are transformed with the object's local frame."""
    geometric=face_normal(points)
    average=tuple(sum(n[i] for n in normals) for i in range(3))
    if sum(geometric[i]*average[i] for i in range(3))<0:
        points=list(reversed(points));normals=list(reversed(normals));uvs=list(reversed(uvs))
    g=groups.setdefault(mat,[[],[],[],[]]); start=len(g[0])
    g[0].extend(world(p) for p in points)
    g[1].extend(world(n,True) for n in normals)
    g[2].extend(uvs)
    g[3].extend([start,start+1,start+2,start,start+2,start+3])


def unit(v):
    length=math.sqrt(sum(n*n for n in v))
    return tuple(n/length for n in v)


def face_normal(points):
    a=[points[1][i]-points[0][i] for i in range(3)]
    b=[points[2][i]-points[0][i] for i in range(3)]
    return unit((a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]))


def rounded_box(mat,pos,size,radius=.025,collide=False,name="Obstacle",steps=2,front=True):
    """Six rounded surfaces, with shared analytic shading across bevels.

    Large flat faces stay flat; extra vertices are concentrated at the edges.
    The result fits the exact same bounds as the corresponding box collider.
    """
    half=[v*.5 for v in size]; radius=min(radius,min(half)*.9)
    def samples(h):
        low=[-h+radius*i/steps for i in range(steps+1)]
        return low+[-v for v in reversed(low)]
    for axis,sign,u,v in [(2,1,0,1),(2,-1,0,1),(0,1,2,1),(0,-1,2,1),(1,1,0,2),(1,-1,0,2)]:
        if not front and axis==2 and sign==1: continue
        us,vs=samples(half[u]),samples(half[v])
        def vertex(a,b):
            p=[0.,0.,0.];p[axis]=sign*half[axis];p[u]=a;p[v]=b
            inner=[max(-half[i]+radius,min(half[i]-radius,p[i])) for i in range(3)]
            normal=unit([p[i]-inner[i] for i in range(3)])
            return tuple(pos[i]+inner[i]+normal[i]*radius for i in range(3)),normal
        for j in range(len(vs)-1):
            for i in range(len(us)-1):
                corners=[(us[i],vs[j]),(us[i+1],vs[j]),(us[i+1],vs[j+1]),(us[i],vs[j+1])]
                vertices=[vertex(a,b) for a,b in corners]
                points=[p for p,n in vertices];normals=[n for p,n in vertices]
                geometric=face_normal(points)
                if geometric[axis]*sign<0: points.reverse();normals.reverse();corners.reverse()
                uvs=[((a/size[u]+.5)*size[u],(.5-b/size[v])*size[v]) for a,b in corners]
                surface(mat,points,normals,uvs)
    if collide: collisions.append({"name":name,"position":world(pos),"size":size,"yaw":frame[3]})


def torus(mat,pos,major,minor,segments=32,sides=8):
    """A solid, smoothly shaded ring around the local Z axis."""
    def vertex(a,b):
        n=(math.cos(a)*math.cos(b),math.sin(a)*math.cos(b),math.sin(b))
        p=(pos[0]+(major+minor*math.cos(b))*math.cos(a),pos[1]+(major+minor*math.cos(b))*math.sin(a),pos[2]+minor*math.sin(b))
        return p,n
    for i in range(segments):
        for j in range(sides):
            params=[(TAU*i/segments,TAU*j/sides),(TAU*(i+1)/segments,TAU*j/sides),(TAU*(i+1)/segments,TAU*(j+1)/sides),(TAU*i/segments,TAU*(j+1)/sides)]
            vertices=[vertex(a,b) for a,b in params]
            surface(mat,[p for p,n in vertices],[n for p,n in vertices],[(a/TAU,b/TAU) for a,b in params])


def ellipsoid(mat,pos,radii,segments=16,rings=8):
    # Slightly truncated poles avoid zero-area faces while retaining the silhouette.
    def vertex(a,b):
        direction=(math.cos(a)*math.cos(b),math.sin(b),math.sin(a)*math.cos(b))
        return tuple(pos[i]+radii[i]*direction[i] for i in range(3)),unit([direction[i]/radii[i] for i in range(3)])
    for j in range(rings):
        b0=-math.pi/2+.003+(math.pi-.006)*j/rings
        b1=-math.pi/2+.003+(math.pi-.006)*(j+1)/rings
        for i in range(segments):
            params=[(TAU*i/segments,b0),(TAU*(i+1)/segments,b0),(TAU*(i+1)/segments,b1),(TAU*i/segments,b1)]
            vertices=[vertex(a,b) for a,b in params]
            surface(mat,[p for p,n in vertices],[n for p,n in vertices],[(a/TAU,b/math.pi+.5) for a,b in params])


def tube_path(mat,points,radius=.018,sides=8):
    """Swept tube with continuous normals, for bent frames, handles and receivers."""
    rings=[]
    previous_u=None
    for i,p in enumerate(points):
        lo=points[max(0,i-1)];hi=points[min(len(points)-1,i+1)]
        w=unit([hi[k]-lo[k] for k in range(3)]);ref=(0,1,0) if abs(w[1])<.9 else (1,0,0)
        if previous_u is not None:
            projection=sum(previous_u[k]*w[k] for k in range(3))
            projected=[previous_u[k]-projection*w[k] for k in range(3)]
        else:
            projected=[0,0,0]
        u=unit(projected) if sum(v*v for v in projected)>1e-8 else unit((w[1]*ref[2]-w[2]*ref[1],w[2]*ref[0]-w[0]*ref[2],w[0]*ref[1]-w[1]*ref[0]))
        previous_u=u
        v=(w[1]*u[2]-w[2]*u[1],w[2]*u[0]-w[0]*u[2],w[0]*u[1]-w[1]*u[0])
        ring=[]
        for j in range(sides):
            a=TAU*j/sides;n=tuple(math.cos(a)*u[k]+math.sin(a)*v[k] for k in range(3))
            ring.append((tuple(p[k]+radius*n[k] for k in range(3)),n))
        rings.append(ring)
    for i in range(len(rings)-1):
        for j in range(sides):
            jj=(j+1)%sides
            verts=[rings[i][j],rings[i+1][j],rings[i+1][jj],rings[i][jj]]
            surface(mat,[p for p,n in verts],[n for p,n in verts],[(0,0),(0,1),(1,1),(1,0)])


def loft(mat,sections):
    """Join matching closed XY contours along Z, retaining deliberate planar facets."""
    for k in range(len(sections)-1):
        z0,shape0=sections[k];z1,shape1=sections[k+1]
        for i in range(len(shape0)):
            j=(i+1)%len(shape0)
            points=[(*shape0[i],z0),(*shape0[j],z0),(*shape1[j],z1),(*shape1[i],z1)]
            n=face_normal(points)
            quad(mat,points,n)
    # Fan caps use real triangles rather than overlapping degenerate quads.
    for (z,shape),direction in [(sections[0],-1),(sections[-1],1)]:
        g=groups.setdefault(mat,[[],[],[],[]]);start=len(g[0])
        pts=[(*p,z) for p in shape]
        g[0].extend(world(p) for p in pts);g[1].extend([world((0,0,direction),True)]*len(pts))
        g[2].extend([(p[0],p[1]) for p in shape])
        for i in range(1,len(pts)-1): g[3].extend([start,start+i,start+i+1] if direction==1 else [start,start+i+1,start+i])


def chamfer_contour(width,bottom,top,bevel=.04):
    w=width*.5;b=min(bevel,(top-bottom)*.3,width*.15)
    return [(-w+b,bottom),(w-b,bottom),(w,bottom+b),(w,top-b),(w-b,top),(-w+b,top),(-w,top-b),(-w,bottom+b)]


def washer_front(mat,pos,half,radius,segments=48):
    """Front sheet with a genuine circular opening, rather than a disc pasted on a cube."""
    x,y,z=pos
    for i in range(segments):
        a,b=TAU*i/segments,TAU*(i+1)/segments
        def outer(t):
            co,si=math.cos(t),math.sin(t)
            reach=min(half[0]/max(abs(co),1e-8),half[1]/max(abs(si),1e-8))
            return (x+reach*co,y+reach*si,z)
        quad(mat,[(x+radius*math.cos(a),y+radius*math.sin(a),z),outer(a),outer(b),(x+radius*math.cos(b),y+radius*math.sin(b),z)],(0,0,1))


def chair_shell(mat):
    profile=[(-.27,1.04,.235),(-.29,1.00,.285),(-.28,.82,.30),(-.25,.65,.30),(-.18,.52,.30),(-.06,.475,.30),(.14,.465,.30),(.25,.48,.285),(.285,.49,.235)]
    across=[-1,-.88,0,.88,1]
    for sign in (-1,1):
        for j in range(len(profile)-1):
            for i in range(len(across)-1):
                points=[]
                for row,col in [(j,i),(j,i+1),(j+1,i+1),(j+1,i)]:
                    z,y,w=profile[row];u=across[col]
                    points.append((u*w,y+.026*u*u+sign*.018,z))
                n=face_normal(points)
                if sign<0: points.reverse();n=tuple(-v for v in n)
                quad(mat,points,n)
    for side in (-1,1):
        path=[(side*w,y+.026,z) for z,y,w in profile]
        tube_path(mat,path,.019,6)


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
    # The front is open: the drum, rubber seal and domed glass have actual depth.
    rounded_box(enamel,(0,.71,0),(1.17,1.4,.94),.045,True,"Washing machine %02d"%index,front=False)
    washer_front(enamel,(0,.66,.471),(.54,.62),.343)
    rounded_box(enamel,(0,1.335,.466),(1.08,.14,.035),.012,steps=1)
    rounded_box(black,(0,.085,.01),(1.12,.15,.91),.022)
    rounded_box(metal,(0,1.39,0),(1.18,.045,.95),.018,steps=1)
    rounded_box(metal,(0,1.245,.483),(1.075,.21,.032),.016,steps=1)
    rounded_box(black,(-.27,1.255,.508),(.42,.12,.023),.012,steps=1)
    rounded_box(blue_led,(-.29,1.265,.524),(.23,.044,.012),.006,steps=1)
    for k in range(3): rounded_box(phone,(-.4+k*.09,1.21,.525),(.045,.02,.016),.005,steps=1)
    torus(black,(.27,1.25,.526),.058,.012,24,6)
    disk(metal,(.27,1.25,.544),.052,24)
    rounded_box(phone,(.27,1.26,.555),(.012,.065,.012),.004,steps=1)
    rounded_box(black,(.455,1.25,.515),(.04,.10,.016),.005,steps=1)
    disk(red,(.455,1.295,.529),.011,12)
    # Drum interior is recessed by 30 cm; three lifters and holes remain visible.
    disk(drum,(0,.66,.155),.334,40)
    ring(drum,(0,.66,.306),.337,.331,.30,40)
    torus(black,(0,.66,.486),.344,.026,40,8)
    torus(metal,(0,.66,.525),.386,.029,40,8)
    torus(enamel,(0,.66,.55),.373,.023,40,8)
    torus(black,(0,.66,.559),.329,.019,40,6)
    for j in range(30):
        a=TAU*j/30
        disk(black,(.282*math.cos(a),.66+.282*math.sin(a),.167),.009,6)
    for j in range(3):
        a=TAU*j/3+.4
        tube_path(drum,[(.27*math.cos(a),.66+.27*math.sin(a),zz) for zz in (.18,.23,.36,.43)],.017,6)
    if index%3==0:
        for j in range(4):
            ellipsoid(fabric_blue if j%2==0 else fabric_cream,(-.18+j*.105,.48+(.035 if j%2 else 0),.29),(.12,.075,.09),12,6)
    # Convex front glass, with smooth normals and no opaque disc blocking the drum.
    for j in range(5):
        r0=.0005+.317*j/5;r1=.0005+.317*(j+1)/5
        for i in range(40):
            params=[(r0,TAU*i/40),(r1,TAU*i/40),(r1,TAU*(i+1)/40),(r0,TAU*(i+1)/40)]
            points=[];normals=[]
            for r,a in params:
                xx,yy=r*math.cos(a),r*math.sin(a)
                points.append((xx,.66+yy,.563+.036*(1-(r/.318)**2)))
                normals.append(unit((xx*.72,yy*.72,1)))
            surface(glass,points,normals,[(r/.318,a/TAU) for r,a in params])
    tube_path(metal,[(.345,.56,.57),(.395,.59,.615),(.405,.71,.615),(.35,.76,.57)],.019,8)
    for yy in (.44,.88): rounded_box(metal,(-.39,yy,.536),(.075,.09,.045),.012,steps=1)
    label(machine_numbers,(0,1.025,.478),.19,.055)
    # Separate detergent drawer, grille, fasteners and seams.
    rounded_box(black,(-.29,1.035,.481),(.24,.07,.018),.007,steps=1)
    rounded_box(enamel,(-.29,1.045,.493),(.21,.035,.021),.006,steps=1)
    for k in range(10): rounded_box(black,(-.37+k*.082,.17,.481),(.045,.012,.012),.004,steps=1)
    for xx in (-.49,.49):
        for yy in (.12,1.31): disk(metal,(xx,yy,.486),.008,8)
    frame=(0,0,0,0)

for i in range(7): washer(-3.7+i*1.29,-4.41,0,i+1)
for i in range(5): washer(5.4,-2.9+i*1.29,-math.pi/2,i+8)

# Posters on the back wall, above machine height.
label(brand,(1.25,2.35,-4.978),3.35,1.12)
label(poster,(-3.15,2.28,-4.974),.8,1.05)
label(poster,(4.23,2.28,-4.974),.65,.9)

# Moulded shell chairs on bent tubular sled legs.
for i in range(3):
    frame=(-5.1,0,-2.55+i*.72,math.pi/2)
    collisions.append({"name":"Waiting chair","position":world((0,.47,0)),"size":[.61,.13,.55],"yaw":frame[3]})
    chair_shell(seat)
    for x in (-.23,.23):
        tube_path(metal,[(x,.46,.19),(x,.16,.22),(x,.075,.16),(x,.075,-.22),(x,.14,-.25),(x,.6,-.19)],.02,8)
        rounded_box(black,(x,.065,0),(.055,.035,.27),.01,steps=1)
    frame=(0,0,0,0)

# Tapered wire basket with rolled edges, bent chassis and solid caster wheels.
frame=(-1.9,0,-.35,-.2)
rounded_box(metal,(0,.53,0),(.82,.045,.61),.016,True,"Laundry trolley basket",steps=1)
collisions.append({"name":"Laundry trolley","position":world((0,.66,0)),"size":[.88,1.08,.66],"yaw":frame[3]})
for y in (.55,.72,.9,1.05):
    t=(y-.55)/.5;w=.35+.075*t;d=.24+.08*t
    path=[(-w+.04,y,-d), (w-.04,y,-d),(w,y,-d+.04),(w,y,d-.04),(w-.04,y,d),(-w+.04,y,d),(-w,y,d-.04),(-w,y,-d+.04),(-w+.04,y,-d)]
    tube_path(metal,path,.014 if y==1.05 else .009,6)
for i in range(9):
    u=-1+i*.25
    for basket_side in (-1,1): tube_path(metal,[(u*.35,.54,basket_side*.24),(u*.425,1.05,basket_side*.32)],.008,6)
for i in range(7):
    u=-1+i/3
    for basket_side in (-1,1): tube_path(metal,[(basket_side*.35,.54,u*.24),(basket_side*.425,1.05,u*.32)],.008,6)
for x in (-.34,.34):
    tube_path(metal,[(x,.16,-.23),(x,.19,0),(x,.16,.23)],.018,8)
    for z in (-.24,.24):
        rod(metal,(x,.17,z),(x,.51,z),.016,8)
        rounded_box(metal,(x,.15,z),(.09,.1,.065),.015,steps=1)
        torus(black,(x,.095,z+.035),.056,.019,16,6)
        disk(metal,(x,.095,z+.051),.033,12)
        disk(black,(x,.095,z+.055),.011,8)
tube_path(metal,[(-.425,1.05,.32),(-.425,1.12,.47),(-.38,1.15,.51),(.38,1.15,.51),(.425,1.12,.47),(.425,1.05,.32)],.016,8)
tube_path(black,[(-.34,1.15,.51),(0,1.15,.51),(.34,1.15,.51)],.023,10)
frame=(0,0,0,0)

# Service counter with a rounded worktop, recessed cupboard panels and pulls.
rounded_box(wood,(-3.9,.46,2.7),(3.9,.92,.88),.035,True,"Service counter")
rounded_box(wood,(-3.9,.97,2.7),(4.05,.1,1.02),.035)
rounded_box(metal,(-3.9,.928,3.19),(4.0,.024,.035),.009,steps=1)
for x in (-5.35,-4.62,-3.89,-3.16,-2.43):
    rounded_box(black,(x,.43,3.145),(.68,.75,.015),.018,steps=1)
    rounded_box(wood,(x,.43,3.159),(.645,.71,.018),.018,steps=1)
    tube_path(metal,[(x-.085,.72,3.175),(x-.085,.72,3.205),(x+.085,.72,3.205),(x+.085,.72,3.175)],.009,6)

# Sloping desk telephone, twelve labelled keys, curved receiver and coiled cable.
frame=(-3.55,1.04,2.65,-.12)
loft(phone,[(-.22,chamfer_contour(.49,.025,.185,.035)),(-.17,chamfer_contour(.52,.018,.19,.035)),(.16,chamfer_contour(.50,.018,.075,.025)),(.22,chamfer_contour(.43,.03,.062,.02))])
rounded_box(black,(0,.179,-.155),(.29,.012,.048),.006,steps=1)
for x in range(3):
    for z in range(4):
        xx=-.10+x*.098;zz=-.062+z*.052
        yy=.151-(zz+.06)*.34
        rounded_box(black,(xx,yy,zz),(.074,.018,.042),.009,steps=1)
        points=[(xx-.03,yy+.010,zz-.016),(xx+.03,yy+.010,zz-.016),(xx+.03,yy+.010,zz+.016),(xx-.03,yy+.010,zz+.016)]
        u0,u1=x/3,(x+1)/3;v0,v1=z/4,(z+1)/4
        surface(keypad,points,[(0,1,0)]*4,[(u0,v0),(u1,v0),(u1,v1),(u0,v1)])
tube_path(phone,[(-.31,.135,-.185),(-.36,.205,-.12),(-.385,.23,0),(-.36,.205,.12),(-.31,.135,.185)],.041,10)
for z in (-.19,.19):
    ellipsoid(phone,(-.31,.13,z),(.077,.057,.073),16,8)
    for j in range(4): rounded_box(black,(-.31+(j-1.5)*.018,.177,z),(.006,.005,.05),.002,steps=1)
ellipsoid(red,(.196,.094,.127),(.018,.007,.018),12,6)
for i in range(80):
    t=i/79;angle=t*TAU*13;t2=(i+1)/79;angle2=t2*TAU*13
    a=(-.34-t*.63,.06+math.cos(angle)*.025,.19+math.sin(angle)*.025+t*.4)
    b=(-.34-t2*.63,.06+math.cos(angle2)*.025,.19+math.sin(angle2)*.025+t2*.4)
    rod(black,a,b,.008,6)
frame=(0,0,0,0)
box(paper,(-2.75,1.027,2.7),(.62,.014,.41))
for i in range(9): box(metal,(-2.75,1.036,2.56+i*.034),(.48,.002,.003))
rod(metal,(-2.35,1.045,2.66),(-2.35,1.045,2.94),.006,8)

# Tapered CRT casing, curved front glass, pedestal and a separate keyboard.
frame=(-5.25,1.08,2.67,0)
loft(phone,[(-.23,chamfer_contour(.39,.09,.48,.035)),(-.14,chamfer_contour(.52,.08,.54,.04)),(.18,chamfer_contour(.64,.04,.59,.045)),(.235,chamfer_contour(.60,.055,.575,.035))])
rounded_box(black,(0,.323,.245),(.508,.4,.019),.035)
# Smooth convex screen, curved in both axes rather than a flat black rectangle.
for y in range(8):
    for x in range(10):
        points=[];normals=[]
        for xx,yy in [(x,y),(x+1,y),(x+1,y+1),(x,y+1)]:
            u=xx/10*2-1;v=yy/8*2-1
            points.append((u*.233,.323+v*.174,.259+.025*(1-u*u)* (1-v*v)))
            normals.append(unit((u*.11,v*.12,1)))
        surface(crt_glass,points,normals,[(0,1),(1,1),(1,0),(0,0)])
for x in range(6): rounded_box(black,(-.19+x*.065,.09,.253),(.035,.008,.01),.003,steps=1)
disk(blue_led,(.24,.102,.259),.008,10)
rod(metal,(0,-.04,-.015),(0,.12,-.015),.055,12)
rounded_box(phone,(0,-.055,0),(.43,.055,.31),.024)
rounded_box(phone,(0,-.055,.36),(.55,.055,.18),.016)
for j in range(4):
    for i in range(12): rounded_box(black,(-.24+i*.043,-.022,.3+j*.035),(.033,.012,.026),.004,steps=1)
frame=(0,0,0,0)

# Fluorescent fixtures and hanging conduit.
for x,z in [(-2,-1.2),(2,-2.4),(2,2.2)]:
    rounded_box(metal,(x,3.005,z),(1.72,.12,.34),.025)
    for dz in (-.095,.095): rod(tube,(x-.775,2.925,z+dz),(x+.775,2.925,z+dz),.022,10)
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
    rounded_box(metal,(x+reach,5.35,z),(.7,.16,.35),.055)
    box(streetlamp,(x+reach,5.25,z),(.52,.035,.25))
for z in (-10,11):
    rounded_box(black,(-8.7,.38,z),(.48,.9,.48),.065,True,"Street litter bin")
    rounded_box(metal,(-8.7,.85,z),(.52,.06,.52),.025)
# Closed drains and puddle depressions along both gutters.
for z in (-16,-8,0,8,16):
    box(metal,(-10.45,-.207,z),(.45,.02,.65))
    for k in range(6): box(black,(-10.6+k*.06,-.193,z),(.025,.008,.57))


def car(x,z,yaw,paint,estate=False):
    global frame
    frame=(x,-.13,z,yaw)
    length=4.5 if estate else 4.2;half=length*.5
    collisions.append({"name":"Parked grey estate" if estate else "Parked dark car","position":world((0,.48,0)),"size":[1.78,.68,length],"yaw":frame[3]})
    # A shaped lower body: tapered nose and tail, chamfered shoulders and bonnet.
    lower=[(-half,chamfer_contour(1.5,.32,.66,.06)),(-half+.18,chamfer_contour(1.72,.25,.77,.055))]
    # Wheel arches are cut into the body silhouette, not painted circles on its sides.
    for zz,bottom in [(-1.83,.25),(-1.75,.28),(-1.65,.46),(-1.43,.65),(-1.21,.46),(-1.11,.28),(-1.03,.24),(1.03,.24),(1.11,.28),(1.21,.46),(1.43,.65),(1.65,.46),(1.75,.30),(1.83,.30)]:
        lower.append((zz,chamfer_contour(1.79,bottom,.80,.035)))
    lower.extend([(half-.12,chamfer_contour(1.65,.34,.67,.045)),(half,chamfer_contour(1.48,.37,.61,.04))])
    loft(paint,lower)
    rear=-1.65 if estate else -1.28;roof_rear=-1.35 if estate else -.84
    # Sloped A and D pillars form a recognisable estate/saloon silhouette.
    loft(paint,[(rear,chamfer_contour(1.59,.70,.88,.035)),(roof_rear,chamfer_contour(1.47,.70,1.33,.055)),(.36,chamfer_contour(1.45,.70,1.33,.05)),(1.02,chamfer_contour(1.58,.70,.78,.025))])
    # Windshield and rear glazing sit on the actual sloped cabin faces.
    quad(car_window,[(-.735,.805,1.001),(.735,.805,1.001),(.661,1.274,.377),(-.661,1.274,.377)],(0,.55,.83))
    quad(car_window,[(.72,.894,rear-.008),(-.72,.894,rear-.008),(-.66,1.265,roof_rear-.015),(.66,1.265,roof_rear-.015)],(0,.68,-.73))
    for side in (-1,1):
        xx=side*.797;xtop=side*.729
        # Two separate windows leave a broad central B pillar and narrow borders.
        quad(car_window,[(xx,.81,.87),(xx,.81,-.13),(xtop,1.26,-.13),(xtop,1.26,.345)],(side,.1,0))
        quad(car_window,[(xx,.81,-.22),(xx,.81,rear+.14),(xtop,1.26,roof_rear+.07),(xtop,1.26,-.22)],(side,.1,0))
        tube_path(metal,[(side*.81,.785,rear+.05),(side*.81,.785,-.1),(side*.81,.785,.94)],.009,6)
        # Door outlines, recessed pulls, mirrors and rocker strips.
        for zz in (-.18,rear+.13):
            tube_path(black,[(side*.873,.72,zz),(side*.895,.42,zz),(side*.87,.31,zz)],.006,5)
        for zz in (.18,-.78):
            rounded_box(black,(side*.89,.7,zz),(.023,.055,.18),.009,steps=1)
            rod(metal,(side*.91,.706,zz-.06),(side*.91,.706,zz+.06),.008,6)
        tube_path(paint,[(side*.77,.88,.77),(side*.95,.9,.72)],.022,8)
        ellipsoid(paint,(side*1.0,.92,.70),(.11,.065,.14),12,6)
        rounded_box(black,(side*1.075,.93,.70),(.018,.07,.19),.007,steps=1)
        tube_path(black,[(side*.9,.32,-1.74),(side*.9,.31,1.7)],.019,8)
        for zz in (-1.43,1.43):
            arch=[(side*.898,.24+.4*math.sin(TAU*k/24),zz+.4*math.cos(TAU*k/24)) for k in range(13)]
            tube_path(black,arch,.011,6)
            saved=frame;wheel=world((side*.94,.24,zz));frame=(*wheel,yaw+side*math.pi/2)
            torus(black,(0,0,0),.265,.065,28,8)
            torus(metal,(0,0,.069),.17,.018,24,6)
            disk(black,(0,0,.065),.162,24)
            disk(metal,(0,0,.082),.045,16)
            for k in range(5):
                a=TAU*k/5
                tube_path(metal,[(.045*math.cos(a),.045*math.sin(a),.083),(.15*math.cos(a+.14),.15*math.sin(a+.14),.077)],.014,6)
                disk(black,(.028*math.cos(a),.028*math.sin(a),.086),.005,6)
            # Thin blocks of tread around the visible tyre shoulder.
            for k in range(28):
                a=TAU*k/28
                rod(metal,(.3*math.cos(a),.3*math.sin(a),.027),(.31*math.cos(a+.016),.31*math.sin(a+.016),.047),.003,5)
            frame=saved
    for side in (-1,1):
        rounded_box(black,(side*.54,.578,half-.01),(.49,.20,.06),.032)
        rounded_box(paper,(side*.54,.59,half+.026),(.42,.145,.035),.027)
        rounded_box(red,(side*.60,.61,-half-.015),(.29,.19,.052),.025)
    rounded_box(black,(0,.42,half-.015),(1.53,.12,.09),.04)
    rounded_box(black,(0,.40,-half-.025),(1.55,.12,.075),.035)
    rounded_box(black,(0,.584,half+.035),(.40,.115,.018),.012)
    for i in range(5): rod(metal,(-.16+i*.08,.545,half+.046),(-.16+i*.08,.624,half+.046),.005,5)
    rounded_box(paper,(0,.47,-half-.075),(.44,.10,.015),.008,steps=1)
    # Bonnet crease, windscreen wipers, exhaust and roof rails on the estate.
    for side in (-1,1):
        tube_path(metal,[(side*.28,.83,.93),(side*.38,.90,.82),(side*.53,1.05,.63)],.006,5)
        tube_path(paint,[(side*.49,.795,1.05),(side*.50,.72,half-.12)],.008,6)
    rod(metal,(-.55,.29,-half-.015),(-.55,.29,-half-.13),.032,10)
    if estate:
        for side in (-1,1): tube_path(metal,[(side*.55,1.32,-1.23),(side*.55,1.41,-1.12),(side*.55,1.41,.19),(side*.55,1.34,.32)],.019,8)
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
