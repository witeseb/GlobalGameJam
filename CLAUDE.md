# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Global Game Jam Unity project: a first-person stealth game. You play a mouse who sneaks past cats to steal a "Big Cheese" and escape. The player can put on a **mask** (a disguise). Cats react differently depending on whether the mask is on, and the mask drains over time.

- Unity **6000.4.10f1** (Unity 6.4), URP 17.4, Input System 1.19 (`activeInputHandler: 1` means the new Input System only; legacy `Input.*` calls will not work).
- All gameplay code is in `Assets/Scripts/` and compiles into the default `Assembly-CSharp` assembly. There are no asmdefs, namespaces, or tests.
- Generated files (`*.csproj`, `GlobalGameJam.sln`, `Library/`, `Temp/`, `obj/`) come from Unity. Don't edit them.

## Working with the project

- There is no CLI build or test workflow. Code compiles when the Unity Editor regains focus or refreshes.
- The **unity-mcp** server is connected. Use `Unity_GetConsoleLogs` to check compile errors after editing scripts, and `Unity_RunCommand` to inspect or modify scenes and objects in the Editor.
- Batch build, if ever needed: `"C:/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe" -batchmode -quit -projectPath . -buildWindows64Player <out.exe>`. This fails while the Editor has the project open.

## Scene flow

`Main Menu` → `Cutscene` → `Level` (the enabled scenes in Build Settings).

- `MainMenuManager` loads the hardcoded scene name `"Cutscene"`.
- `CutsceneManager` (in `CutsceneSlides.cs`) loads the scene named in its `nextSceneName` inspector field.
- `Level_Im.unity` is a working or alternate copy of the level and is disabled in the build.

Renaming a scene breaks these string references.

## Architecture

**Input**
- `InputHandler` reads the project-wide actions (`InputSystem.actions`, defined in `Assets/InputSystem_Actions.inputactions`) by name: `Move`, `Look`, `Jump`, `Sprint`, `Mask`.
- It forwards these to `PlayerController.Move/Look/Jump` and `PlayerMask.SetMaskActive`. The mask is held, not toggled: `started` puts it on and `canceled` takes it off.
- `PlayerInteractor` separately reads the `Interact` action.
- Adding a new action means adding it to the `.inputactions` asset with the exact name the code looks up.

**Player** (a single GameObject that has `PlayerController`, `CharacterController`, `PlayerMask`, `PlayerInventory`, and `PlayerInteractor`)
- `PlayerController` handles CharacterController movement, gravity, stamina for sprinting, sprint FOV, and head bob.
- It finds its `CharacterController` and `Camera` with `FindFirstObjectByType`, not `GetComponent`. A second camera or CharacterController in the scene can therefore bind the wrong one.
- `PlayerInteractor` raycasts from `Camera.main` along `interactableLayer` and calls `IInteractable.Interact(PlayerInventory)`. `BigCheese` and `SmallCheese` implement this interface. New pickups should implement `IInteractable` and sit on the interactable layer.
- `PlayerInventory` raises `OnInventoryChanged(int smallCount, bool hasBigCheese)`, and the UI subscribes to it.
- `PlayerMask.IsMasked` is the core stealth state. It swaps between the `unmaskedModel` and `maskedModel` GameObjects.

**Cats.** Each cat combines a `VisionSensor` with an AI script and a movement script.
- `VisionSensor.CanSeeTarget()` uses an overlap sphere on `targetMask`, then a view-angle check, then a raycast against `obstacleMask`. It runs every frame for each cat.
- `RegularCatAI` works with `CatPatrol`. It is a state machine with the states Idle, Confused, and Chasing:
  - If the player is unmasked and in view for `unmaskedDetectionDelay` seconds, the cat chases.
  - Taking the mask off while in view causes an instant chase.
  - Being seen while masked adds a strike and makes the cat Confused, which pauses the patrol. Reaching `maxStrikes` triggers a chase.
  - A chase is permanent.
  - The AI drives `CatPatrol.StopPatrol/ResumePatrol/SetWalking`. `SetWalking` sets the Animator bool `IsWalking`.
- `BossCatAI` works with `BossHallwayPatrol`, which moves between two points.
  - The boss chases instantly if the player is unmasked, or if the player puts the mask on while in view.
  - Staying masked in view builds suspicion up to `maxSuspicion`, then the boss chases.
- Cats move by setting `transform.position` directly. They don't use a NavMesh, even though the `ai.navigation` package is installed.

**Level setup**
- `PlayerSpawnManager` (in `PlayerSpawner.cs`), `CheeseSpawner`, and `ExitSpawner` each pick random children of an assigned container Transform as spawn points.
- `PlayerSpawnManager` disables the CharacterController before teleporting the player and re-enables it afterwards.
- `ExtractionPoint` is a trigger: the player wins when they enter it while holding the Big Cheese.

**UI**
- `CheeseUIController` subscribes to `PlayerInventory.OnInventoryChanged`.
- `MaskMeterController` owns the mask durability. It drains the meter while masked, regenerates it otherwise, and calls `PlayerMask.RemoveMask()` when the meter is empty.
- `UIManager` is a thin facade that wraps these two controllers.
- `Billboard` (in `BillBoard.cs`) turns world-space icons, such as the cats' question-mark and chase icons, to face the camera.

## Gotchas

- Several class names don't match their file names: `CutsceneManager` is in `CutsceneSlides.cs`, `PlayerSpawnManager` is in `PlayerSpawner.cs`, and `Billboard` is in `BillBoard.cs`. Unity expects a MonoBehaviour's class name to match its file name. If you fix a mismatch, rename the file and the class together, keep the `.meta` file (it holds the GUID), and check in the Editor that the components in the scene still resolve.
- The following are still unimplemented and appear as `TODO`s or `Debug.Log` placeholders:
  - Game over (`RegularCatAI.HandleChase`, `BossCatAI.TriggerChase`)
  - Win screen (`ExtractionPoint`)
  - The boss's chase behavior
- `CameraController.cs` is an empty stub.
- Mixed indentation: most files use tabs, and some (`CatPatrol`, `RegularCatAI`, the UI scripts) use 4 spaces. Match the file you're editing.
