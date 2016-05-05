import bpy
import bmesh
import struct
from mathutils import *
import os

bl_info = {
    "name": "Rise of Nations BH3 format",
    "author": "Petar Tasev",
    "version": (1, 0, 2016, 503),
    "blender": (2, 76, 0),
    "location": "File > Import-Export",
    "description": "Import-Export BH3 mesh, UV's, "
                   "materials, textures, and skeleton",
    "warning": "",
    "wiki_url": "",
    "support": 'COMMUNITY',
    "category": "Import-Export"}

vertices = []
normals = []
texVerts = []
faces = []
bones = []
armature = ''
boneToInitMatrix = Matrix()
boneToInitMatrix[0] = [1,0,0,0]
boneToInitMatrix[1] = [0,0,1,0]
boneToInitMatrix[2] = [0,-1,0,0]

# read unsigned byte from file
def read_byte(file_object, endian = '<'):
    data = struct.unpack(endian+'B', file_object.read(1))[0]
    return data

# read unsgned short from file
def read_short(file_object, endian = '<'):
    data = struct.unpack(endian+'H', file_object.read(2))[0]
    return data

# read unsigned integer from file
def read_uint(file_object, endian = '<'):
    data = struct.unpack(endian+'I', file_object.read(4))[0]
    return data

# read signed integer from file
def read_int(file_object, endian = '<'):
    data = struct.unpack(endian+'i', file_object.read(4))[0]
    return data

# read floating point number from file
def read_float(file_object, endian = '<'):
    data = struct.unpack(endian+'f', file_object.read(4))[0]
    return data

def read_string(file_object):
    strLength = read_uint(file_object) - 1
    data = file_object.read(strLength).decode('utf-8', 'ignore')
    file_object.read(1)
    return data

def readChunk(file_object, parent = None):
    dataLength = read_uint(file_object)
    chunkType = read_short(file_object)
    numChildren = read_short(file_object)
    
    if chunkType == 2:
        numElements = read_uint(file_object)
        for vt in range(0, numElements):
            vertices.append([read_float(file_object),read_float(file_object),read_float(file_object)])
            read_float(file_object)
    elif chunkType == 3:
        numElements = read_uint(file_object)
        for ni in range(0, numElements):
            normals.append([read_float(file_object),read_float(file_object),read_float(file_object)])
        for ni in range(0, numElements):
            read_float(file_object)
    elif chunkType == 4:
        numElements = read_uint(file_object)
        for tv in range(0, numElements):
            texVerts.append([read_float(file_object), 1.0 - read_float(file_object)])
    elif chunkType == 5:
        numElements = int(read_uint(file_object) / 3)
        for fa in range(0, numElements):
            z = read_short(file_object)
            y = read_short(file_object)
            x = read_short(file_object)
            faces.append([x,y,z])
    elif chunkType == 6:
        parent = readChunk(file_object, parent)
        numChildren -= 1
        for bc in range(0, numChildren):
            readChunk(file_object, parent)
        return
    elif chunkType == 7:
        vertIndex = read_uint(file_object)
        vertCount = read_uint(file_object)
        boneName = read_string(file_object)
        rotation = [read_float(file_object), read_float(file_object), read_float(file_object), read_float(file_object)]
        position = [read_float(file_object), read_float(file_object), read_float(file_object)]
        read_float(file_object)
        
        abone = armature.edit_bones.new(boneName)
        abone.tail = Vector([0,1,0])
        
        if parent != None:
            abone.parent = parent[4]
        
        rotation = rotation[-1:] + rotation[:-1]
        rot = Quaternion(rotation)
        rot.invert()
        rotPart = Quaternion(rotation).to_matrix()
        posPart = Vector(position)
        transform = Matrix.Translation(position) * rotPart.to_4x4()
        
        if parent != None:
            parTranslation = parent[3].to_translation()
            posInParCoord = Vector(position) * parent[3].to_3x3()
            rotPart = rotPart * parent[3].to_3x3()
            posPart = parTranslation + posInParCoord
            transform = Matrix.Translation(list(posPart)) * rotPart.to_4x4()
            #parent[4].tail = transform.to_translation()
            #transform = transform * parent[3]

        abone.transform(rotPart)
        abone.translate(posPart)
        print(abone.matrix.to_3x3().to_quaternion())
        print(transform.to_3x3().to_quaternion())
        #abone.transform(boneToInitMatrix)
        #abone.transform(transform)
        
