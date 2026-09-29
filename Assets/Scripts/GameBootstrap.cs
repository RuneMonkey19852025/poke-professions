using UnityEngine;
using PokeProfessions.Core;
using PokeProfessions.World;
using PokeProfessions.UI;
using PokeProfessions.Audio;

/// <summary>
/// The one component placed in the scene by the editor bootstrap.
/// Initializes all databases, builds the world, spawns the player and UI.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        // uGUI event system so buttons work
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // global singletons
        var sfxGo = new GameObject("Sfx");
        sfxGo.AddComponent<Sfx>();

        var gameGo = new GameObject("Game");
        gameGo.AddComponent<Game>();   // builds world, player, camera in Awake

        var uiGo = new GameObject("UiRouter");
        uiGo.AddComponent<UiRouter>();
    }
}
