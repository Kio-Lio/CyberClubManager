using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CyberClubSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Cyber Club/Apply Prototype Setup")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureObjectWithComponent<EconomyManager>("EconomyManager", Vector3.zero);
        EnsureObjectWithComponent<EconomyUI>("EconomyUI", Vector3.zero);
        EnsurePcTest();
        EnsureObjectWithComponent<ClientSpawner>("ClientSpawner", new Vector3(-6f, 0f, 0f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureObjectWithComponent<T>(string name, Vector3 position) where T : Component
    {
        GameObject target = GameObject.Find(name);
        if (target == null)
        {
            target = new GameObject(name);
            target.transform.position = position;
        }

        if (target.GetComponent<T>() == null)
        {
            target.AddComponent<T>();
        }
    }

    private static void EnsurePcTest()
    {
        GameObject pcObject = GameObject.Find("PC_Test");
        if (pcObject == null)
        {
            pcObject = new GameObject("PC_Test");
            pcObject.transform.position = new Vector3(2.2f, 1.4f, 0f);
        }

        SpriteRenderer renderer = pcObject.GetComponent<SpriteRenderer>() ?? pcObject.AddComponent<SpriteRenderer>();
        if (renderer.sprite == null)
        {
            renderer.sprite = CreateRuntimeSquareSprite();
        }

        if (pcObject.GetComponent<BoxCollider2D>() == null)
        {
            BoxCollider2D collider = pcObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
        }

        if (pcObject.GetComponent<PC>() == null)
        {
            pcObject.AddComponent<PC>();
        }
    }

    private static Sprite CreateRuntimeSquareSprite()
    {
        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }
}
