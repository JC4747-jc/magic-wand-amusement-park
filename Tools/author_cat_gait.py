"""Author a bounded four-beat in-place preview on the existing CC BY cat rig.

No topology/weights changes, helper bones, simulation or training. This is
authored animation, not motion capture or inferred real pet locomotion.
"""
import math
import bpy
from mathutils import Vector, Matrix, Quaternion
from cat_gait_trajectory import paw_trajectory


def author_walk(rig, meshes):
    scene=bpy.context.scene
    for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
    scale=rig.matrix_world.to_scale().z
    legs=[]
    # Lateral sequence: hind then fore on one side, then the opposite side.
    for side,front,offset in [('l',False,0),('l',True,.25),('r',False,.5),('r',True,.75)]:
        prefix=f'Wolf_{side}_'+('FrontLeg_' if front else 'HindLeg_')
        names=[b.name for b in rig.data.bones if b.name.startswith(prefix)]
        def find(part):return next(n for n in names if n[len(prefix):].startswith(part+'SHJnt'))
        hip=find('Hip');knee=find('Knee' if front else 'Knee1');end=find('Ankle' if front else 'Knee2')
        legs.append(dict(name=side+('F' if front else 'H'),prefix=prefix,hip=hip,knee=knee,end=end,offset=offset,pole=Vector((0,1 if front else -1,0)),front=front))
    body=rig.pose.bones['Wolf_ROOTSHJnt_38']
    def skin_points():
        points=[]
        for obj in meshes:
            evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh()
            try:points.extend(evaluated.matrix_world@v.co for v in mesh.vertices)
            finally:evaluated.to_mesh_clear()
        return points
    neutral=skin_points();floor=min(p.z for p in neutral)
    for leg in legs:leg['vertices']=[]
    vertex_offset=0
    for obj in meshes:
        for vertex in obj.data.vertices:
            i=vertex_offset+vertex.index
            if neutral[i].z<floor+.025:
                scores=[sum(g.weight for g in vertex.groups if obj.vertex_groups[g.group].name.startswith(leg['prefix'])) for leg in legs]
                index=max(range(4),key=lambda k:scores[k])
                if scores[index]>.8:legs[index]['vertices'].append(i)
        vertex_offset+=len(obj.data.vertices)
    assert all(len(leg['vertices'])>=8 for leg in legs),'Insufficient original paw vertices'
    action=bpy.data.actions.new('BicolorCat_Walk');rig.animation_data.action=action
    def solve(leg,target):
        hip=rig.pose.bones[leg['hip']];knee=rig.pose.bones[leg['knee']];end=rig.pose.bones[leg['end']]
        a=hip.head.copy();u=rest[knee.name].translation-rest[hip.name].translation;v=rest[end.name].translation-rest[knee.name].translation
        direction=target-a;distance=direction.length;l1=u.length;l2=v.length
        assert abs(l1-l2)+1e-5<distance<l1+l2-1e-5,('Unreachable paw',leg['name'],distance,l1+l2)
        axis=direction.normalized();pole=leg['pole']-axis*leg['pole'].dot(axis);pole.normalize()
        along=(l1*l1-l2*l2+distance*distance)/(2*distance)
        elbow=a+axis*along+pole*math.sqrt(max(0,l1*l1-along*along))
        hip.matrix=Matrix.Translation(a)@u.rotation_difference(elbow-a).to_matrix().to_4x4()@rest[hip.name].to_quaternion().to_matrix().to_4x4()
        bpy.context.view_layer.update();b=knee.head.copy()
        knee.matrix=Matrix.Translation(b)@v.rotation_difference(target-b).to_matrix().to_4x4()@rest[knee.name].to_quaternion().to_matrix().to_4x4()
        bpy.context.view_layer.update();actual=end.head.copy()
        # Keep the paw's sole orientation; no planted toe rolling below ground.
        end.matrix=Matrix.Translation(actual)@rest[end.name].to_quaternion().to_matrix().to_4x4()
        bpy.context.view_layer.update()
        return (actual-target).length*scale
    max_error=0
    for step in range(97):
        frame=1+step*.5;t=step/96
        for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
        body.matrix=Matrix.Translation((0,0,-.035+.006*math.cos(t*math.tau*2)))@rest[body.name]
        bpy.context.view_layer.update()
        targets=[];lifts=[]
        for leg in legs:
            p=(t-leg['offset'])%1
            longitudinal,lift=paw_trajectory(p)
            target=rest[leg['end']].translation+Vector((0,longitudinal,lift))
            targets.append(target);lifts.append(lift)
            max_error=max(max_error,solve(leg,target))
        # Correct the evaluated paw surface, not just a joint pivot. The source
        # foot meshes have different offsets below their ankle/ball bones.
        for correction in range(4):
            points=skin_points()
            for leg,target,lift in zip(legs,targets,lifts):
                error=floor+.00015+lift*scale-min(points[i].z for i in leg['vertices'])
                target.z+=error/scale;max_error=max(max_error,solve(leg,target))
        # Subtle tail counter motion; the tracking/MAIN roots stay untouched.
        for i,name in enumerate(['Wolf_Tail_01_02SHJnt_37','Wolf_Tail_01_03SHJnt_36','Wolf_Tail_01_04SHJnt_35','Wolf_Tail_01_05SHJnt_34']):
            pb=rig.pose.bones[name];axis=pb.bone.matrix_local.to_quaternion().inverted()@Vector((0,0,1))
            pb.rotation_quaternion=Quaternion(axis,math.sin(t*math.tau+i*.3)*.025)
        for pb in rig.pose.bones:
            for channel in ['location','rotation_quaternion','scale']:pb.keyframe_insert(data_path=channel,frame=frame,group=pb.name)
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                bag=strip.channelbag(slot)
                if bag:
                    for curve in bag.fcurves:
                        for point in curve.keyframe_points:point.interpolation='LINEAR'
    assert max_error<.0001,('IK endpoint residual',max_error)
    # Check actual evaluated skin, including inter-key samples and loop closure.
    root_matrix=rig.matrix_world.copy();main=rig.pose.bones['Wolf_MAINSHJnt_39'];main_matrix=main.matrix.copy()
    minimum=math.inf;first=None;last=None;maximum_lift={leg['name']:0.0 for leg in legs};stance_error=0;worst_contact=None
    for step in range(193):
        frame=1+step*.25;scene.frame_set(int(frame),subframe=frame%1);bpy.context.view_layer.update()
        assert max(abs((rig.matrix_world-root_matrix)[r][c]) for r in range(4) for c in range(4))<1e-6
        assert max(abs((main.matrix-main_matrix)[r][c]) for r in range(4) for c in range(4))<1e-6
        points=skin_points()
        minimum=min(minimum,min(p.z for p in points))
        if step==0:first=[p.copy() for p in points]
        if step==192:last=points
        for leg in legs:
            p=(rig.pose.bones[leg['end']].head-rest[leg['end']].translation).z*scale
            maximum_lift[leg['name']]=max(maximum_lift[leg['name']],p)
            phase=(step/192-leg['offset'])%1
            if phase<.64:
                error=abs(min(points[i].z for i in leg['vertices'])-floor)
                if error>stance_error:stance_error=error;worst_contact=(frame,leg['name'],phase,error)
    loop_error=max((a-b).length for a,b in zip(first,last))
    assert loop_error<1e-5,('Walk loop seam',loop_error)
    assert minimum>=floor-.0003,('Skin penetrated ground',minimum-floor)
    assert stance_error<.0005,('Paw stance height',worst_contact)
    report=dict(seconds=2,keySamples=97,validationSamples=193,ikEndpointErrorMeters=max_error,minimumSkinWorldZ=minimum,neutralFloorWorldZ=floor,maximumStanceSurfaceHeightErrorMeters=stance_error,loopSkinErrorMeters=loop_error,maximumLegLiftMeters=maximum_lift,
                scope='Blender evaluated skin at source scale; authored slow in-place gait, no measured animal or hardware acceptance')
    print('CAT_WALK_AUTHORED',report)
    return report
