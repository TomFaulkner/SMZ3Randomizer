﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using static System.Linq.Enumerable;
using Randomizer.SMZ3.Regions.Zelda;
using Randomizer.SMZ3.Text;
using static Randomizer.SMZ3.ItemType;
using static Randomizer.SMZ3.RewardType;
using static Randomizer.SMZ3.DropPrize;

namespace Randomizer.SMZ3 {

    static class KeycardPlaque {
        public const ushort Level1 = 0xe0;
        public const ushort Level2 = 0xe1;
        public const ushort Boss = 0xe2;
        public const ushort None = 0x00;
        public const ushort Zero = 0xe3;
        public const ushort One = 0xe4;
        public const ushort Two = 0xe5;
        public const ushort Three = 0xe6;
        public const ushort Four = 0xe7;
    }

    static class KeycardDoors {
        public const ushort Left = 0xd414;
        public const ushort Right = 0xd41a;
        public const ushort Up = 0xd420;
        public const ushort Down = 0xd426;
        public const ushort BossLeft = 0xc842;
        public const ushort BossRight = 0xc848;
    }

    static class KeycardEvents {
        public const ushort CrateriaLevel1 = 0x0000;
        public const ushort CrateriaLevel2 = 0x0100;
        public const ushort CrateriaBoss = 0x0200;
        public const ushort BrinstarLevel1 = 0x0300;
        public const ushort BrinstarLevel2 = 0x0400;
        public const ushort BrinstarBoss = 0x0500;
        public const ushort NorfairLevel1 = 0x0600;
        public const ushort NorfairLevel2 = 0x0700;
        public const ushort NorfairBoss = 0x0800;
        public const ushort MaridiaLevel1 = 0x0900;
        public const ushort MaridiaLevel2 = 0x0a00;
        public const ushort MaridiaBoss = 0x0b00;
        public const ushort WreckedShipLevel1 = 0x0c00;
        public const ushort WreckedShipBoss = 0x0d00;
        public const ushort LowerNorfairLevel1 = 0x0e00;
        public const ushort LowerNorfairBoss = 0x0f00;
    }

    enum DropPrize : byte {
        Heart = 0xD8,
        Green = 0xD9,
        Blue = 0xDA,
        Red = 0xDB,
        Bomb1 = 0xDC,
        Bomb4 = 0xDD,
        Bomb8 = 0xDE,
        Magic = 0xDF,
        FullMagic = 0xE0,
        Arrow5 = 0xE1,
        Arrow10 = 0xE2,
        Fairy = 0xE3,
    }

    class Patch {

        readonly List<World> allWorlds;
        readonly World myWorld;
        readonly string seedGuid;
        readonly int seed;
        readonly Random rnd;
        StringTable stringTable;
        List<(int offset, byte[] bytes)> patches;

        public Patch(World myWorld, List<World> allWorlds, string seedGuid, int seed, Random rnd) {
            this.myWorld = myWorld;
            this.allWorlds = allWorlds;
            this.seedGuid = seedGuid;
            this.seed = seed;
            this.rnd = rnd;
        }

        public Dictionary<int, byte[]> Create(Config config) {
            stringTable = new StringTable();
            patches = new List<(int, byte[])>();

            WriteMedallions();
            WriteRewards();
            WriteDungeonMusic(config.Keysanity);

            WriteDiggingGameRng();

            WritePrizeShuffle();

            WriteRemoveEquipmentFromUncle(myWorld.GetLocation("Link's Uncle").Item);

            WriteGanonInvicible(config.Goal);
            WritePreOpenPyramid(config.Goal);
            WriteCrystalsNeeded(myWorld.OpenTower, myWorld.GanonVulnerable);
            WriteBossesNeeded(myWorld.OpenTourian);
            WriteRngBlock();

            WriteSaveAndQuitFromBossRoom();
            WriteWorldOnAgahnimDeath();

            WriteTexts(config);

            WriteSMLocations(myWorld.Regions.OfType<SMRegion>().SelectMany(x => x.Locations));
            WriteZ3Locations(myWorld.Regions.OfType<Z3Region>().SelectMany(x => x.Locations));

            WriteStringTable();

            WriteSMKeyCardDoors();
            WriteZ3KeysanityFlags();

            WritePlayerNames();
            WriteSeedData();
            WriteGameTitle();
            WriteCommonFlags();

            return patches.ToDictionary(x => x.offset, x => x.bytes);
        }

        void WriteMedallions() {
            var turtleRock = myWorld.Regions.OfType<TurtleRock>().First();
            var miseryMire = myWorld.Regions.OfType<MiseryMire>().First();

            var turtleRockAddresses = new int[] { 0x308023, 0xD020, 0xD0FF, 0xD1DE };
            var miseryMireAddresses = new int[] { 0x308022, 0xCFF2, 0xD0D1, 0xD1B0 };

            var turtleRockValues = turtleRock.Medallion switch {
                Bombos => new byte[] { 0x00, 0x51, 0x10, 0x00 },
                Ether => new byte[] { 0x01, 0x51, 0x18, 0x00 },
                Quake => new byte[] { 0x02, 0x14, 0xEF, 0xC4 },
                var x => throw new InvalidOperationException($"Tried using {x} in place of Turtle Rock medallion")
            };

            var miseryMireValues = miseryMire.Medallion switch {
                Bombos => new byte[] { 0x00, 0x51, 0x00, 0x00 },
                Ether => new byte[] { 0x01, 0x13, 0x9F, 0xF1 },
                Quake => new byte[] { 0x02, 0x51, 0x08, 0x00 },
                var x => throw new InvalidOperationException($"Tried using {x} in place of Misery Mire medallion")
            };

            patches.AddRange(turtleRockAddresses.Zip(turtleRockValues, (i, b) => (Snes(i), new byte[] { b })));
            patches.AddRange(miseryMireAddresses.Zip(miseryMireValues, (i, b) => (Snes(i), new byte[] { b })));
        }

