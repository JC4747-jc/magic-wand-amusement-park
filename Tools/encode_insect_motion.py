"""Encode bounded Unity motion frames with Blender's bundled encoder; no downloads.

Defaults preserve the insect preview workflow. For pets append Blender args:
-- --folder pet-motion --models CatIdle CatSniff --frames 60
"""
from pathlib import Path
import argparse
import hashlib
import json
import sys
import bpy

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--folder',choices=['insect-motion','pet-motion','room-motion','bronze-motion'],default='insect-motion')
parser.add_argument('--models',nargs='+',default=['Cockroach','Spider'])
parser.add_argument('--frames',type=int,default=48)
parser.add_argument('--fps',type=int,choices=[24,30],default=24)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
assert 2<=args.frames<=144 and 1<=len(args.models)<=4
assert all(name in ['Cockroach','Spider','CatIdle','CatSniff','CatWalk','DogWalk','JellyfishPulse','ReadingCornerPulse','BronzeAwaken'] for name in args.models)
root=Path(__file__).resolve().parents[1]/'.codex-tmp'/args.folder
results=[]
for name in args.models:
    # The two gait loops retain their actual source/authored duration at 24fps.
    frame_count={'CatWalk':48,'DogWalk':26}.get(name,args.frames)
    if name in ['CatWalk','DogWalk']:assert args.fps==24
    folder=root/name;images=[folder/f'{i:03d}.png' for i in range(frame_count)]
    assert all(path.is_file() for path in images),'Missing Unity motion frames'
    assert sum(path.stat().st_size for path in images)<24*1024*1024,'Frame sequence exceeds disk budget'
    probes=[frame_count//4,frame_count//2] if name=='BronzeAwaken' else [frame_count//2]
    assert any(hashlib.sha256(images[0].read_bytes()).digest()!=hashlib.sha256(images[i].read_bytes()).digest() for i in probes),'Motion sequence is static'
    scene=bpy.context.scene
    if scene.sequence_editor:scene.sequence_editor_clear()
    editor=scene.sequence_editor_create()
    strips=getattr(editor,'strips',None)
    if strips is None:strips=editor.sequences
    strip=strips.new_image(name,filepath=str(images[0]),channel=1,frame_start=1)
    for path in images[1:]:strip.elements.append(path.name)
    strip.frame_final_duration=frame_count
    for option in ('use_cache_raw','use_cache_preprocessed','use_cache_composite','use_cache_final'):
        if hasattr(editor,option):setattr(editor,option,False)
    scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
    scene.render.fps=args.fps;scene.frame_start=1;scene.frame_end=frame_count
    scene.render.threads_mode='FIXED';scene.render.threads=1
    scene.render.use_sequencer=True;scene.render.use_compositing=False
    scene.sequencer_colorspace_settings.name='sRGB';scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
    scene.render.image_settings.file_format='FFMPEG';scene.render.ffmpeg.format='MPEG4';scene.render.ffmpeg.codec='H264'
    scene.render.ffmpeg.constant_rate_factor='MEDIUM';scene.render.ffmpeg.ffmpeg_preset='GOOD';scene.render.ffmpeg.audio_codec='NONE'
    output=root/(name+'.mp4');scene.render.filepath=str(output)
    bpy.ops.render.render(animation=True)
    assert output.is_file() and 0<output.stat().st_size<8*1024*1024,'Invalid/unbounded motion movie'
    clip=bpy.data.movieclips.load(str(output))
    assert tuple(clip.size)==(640,640) and clip.frame_duration==frame_count,'Decoded movie shape/frame count mismatch'
    results.append(dict(model=name,path=str(output),bytes=output.stat().st_size,frames=frame_count,fps=args.fps,
        mode='Offline Unity pose/render sampling; separate Play Mode lifecycle evidence exists.'))
    bpy.data.movieclips.remove(clip)
(root/'movies.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print('UNITY_MOTION_MOVIES_PASS',json.dumps(results),flush=True)