#        trs = Matrix.Translation(position) * rotPart.to_4x4()
#        output = []
#        for col in trs:
#            output += list(col)
#        tuple(output)
#        abone.matrix_local = output
        
        nrmMtx = transform.to_3x3()
        nrmMtx.invert()
        nrmMtx.transpose()
            
        for vt in range(0, vertCount):
            vtInd = vt + vertIndex
            vert = Vector(vertices[vtInd]) * transform.to_3x3() + transform.to_translation()
            vertices[vtInd] = list(vert)
            normals[vtInd] = list(Vector(normals[vtInd]) * nrmMtx)
        
        bone = [vertIndex, vertCount, boneName, transform, abone]
        bones.append(bone)
        return bone
    else:
        for c in range(0, numChildren):
            readChunk(file_object)


def read_bh3_data(context, filepath):
    print("running read_bh3_data...")

    fileName = os.path.splitext(os.path.basename(filepath))[0]
    texPath = filepath[:-3] + 'tga'
    global armature
    
    armature = bpy.data.armatures.new(fileName + '_SkelData')
    rig = bpy.data.objects.new(fileName + '_Skel', armature)
    rig.location = [0,0,0]
    bpy.context.scene.objects.link(rig)
    bpy.context.scene.objects.active = rig
    bpy.context.scene.update()
    bpy.ops.object.mode_set(mode='EDIT')
    
    f = open(filepath, 'rb')
    readChunk(f)
    f.close()
    
    bpy.ops.object.mode_set(mode='OBJECT')

    # create the mesh
    mesh = bpy.data.meshes.new(fileName + '_Mesh')
    object = bpy.data.objects.new(fileName, mesh)
    object.location = [0,0,0]
    bpy.context.scene.objects.link(object)
    bpy.context.scene.objects.active = object
    bpy.context.scene.update()
    
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True, calc_tessface=True)
    
    mesh.normals_split_custom_set_from_vertices(normals)
    mesh.use_auto_smooth = True
    
#    for face in mesh.tessfaces:
#        for norm in face.split_normals:
#            print(list(norm))
#    print(*mesh.tessfaces[0].split_normals[0])
#    print(mesh.vertices[mesh.polygons[0].vertices[0]].normal)
        
    uvtex = mesh.uv_textures.new()
    uvtex.name = 'DefaultUV'
    uv_layer = mesh.uv_layers[-1].data
    
    vert_loops = {}
    for l in mesh.loops:
        vert_loops.setdefault(l.vertex_index, []).append(l.index)

    for i, coord in enumerate(texVerts):
        # For every loop of a vertex
        for li in vert_loops[i]:
            uv_layer[li].uv = coord
    
    for bone in bones:
        vertgroup = object.vertex_groups.new(name=bone[2])
        for vt in range(0, bone[1]):
            vtInd = vt + bone[0]
            vertgroup.add([vtInd], 1.0, 'ADD')
    
    mod = object.modifiers.new('MyRigModif', 'ARMATURE')
    mod.object = rig
    mod.use_bone_envelopes = False
    mod.use_vertex_groups = True
            
    material = bpy.data.materials.new('mat')
    
    texture = bpy.data.textures.new('tex', type = 'IMAGE')
    if os.path.isfile(texPath):
        texture.image = bpy.data.images.load(texPath)
    texture.use_alpha = True
    
    mtex = material.texture_slots.add()
    mtex.texture = texture
    mtex.texture_coords = 'UV'
    
    mesh.materials.append(material)

    return {'FINISHED'}

# === Export ==============================================================
def write_byte(file_object, data, endian = '<'):
    file_object.write(struct.pack(endian+'B', data))