        void WriteRewards() {
            var crystalsBlue = new[] { 1, 2, 3, 4, 7 }.Shuffle(rnd);
            var crystalsRed = new[] { 5, 6 }.Shuffle(rnd);
            var crystalRewards = crystalsBlue.Concat(crystalsRed);

            var pendantsGreen = new[] { 1 };
            var pendantsBlueRed = new[] { 2, 3 }.Shuffle(rnd);
            var pendantRewards = pendantsGreen.Concat(pendantsBlueRed);

            var bossTokens = new[] { 1, 2, 3, 4 };

            var regions = myWorld.Regions.OfType<IReward>();
            var crystalRegions = regions.Where(x => x.Reward == CrystalBlue).Concat(regions.Where(x => x.Reward == CrystalRed));
            var pendantRegions = regions.Where(x => x.Reward == PendantGreen).Concat(regions.Where(x => x.Reward == PendantNonGreen));
            var bossRegions = regions
                .Where(x => x.Reward == BossTokenKraid)
                .Concat(regions.Where(x => x.Reward == BossTokenPhantoon))
                .Concat(regions.Where(x => x.Reward == BossTokenDraygon))
                .Concat(regions.Where(x => x.Reward == BossTokenRidley));

            patches.AddRange(RewardPatches(crystalRegions, crystalRewards, CrystalValues));
            patches.AddRange(RewardPatches(pendantRegions, pendantRewards, PendantValues));
            patches.AddRange(RewardPatches(bossRegions, bossTokens, BossTokenValues));
        }

        IEnumerable<(int, byte[])> RewardPatches(IEnumerable<IReward> regions, IEnumerable<int> rewards, Func<int, byte[]> rewardValues) {
            var addresses = regions.Select(RewardAddresses);
            var values = rewards.Select(rewardValues);
            var associations = addresses.Zip(values, (a, v) => (a, v));
            return associations.SelectMany(x => x.a.Zip(x.v, (i, b) => (Snes(i), new byte[] { b })));
        }

        int[] RewardAddresses(IReward region) {
            return region switch {
                EasternPalace _ => new[] { 0x2A09D, 0xABEF8, 0xABEF9, 0x308052, 0x30807C, 0x1C6FE, 0x30D100 },
                DesertPalace _ => new[] { 0x2A09E, 0xABF1C, 0xABF1D, 0x308053, 0x308078, 0x1C6FF, 0x30D101 },
                TowerOfHera _ => new[] { 0x2A0A5, 0xABF0A, 0xABF0B, 0x30805A, 0x30807A, 0x1C706, 0x30D102 },
                PalaceOfDarkness _ => new[] { 0x2A0A1, 0xABF00, 0xABF01, 0x308056, 0x30807D, 0x1C702, 0x30D103 },
                SwampPalace _ => new[] { 0x2A0A0, 0xABF6C, 0xABF6D, 0x308055, 0x308071, 0x1C701, 0x30D104 },
                SkullWoods _ => new[] { 0x2A0A3, 0xABF12, 0xABF13, 0x308058, 0x30807B, 0x1C704, 0x30D105 },
                ThievesTown _ => new[] { 0x2A0A6, 0xABF36, 0xABF37, 0x30805B, 0x308077, 0x1C707, 0x30D106 },
                IcePalace _ => new[] { 0x2A0A4, 0xABF5A, 0xABF5B, 0x308059, 0x308073, 0x1C705, 0x30D107 },
                MiseryMire _ => new[] { 0x2A0A2, 0xABF48, 0xABF49, 0x308057, 0x308075, 0x1C703, 0x30D108 },
                TurtleRock _ => new[] { 0x2A0A7, 0xABF24, 0xABF25, 0x30805C, 0x308079, 0x1C708, 0x30D109 },

                Regions.SuperMetroid.Brinstar.Kraid _ => new[] { 0xF26002, 0xF26004, 0xF26005, 0xF26000, 0xF26006, 0xF26007, 0x82FD36 },
                Regions.SuperMetroid.WreckedShip _ => new[] { 0xF2600A, 0xF2600C, 0xF2600D, 0xF26008, 0xF2600E, 0xF2600F, 0x82FE26 },
                Regions.SuperMetroid.Maridia.Inner _ => new[] { 0xF26012, 0xF26014, 0xF26015, 0xF26010, 0xF26016, 0xF26017, 0x82FE76 },
                Regions.SuperMetroid.NorfairLower.East _ => new[] { 0xF2601A, 0xF2601C, 0xF2601D, 0xF26018, 0xF2601E, 0xF2601F, 0x82FDD6 },

                var x => throw new InvalidOperationException($"Region {x} should not be a dungeon reward region")
            };
        }

        byte[] CrystalValues(int crystal) {
            return crystal switch {
                1 => new byte[] { 0x02, 0x34, 0x64, 0x40, 0x7F, 0x06, 0x10 },
                2 => new byte[] { 0x10, 0x34, 0x64, 0x40, 0x79, 0x06, 0x10 },
                3 => new byte[] { 0x40, 0x34, 0x64, 0x40, 0x6C, 0x06, 0x10 },
                4 => new byte[] { 0x20, 0x34, 0x64, 0x40, 0x6D, 0x06, 0x10 },
                5 => new byte[] { 0x04, 0x32, 0x64, 0x40, 0x6E, 0x06, 0x11 },
                6 => new byte[] { 0x01, 0x32, 0x64, 0x40, 0x6F, 0x06, 0x11 },
                7 => new byte[] { 0x08, 0x34, 0x64, 0x40, 0x7C, 0x06, 0x10 },
                var x => throw new InvalidOperationException($"Tried using {x} as a crystal number")
            };
        }

        byte[] PendantValues(int pendant) {
            return pendant switch {
                1 => new byte[] { 0x04, 0x38, 0x62, 0x00, 0x69, 0x01, 0x12 },
                2 => new byte[] { 0x01, 0x32, 0x60, 0x00, 0x69, 0x03, 0x14 },
                3 => new byte[] { 0x02, 0x34, 0x60, 0x00, 0x69, 0x02, 0x13 },
                var x => throw new InvalidOperationException($"Tried using {x} as a pendant number")
            };
        }

