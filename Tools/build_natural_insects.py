"""Original anatomical insect studies; one skinned mesh, no downloaded model or image assets."""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUTPUT=ROOT/'Assets/MagicMR/NaturalInsects'
OUTPUT.mkdir(parents=True,exist_ok=True)


def build(spider=False):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    name='Spider' if spider else 'Cockroach'
    vertices=[];faces=[];colours=[];weights=[];bones=[]
    shell=(.10,.052,.022) if spider else (.16,.038,.011)
    dark=(.025,.013,.008);leg_colour=(.16,.085,.034) if spider else (.20,.084,.025)
    def vertex(point,bone,colour,roughness=.4):
        vertices.append(tuple(point));weights.append(bone);colours.append((*colour,roughness));return len(vertices)-1
    def bone(name,head,tail,parent='BodyRoot'):
        bones.append((name,tuple(head),tuple(tail),parent));return name
    bone('BodyRoot',(0,0,0),(0,0,.005),None)
    def tube(points,radii,binding,colour,sides=8,roughness=.4):
        points=[Vector(p) for p in points];loops=[]
        for i,point in enumerate(points):
            direction=(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized()
            axis=direction.cross(Vector((0,0,1)))
            if axis.length<.01:axis=direction.cross(Vector((1,0,0)))
            axis.normalize();other=direction.cross(axis).normalized()
            ring=[]
            for side in range(1 if radii[i]<=1e-12 else sides):
                angle=side*2*math.pi/sides
                ring.append(vertex(point+radii[i]*(axis*math.cos(angle)+other*math.sin(angle)),binding[i] if isinstance(binding,list) else binding,colour,roughness))
            if i:
                for side in range(sides):
                    previous=loops[-1]
                    if len(previous)==1:faces.append((previous[0],ring[(side+1)%sides],ring[side]))
                    elif len(ring)==1:faces.append((previous[side],previous[(side+1)%sides],ring[0]))
                    else:faces.append((previous[side],previous[(side+1)%sides],ring[(side+1)%sides],ring[side]))
            loops.append(ring)
        if len(loops[0])>1:faces.append(tuple(reversed(loops[0])))
        if len(loops[-1])>1:faces.append(tuple(loops[-1]))
    def ellipsoid(centre,size,colour,binding='BodyRoot',roughness=.4,rings=12,sides=24):
        top=vertex(Vector(centre)+Vector((0,0,size[2])),binding,colour(0,0,1) if callable(colour) else colour,roughness)
        start=len(vertices)
        for ring in range(1,rings):
            angle=math.pi*ring/rings
            for side in range(sides):
                a=side*2*math.pi/sides
                point=Vector(centre)+Vector((math.sin(angle)*math.cos(a)*size[0],math.sin(angle)*math.sin(a)*size[1],math.cos(angle)*size[2]))
                rgb=colour(math.sin(angle)*math.cos(a),math.sin(angle)*math.sin(a),math.cos(angle)) if callable(colour) else colour
                vertex(point,binding,tuple(c*(.88+.12*math.sin(angle)) for c in rgb),roughness)
                if ring>1:
                    p=start+(ring-2)*sides+side;q=start+(ring-2)*sides+(side+1)%sides
                    faces.append((p,p+sides,q+sides,q))
        bottom=vertex(Vector(centre)-Vector((0,0,size[2])),binding,colour(0,0,-1) if callable(colour) else colour,roughness)
        last=start+(rings-2)*sides
        for side in range(sides):faces.append((top,start+side,start+(side+1)%sides));faces.append((bottom,last+(side+1)%sides,last+side))
    if spider:
        def spider_coat(x,y,z):
            mottling=.83+.17*math.sin(y*17+x*8)*math.sin(x*19-y*5)
            stripe=math.exp(-((abs(x)-.32)/.11)**2)*.6
            return tuple((shell[i]*(1-stripe)+(.22,.15,.075)[i]*stripe)*mottling for i in range(3))
        ellipsoid((0,.009,.008),(.0065,.009,.007),spider_coat,roughness=.7,rings=24,sides=40)
        ellipsoid((0,-.003,.006),(.005,.006,.004),(.16,.095,.04),roughness=.5)
        # Eight small dark eyes; no human iris or facial expression.
        for row,count in [(0,4),(1,4)]:
            for i in range(count):ellipsoid(((i-1.5)*.0012,-.008+row*.0005,.007+row*.001),(.00036,.0003,.00035),(.008,.006,.004),roughness=.15,rings=6,sides=10)
        for side in (-1,1):
            tube([(side*.0018,-.008,.0045),(side*.0028,-.012,.003),(side*.004,-.013,.001)], [.0007,.00045,.00012],'BodyRoot',dark)
        # Sparse fine hairs read as natural surface detail, within a fixed geometry budget.
        for i in range(700):
            a=i*2.399963;z=1-2*(i+.5)/700;r=math.sqrt(max(0,1-z*z));normal=Vector((r*math.cos(a),r*math.sin(a),z))
            p=Vector((normal.x*.0065,.009+normal.y*.009,.008+normal.z*.007))
            tube([p,p+normal*.00065],[.000025,0],'BodyRoot',(.24,.18,.09),sides=3,roughness=.8)
    else:
        # Overlapping abdominal plates remain visible around the wing margin and on the underside.
        ellipsoid((0,.008,.0031),(.0067,.015,.0028),shell,roughness=.45,rings=18)
        for i in range(9):
            y=-.004+i*.003;span=.0064*math.sqrt(max(.1,1-((y-.008)/.017)**2))
            points=[(math.cos(a)*span,y,.0037+math.sin(a)*.0032) for a in [math.pi+j*math.pi/14 for j in range(15)]]
            tube(points,[.00014]*len(points),'BodyRoot',dark,sides=4,roughness=.6)
        ellipsoid((0,-.012,.0038),(.003,.003,.0017),dark,roughness=.3)
        def pronotum(x,y,z):
            radial=math.sqrt(x*x+y*y);rim=max(0,min(1,(radial-.70)/.23))
            lobes=max(math.exp(-((x-.4)/.3)**2-((y+.02)/.75)**2),math.exp(-((x+.4)/.3)**2-((y+.02)/.75)**2))
            base=tuple(shell[i]*(1-lobes*.55) for i in range(3))
            return tuple(base[i]*(1-rim)+(.29,.14,.043)[i]*rim for i in range(3))
        ellipsoid((0,-.0075,.0055),(.0065,.0054,.0015),pronotum,roughness=.32,rings=20,sides=40)
        for side in (-1,1):
            ellipsoid((side*.00245,-.013,.0042),(.00045,.00065,.0005),(.009,.006,.004),roughness=.12,rings=8,sides=12)
            start=len(vertices);rows=20;columns=8
            for row in range(rows+1):
                u=row/rows;y=-.0055+u*.031
                width=.007*math.pow(max(.00001,math.sin(math.pi*u)),.38)
                for column in range(columns+1):
                    v=column/columns;x=side*(.00012+v*width)
                    z=.0058+.00075*math.sin(math.pi*u)+.00045*math.sin(v*math.pi)-.0006*v*v
                    vertex((x,y,z),'BodyRoot',tuple(c*(.85+.15*u) for c in shell),.25)
                    if row and column:
                        p=start+(row-1)*(columns+1)+column-1
                        face=(p,p+1,p+columns+2,p+columns+1)
                        faces.append(face if side==1 else tuple(reversed(face)))
            for line in range(10):
                y=-.003+line*.0024;u=(y+.0055)/.031;width=.007*math.pow(max(.00001,math.sin(math.pi*u)),.38)
                pts=[]
                for step in range(6):
                    v=step/5;yy=y+v*.003;uu=(yy+.0055)/.031
                    pts.append((side*(.0002+v*width),yy,.0058+.00075*math.sin(math.pi*uu)+.00045*math.sin(v*math.pi)-.0006*v*v+.000035))
                tube(pts,[.000032]*len(pts),'BodyRoot',(.13,.035,.009),sides=4,roughness=.35)
            tube([(side*.0025,.021,.003),(side*.004,.027,.0037),(side*.005,.029,.004)],[.00065,.00032,.00003],'BodyRoot',leg_colour)
            points=[(side*(.0018+.020*u+.006*u*u),-.014-.028*u,.0048+.004*math.sin(u*math.pi)) for u in [j/32 for j in range(33)]]
            names=[]
            for section in range(4):names.append(bone(f'Antenna_{side}_{section}',points[section*8],points[(section+1)*8],names[-1] if names else 'BodyRoot'))
            bindings=[names[min(3,j//8)] for j in range(33)]
            tube(points,[.00029*(1-j/34)**1.25 for j in range(33)],bindings,leg_colour,sides=6,roughness=.45)
    pairs=4 if spider else 3
    for pair in range(pairs):
        for side_index,side in enumerate((-1,1)):
            index=pair*2+side_index;y=-.006+pair*(.0027 if spider else .005)
            hip=Vector((side*(.0035 if spider else .0045),y,.005))
            spread=(pair-(pairs-1)*.5)*(.008 if spider else .009)
            knee=Vector((side*(.014 if spider else .0105),y+spread*.35,.009 if spider else .0065))
            ankle=Vector((side*(.024 if spider else .019),y+spread,.0008))
            toe=ankle+Vector((side*.003,-.0015,-.00055))
            upper=bone(f'Leg_{index:02d}_Upper',hip,knee);lower=bone(f'Leg_{index:02d}_Lower',knee,ankle,upper);foot=bone(f'Leg_{index:02d}_Foot',ankle,toe,lower)
            ellipsoid(hip,(.001,.0015,.0009),leg_colour,upper,roughness=.5,rings=6,sides=10)
            tube([hip,hip.lerp(knee,.45),knee],[.0008,.00075,.00042],upper,leg_colour,roughness=.45)
            tube([knee,knee.lerp(ankle,.75),ankle],[.00048,.00032,.00019],lower,leg_colour,roughness=.5)
            for j in range(5):
                p=ankle.lerp(toe,j/5);q=ankle.lerp(toe,(j+1)/5)
                tube([p,q],[.00021*(1-j/6),.00016*(1-j/6)],foot,leg_colour,sides=6,roughness=.55)
            for offset in (-1,1):tube([toe,toe+Vector((side*.0005,offset*.00035,-.00018))],[.00009,0],foot,dark,sides=4)
            for j in range(6):
                p=knee.lerp(ankle,.18+j*.12)
                tube([p,p+Vector((side*.0012,(-1 if j%2 else 1)*.0005,.00035))],[.00011,0],lower,leg_colour,sides=4,roughness=.65)

    mesh=bpy.data.meshes.new(name+' original cuticle');mesh.from_pydata(vertices,[],faces);mesh.update()
    uv=mesh.uv_layers.new(name='Stable cuticle detail')
    for loop in mesh.loops:
        p=mesh.vertices[loop.vertex_index].co;uv.data[loop.index].uv=(p.x*900,p.y*900+p.z*310)
    attribute=mesh.color_attributes.new(name='CuticleColourRoughness',type='FLOAT_COLOR',domain='POINT')
    for i,entry in enumerate(attribute.data):entry.color=colours[i]
    mesh.color_attributes.active_color=attribute
    for polygon in mesh.polygons:polygon.use_smooth=True
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    rig=bpy.data.objects.new(name+'Rig',bpy.data.armatures.new(name+' anatomical joints'));bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for key,head,tail,parent in bones:
        node=rig.data.edit_bones.new(key);node.head=head;node.tail=tail
        if parent:node.parent=rig.data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    grouped={}
    for i,key in enumerate(weights):grouped.setdefault(key,[]).append(i)
    for key,indices in grouped.items():obj.vertex_groups.new(name=key).add(indices,1,'REPLACE')
    obj.parent=rig;modifier=obj.modifiers.new('Anatomical rigid skin','ARMATURE');modifier.object=rig
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT/(name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=False,
        axis_forward='-Z',axis_up='Y',colors_type='LINEAR')
    triangles=sum(len(p.vertices)-2 for p in mesh.polygons)
    assert triangles<30000 and len(bones)<64
    print('ORIGINAL_INSECT_GEOMETRY_PASS',name,'triangles',triangles,'vertices',len(vertices),'bones',len(bones),flush=True)
    return dict(name=name,triangles=triangles,vertices=len(vertices),bones=len(bones),leg_pairs=pairs)

result=[build(False),build(True)]
(OUTPUT/'provenance.json').write_text(json.dumps(dict(
    origin='Original procedural anatomical studies authored for this project. No third-party model, photograph, texture or training data is embedded.',
    references=['https://entnemdept.ufl.edu/projex/gallery/dl/cockroaches/text/american_cockroach.htm','https://animaldiversity.org/accounts/Periplaneta_americana/'],
    limitations='Anatomical approximations, not scans or scientific species-identification assets. Photographs were consulted only as references, not copied into assets.',
    models=result),indent=2),encoding='utf-8')