# read unsgned short from file
def write_short(file_object, data, endian = '<'):
    file_object.write(struct.pack(endian+'H', data))

# read unsigned integer from file
def write_uint(file_object, data, endian = '<'):
    file_object.write(struct.pack(endian+'I', data))

# read signed integer from file
def write_int(file_object, data, endian = '<'):
    file_object.write(struct.pack(endian+'i', data))

# read floating point number from file
def write_float(file_object, data, endian = '<'):
    file_object.write(struct.pack(endian+'f', data))

def write_string(file_object, data):
    data = data.encode('utf-8')
    strLength = len(data) + 1
    file_object.write(struct.pack('<I', strLength))
    file_object.write(data)
    file_object.write(struct.pack('<B', 0))

class EmptyUV:
    uv = (0.0, 0.0)
    def __getitem__(self, index): return self

class BhBone:
    def __init__(self, boneNode):
        self.bone = boneNode
        self.rotation = None
        self.position = None
        self.children = []
        self.vertIndexStart = 4294967295
        self.vertCount = 0
        self.dataLength = 53
        self.totDataLength = 0
        self.verts = []
    
    def writeVerts(self, f, mesh, vIndStart):
        self.vertCount = len(self.verts)
        if self.bone.parent != None:
            parPos = self.bone.parent.head
            parRotInv = self.bone.parent.matrix.copy()
            parRotInv.invert()
            self.rotation = list((self.bone.matrix * parRotInv).to_quaternion())
            self.position = list((self.bone.head - parPos) * parRotInv)
        else:
            self.rotation = list(self.bone.matrix.to_quaternion())
            self.position = list(self.bone.head)
        
        if self.vertCount > 0:
            self.vertIndexStart = vIndStart[0]
            vIndStart[0] += self.vertCount
            
            rotInv = self.bone.matrix.copy().to_3x3()
            rotInv.invert()
            nrmMtx = rotInv.copy()
            nrmMtx.transpose()
            nrmMtx.invert()
            
            for vi in self.verts:
                vertCoord = list((mesh.vertices[vi].co - self.bone.head) * rotInv)
                #vertCoord2 = Vector(vertCoord) * self.bone.matrix.copy().to_3x3() + self.bone.head
                #print(mesh.vertices[vi].co)
                #print(vertCoord2)
                write_float(f, vertCoord[0])
                write_float(f, vertCoord[1])
                write_float(f, vertCoord[2])
                write_float(f, 1.0)
                
                #print(mesh.vertices[vi].normal)
                mesh.vertices[vi].normal = mesh.vertices[vi].normal * nrmMtx
                if vi == 75:
                    print(self.bone.name)
                    print(self.verts)
                    print(normals[vi])
                    print(self.vertIndexStart)
                normals[vi] = normals[vi] * nrmMtx
                #print(mesh.vertices[vi].normal)
        
        for ch in self.children:
            ch.writeVerts(f, mesh, vIndStart)
    
    def write(self, f):
        write_uint(f, self.totDataLength)
        write_short(f, 6)
        write_short(f, len(self.children) + 1)
        
        write_uint(f, self.dataLength)
        write_short(f, 7)
        write_short(f, 0)
        
        write_uint(f, self.vertIndexStart)
        write_uint(f, self.vertCount)
        write_string(f, self.bone.name)
        
        write_float(f, self.rotation[1])
        write_float(f, self.rotation[2])
        write_float(f, self.rotation[3])
        write_float(f, self.rotation[0])
        
        write_float(f, self.position[0])
        write_float(f, self.position[1])
        write_float(f, self.position[2])
        
        write_float(f, self.rotation[1])
        
        for ch in self.children:
            ch.write(f)
                

