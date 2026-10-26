"""CPU-only Blender model QA. These are NOT game/store screenshots."""
import bpy
from mathutils import Vector
from pathlib import Path

root = Path(__file__).resolve().parents[1]
out = root/'docs/evidence/r6-store/model-qa'
out.mkdir(parents=True, exist_ok=True)
for name in ['Sentinel','Stalker','Lancer','Warden']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/f'Assets/_Project/Resources/Characters/{name}.fbx'))
    for obj in bpy.data.objects:
        if obj.animation_data: obj.animation_data_clear()
        if obj.type == 'ARMATURE':
            for bone in obj.pose.bones: bone.matrix_basis.identity()
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=12
    scene.render.threads_mode='FIXED'; scene.render.threads=2
    scene.render.resolution_x=256; scene.render.resolution_y=320; scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('QA charcoal');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.08,.07,.055,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
    scene.view_settings.view_transform='Standard'
    meshes=[o for o in scene.objects if o.type=='MESH']
    corners=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
    zmin=min(v.z for v in corners);zmax=max(v.z for v in corners);height=zmax-zmin
    center=Vector((0,0,(zmin+zmax)/2))
    light=bpy.data.lights.new('QA area','AREA');light.energy=700;light.shape='DISK';light.size=5
    lamp=bpy.data.objects.new('QA area',light);scene.collection.objects.link(lamp);lamp.location=(2,-4,6);lamp.rotation_euler=(center-lamp.location).to_track_quat('-Z','Y').to_euler()
    camera=bpy.data.cameras.new('QA camera');camera.type='ORTHO';camera.ortho_scale=height*1.22
    cam=bpy.data.objects.new('QA camera',camera);scene.collection.objects.link(cam);scene.camera=cam
    for label,delta in [('front',(0,-7,.3)),('side',(7,0,.3)),('back',(0,7,.3))]:
        cam.location=center+Vector(delta);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(out/f'{name}-{label}.png');bpy.ops.render.render(write_still=True)