        byte[] BossTokenValues(int token) {
            return token switch {
                1 => new byte[] { 0x01, 0x38, 0x40, 0x80, 0x69, 0x80, 0x15 },
                2 => new byte[] { 0x02, 0x34, 0x42, 0x80, 0x69, 0x81, 0x16 },
                3 => new byte[] { 0x04, 0x34, 0x44, 0x80, 0x69, 0x82, 0x17 },
                4 => new byte[] { 0x08, 0x32, 0x46, 0x80, 0x69, 0x83, 0x18 },
                var x => throw new InvalidOperationException($"Tried using {x} as a boss token number")
            };
        }
        void WriteSMLocations(IEnumerable<Location> locations) {
            foreach (var location in locations) {
                if (myWorld.Config.MultiWorld) {
                    patches.Add((Snes(location.Address), UshortBytes(GetSMItemPLM(location))));
                    patches.Add(ItemTablePatch(location, GetZ3ItemId(location)));
                } else {
                    ushort plmId = GetSMItemPLM(location);
                    patches.Add((Snes(location.Address), UshortBytes(plmId)));
                    if (plmId >= 0xEFE0) {
                        patches.Add((Snes(location.Address + 5), new byte[] { GetZ3ItemId(location) }));
                    }
                }
            }
        }

        ushort GetSMItemPLM(Location location) {
            int plmId = myWorld.Config.MultiWorld ?
                0xEFE0 :
                location.Item.Type switch {
                    ETank => 0xEED7,
                    Missile => 0xEEDB,
                    Super => 0xEEDF,
                    PowerBomb => 0xEEE3,
                    Bombs => 0xEEE7,
                    Charge => 0xEEEB,
                    Ice => 0xEEEF,
                    HiJump => 0xEEF3,
                    SpeedBooster => 0xEEF7,
                    Wave => 0xEEFB,
                    Spazer => 0xEEFF,
                    SpringBall => 0xEF03,
                    Varia => 0xEF07,
                    Plasma => 0xEF13,
                    Grapple => 0xEF17,
                    Morph => 0xEF23,
                    ReserveTank => 0xEF27,
                    Gravity => 0xEF0B,
                    XRay => 0xEF0F,
                    SpaceJump => 0xEF1B,
                    ScrewAttack => 0xEF1F,
                    _ => 0xEFE0,
                };

            plmId += plmId switch {
                0xEFE0 => location.Type switch {
                    LocationType.Chozo => 4,
                    LocationType.Hidden => 8,
                    _ => 0
                },
                _ => location.Type switch {
                    LocationType.Chozo => 0x54,
                    LocationType.Hidden => 0xA8,
                    _ => 0
                }
            };

            return (ushort)plmId;
        }

        void WriteZ3Locations(IEnumerable<Location> locations) {
            foreach (var location in locations) {
                if (location.Type == LocationType.HeraStandingKey) {
                    patches.Add((Snes(0x9E3BB), location.Item.Type == KeyTH ? new byte[] { 0xE4 } : new byte[] { 0xEB }));
                } else if (new[] { LocationType.Pedestal, LocationType.Ether, LocationType.Bombos }.Contains(location.Type)) {
                    var text = Texts.ItemTextbox(location.Item);
                    var dialog = Dialog.Simple(text);
                    if (location.Type == LocationType.Pedestal) {
                        patches.Add((Snes(0x308300), dialog));
                        stringTable.SetPedestalText(text);
                    }
                    else if (location.Type == LocationType.Ether) {
                        patches.Add((Snes(0x308F00), dialog));
                        stringTable.SetEtherText(text);
                    }
                    else if (location.Type == LocationType.Bombos) {
                        patches.Add((Snes(0x309000), dialog));
                        stringTable.SetBombosText(text);
                    }
                }

                if (myWorld.Config.MultiWorld) {
                    patches.Add((Snes(location.Address), new byte[] { (byte)(location.Id - 256) }));
                    patches.Add(ItemTablePatch(location, GetZ3ItemId(location)));
                } else {
                    patches.Add((Snes(location.Address), new byte[] { GetZ3ItemId(location) }));
                }
            }
        }

        byte GetZ3ItemId(Location location) {
            var item = location.Item;
            var value = location.Type == LocationType.NotInDungeon ||
                !(item.IsDungeonItem && location.Region.IsRegionItem(item) && item.World == myWorld) ? item.Type : item switch {
                    _ when item.IsKey => Key,
                    _ when item.IsBigKey => BigKey,
                    _ when item.IsMap => Map,
                    _ when item.IsCompass => Compass,
                    _ => throw new InvalidOperationException($"Tried replacing {item} with a dungeon region item"),
                };
            return (byte)value;
        }

        (int, byte[]) ItemTablePatch(Location location, byte itemId) {
            var type = location.Item.World == location.Region.World ? 0 : 1;
            var owner = location.Item.World.Id;
            return (0x386000 + (location.Id * 8), new[] { type, itemId, owner, 0 }.SelectMany(UshortBytes).ToArray());
        }

        void WriteDungeonMusic(bool keysanity) {
            if (!keysanity) {
                var regions = myWorld.Regions.OfType<Z3Region>().OfType<IReward>().Where(x => x.Reward != None && x.Reward != Agahnim);
                var music = regions.Select(x => (byte)(x.Reward switch {
                    PendantGreen => 0x11,
                    PendantNonGreen => 0x11,
                    _ => 0x16
                }));

                patches.AddRange(MusicPatches(regions, music));
            }
        }

        IEnumerable<byte> RandomDungeonMusic() {
            while (true) yield return rnd.Next(2) == 0 ? (byte)0x11 : (byte)0x16;
        }

        IEnumerable<(int, byte[])> MusicPatches(IEnumerable<IReward> regions, IEnumerable<byte> music) {
            var addresses = regions.Select(MusicAddresses);
            var associations = addresses.Zip(music, (a, b) => (a, b));
            return associations.SelectMany(x => x.a.Select(i => (Snes(i), new byte[] { x.b })));
        }

        int[] MusicAddresses(IReward region) {
            return region switch {
                EasternPalace _ => new[] { 0x2D59A },
                DesertPalace _ => new[] { 0x2D59B, 0x2D59C, 0x2D59D, 0x2D59E },
                TowerOfHera _ => new[] { 0x2D5C5, 0x2907A, 0x28B8C },
                PalaceOfDarkness _ => new[] { 0x2D5B8 },
                SwampPalace _ => new[] { 0x2D5B7 },
                SkullWoods _ => new[] { 0x2D5BA, 0x2D5BB, 0x2D5BC, 0x2D5BD, 0x2D608, 0x2D609, 0x2D60A, 0x2D60B },
                ThievesTown _ => new[] { 0x2D5C6 },
                IcePalace _ => new[] { 0x2D5BF },
                MiseryMire _ => new[] { 0x2D5B9 },
                TurtleRock _ => new[] { 0x2D5C7, 0x2D5A7, 0x2D5AA, 0x2D5AB },
                var x => throw new InvalidOperationException($"Region {x} should not be a dungeon music region"),
            };
        }

