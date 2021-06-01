#region License (GPL v3)
/*
    SAM Targeting - Make SamSites target players, NPCs, and animals
    Copyright (c) 2021 RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License (GPL v3)
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SAM Targeting", "RFC1920", "1.0.1")]
    [Description("Make SAMSites target other things")]
    internal class SAMTargeting : RustPlugin
    {
        private ConfigData configData;
        public static SAMTargeting Instance;
        private bool enabled = false;

        [PluginReference]
        private readonly Plugin Friends, Clans, RustIO, Vanish;

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["enabled"] = "Animal targeting enabled for this autosamsite",
                ["disabled"] = "Animal targeting disabled for this autosamsite"

            }, this);
        }
        #endregion

        private bool IsFriend(ulong playerid, ulong ownerid)
        {
            if (!configData.HonorRelationships) return false;

            if (configData.useFriends && Friends != null)
            {
                var fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if (fr != null && (bool)fr)
                {
                    return true;
                }
            }
            if (configData.useClans && Clans != null)
            {
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan = (string)Clans?.CallHook("GetClanOf", ownerid);
                if (playerclan != null && ownerclan != null)
                {
                    if (playerclan == ownerclan)
                    {
                        return true;
                    }
                }
            }
            if (configData.useTeams)
            {
                BasePlayer player = BasePlayer.FindByID(playerid);
                if (player != null)
                {
                    if (player.currentTeam != 0)
                    {
                        RelationshipManager.PlayerTeam playerTeam = RelationshipManager.Instance.FindTeam(player.currentTeam);
                        if (playerTeam != null)
                        {
                            if (playerTeam.members.Contains(ownerid))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        void OnServerInitialized()
        {
            Instance = this;
            LoadConfigVariables();
            enabled = true;
            var samsites = UnityEngine.Object.FindObjectsOfType<SamSite>();
            foreach(var t in samsites)
            {
                t.gameObject.AddComponent<SamTargeting>();
            }
        }

        void Unload()
        {
            var samsites = UnityEngine.Object.FindObjectsOfType<SamSite>();
            foreach(var t in samsites)
            {
                if (t != null)
                {
                    var at = t.gameObject.GetComponent<SamTargeting>();
                    if (at != null) UnityEngine.Object.Destroy(at);
                }
            }
        }

        private object CheckSamTargeting(string target, bool npc = false)
        {
            switch (target)
            {
                case "player":
                    if (npc)
                    {
                        if (configData.NPCTargetPlayers) return true;
                        return false;
                    }
                    if (configData.TargetPlayers) return true;
                    return false;
                case "npc":
                    if (npc)
                    {
                        if (configData.NPCTargetNPCs) return true;
                        return false;
                    }
                    if (configData.TargetNPCs) return true;
                    return false;
                case "animal":
                    if (npc)
                    {
                        if (configData.NPCTargetAnimals) return true;
                        return false;
                    }
                    if (configData.TargetAnimals) return true;
                    return false;
                default:
                    break;
            }
            return null;
        }

        private object SetSamTargeting(string target, bool enabled, bool npc = false)
        {
            Puts($"Plugin called SetSamTargeting for {target} {enabled.ToString()}, NPC: {npc.ToString()}");
            switch (target)
            {
                case "player":
                    if (npc)
                    {
                        configData.NPCTargetPlayers = enabled;
                        if (configData.NPCTargetPlayers) return true;
                        return false;
                    }
                    configData.TargetPlayers = enabled;
                    if (configData.TargetPlayers) return true;
                    return false;
                case "npc":
                    if (npc)
                    {
                        configData.NPCTargetNPCs = enabled;
                        if (configData.NPCTargetNPCs) return true;
                        return false;
                    }
                    configData.TargetNPCs = enabled;
                    if (configData.TargetNPCs) return true;
                    return false;
                case "animal":
                    if (npc)
                    {
                        configData.NPCTargetAnimals = enabled;
                        if (configData.NPCTargetAnimals) return true;
                        return false;
                    }
                    configData.TargetAnimals = enabled;
                    if (configData.TargetAnimals) return true;
                    return false;
                default:
                    break;
            }
            SaveConfig();
            return null;
        }

        void OnEntitySpawned(SamSite samsite)
        {
            if (!enabled) return;
            if (samsite.OwnerID == 0) return;
            var player = BasePlayer.Find(samsite.OwnerID.ToString());

            samsite.gameObject.AddComponent<SamTargeting>();
            Message(player.IPlayer, "enabled");
        }

        class SamTargeting : MonoBehaviour
        {
            private SamSite samsite;

            private void Awake()
            {
                samsite = GetComponent<SamSite>();
                if (samsite != null) InvokeRepeating("FindTargets", 5f, 1.0f);
            }

            internal void FindTargets()
            {
                bool found = false;
                if (samsite.currentTarget == null && samsite.IsPowered())
                {
                    if (Instance.configData.TargetPlayers || (samsite.OwnerID == 0 && Instance.configData.NPCTargetPlayers))
                    {
                        List<BasePlayer> localpeep = new List<BasePlayer>();
                        Vis.Entities(samsite.transform.position, Instance.configData.range, localpeep);

                        foreach (BaseCombatEntity bce in localpeep)
                        {
                            if (string.IsNullOrEmpty(bce.ShortPrefabName)) continue;
                            if (Instance.Vanish.Call<bool>("IsInvisible", bce as BasePlayer)) continue;
                            if (samsite.IsVisibleAndCanSee(bce.transform.position))
                            {
                                samsite.currentTarget = bce;
                                found = true;
                                break;
                            }
                        }
                    }

                    if (found) return;

                    if (Instance.configData.TargetNPCs || (samsite.OwnerID == 0 && Instance.configData.NPCTargetNPCs))
                    {
                        List<NPCPlayerApex> localnpc = new List<NPCPlayerApex>();
                        Vis.Entities(samsite.transform.position, Instance.configData.range, localnpc);

                        foreach (BaseCombatEntity bce in localnpc)
                        {
                            if (string.IsNullOrEmpty(bce.ShortPrefabName)) continue;
                            if (samsite.IsVisibleAndCanSee(bce.transform.position))
                            {
                                samsite.currentTarget = bce;
                                found = true;
                                break;
                            }
                        }
                    }

                    if (found) return;

                    if (Instance.configData.TargetAnimals || (samsite.OwnerID == 0 && Instance.configData.NPCTargetAnimals))
                    {
                        List<BaseAnimalNPC> localpig = new List<BaseAnimalNPC>();
                        Vis.Entities(samsite.transform.position, Instance.configData.range, localpig);

                        foreach (BaseCombatEntity bce in localpig)
                        {
                            if (string.IsNullOrEmpty(bce.ShortPrefabName)) continue;
                            if (samsite.IsVisibleAndCanSee(bce.transform.position) && !Instance.configData.exclusions.Contains(bce.ShortPrefabName))
                            {
                                samsite.currentTarget = bce;
                                break;
                            }
                        }
                    }
                }
            }
        }

        #region config
        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            configData.Version = Version;
            SaveConfig(configData);
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            var config = new ConfigData
            {
                TargetPlayers = true,
                TargetNPCs = true,
                TargetAnimals = true,
                exclusions = new List<string>() { "chicken" },
                range = 150f,
                Version = Version
            };
            SaveConfig(config);
        }
        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Player targeting by SamSite")]
            public bool TargetPlayers = false;

            [JsonProperty(PropertyName = "NPC targeting by SamSite")]
            public bool TargetNPCs = false;

            [JsonProperty(PropertyName = "Animal targeting by SamSite")]
            public bool TargetAnimals = false;

            [JsonProperty(PropertyName = "Player targeting by NPC SamSite")]
            public bool NPCTargetPlayers = false;

            [JsonProperty(PropertyName = "NPC targeting by NPC SamSite")]
            public bool NPCTargetNPCs = false;

            [JsonProperty(PropertyName = "Animal targeting by NPC SamSite")]
            public bool NPCTargetAnimals = false;

            [JsonProperty(PropertyName = "Animals to exclude")]
            public List<string> exclusions;

            [JsonProperty(PropertyName = "Honor Friends/Clans/Teams for commands")]
            public bool HonorRelationships = false;

            [JsonProperty(PropertyName = "Use Friends plugins for commands")]
            public bool useFriends = false;

            [JsonProperty(PropertyName = "Use Clans plugins for commands")]
            public bool useClans = false;

            [JsonProperty(PropertyName = "Use Rust teams for commands")]
            public bool useTeams = false;

            [JsonProperty(PropertyName = "SamSite Range")]
            public float range;

            public VersionNumber Version;
        }
        #endregion
    }
}
