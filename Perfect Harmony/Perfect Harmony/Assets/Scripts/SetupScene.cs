using UnityEngine;

public class SetupScene : MonoBehaviour
{
    void Awake()
    {
        // Create a setup manager to handle automatic setup
        GameObject setupObj = new GameObject("SetupManager");
        AutoSetup autoSetup = setupObj.AddComponent<AutoSetup>();
    }
}