        void WritePrizeShuffle() {
            const int prizePackItems = 56;
            const int treePullItems = 3;

            IEnumerable<byte> bytes;
            byte drop, final;

            var pool = new DropPrize[] {
                Heart, Heart, Heart, Heart, Green, Heart, Heart, Green,         // pack 1
                Blue, Green, Blue, Red, Blue, Green, Blue, Blue,                // pack 2
                FullMagic, Magic, Magic, Blue, FullMagic, Magic, Heart, Magic,  // pack 3
                Bomb1, Bomb1, Bomb1, Bomb4, Bomb1, Bomb1, Bomb8, Bomb1,         // pack 4
                Arrow5, Heart, Arrow5, Arrow10, Arrow5, Heart, Arrow5, Arrow10, // pack 5
                Magic, Green, Heart, Arrow5, Magic, Bomb1, Green, Heart,        // pack 6
                Heart, Fairy, FullMagic, Red, Bomb8, Heart, Red, Arrow10,       // pack 7
                Green, Blue, Red, // from pull trees
                Green, Red, // from prize crab
                Green, // stunned prize
                Red, // saved fish prize
            }.AsEnumerable();

            var prizes = pool.Shuffle(rnd).Cast<byte>();

            /* prize pack drop order */
            (bytes, prizes) = prizes.SplitOff(prizePackItems);
            patches.Add((Snes(0x6FA78), bytes.ToArray()));

            /* tree pull prizes */
            (bytes, prizes) = prizes.SplitOff(treePullItems);
            patches.Add((Snes(0x1DFBD4), bytes.ToArray()));

            /* crab prizes */
            (drop, final, prizes) = prizes;
            patches.Add((Snes(0x6A9C8), new[] { drop }));
            patches.Add((Snes(0x6A9C4), new[] { final }));

            /* stun prize */
            (drop, prizes) = prizes;
            patches.Add((Snes(0x6F993), new[] { drop }));

            /* fish prize */
            (drop, _) = prizes;
            patches.Add((Snes(0x1D82CC), new[] { drop }));

            patches.AddRange(EnemyPrizePackDistribution());

            /* Pack drop chance */
            /* Normal difficulty is 50%. 0 => 100%, 1 => 50%, 3 => 25% */
            const int nrPacks = 7;
            const byte probability = 1;
            patches.Add((Snes(0x6FA62), Repeat(probability, nrPacks).ToArray()));
        }

        IEnumerable<(int, byte[])> EnemyPrizePackDistribution() {
            var (prizePacks, duplicatePacks) = EnemyPrizePacks();

            var n = prizePacks.Sum(x => x.bytes.Length);
            var randomization = PrizePackRandomization(n, 1);

            var patches = prizePacks.Select(x => {
                IEnumerable<byte> packs;
                (packs, randomization) = randomization.SplitOff(x.bytes.Length);
                return (x.offset, bytes: x.bytes.Zip(packs, (b, p) => (byte)(b | p)).ToArray());
            }).ToList();

            var duplicates =
                from d in duplicatePacks
                from p in patches
                where p.offset == d.src
                select (d.dest, p.bytes);
            patches.AddRange(duplicates.ToList());

            return patches.Select(x => (Snes(x.offset), x.bytes));
        }

        /* Guarantees at least s of each prize pack, over a total of n packs.
         * In each iteration, from the product n * m, use the guaranteed number
         * at k, where k is the "row" (integer division by m), when k falls
         * within the list boundary. Otherwise use the "column" (modulo by m)
         * as the random element.
         */
        IEnumerable<byte> PrizePackRandomization(int n, int s) {
            const int m = 7;
            var g = Repeat(Range(0, m), s).SelectMany(x => x).ToList();

            IEnumerable<int> randomization(int n) {
                n = m * n;
                while (n > 0) {
                    var r = rnd.Next(n);
                    var k = r / m;
                    yield return k < g.Count ? g[k] : r % m;
                    if (k < g.Count) g.RemoveAt(k);
                    n -= m;
                }
            }

            return randomization(n).Select(x => (byte)(x + 1)).ToList();
        }

