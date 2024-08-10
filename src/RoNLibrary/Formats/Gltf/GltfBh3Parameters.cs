namespace RoNLibrary.Formats.Gltf;

public class GltfBh3Parameters
{
    public bool ConvertMeshes { get; set; }

    public bool ConvertAnimations { get; set; }

    public int AnimationIndex { get; set; }

    public int SceneIndex { get; set; }

    public GltfBh3Parameters()
    {
        ConvertMeshes = true;
        ConvertAnimations = false;
        AnimationIndex = 0;
        SceneIndex = 0;
    }
}
