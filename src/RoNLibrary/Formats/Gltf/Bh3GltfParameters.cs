namespace RoNLibrary.Formats.Gltf;

public class Bh3GltfParameters
{
    public string? MeshFilePath { get; set; }
    
    public string? AnimFilePath { get; set; }

    public string? MeshName => Path.GetFileNameWithoutExtension(MeshFilePath);
    
    public string? AnimName => Path.GetFileNameWithoutExtension(AnimFilePath);
}