        /* Todo: Deadrock turns into $8F Blob when powdered, but those "onion blobs" always drop prize pack 1. */
        (IList<(int offset, byte[] bytes)>, IList<(int src, int dest)>) EnemyPrizePacks() {
            const int offset = 0xDB632;
            var patches = new[] {
                /* sprite_prep */
                (0x6888D, new byte[] { 0x00 }), // Keese DW
                (0x688A8, new byte[] { 0x00 }), // Rope
                (0x68967, new byte[] { 0x00, 0x00 }), // Crow/Dacto
                (0x69125, new byte[] { 0x00, 0x00 }), // Red/Blue Hardhat Bettle
                /* sprite properties */
                (offset+0x01, new byte[] { 0x90 }), // Vulture
                (offset+0x08, new byte[] { 0x00 }), // Octorok (One Way)
                (offset+0x0A, new byte[] { 0x00 }), // Octorok (Four Way)
                (offset+0x0D, new byte[] { 0x80, 0x90 }), // Buzzblob, Snapdragon
                (offset+0x11, new byte[] { 0x90, 0x90, 0x00 }), // Hinox, Moblin, Mini Helmasaur
                (offset+0x18, new byte[] { 0x90, 0x90 }), // Mini Moldorm, Poe/Hyu
                (offset+0x20, new byte[] { 0x00 }), // Sluggula
                (offset+0x22, new byte[] { 0x80, 0x00, 0x00 }), // Ropa, Red Bari, Blue Bari
                // Blue Soldier/Tarus, Green Soldier, Red Spear Soldier
                // Blue Assault Soldier, Red Assault Spear Soldier/Tarus
                // Blue Archer, Green Archer
                // Red Javelin Soldier, Red Bush Javelin Soldier
                // Red Bomb Soldiers, Green Soldier Recruits,
                // Geldman, Toppo
                (offset+0x41, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x10, 0x90, 0x90, 0x80 }),
                (offset+0x4F, new byte[] { 0x80 }), // Popo 2
                (offset+0x51, new byte[] { 0x80 }), // Armos
                (offset+0x55, new byte[] { 0x00, 0x00 }), // Ku, Zora
                (offset+0x58, new byte[] { 0x90 }), // Crab
                (offset+0x64, new byte[] { 0x80 }), // Devalant (Shooter)
                (offset+0x6A, new byte[] { 0x90, 0x90 }), // Ball N' Chain Trooper, Cannon Soldier
                (offset+0x6D, new byte[] { 0x80, 0x80 }), // Rat/Buzz, (Stal)Rope
                (offset+0x71, new byte[] { 0x80 }), // Leever
                (offset+0x7C, new byte[] { 0x90 }), // Initially Floating Stal
                (offset+0x81, new byte[] { 0xC0 }), // Hover
                // Green Eyegore/Mimic, Red Eyegore/Mimic
                // Detached Stalfos Body, Kodongo
                (offset+0x83, new byte[] { 0x10, 0x10, 0x10, 0x00 }),
                (offset+0x8B, new byte[] { 0x10 }), // Gibdo
                (offset+0x8E, new byte[] { 0x00, 0x00 }), // Terrorpin, Blob
                (offset+0x91, new byte[] { 0x10 }), // Stalfos Knight
                (offset+0x99, new byte[] { 0x10 }), // Pengator
                (offset+0x9B, new byte[] { 0x10 }), // Wizzrobe
                // Blue Zazak, Red Zazak, Stalfos
                // Green Zirro, Blue Zirro, Pikit
                (offset+0xA5, new byte[] { 0x10, 0x10, 0x10, 0x80, 0x80, 0x80 }),
                (offset+0xC7, new byte[] { 0x10 }), // Hokku-Bokku
                (offset+0xC9, new byte[] { 0x10 }), // Tektite
                (offset+0xD0, new byte[] { 0x10 }), // Lynel
                (offset+0xD3, new byte[] { 0x00 }), // Stal
            };
            var duplicates = new[] {
                /* Popo2 -> Popo. Popo is not used in vanilla Z3, but we duplicate from Popo2 just to be sure */
                (offset + 0x4F, offset + 0x4E),
            };
            return (patches, duplicates);
        }

        void WriteTexts(Config config) {
            var regions = myWorld.Regions.OfType<IReward>();
            var greenPendantDungeon = regions.Where(x => x.Reward == PendantGreen).Cast<Region>().First();
            var redCrystalDungeons = regions.Where(x => x.Reward == CrystalRed).Cast<Region>();

            var sahasrahla = Texts.SahasrahlaReveal(greenPendantDungeon);
            stringTable.SetSahasrahlaRevealText(sahasrahla);

            var bombShop = Texts.BombShopReveal(redCrystalDungeons);
            stringTable.SetBombShopRevealText(bombShop);

            var blind = Texts.Blind(rnd);
            stringTable.SetBlindText(blind);

            var tavernMan = Texts.TavernMan(rnd);
            stringTable.SetTavernManText(tavernMan);

            var ganon = Texts.GanonFirstPhase(rnd);
            stringTable.SetGanonFirstPhaseText(ganon);

            var silversLocation = allWorlds.SelectMany(world => world.Locations).Where(l => l.ItemIs(SilverArrows, myWorld)).First();
            var silvers = config.MultiWorld ?
                Texts.GanonThirdPhaseMulti(silversLocation.Region, myWorld) :
                Texts.GanonThirdPhaseSingle(silversLocation.Region);           
            stringTable.SetGanonThirdPhaseText(silvers);

            var triforceRoom = Texts.TriforceRoom(rnd);            
            stringTable.SetTriforceRoomText(triforceRoom);
        }

        void WriteStringTable() {
            // Todo: v12, base table in asm, use move instructions in seed patch
            patches.Add((Snes(0x1C8000), stringTable.GetPaddedBytes()));
        }

        void WritePlayerNames() {
            patches.AddRange(allWorlds.Select(world => (0x385000 + (world.Id * 16), PlayerNameBytes(world.Player))));
        }

        byte[] PlayerNameBytes(string name) {
            name = name.Length > 12 ? name[..12].TrimEnd() : name;

            const int width = 12;
            var pad = (width - name.Length) / 2;
            name = name.PadLeft(name.Length + pad);
            name = name.PadRight(width);

            return AsAscii(name).Concat(UintBytes(0)).ToArray();
        }

        void WriteSeedData() {
            var configField1 =
                ((myWorld.Config.Race ? 1 : 0) << 15) |
                ((myWorld.Config.Keysanity ? 1 : 0) << 13) |
                ((myWorld.Config.MultiWorld ? 1 : 0) << 12) |
                ((int)myWorld.Config.Z3Logic << 10) |
                ((int)myWorld.Config.SMLogic << 8) |
                (Randomizer.version.Major << 4) |
                (Randomizer.version.Minor << 0);

            var configField2 =
                ((int)myWorld.Config.SwordLocation << 14) |
                ((int)myWorld.Config.MorphLocation << 12) |
                ((int)myWorld.Config.Goal << 8);

            patches.Add((Snes(0x80FF50), UshortBytes(myWorld.Id)));
            patches.Add((Snes(0x80FF52), UshortBytes(configField1)));
            patches.Add((Snes(0x80FF54), UintBytes(seed)));
            patches.Add((Snes(0x80FF58), UshortBytes(configField2)));
            /* Reserve the rest of the space for future use */
            patches.Add((Snes(0x80FF5A), Repeat<byte>(0x00, 6).ToArray()));
            patches.Add((Snes(0x80FF60), AsAscii(seedGuid)));
            patches.Add((Snes(0x80FF80), AsAscii(myWorld.Guid)));
        }

        void WriteCommonFlags() {
            /* Common Combo Configuration flags at [asm]/config.asm */
            if (myWorld.Config.MultiWorld) {
                patches.Add((Snes(0xF47000), UshortBytes(0x0001)));
            }
            if (myWorld.Config.Keysanity) {
                patches.Add((Snes(0xF47006), UshortBytes(0x0001)));
            }
        }

