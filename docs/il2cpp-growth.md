# IL2CPP growth at 04:00

The IL2CPP build advances plants and mushroom colonies on the host while the
game clock is stopped at 04:00. It does not advance the game clock or other
production systems. Normal growth before 04:00 remains the game's responsibility.
The Mono build retains its existing source files and Harmony implementation.

The update loop waits for a loaded Main scene, server authority, end of day,
04:00 and no sleep in progress. It uses scaled frame time and the game's minute
duration/time-speed multiplier. Loading, sleep, scene changes and large frame
gaps reset accumulated time. It scans current pots/colonies each growth step,
so it does not retain plant wrappers after harvesting or changing scenes.

Plants require water, suitable temperature and light. Growth uses the game's
light calculation and growth multipliers, including the tutorial multiplier,
and is capped at 100%. Elapsed minutes multiply growth instead of dividing it.
Mushrooms use their existing temperature and growth-rate calculations.

Schedule I 0.4.7f12 hides plants and mushroom colonies by deactivating their
objects (`Plant.SetVisible`, `ShroomColony.SetColonyVisible`) and keeps growing
them: `Pot.OnMinPass` calls `Plant.MinPass` whenever the pot has a plant, and
neither that nor `ShroomColony.OnMinPass` checks whether the object is active.
The mod follows the game. It reaches plants through their active pot and
colonies through their active mushroom bed, and does not skip a hidden one. A
scene search does not return a deactivated colony at all, which is why colonies
are found through `MushroomBed.CurrentColony`. 1.2.4 skipped hidden plants and
searched for colonies directly, so on 0.4.7f12 it grew nothing the game had
hidden.

The IL2CPP build installs no Harmony patches, inspects no native delegates and
does not move callbacks between the game's minute-event lists. A managed error
disables extra growth until the Main scene is loaded again and is logged once.
This cannot catch or prove the absence of a native crash.

The update body lives in its own non-inlined method, so a game member that a
later update removes fails when that method is compiled, inside the update's
`try`, and is logged once as well. In 1.2.3 the missing
`TimeManager.IsSleepInProgress` failed the update method itself on every frame,
outside the `try`. The managed tests only cover a game call that throws at run
time; the compile-time path rests on the method split and the 0.4.7f12 run.

## Multiplayer and saves

Only the host computes extra growth. Plant progress is sent through the game's
existing `Pot.SetGrowthProgress_Client` observers RPC; mushroom progress uses
`ShroomColony.SetGrowthPercentage_Local`. Both send absolute progress values.
The intended setup is to install this mod only on the host. Installing it on a
client does not enable local growth.

**A real multiplayer session with an unmodified client has not yet been tested.**
Managed tests check the server gate and outgoing values, not FishNet delivery,
late joining or client visuals. FullHouse's own installation requirements still
apply. Local coexistence with FullHouse 1.1.0 has been checked; full multiplayer
compatibility has not been established.

The mod writes normal in-memory growth fields and adds no save-file schema or
custom save data. Growth becomes part of the normal save when the game saves.
Removing the DLL stops the extra growth; it does not undo previously saved growth.
Save/reload behavior still needs an explicit in-game check.

## Settings

MelonLoader preferences category: `PlantsAlwaysGrow`.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Enable growth at 04:00. |
| `GrowMushrooms` | `true` | Include mushroom colonies. |
| `DiagnosticLogging` | `false` | Log growth ticks at most once every ten seconds. |

The first growth step each night logs the number of plants/colonies advanced
and the unchanged clock. A count of zero can mean nothing currently meets the
growth conditions.

## Build and regression tests

Populate `PlantsAlwaysGrow/il2cpp_libs` as described in the main README, using
the generated assemblies from the installed game and its MelonLoader runtime.
These dependencies must not be committed or redistributed.

Build without packaging or deploying into a game directory:

```sh
dotnet build PlantsAlwaysGrow/PlantsAlwaysGrow.csproj -c IL2CPP -p:RunPostBuildScripts=false
```

The existing Windows packaging/deployment scripts still run by default. The
IL2CPP output remains in `PlantsAlwaysGrow/bin/IL2CPP/net6/`.

Run the regression executable with .NET SDK 10 (no game files required):

```sh
dotnet run --project tests/PlantsAlwaysGrow.Tests/PlantsAlwaysGrow.Tests.csproj -c Release
```

The tests compile the production update loop and growth policy against managed
game doubles. They cover daytime inactivity, host/client gating, elapsed time,
water/light/temperature, multipliers, pause, loading/sleep, scene reload, pot
replacement/removal, hidden plants and colonies, growth caps, mushrooms, invalid
inputs and managed failures.
An assertion failure exits the executable with a nonzero status. These are
regression tests added after the implementation, not a reproduction of a native
crash or proof of native/network compatibility.

## Runtime evidence and remaining checks

1.2.5 was run on 2026-10-17 with Schedule I 0.4.7f12 IL2CPP, MelonLoader 0.7.3
and Proton on Linux, on a test save whose one planted pot (Sour Diesel, plastic
pot, motel room) had been given water while the player stood in another
property. With the clock set to 03:55 it logged `04:00 growth active; host
advanced 1 plants/colonies. Clock remains 0400.` and then `advanced=1` on every
tick, with no mod error. A diagnostic build of the same code logged the plant as
hidden and its progress rising from 0.0493 to 0.1167 over 26 game minutes at
04:00, 0.0026 a minute: one minute of a nine-hour plant at the pot's 1.4 light
multiplier, the rate the game itself grows it at during the day. The save was
not written and was restored from a backup afterwards.

1.2.4 had loaded and stopped the clock on the same save but logged `advanced 0`
every tick: the plant was hidden, and 1.2.4 skipped it. Mushroom growth has not
been observed on 0.4.7f12; that save has no mushroom bed.

The equivalent local candidate (`1.2.3-local.1`) was tested with Schedule I
0.4.6f13 IL2CPP, MelonLoader 0.7.3, FullHouse 1.1.0 and Proton Experimental on
Linux. Its log confirmed that two plants/colonies advanced while the clock
remained 04:00. Session logs continued about 40 minutes later without a logged
mod error. The upstream version changes metadata, log wording and the preference
category; the growth logic is the same.

The original 1.2.2 mod had been present during unexpected exits. There was no
known reproducible player action or native crash backtrace, so this change is
an alternative integration approach, not a confirmed root-cause crash fix.

Before a release, check native Windows, host plus client without this mod,
late joining, full plant/mushroom maturity and harvesting, save/reload, and an
extended session with FullHouse. The unchanged Mono implementation has not been
runtime-tested as part of this change.