def loadBhBones(boneNode, object, newVertOrder):
    bhboneNode = BhBone(boneNode)
    bhboneNode.dataLength += len(boneNode.name)
    bhboneNode.totDataLength += bhboneNode.dataLength + 8
    
    for v in object.data.vertices:
        if not(v.index in newVertOrder):
            for vg in v.groups:
                groupName = object.vertex_groups[vg.group].name
                
                if groupName == boneNode.name:
                    bhboneNode.verts.append(v.index)
                    newVertOrder[v.index] = len(newVertOrder)
                    break
    
    for ch in boneNode.children:
        childBhBone = loadBhBones(ch, object, newVertOrder)
        bhboneNode.children.append(childBhBone)
        bhboneNode.totDataLength += childBhBone.totDataLength
    
    return bhboneNode

def write_bh3_data(context, filepath):
    print("running write_bh3_data...")
    
    object = bpy.context.scene.objects.active
    #mesh = object.data
    mesh = object.to_mesh(bpy.context.scene, True, 'PREVIEW', True)
    mesh.calc_normals_split()
    loop_vert = {l.index: l.vertex_index for l in object.data.loops}
    #print(loop_vert)
    
    uv_act = mesh.uv_layers.active
    uv_layer = uv_act.data if uv_act is not None else EmptyUV()

    verts = mesh.vertices
    texLayerIndex = dict()
    
#    for face in mesh.tessfaces:
#        print(face.normal)
#        for norm in face.split_normals:
#            print(list(norm))
    #ntest = [list(l.normal) for l in mesh.loops]
    #print(ntest)
    global normals
    normals = []
    for v in mesh.vertices:
        vertLoops = []
        for loop in mesh.loops:
            if loop.vertex_index == v.index:
                vertLoops.append(loop)

        average_normal = Vector()
        for loop in vertLoops:
            average_normal += loop.normal
        average_normal.normalize()
        #v.normal = average_normal
        normals.append(average_normal)
    
    print(normals)
    print(normals[7])
        
#        for face in mesh.tessfaces:
#            if v.index in face.vertices:
#                ls_faces.append(face)
#
#        average_normal = Vector()
#        for f in ls_faces:
#            average_normal += f.normal
#        average_normal /= len(ls_faces)
#        #v.normal = average_normal
    
    for face in object.data.polygons:
        #print(face.loop_indices)
        for li in face.loop_indices:
            struct.pack("fff", *verts[loop_vert[li]].normal)
            struct.pack("fff", *verts[loop_vert[li]].co)
            struct.pack("ff", *uv_layer[li].uv)
            texLayerIndex[loop_vert[li]] = li
    
    skinMod = object.modifiers[0]
    rig = skinMod.object
    bpy.context.scene.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    armature = rig.data
    
    newVertOrder = dict()
    vertCount = len(mesh.vertices)
    faceCount = len(mesh.polygons)
    rootBhBone = loadBhBones(armature.edit_bones[0], object, newVertOrder)
    vertWriteOrder = {v:k for k, v in newVertOrder.items()}
    #bpy.ops.object.mode_set(mode='OBJECT')
    meshDataLength = 56 + 40 * vertCount + 6 * faceCount
    totalFileDataLength = 8 + meshDataLength + rootBhBone.totDataLength
    
    print([bone.bone.name for bone in rootBhBone.children])
    #print(rootBhBone.verts)
    print(newVertOrder)
    
    f = open(filepath, 'wb')
    write_uint(f, totalFileDataLength)
    write_short(f, 0)
    write_short(f, 2)

    write_uint(f, meshDataLength)
    write_short(f, 1)
    write_short(f, 4)
    
    write_uint(f, 12 + vertCount * 16)
    write_short(f, 2)
    write_short(f, 0)
    write_uint(f, vertCount)
    #bpy.ops.object.mode_set(mode='EDIT')
    print(normals[newVertOrder[39]])
    rootBhBone.writeVerts(f, mesh, [0])

    write_uint(f, 12 + vertCount * 16)
    write_short(f, 3)
    write_short(f, 0)
    write_uint(f, vertCount)
    for vi in range(0, vertCount):#newVertOrder.values():
        write_float(f, normals[vertWriteOrder[vi]][0])
        write_float(f, normals[vertWriteOrder[vi]][1])
        write_float(f, normals[vertWriteOrder[vi]][2])
