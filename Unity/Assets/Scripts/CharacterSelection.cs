using UnityEngine;

public class CharacterSelection : MonoBehaviour
{
    [Header("Character Options")]
    public GameObject[] characterPrefabs;
    public float[] characterScales;

    [Header("Materials")]
    public Material splashBaseMaterial;
    public Material projectileBaseMaterial;

    public Color[] baseColors;
    public Texture[] splashTextures;

    [HideInInspector] public Material splashMaterial;
    [HideInInspector] public Material projectileMaterial;

    private GameObject activeCharacter;

    void Start()
    {
        SelectRandomCharacter();
    }

    private void SelectRandomCharacter()
{
    if (characterPrefabs == null || characterPrefabs.Length == 0)
    {
        Debug.LogWarning("CharacterSelection: No prefabs assigned.");
        return;
    }

    if (characterScales == null || characterScales.Length != characterPrefabs.Length)
    {
        Debug.LogWarning("CharacterSelection: Scales array must match prefabs array length.");
        return;
    }

    if (baseColors == null || baseColors.Length != characterPrefabs.Length)
    {
        Debug.LogWarning("CharacterSelection: baseColors must match prefabs array length.");
        return;
    }

    if (splashTextures == null || splashTextures.Length != characterPrefabs.Length)
    {
        Debug.LogWarning("CharacterSelection: splashTextures must match prefabs array length.");
        return;
    }

    // Pick a shared index
    int index = Random.Range(0, characterPrefabs.Length);

    // Instantiate selected prefab
    GameObject chosen = Instantiate(characterPrefabs[index], transform);

    // Set local transform
    chosen.transform.localPosition = new Vector3(0f, 1f, 0f);
    chosen.transform.localRotation = Quaternion.identity;
    chosen.transform.localScale = new Vector3(
        characterScales[index],
        characterScales[index],
        characterScales[index]
    );

    activeCharacter = chosen;

    // -----------------------------------------------------
    // COPY BASE MATERIALS
    // -----------------------------------------------------

    // SPLASH MATERIAL
    if (splashBaseMaterial)
    {
        splashMaterial = new Material(splashBaseMaterial);

        // Assign splashTextures[index] to Base Map (_BaseMap)
        if (splashTextures[index])
            splashMaterial.SetTexture("_BaseMap", splashTextures[index]);
    }

    // PROJECTILE MATERIAL
    if (projectileBaseMaterial)
    {
        projectileMaterial = new Material(projectileBaseMaterial);

        // Assign color using baseColors[index]
        projectileMaterial.SetColor("_BaseColor", baseColors[index]);
    }
}

}
