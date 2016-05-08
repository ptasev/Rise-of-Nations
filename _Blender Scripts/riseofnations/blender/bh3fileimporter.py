import bpy
from mathutils import Vector, Quaternion, Matrix
from ..formats.bh3.bh3file import BH3File
import os


class BH3FileImporter:
    def __init__(self):
        self._file = None
        self._model = None
        self._armature = None
        return

    def load(self, ctx, filename):
        model_name = os.path.splitext(os.path.basename(filename))[0]
        tex_path = filename[:-3] + 'tga'

        self._file = BH3File()
        self._file.read(filename)

        self._armature = bpy.data.armatures.new(model_name + '_Arm')
        skin = bpy.data.objects.new(model_name + '_Skin', self._armature)
        skin.location = [0, 0, 0]
        ctx.scene.objects.link(skin)
        ctx.scene.objects.active = skin
        ctx.scene.update()
        bpy.ops.object.mode_set(mode='EDIT')
        self._create_bones(self._file.root_bone, None)

        # create the mesh
        bpy.ops.object.mode_set(mode='OBJECT')
        mesh = bpy.data.meshes.new(model_name + '_Mesh')
        self._model = bpy.data.objects.new(model_name, mesh)
        self._model.location = [0, 0, 0]
        ctx.scene.objects.link(self._model)
        ctx.scene.objects.active = self._model
        ctx.scene.update()

        mesh.from_pydata(self._file.vertices, [], self._file.faces)
        mesh.update(calc_edges=True, calc_tessface=True)
        mesh.normals_split_custom_set_from_vertices(self._file.normals)
        mesh.use_auto_smooth = True

        uv_layer = mesh.uv_textures.new()
        uv_layer.name = 'DefaultUV'
        uv_loops = mesh.uv_layers[-1].data

        vertex_loops = {}
        for l in mesh.loops:
            vertex_loops.setdefault(l.vertex_index, []).append(l.index)

        for i, uv in enumerate(self._file.uvs):
            # For every loop of a vertex
            for li in vertex_loops[i]:
                uv_loops[li].uv = uv

        self._create_vertex_groups(self._file.root_bone)

        mod = self._model.modifiers.new('MySkinMod', 'ARMATURE')
        mod.object = skin
        mod.use_bone_envelopes = False
        mod.use_vertex_groups = True

        material = bpy.data.materials.new('mat')

        texture = bpy.data.textures.new('tex', type='IMAGE')
        if os.path.isfile(tex_path):
            texture.image = bpy.data.images.load(tex_path)
        texture.use_alpha = True

        mtex = material.texture_slots.add()
        mtex.texture = texture
        mtex.texture_coords = 'UV'

        mesh.materials.append(material)

        return {'FINISHED'}

    def _create_bones(self, bone, parent):
        abone = self._armature.edit_bones.new(bone.name)
        abone.tail = Vector([0, 1, 0])

        if parent:
            abone.parent = parent

        rot_part = Quaternion(bone.rotation).to_matrix()
        pos_part = Vector(bone.position)
        transform = Matrix.Translation(bone.position) * rot_part.to_4x4()

        if parent:
            rot_part = rot_part * parent.matrix.to_3x3()
            pos_part = parent.matrix.to_translation() + (pos_part * parent.matrix.to_3x3())
            transform = Matrix.Translation(list(pos_part)) * rot_part.to_4x4()

        abone.transform(rot_part)
        abone.translate(pos_part)
        # print(abone.matrix.to_3x3().to_quaternion())
        # print(transform.to_3x3().to_quaternion())

        nrm_mtx = transform.to_3x3()
        nrm_mtx.invert()
        nrm_mtx.transpose()

        for vi in range(0, bone.vertex_count):
            vt_ind = vi + bone.vertex_index
            self._file.vertices[vt_ind] = list(
                Vector(self._file.vertices[vt_ind]) * transform.to_3x3() + transform.to_translation())
            self._file.normals[vt_ind] = list(Vector(self._file.normals[vt_ind]) * nrm_mtx)

        for child in bone.children:
            self._create_bones(child, abone)

    def _create_vertex_groups(self, bone):
        vertex_group = self._model.vertex_groups.new(name=bone.name)
        for vt in range(0, bone.vertex_count):
            vt_ind = vt + bone.vertex_index
            vertex_group.add([vt_ind], 1.0, 'ADD')

        for child in bone.children:
            self._create_vertex_groups(child)
