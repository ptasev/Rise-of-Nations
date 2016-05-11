import bpy
from mathutils import Vector
from ..formats.bh3.bh3bone import BH3Bone
from ..formats.bh3.bh3file import BH3File


class BH3FileExporter:
    def __init__(self):
        self._file = None
        self._model = None
        self._mesh = None
        self._normals = []
        self._uv_loops = None
        self._vertex_uv_loop_index = dict()
        self._vertex_old_new_index = dict()

    def save(self, ctx, filename):
        self._file = BH3File()
        self._model = ctx.scene.objects.active
        # self._mesh = object_.data
        self._mesh = self._model.to_mesh(ctx.scene, True, 'PREVIEW', True)
        self._mesh.calc_normals_split()

        # TODO: Duplicate vertices for each one of their unique uvs
        uv_layer = self._mesh.uv_layers.active
        self._uv_loops = uv_layer.data if uv_layer is not None else None
        if self._uv_loops:
            for face in self._mesh.polygons:
                for vi, li in zip(face.vertices, face.loop_indices):
                    self._vertex_uv_loop_index[vi] = li

        for v in self._mesh.vertices:
            average_normal = Vector()
            for loop in self._mesh.loops:
                if loop.vertex_index == v.index:
                    average_normal += loop.normal
            average_normal.normalize()
            self._normals.append(average_normal)

        # TODO: Find the skin mod properly
        skin_mod = self._model.modifiers[0]
        skin = skin_mod.object
        ctx.scene.objects.active = skin
        bpy.ops.object.mode_set(mode='EDIT')
        armature = skin.data

        self._vertex_old_new_index = dict()
        self._file.root_bone = self._create_bh3_bones(armature.edit_bones[0], [0])

        for face in self._mesh.polygons:
            self._file.faces.append([self._vertex_old_new_index[face.vertices[0]],
                                     self._vertex_old_new_index[face.vertices[1]],
                                     self._vertex_old_new_index[face.vertices[2]]])

        bpy.ops.object.mode_set(mode='OBJECT')
        ctx.scene.objects.active = self._model
        bpy.data.meshes.remove(self._mesh)

        self._file.write(filename)

        return {'FINISHED'}

    def _create_bh3_bones(self, abone, vertex_index):
        bone = BH3Bone()
        bone.name = abone.name
        bone_vertices = []

        # TODO: Implement this method to calculate all data based on looping through the faces (polygons)
        # Find verts based on the bones, and create a dictionary mapping old vertex index to new vertex index
        for v in self._mesh.vertices:
            if not (v.index in self._vertex_old_new_index):
                for vg in v.groups:
                    group_name = self._model.vertex_groups[vg.group].name

                    if group_name == abone.name:
                        bone_vertices.append(v.index)
                        self._vertex_old_new_index[v.index] = len(self._vertex_old_new_index)
                        break

        bone.vertex_count = len(bone_vertices)
        # Calculate the local rotation and position for the bone
        if abone.parent:
            transform = abone.parent.matrix.inverted() * abone.matrix
            bone.rotation = list(transform.transposed().to_quaternion())
            bone.position = list(transform.to_translation())
        else:
            bone.rotation = list(abone.matrix.transposed().to_quaternion())
            bone.position = list(abone.matrix.to_translation())

        # Localize, and add the bone's vertices, normals, and uvs to the file
        if bone.vertex_count > 0:
            bone.vertex_index = vertex_index[0]
            vertex_index[0] += bone.vertex_count

            transform_inverse = abone.matrix.inverted()
            nrm_mtx = transform_inverse.transposed().inverted()

            for vi in bone_vertices:
                self._file.vertices.append(list(transform_inverse * self._mesh.vertices[vi].co))
                self._file.normals.append(list(nrm_mtx * self._normals[vi]))
                self._file.uvs.append(list(self._uv_loops[self._vertex_uv_loop_index[vi]].uv) if self._uv_loops else
                                      [0, 1])

        for achild in abone.children:
            child = self._create_bh3_bones(achild, vertex_index)
            child.parent = bone
            bone.children.append(child)

        return bone
