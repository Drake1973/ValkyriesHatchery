using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ValkyriesHatchery
{
    public enum ProgressionSource { World, Player }

    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.drakexi.valkyrieshatchery";
        public const string PluginName = "Valkyrie's Hatchery";
        public const string PluginVersion = "0.6.0";

        public static Plugin Instance;

        // ----- Config -----
        public static ConfigEntry<int> Slots;
        public static ConfigEntry<float> HatchMinutes;
        public static ConfigEntry<float> XpMultiplier;
        public static ConfigEntry<int> HomeBiomeChance;
        public static ConfigEntry<ProgressionSource> Progression;
        public static ConfigEntry<int> RoePrice;
        public static ConfigEntry<bool> ShowXpMessage;
        public static ConfigEntry<bool> PlayHatchSoundSetting;

        private const string PiecePrefabName = "piece_ValkyriesHatchery";
        private const string DisplayName = "Valkyrie's Hatchery";
        public const string RoePrefabName = "ValkyriesHatchery_Roe";

        private static readonly Color DarkStain = new Color(0.45f, 0.36f, 0.30f, 1f);
        private static readonly Color CaviarBlack = new Color(0.12f, 0.12f, 0.14f, 1f);

        public static readonly string[] FishIds =
        {
            "Fish1", "Fish2", "Fish3", "Fish4_cave", "Fish5", "Fish6",
            "Fish7", "Fish8", "Fish9", "Fish10", "Fish11", "Fish12"
        };

        // Hatch sound: nibble and float-splash, alternating, twice each.
        private static readonly string[] HatchSequence = { "fx_float_nibble", "fx_float_hitwater", "fx_float_nibble", "fx_float_hitwater" };
        private const float HatchSoundGap = 0.5f;

        private Trader.TradeItem _roeTrade;

        private void Awake()
        {
            Instance = this;
            BindConfig();
            PrefabManager.OnVanillaPrefabsAvailable += AddHatcheryPiece;
            PrefabManager.OnVanillaPrefabsAvailable += AddRoe;
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }

        private void BindConfig()
        {
            Slots = Config.Bind("Server", "Slots", 12,
                Server("Storage slots in each hatchery. Hatcheries resize live; anything in removed slots is moved to a free slot or dropped in front of the hatchery.",
                    new AcceptableValueList<int>(4, 6, 8, 12)));
            HatchMinutes = Config.Bind("Server", "HatchMinutes", 10f,
                Server("Real minutes for one roe to hatch into a fish.", new AcceptableValueRange<float>(0.5f, 120f)));
            XpMultiplier = Config.Bind("Server", "XpMultiplier", 1f,
                Server("Scales the Fishing XP for each fish collected (2 = double, 0 = none).", new AcceptableValueRange<float>(0f, 10f)));
            HomeBiomeChance = Config.Bind("Server", "HomeBiomeChance", 80,
                Server("Percent of hatches that are fish from the hatchery's own biome, when other fish are also unlocked.", new AcceptableValueRange<int>(0, 100)));
            Progression = Config.Bind("Server", "ProgressionSource", ProgressionSource.World,
                Server("Which boss kills unlock other biomes' fish: World (the world's kills) or Player (the kills of the player whose game is running the hatchery).", null));
            RoePrice = Config.Bind("Server", "RoePrice", 10,
                Server("Haldor's price for one Fish Roe.", new AcceptableValueRange<int>(1, 1000)));

            ShowXpMessage = Config.Bind("Personal", "ShowXpMessage", true,
                "Show a message with the Fishing XP gained when you collect fish.");
            PlayHatchSoundSetting = Config.Bind("Personal", "PlayHatchSound", true,
                "Play the nibble-and-splash sound when a fish hatches.");

            RoePrice.SettingChanged += (s, e) => { if (_roeTrade != null) _roeTrade.m_price = RoePrice.Value; };
        }

        private static ConfigDescription Server(string text, AcceptableValueBase range) =>
            new ConfigDescription(text, range, new ConfigurationManagerAttributes { IsAdminOnly = true });

        public static Vector2Int GridFor(int slots)
        {
            switch (slots)
            {
                case 4: return new Vector2Int(4, 1);
                case 6: return new Vector2Int(3, 2);
                case 8: return new Vector2Int(4, 2);
                default: return new Vector2Int(4, 3);
            }
        }

        // ---------- The hatchery piece ----------

        private void AddHatcheryPiece()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddHatcheryPiece;

            var config = new PieceConfig
            {
                Name = DisplayName,
                Description = "Fill it with roe, wait, and collect your fish.",
                PieceTable = "Hammer",
                Category = "Crafting",
                CraftingStation = "piece_workbench",
                Requirements = new[]
                {
                    new RequirementConfig("Wood", 20, 0, true),
                    new RequirementConfig("FineWood", 10, 0, true),
                    new RequirementConfig("Stone", 8, 0, true)
                }
            };

            var piece = new CustomPiece(PiecePrefabName, "piece_bathtub", config);
            if (piece.PiecePrefab == null)
            {
                Logger.LogError("Could not clone piece_bathtub.");
                return;
            }

            GameObject prefab = piece.PiecePrefab;
            StripHotTubParts(prefab);
            AddStorage(prefab);
            TintRenderers(prefab, DarkStain, "WaterSurface");

            Transform water = prefab.transform.Find("water");
            if (water != null) water.gameObject.SetActive(false);
            prefab.AddComponent<HatcheryBrain>();

            PieceManager.Instance.AddPiece(piece);
            Logger.LogInfo($"Added {DisplayName} piece.");
        }

        private void StripHotTubParts(GameObject prefab)
        {
            Smelter smelter = prefab.GetComponent<Smelter>();
            if (smelter != null) Object.DestroyImmediate(smelter);

            string[] removeChildren = { "add_fuel", "SitPoints", "_Enabled/fire", "_Enabled/WarmAndCozy" };
            foreach (string path in removeChildren)
            {
                Transform t = prefab.transform.Find(path);
                if (t != null) Object.DestroyImmediate(t.gameObject);
                else Logger.LogWarning($"Hatchery: part '{path}' not found on the hot tub clone.");
            }

            Piece p = prefab.GetComponent<Piece>();
            if (p != null)
            {
                p.m_comfort = 0;
                p.m_comfortObject = null;
                p.m_category = Piece.PieceCategory.Crafting;  // old hammer tabs
                p.m_usage = Piece.UsageTagFlags.Crafting;     // new build menu groups
            }
        }

        private void AddStorage(GameObject prefab)
        {
            Container chest = PrefabManager.Instance.GetPrefab("piece_chest_wood")?.GetComponent<Container>();
            Container c = prefab.AddComponent<Container>();
            Vector2Int grid = GridFor(Slots.Value);
            c.m_name = DisplayName;
            c.m_width = grid.x;
            c.m_height = grid.y;
            if (chest != null)
            {
                c.m_bkg = chest.m_bkg;
                c.m_openEffects = chest.m_openEffects;
                c.m_closeEffects = chest.m_closeEffects;
            }
        }

        private static void TintRenderers(GameObject prefab, Color tint, string skipName = null)
        {
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (skipName != null && r.transform.name == skipName) continue;
                if (r is ParticleSystemRenderer) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty("_Color")) continue;
                    Material copy = new Material(mats[i]);
                    copy.color = mats[i].color * tint;
                    mats[i] = copy;
                }
                r.sharedMaterials = mats;
            }
        }

        // ---------- Roe ----------

        private void AddRoe()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddRoe;

            var config = new ItemConfig
            {
                Name = "Fish Roe",
                Description = "Fish eggs, glistening and black. Place them in a Valkyrie's Hatchery to raise fish.",
                CraftingStation = "piece_preptable",
                Amount = 2,
                Requirements = FishIds.Select(id => new RequirementConfig(id, 1)).ToArray()
            };

            var roe = new CustomItem(RoePrefabName, "HatefulBlood", config);
            if (roe.ItemPrefab == null)
            {
                Logger.LogError("Could not clone HatefulBlood for roe.");
                return;
            }

            foreach (Renderer r in roe.ItemPrefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].HasProperty("_Color")) continue;
                    Material copy = new Material(mats[i]);
                    copy.color = mats[i].color * CaviarBlack;
                    mats[i] = copy;
                }
                r.sharedMaterials = mats;
            }

            var shared = roe.ItemDrop.m_itemData.m_shared;
            shared.m_maxStackSize = 50;
            shared.m_weight = 0.1f;
            shared.m_teleportable = true;

            Sprite icon = RenderManager.Instance.Render(roe.ItemPrefab, RenderManager.IsometricRotation);
            if (icon != null) shared.m_icons = new[] { icon };
            else Logger.LogWarning("Hatchery: could not render roe icon; keeping the original.");

            ItemManager.Instance.AddItem(roe);

            if (roe.Recipe?.Recipe != null)
            {
                roe.Recipe.Recipe.m_requireOnlyOneIngredient = true;
                Recipe vanilla = PrefabManager.Cache.GetPrefab<Recipe>("Recipe_Fish1");
                roe.Recipe.Recipe.m_qualityResultAmountMultiplier = vanilla != null ? vanilla.m_qualityResultAmountMultiplier : 1f;
            }
            else Logger.LogWarning("Hatchery: roe recipe was not created.");

            Logger.LogInfo("Added Fish Roe.");
            AddRoeToHaldor(roe.ItemDrop);
        }

        private void AddRoeToHaldor(ItemDrop roeDrop)
        {
            Trader haldor = PrefabManager.Instance.GetPrefab("Haldor")?.GetComponent<Trader>();
            if (haldor == null)
            {
                Logger.LogWarning("Hatchery: couldn't find Haldor; roe won't be sold.");
                return;
            }
            _roeTrade = haldor.m_items.FirstOrDefault(t => t.m_prefab != null && t.m_prefab.name == RoePrefabName);
            if (_roeTrade == null)
            {
                _roeTrade = new Trader.TradeItem { m_prefab = roeDrop, m_stack = 1, m_price = RoePrice.Value };
                haldor.m_items.Add(_roeTrade);
            }
            Logger.LogInfo("Added Fish Roe to Haldor's shop.");
        }

        // ---------- Hatch sound ----------

        public static void PlayHatchSound(Vector3 position)
        {
            if (Instance != null) Instance.StartCoroutine(HatchSoundRoutine(position));
        }

        private static IEnumerator HatchSoundRoutine(Vector3 position)
        {
            foreach (string name in HatchSequence)
            {
                GameObject fx = PrefabManager.Cache.GetPrefab<GameObject>(name);
                if (fx != null) Object.Instantiate(fx, position, Quaternion.identity);
                yield return new WaitForSeconds(HatchSoundGap);
            }
        }

        public static void Log(string msg) => Instance?.Logger.LogInfo(msg);
        public static void Warn(string msg) => Instance?.Logger.LogWarning(msg);
    }

    // ---------- Hatch rules ----------

    public static class HatchRules
    {
        private class FishInfo
        {
            public string Id;
            public Heightmap.Biome Home;
            public string UnlockKey;
            public float Xp;
            public float Weight;
            public FishInfo(string id, Heightmap.Biome home, string key, float xp, float weight = 1f)
            { Id = id; Home = home; UnlockKey = key; Xp = xp; Weight = weight; }
        }

        // Home biome(s), the boss key that unlocks the fish OUTSIDE its home biome, Fishing XP per fish,
        // and rarity weight (1 = normal; lower = rarer when competing with other fish in the same roll).
        private static readonly FishInfo[] Fish =
        {
            new FishInfo("Fish1",      Heightmap.Biome.Meadows | Heightmap.Biome.BlackForest, "defeated_eikthyr", 10),  // Perch
            new FishInfo("Fish2",      Heightmap.Biome.Meadows | Heightmap.Biome.BlackForest, "defeated_eikthyr", 10),  // Pike
            new FishInfo("Fish5",      Heightmap.Biome.BlackForest, "defeated_gdking", 15),        // Trollfish
            new FishInfo("Fish6",      Heightmap.Biome.Swamp,       "defeated_bonemass", 20),      // Giant Herring
            new FishInfo("Fish4_cave", Heightmap.Biome.Mountain,    "defeated_dragon", 25, 0.25f), // Tetra: rare, like the frost caves
            new FishInfo("Fish7",      Heightmap.Biome.Plains,      "defeated_goblinking", 25),    // Grouper
            new FishInfo("Fish3",      Heightmap.Biome.Ocean,       "defeated_queen", 30),         // Tuna
            new FishInfo("Fish8",      Heightmap.Biome.Ocean,       "defeated_queen", 30),         // Coral Cod
            new FishInfo("Fish12",     Heightmap.Biome.Mistlands | Heightmap.Biome.Ocean, "defeated_queen", 35), // Pufferfish
            new FishInfo("Fish9",      Heightmap.Biome.Mistlands,   "defeated_queen", 35),         // Anglerfish
            new FishInfo("Fish11",     Heightmap.Biome.AshLands,    "defeated_fader", 40),         // Magmafish
            new FishInfo("Fish10",     Heightmap.Biome.DeepNorth,   "defeated_frozenking_p3", 40), // Northern Salmon
        };

        public static float XpFor(string fishId) => Fish.FirstOrDefault(f => f.Id == fishId)?.Xp ?? 10f;

        // Picks a fish that currently fits in the storage. Returns null if nothing fits.
        public static string RollFish(Vector3 position, System.Func<string, bool> fits)
        {
            Heightmap.Biome biome = WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(position) : Heightmap.Biome.Meadows;

            var home = Fish.Where(f => (f.Home & biome) != 0 && fits(f.Id)).ToList();
            var others = Fish.Where(f => (f.Home & biome) == 0 && KeySet(f.UnlockKey) && fits(f.Id)).ToList();

            if (home.Count == 0 && others.Count == 0)
            {
                // Nothing fits (or, off-map, nothing is allowed): Perch as a last resort if it fits.
                bool anyHomeExists = Fish.Any(f => (f.Home & biome) != 0);
                return !anyHomeExists && fits("Fish1") ? "Fish1" : null;
            }
            if (home.Count == 0) return Pick(others);
            if (others.Count == 0 || Random.Range(0, 100) < Plugin.HomeBiomeChance.Value) return Pick(home);
            return Pick(others);
        }

        private static string Pick(List<FishInfo> pool)
        {
            float total = pool.Sum(f => f.Weight);
            float r = Random.Range(0f, total);
            foreach (FishInfo f in pool)
            {
                if (r < f.Weight) return f.Id;
                r -= f.Weight;
            }
            return pool[pool.Count - 1].Id;
        }

        private static bool KeySet(string key)
        {
            if (Plugin.Progression.Value == ProgressionSource.Player)
                return Player.m_localPlayer != null && Player.m_localPlayer.HaveUniqueKey(key);
            return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key);
        }
    }

    // ---------- Runs on each placed hatchery ----------

    public class HatcheryBrain : MonoBehaviour
    {
        private const string StartKey = "hatchery_start_ticks";
        private const string HatchRpc = "RPC_HatcheryHatch";

        private static readonly FieldInfo InvWidth = typeof(Inventory).GetField("m_width", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo InvChanged = typeof(Inventory).GetMethod("Changed", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null);
        private static readonly FieldInfo InvHeight = typeof(Inventory).GetField("m_height", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        private ZNetView _nview;
        private Container _container;
        private GameObject _water;
        private GameObject _bubbles;
        private Dictionary<string, int> _fishCounts = new Dictionary<string, int>();

        private void Start()
        {
            _nview = GetComponent<ZNetView>();
            _container = GetComponent<Container>();
            _water = transform.Find("water")?.gameObject;
            _bubbles = transform.Find("_Enabled")?.gameObject;
            if (_nview == null || _nview.GetZDO() == null || _container == null) return;

            _nview.Register(HatchRpc, (long sender) =>
            {
                if (Plugin.PlayHatchSoundSetting.Value) Plugin.PlayHatchSound(transform.position);
            });

            Inventory inv = _container.GetInventory();
            if (inv != null)
            {
                _fishCounts = CountFish(inv);
                inv.m_onChanged += OnInventoryChanged;
            }
            InvokeRepeating(nameof(Tick), 1f, 1f);
        }

        private void Tick()
        {
            if (_nview == null || !_nview.IsValid()) return;
            Inventory inv = _container.GetInventory();
            if (inv == null) return;

            ApplySlotCount(inv);
            if (_nview.IsOwner()) Hatch(inv);
            UpdateVisuals(inv);
        }

        // ----- Live resizing from the Slots setting -----

        private void ApplySlotCount(Inventory inv)
        {
            Vector2Int grid = Plugin.GridFor(Plugin.Slots.Value);
            if (inv.GetWidth() == grid.x && inv.GetHeight() == grid.y) return;
            if (InvWidth == null || InvHeight == null) return;
            if (_container.IsInUse()) return; // wait until nobody has it open

            _container.m_width = grid.x;
            _container.m_height = grid.y;
            InvWidth.SetValue(inv, grid.x);
            InvHeight.SetValue(inv, grid.y);

            if (!_nview.IsOwner()) return;

            // Anything now outside the grid: move to a free slot, or drop it in front of the hatchery.
            _busy = true;
            try
            {
                foreach (ItemDrop.ItemData item in inv.GetAllItems().ToList())
                {
                    if (item.m_gridPos.x < grid.x && item.m_gridPos.y < grid.y) continue;
                    Vector2i? free = FindFreeSlot(inv, grid);
                    if (free.HasValue)
                    {
                        item.m_gridPos = free.Value;
                    }
                    else
                    {
                        ItemDrop.DropItem(item.Clone(), item.m_stack, transform.position + transform.forward * 2f + Vector3.up, Quaternion.identity);
                        inv.RemoveItem(item);
                    }
                }
                InvChanged?.Invoke(inv, null); // save the new layout
            }
            finally { _busy = false; }
            _fishCounts = CountFish(inv);
            Plugin.Log($"Hatchery resized to {grid.x}x{grid.y}.");
        }

        private static Vector2i? FindFreeSlot(Inventory inv, Vector2Int grid)
        {
            for (int y = 0; y < grid.y; y++)
                for (int x = 0; x < grid.x; x++)
                    if (inv.GetItemAt(x, y) == null) return new Vector2i(x, y);
            return null;
        }

        // ----- Hatching -----

        private void Hatch(Inventory inv)
        {
            ZDO zdo = _nview.GetZDO();
            long now = ZNet.instance.GetTime().Ticks;
            long start = zdo.GetLong(StartKey, 0L);
            long step = (long)(Plugin.HatchMinutes.Value * 60f * System.TimeSpan.TicksPerSecond);

            if (FindRoe(inv) == null)
            {
                if (start != 0L) zdo.Set(StartKey, 0L);
                return;
            }
            if (start == 0L)
            {
                zdo.Set(StartKey, now);
                return;
            }

            // Catch up on hatches that were due, one roe at a time, while fish still fit.
            int hatched = 0;
            while (now - start >= step)
            {
                ItemDrop.ItemData roe = FindRoe(inv);
                if (roe == null) { start = 0L; break; }

                string fishId = HatchRules.RollFish(transform.position, id => CanFit(inv, id));
                if (fishId == null || !AddFish(inv, fishId))
                {
                    start = now; // full: the clock stops until there's room again
                    break;
                }
                _fishCounts.TryGetValue(fishId, out int legit);
                _fishCounts[fishId] = legit + 1;
                inv.RemoveItem(roe, 1);
                start += step;
                hatched++;
            }

            if (start != 0L && FindRoe(inv) == null) start = 0L;
            zdo.Set(StartKey, start);

            if (hatched > 0)
            {
                _nview.InvokeRPC(ZNetView.Everybody, HatchRpc);
                Plugin.Log($"Hatchery hatched {hatched} fish.");
            }
        }

        // A fish fits if there's an empty slot, or a quality-1 stack of that fish with room.
        private static bool CanFit(Inventory inv, string fishId)
        {
            if (inv.GetEmptySlots() > 0) return true;
            return inv.GetAllItems().Any(i => i.m_dropPrefab != null && i.m_dropPrefab.name == fishId
                                              && i.m_quality == 1 && i.m_stack < i.m_shared.m_maxStackSize);
        }

        private static ItemDrop.ItemData FindRoe(Inventory inv) =>
            inv.GetAllItems().FirstOrDefault(i => i.m_dropPrefab != null && i.m_dropPrefab.name == Plugin.RoePrefabName);

        private static bool AddFish(Inventory inv, string fishId)
        {
            GameObject prefab = ObjectDB.instance?.GetItemPrefab(fishId);
            ItemDrop drop = prefab?.GetComponent<ItemDrop>();
            if (drop == null) return false;

            ItemDrop.ItemData fish = drop.m_itemData.Clone();
            fish.m_dropPrefab = prefab;
            fish.m_stack = 1;
            fish.m_quality = 1;
            fish.m_durability = fish.GetMaxDurability();
            return inv.AddItem(fish);
        }

        private void UpdateVisuals(Inventory inv)
        {
            bool anything = inv.NrOfItems() > 0;
            bool fishWaiting = inv.GetAllItems().Any(IsFish);
            if (_water != null && _water.activeSelf != anything) _water.SetActive(anything);
            if (_bubbles != null && _bubbles.activeSelf != fishWaiting) _bubbles.SetActive(fishWaiting);
        }

        // ----- Storage watcher: roe-only rule and Fishing XP -----

        private bool _busy;
        private bool _checkQueued;

        private static bool IsRoe(ItemDrop.ItemData i) =>
            i.m_dropPrefab != null && i.m_dropPrefab.name == Plugin.RoePrefabName;

        private static bool IsFish(ItemDrop.ItemData i) =>
            i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Fish && i.m_dropPrefab != null;

        private static Dictionary<string, int> CountFish(Inventory inv)
        {
            var counts = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData i in inv.GetAllItems())
            {
                if (!IsFish(i)) continue;
                counts.TryGetValue(i.m_dropPrefab.name, out int n);
                counts[i.m_dropPrefab.name] = n + i.m_stack;
            }
            return counts;
        }

        private bool LocalPlayerHasItOpen() =>
            _container.IsInUse() && _nview.IsOwner() && InventoryGui.IsVisible();

        // Wait until the current move has fully finished (e.g. "Place stacks"), then check.
        private void OnInventoryChanged()
        {
            if (_busy || _checkQueued) return;
            _checkQueued = true;
            Invoke(nameof(Enforce), 0f);
        }

        private void Enforce()
        {
            _checkQueued = false;
            if (_busy || _nview == null || !_nview.IsValid()) return;
            Inventory inv = _container.GetInventory();
            if (inv == null) return;

            if (!_nview.IsOwner())
            {
                _fishCounts = CountFish(inv);
                return;
            }

            _busy = true;
            try
            {
                bool rejected = false;

                foreach (ItemDrop.ItemData i in inv.GetAllItems().ToList())
                {
                    if (IsRoe(i)) continue;
                    if (IsFish(i) && i.m_quality == 1) continue;
                    Eject(inv, i, i.m_stack);
                    rejected = true;
                }

                var now = CountFish(inv);

                foreach (var kv in now.ToList())
                {
                    _fishCounts.TryGetValue(kv.Key, out int legit);
                    int extra = kv.Value - legit;
                    while (extra > 0)
                    {
                        ItemDrop.ItemData stack = inv.GetAllItems().FirstOrDefault(i => IsFish(i) && i.m_dropPrefab.name == kv.Key);
                        if (stack == null) break;
                        int take = Mathf.Min(extra, stack.m_stack);
                        Eject(inv, stack, take);
                        extra -= take;
                        rejected = true;
                    }
                }
                now = CountFish(inv);

                var removed = new Dictionary<string, int>();
                foreach (var kv in _fishCounts)
                {
                    now.TryGetValue(kv.Key, out int left);
                    if (left < kv.Value) removed[kv.Key] = kv.Value - left;
                }
                if (removed.Count > 0 && LocalPlayerHasItOpen()) AwardXp(removed);

                _fishCounts = now;

                if (rejected && LocalPlayerHasItOpen())
                    Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Only Fish Roe can go in the hatchery.");
            }
            finally
            {
                _busy = false;
            }
        }

        private void Eject(Inventory inv, ItemDrop.ItemData item, int amount)
        {
            ItemDrop.ItemData give = item.Clone();
            give.m_stack = amount;

            Player p = Player.m_localPlayer;
            bool returned = false;
            if (p != null && LocalPlayerHasItOpen() && p.GetInventory().CanAddItem(give))
                returned = p.GetInventory().AddItem(give);

            if (!returned)
                ItemDrop.DropItem(give, amount, transform.position + transform.forward * 2f + Vector3.up, Quaternion.identity);

            inv.RemoveItem(item, amount);
        }

        private static void AwardXp(Dictionary<string, int> removed)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            float total = 0f;
            int count = 0;
            foreach (var kv in removed)
            {
                total += HatchRules.XpFor(kv.Key) * kv.Value;
                count += kv.Value;
            }
            total *= Plugin.XpMultiplier.Value;
            if (total <= 0f) return;
            player.RaiseSkill(Skills.SkillType.Fishing, total);
            if (Plugin.ShowXpMessage.Value)
                player.Message(MessageHud.MessageType.TopLeft, $"Fishing +{total:0} ({count} fish)");
            Plugin.Log($"Hatchery: awarded {total} Fishing XP for {count} fish.");
        }
    }
}