        void WriteGameTitle() {
            var z3Glitch = myWorld.Config.Z3Logic switch {
                Z3Logic.Nmg => "N",
                Z3Logic.Owg => "O",
                _ => "C",
            };
            var smGlitch = myWorld.Config.SMLogic switch {
                SMLogic.Normal => "N",
                SMLogic.Hard => "H",
                _ => "X",
            };
            var title = AsAscii($"ZSM{Randomizer.version}{z3Glitch}{smGlitch}{seed:X8}".PadRight(21)[..21]);
            patches.Add((Snes(0x00FFC0), title));
            patches.Add((Snes(0x80FFC0), title));
        }

        void WriteZ3KeysanityFlags() {
            if (myWorld.Config.Keysanity) {
                patches.Add((Snes(0x40003B), new byte[] { 1 })); // MapMode #$00 = Always On (default) - #$01 = Require Map Item
                patches.Add((Snes(0x400045), new byte[] { 0x0f })); // display ----dcba a: Small Keys, b: Big Key, c: Map, d: Compass
                patches.Add((Snes(0x40016A), new byte[] { 1 })); // FreeItemText: db #$01 ; #00 = Off (default) - #$01 = On
            }
        }

        void WriteSMKeyCardDoors() {
            ushort plaquePlm = 0xd410;
            int plmTablePos = 0xf800;

            if (myWorld.Config.Keysanity) {
                var doorList = new List<ushort[]> {
                                // RoomId  Door Facing                yyxx  Keycard Event Type                   Plaque type               yyxx, Address (if 0 a dynamic PLM is created)
                    // Crateria
                    new ushort[] { 0x91F8, KeycardDoors.Right,      0x2601, KeycardEvents.CrateriaLevel1,        KeycardPlaque.Level1,   0x2400, 0x0000 },  // Crateria - Landing Site - Door to gauntlet
                    new ushort[] { 0x91F8, KeycardDoors.Left,       0x168E, KeycardEvents.CrateriaLevel1,        KeycardPlaque.Level1,   0x148F, 0x801E },  // Crateria - Landing Site - Door to landing site PB
                    new ushort[] { 0x948C, KeycardDoors.Left,       0x062E, KeycardEvents.CrateriaLevel2,        KeycardPlaque.Level2,   0x042F, 0x8222 },  // Crateria - Before Moat - Door to moat (overwrite PB door)
                    new ushort[] { 0x99BD, KeycardDoors.Left,       0x660E, KeycardEvents.CrateriaBoss,          KeycardPlaque.Boss,     0x640F, 0x8470 },  // Crateria - Before G4 - Door to G4
                    new ushort[] { 0x9879, KeycardDoors.Left,       0x062E, KeycardEvents.CrateriaBoss,          KeycardPlaque.Boss,     0x042F, 0x8420 },  // Crateria - Before BT - Door to Bomb Torizo
                
                    // Brinstar
                    new ushort[] { 0x9F11, KeycardDoors.Left,       0x060E, KeycardEvents.BrinstarLevel1,        KeycardPlaque.Level1,   0x040F, 0x8784 },  // Brinstar - Blue Brinstar - Door to ceiling e-tank room

                    new ushort[] { 0x9AD9, KeycardDoors.Right,      0xA601, KeycardEvents.BrinstarLevel2,        KeycardPlaque.Level2,   0xA400, 0x0000 },  // Brinstar - Green Brinstar - Door to etecoon area                
                    new ushort[] { 0x9D9C, KeycardDoors.Down,       0x0336, KeycardEvents.BrinstarBoss,          KeycardPlaque.Boss,     0x0234, 0x863A },  // Brinstar - Pink Brinstar - Door to spore spawn                
                    new ushort[] { 0xA130, KeycardDoors.Left,       0x161E, KeycardEvents.BrinstarLevel2,        KeycardPlaque.Level2,   0x141F, 0x881C },  // Brinstar - Pink Brinstar - Door to wave gate e-tank
                    new ushort[] { 0xA0A4, KeycardDoors.Left,       0x062E, KeycardEvents.BrinstarLevel2,        KeycardPlaque.Level2,   0x042F, 0x0000 },  // Brinstar - Pink Brinstar - Door to spore spawn super

                    new ushort[] { 0xA56B, KeycardDoors.Left,       0x161E, KeycardEvents.BrinstarBoss,          KeycardPlaque.Boss,     0x141F, 0x8A1A },  // Brinstar - Before Kraid - Door to Kraid

                    // Upper Norfair
                    new ushort[] { 0xA7DE, KeycardDoors.Right,      0x3601, KeycardEvents.NorfairLevel1,         KeycardPlaque.Level1,   0x3400, 0x8B00 },  // Norfair - Business Centre - Door towards Ice
                    new ushort[] { 0xA923, KeycardDoors.Right,      0x0601, KeycardEvents.NorfairLevel1,         KeycardPlaque.Level1,   0x0400, 0x0000 },  // Norfair - Pre-Crocomire - Door towards Ice

                    new ushort[] { 0xA788, KeycardDoors.Left,       0x162E, KeycardEvents.NorfairLevel2,         KeycardPlaque.Level2,   0x142F, 0x8AEA },  // Norfair - Lava Missile Room - Door towards Bubble Mountain
                    new ushort[] { 0xAF72, KeycardDoors.Left,       0x061E, KeycardEvents.NorfairLevel2,         KeycardPlaque.Level2,   0x041F, 0x0000 },  // Norfair - After frog speedway - Door to Bubble Mountain
                    new ushort[] { 0xAEDF, KeycardDoors.Down,       0x0206, KeycardEvents.NorfairLevel2,         KeycardPlaque.Level2,   0x0204, 0x0000 },  // Norfair - Below bubble mountain - Door to Bubble Mountain
                    new ushort[] { 0xAD5E, KeycardDoors.Right,      0x0601, KeycardEvents.NorfairLevel2,         KeycardPlaque.Level2,   0x0400, 0x0000 },  // Norfair - LN Escape - Door to Bubble Mountain
                
                    new ushort[] { 0xA923, KeycardDoors.Up,         0x2DC6, KeycardEvents.NorfairBoss,           KeycardPlaque.Boss,     0x2EC4, 0x8B96 },  // Norfair - Pre-Crocomire - Door to Crocomire

                    // Lower Norfair
                    new ushort[] { 0xB4AD, KeycardDoors.Left,       0x160E, KeycardEvents.LowerNorfairLevel1,    KeycardPlaque.Level1,   0x140F, 0x0000 },  // Lower Norfair - WRITG - Door to Amphitheatre
                    new ushort[] { 0xAD5E, KeycardDoors.Left,       0x065E, KeycardEvents.LowerNorfairLevel1,    KeycardPlaque.Level1,   0x045F, 0x0000 },  // Lower Norfair - Exit - Door to "Reverse LN Entry"
                    new ushort[] { 0xB37A, KeycardDoors.Right,      0x0601, KeycardEvents.LowerNorfairBoss,      KeycardPlaque.Boss,     0x0400, 0x8EA6 },  // Lower Norfair - Pre-Ridley - Door to Ridley

                    // Maridia
                    new ushort[] { 0xD0B9, KeycardDoors.Left,       0x065E, KeycardEvents.MaridiaLevel1,         KeycardPlaque.Level1,   0x045F, 0x0000 },  // Maridia - Mt. Everest - Door to Pink Maridia
                    new ushort[] { 0xD5A7, KeycardDoors.Right,      0x1601, KeycardEvents.MaridiaLevel1,         KeycardPlaque.Level1,   0x1400, 0x0000 },  // Maridia - Aqueduct - Door towards Beach

                    new ushort[] { 0xD617, KeycardDoors.Left,       0x063E, KeycardEvents.MaridiaLevel2,         KeycardPlaque.Level2,   0x043F, 0x0000 },  // Maridia - Pre-Botwoon - Door to Botwoon
                    new ushort[] { 0xD913, KeycardDoors.Right,      0x2601, KeycardEvents.MaridiaLevel2,         KeycardPlaque.Level2,   0x2400, 0x0000 },  // Maridia - Pre-Colloseum - Door to post-botwoon

                    new ushort[] { 0xD78F, KeycardDoors.Right,      0x2601, KeycardEvents.MaridiaBoss,           KeycardPlaque.Boss,     0x2400, 0xC73B },  // Maridia - Precious Room - Door to Draygon

                    new ushort[] { 0xDA2B, KeycardDoors.BossLeft,   0x164E, 0x00f0, /* Door id 0xf0 */           KeycardPlaque.None,     0x144F, 0x0000 },  // Maridia - Change Cac Alley Door to Boss Door (prevents key breaking)

                    // Wrecked Ship
                    new ushort[] { 0x93FE, KeycardDoors.Left,       0x167E, KeycardEvents.WreckedShipLevel1,     KeycardPlaque.Level1,   0x147F, 0x0000 },  // Wrecked Ship - Outside Wrecked Ship West - Door to Reserve Tank Check
                    new ushort[] { 0x968F, KeycardDoors.Left,       0x060E, KeycardEvents.WreckedShipLevel1,     KeycardPlaque.Level1,   0x040F, 0x0000 },  // Wrecked Ship - Outside Wrecked Ship West - Door to Bowling Alley
                    new ushort[] { 0xCE40, KeycardDoors.Left,       0x060E, KeycardEvents.WreckedShipLevel1,     KeycardPlaque.Level1,   0x040F, 0x0000 },  // Wrecked Ship - Gravity Suit - Door to Bowling Alley

                    new ushort[] { 0xCC6F, KeycardDoors.Left,       0x064E, KeycardEvents.WreckedShipBoss,       KeycardPlaque.Boss,     0x044F, 0xC29D },  // Wrecked Ship - Pre-Phantoon - Door to Phantoon
                
                };

                ushort doorId = 0x0000;

                foreach (var door in doorList) {
                    
                    /* When "Fast Ganon" is set, don't place the G4 Boss key door to enable faster games */
                    if (door[0] == 0x99BD && myWorld.Config.Goal == Goal.FastGanonDefeatMotherBrain) {
                        continue;
                    }

                    var doorArgs = door[4] != KeycardPlaque.None ? doorId | door[3] : door[3];
                    if (door[6] == 0) {
                        // Write dynamic door
                        var doorData = door[0..3].SelectMany(x => UshortBytes(x)).Concat(UshortBytes(doorArgs)).ToArray();
                        patches.Add((Snes(0x8f0000 + plmTablePos), doorData));
                        plmTablePos += 0x08;
                    }
                    else {
                        // Overwrite existing door
                        var doorData = door[1..3].SelectMany(x => UshortBytes(x)).Concat(UshortBytes(doorArgs)).ToArray();
                        patches.Add((Snes(0x8f0000 + door[6]), doorData));
                        if ((door[3] == KeycardEvents.BrinstarBoss && door[0] != 0x9D9C) || door[3] == KeycardEvents.LowerNorfairBoss || door[3] == KeycardEvents.MaridiaBoss || door[3] == KeycardEvents.WreckedShipBoss) {
                            // Overwrite the extra parts of the Gadora with a PLM that just deletes itself
                            patches.Add((Snes(0x8f0000 + door[6] + 0x06), new byte[] { 0x2F, 0xB6, 0x00, 0x00, 0x00, 0x00, 0x2F, 0xB6, 0x00, 0x00, 0x00, 0x00 }));
                        }
                    }

                    // Plaque data
                    if (door[4] != KeycardPlaque.None) {
                        var plaqueData = UshortBytes(door[0]).Concat(UshortBytes(plaquePlm)).Concat(UshortBytes(door[5])).Concat(UshortBytes(door[4])).ToArray();
                        patches.Add((Snes(0x8f0000 + plmTablePos), plaqueData));
                        plmTablePos += 0x08;
                    }
                    doorId += 1;
                }
            }

            /* Write plaque showing SM bosses that needs to be killed */
            if (myWorld.Config.OpenTourian != OpenTourian.FourBosses) {
                var plaqueData = UshortBytes(0xA5ED).Concat(UshortBytes(plaquePlm)).Concat(UshortBytes(0x044F)).Concat(UshortBytes(KeycardPlaque.Zero + myWorld.OpenTourian)).ToArray();
                patches.Add((Snes(0x8f0000 + plmTablePos), plaqueData));
                plmTablePos += 0x08;
            }

            patches.Add((Snes(0x8f0000 + plmTablePos), new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }));
        }

        void WriteDiggingGameRng() {
            byte digs = (byte)(rnd.Next(30) + 1);
            patches.Add((Snes(0x308020), new byte[] { digs }));
            patches.Add((Snes(0x1DFD95), new byte[] { digs }));
        }

        // Removes Sword/Shield from Uncle by moving the tiles for
        // sword/shield to his head and replaces them with his head.
        void WriteRemoveEquipmentFromUncle(Item item) {
            if (item.Type != ProgressiveSword) {
                patches.AddRange(new[] {
                    (Snes(0xDD263), new byte[] { 0x00, 0x00, 0xF6, 0xFF, 0x00, 0x0E }),
                    (Snes(0xDD26B), new byte[] { 0x00, 0x00, 0xF6, 0xFF, 0x00, 0x0E }),
                    (Snes(0xDD293), new byte[] { 0x00, 0x00, 0xF6, 0xFF, 0x00, 0x0E }),
                    (Snes(0xDD29B), new byte[] { 0x00, 0x00, 0xF7, 0xFF, 0x00, 0x0E }),
                    (Snes(0xDD2B3), new byte[] { 0x00, 0x00, 0xF6, 0xFF, 0x02, 0x0E }),
                    (Snes(0xDD2BB), new byte[] { 0x00, 0x00, 0xF6, 0xFF, 0x02, 0x0E }),
                    (Snes(0xDD2E3), new byte[] { 0x00, 0x00, 0xF7, 0xFF, 0x02, 0x0E }),
                    (Snes(0xDD2EB), new byte[] { 0x00, 0x00, 0xF7, 0xFF, 0x02, 0x0E }),
                    (Snes(0xDD31B), new byte[] { 0x00, 0x00, 0xE4, 0xFF, 0x08, 0x0E }),
                    (Snes(0xDD323), new byte[] { 0x00, 0x00, 0xE4, 0xFF, 0x08, 0x0E }),
                });
            }
            if (item.Type != ProgressiveShield) {
                patches.AddRange(new[] {
                    (Snes(0xDD253), new byte[] { 0x00, 0x00, 0xF6, 0xFF, 0x00, 0x0E }),
                    (Snes(0xDD25B), new byte[] { 0x00, 0x00, 0xF6, 0xFF, 0x00, 0x0E }),
                    (Snes(0xDD283), new byte[] { 0x00, 0x00, 0xF6, 0xFF, 0x00, 0x0E }),
                    (Snes(0xDD28B), new byte[] { 0x00, 0x00, 0xF7, 0xFF, 0x00, 0x0E }),
                    (Snes(0xDD2CB), new byte[] { 0x00, 0x00, 0xF6, 0xFF, 0x02, 0x0E }),
                    (Snes(0xDD2FB), new byte[] { 0x00, 0x00, 0xF7, 0xFF, 0x02, 0x0E }),
                    (Snes(0xDD313), new byte[] { 0x00, 0x00, 0xE4, 0xFF, 0x08, 0x0E }),
                });
            }
        }

        void WritePreOpenPyramid(Goal goal) {
            if (goal == Goal.FastGanonDefeatMotherBrain) {
                patches.Add((Snes(0x30808B), new byte[] { (byte)1 }));
            }
        }

        void WriteGanonInvicible(Goal goal) {
            /* Defaults to $00 (never) at [asm]/z3/randomizer/tables.asm */
            var value = goal switch {
                Goal.DefeatBoth => 0x03,
                Goal.FastGanonDefeatMotherBrain => 0x00,
                Goal.AllDungeonsDefeatMotherBrain => 0x02,                
                var x => throw new ArgumentException($"Unknown Ganon invincible value {x}", nameof(goal))
            };
            patches.Add((Snes(0x30803E), new byte[] { (byte)value }));
        }

        void WriteBossesNeeded(int numBosses) {
            patches.Add((Snes(0xF47200), UshortBytes(numBosses)));
        }

        void WriteCrystalsNeeded(int openTower, int ganonVulnerable) {
            patches.Add((Snes(0x30805E), new byte[] { (byte)openTower }));
            patches.Add((Snes(0x30805F), new byte[] { (byte)ganonVulnerable }));

            stringTable.SetTowerRequirementText($"You need {myWorld.OpenTower} crystals to enter Ganon's Tower.");
            stringTable.SetGanonRequirementText($"You need {myWorld.GanonVulnerable} crystals to defeat Ganon.");
        }

        void WriteRngBlock() {
            /* Repoint RNG Block */
            patches.Add((0x420000, Range(0, 1024).Select(x => (byte)rnd.Next(0x100)).ToArray()));
        }

        void WriteSaveAndQuitFromBossRoom() {
            /* Defaults to $00 at [asm]/z3/randomizer/tables.asm */
            patches.Add((Snes(0x308042), new byte[] { 0x01 }));
        }

        void WriteWorldOnAgahnimDeath() {
            /* Defaults to $01 at [asm]/z3/randomizer/tables.asm */
            // Todo: Z3r major glitches disables this, reconsider extending or dropping with glitched logic later.
            //patches.Add((Snes(0x3080A3), new byte[] { 0x01 }));
        }

        int Snes(int addr) {
            addr = addr switch {
                /* Redirect hi bank $30 access into ExHiRom lo bank $40 */
                _ when (addr & 0xFF8000) == 0x308000 => 0x400000 | (addr & 0x7FFF),
                /* General case, add ExHi offset for banks < $80, and collapse mirroring */
                _ => (addr < 0x800000 ? 0x400000 : 0) | (addr & 0x3FFFFF),
            };
            if (addr > 0x600000)
                throw new InvalidOperationException($"Unmapped pc address target ${addr:X}");
            return addr;
        }

        byte[] UintBytes(int value) => BitConverter.GetBytes((uint)value);

        byte[] UshortBytes(int value) => BitConverter.GetBytes((ushort)value);

        byte[] AsAscii(string text) => Encoding.ASCII.GetBytes(text);

    }

}
