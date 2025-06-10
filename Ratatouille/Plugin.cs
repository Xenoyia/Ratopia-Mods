using BepInEx;

[BepInPlugin("ratatouille", "Ratatouille", "1.0.0")]
public class RatatouillePlugin : BaseUnityPlugin
{
    // This plugin is a shared library for other BepInEx mods.
    // It does not need to do anything on its own.

    void Awake()
    {
        RatatouilleBootstrap.Init();
    }
} 