#        write_float(f, mesh.vertices[nv].normal[0])
#        write_float(f, mesh.vertices[nv].normal[1])
#        write_float(f, mesh.vertices[nv].normal[2])
    for vi in range(0, vertCount):
        write_byte(f, 255)
        write_byte(f, 255)
        write_byte(f, 255)
        write_byte(f, 255)
    print(mesh.vertices[7].normal)
    print(mesh.loops[mesh.polygons[0].loop_indices[0]].normal)
    print(normals[7])

    write_uint(f, 12 + vertCount * 8)
    write_short(f, 4)
    write_short(f, 0)
    write_uint(f, vertCount)
    for vi in range(0, vertCount):
        tvert = uv_layer[texLayerIndex[vertWriteOrder[vi]]].uv
        write_float(f, tvert[0])
        write_float(f, 1 - tvert[1])

    write_uint(f, 12 + faceCount * 6)
    write_short(f, 5)
    write_short(f, 0)
    write_uint(f, faceCount * 3)
    for face in mesh.polygons:
        write_short(f, newVertOrder[face.vertices[2]])
        write_short(f, newVertOrder[face.vertices[1]])
        write_short(f, newVertOrder[face.vertices[0]])

    rootBhBone.write(f)

    f.close()
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.scene.objects.active = object
    bpy.data.meshes.remove(mesh)
    
    return {'FINISHED'}


# ImportHelper is a helper class, defines filename and
# invoke() function which calls the file selector.
from bpy_extras.io_utils import ImportHelper, ExportHelper
from bpy.props import StringProperty, BoolProperty, EnumProperty
from bpy.types import Operator


class ImportBH3(Operator, ImportHelper):
    """Load a Rise of Nations BH3 file"""
    bl_idname = "import_scene.bh3"  # important since its how bpy.ops.import_test.some_data is constructed
    bl_label = "Import BH3"

    # ImportHelper mixin class uses this
    filename_ext = ".BH3"

    filter_glob = StringProperty(
            default="*.BH3",
            options={'HIDDEN'},
            )

    # List of operator properties, the attributes will be assigned
    # to the class instance from the operator settings before calling.
#    use_setting = BoolProperty(
#            name="Example Boolean",
#            description="Example Tooltip",
#            default=True,
#            )

#    type = EnumProperty(
#            name="Example Enum",
#            description="Choose between two items",
#            items=(('OPT_A', "First Option", "Description one"),
#                   ('OPT_B', "Second Option", "Description two")),
#            default='OPT_A',
#            )

    def execute(self, context):
        return read_bh3_data(context, self.filepath)


class ExportBH3(Operator, ExportHelper):
    """Save a Rise of Nations BH3 file"""
    bl_idname = "export_scene.bh3"
    bl_label = "Export BH3"

    # ExportHelper mixin class uses this
    filename_ext = ".BH3"

    filter_glob = StringProperty(
            default="*.BH3",
            options={'HIDDEN'},
            )

    def execute(self, context):
        return write_bh3_data(context, self.filepath)


# Only needed if you want to add into a dynamic menu
def menu_func_import(self, context):
    self.layout.operator(ImportBH3.bl_idname, text="Rise of Nations (.BH3)")


def menu_func_export(self, context):
    self.layout.operator(ExportBH3.bl_idname, text="Rise of Nations (.BH3)")


def register():
    bpy.utils.register_class(ImportBH3)
    bpy.utils.register_class(ExportBH3)
    bpy.types.INFO_MT_file_import.append(menu_func_import)
    bpy.types.INFO_MT_file_export.append(menu_func_export)


def unregister():
    bpy.utils.unregister_class(ImportBH3)
    bpy.utils.unregister_class(ExportBH3)
    bpy.types.INFO_MT_file_import.remove(menu_func_import)
    bpy.types.INFO_MT_file_export.remove(menu_func_export)


if __name__ == "__main__":
    register()

    # test call
    bpy.ops.export_scene.bh3('INVOKE_DEFAULT')
