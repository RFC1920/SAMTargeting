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
    [Info("SAM Targeting", "RFC1920", "1.0.4")]
    [Description("Make SAMSites target other things")]
    internal class SAMTargeting : RustPlugin
    {
        private ConfigData configData;
        public static SAMTargeting Instance;
        private bool enabled;

        [PluginReference]
        private readonly Plugin Vanish;

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

        private void OnServerInitialized()
        {
            Instance = this;
            LoadConfigVariables();
            enabled = true;
            foreach(SamSite t in UnityEngine.Object.FindObjectsOfType<SamSite>())
            {
                t.gameObject.AddComponent<SamTargeting>();
            }
        }

        private void Unload()
        {
            foreach(SamSite t in UnityEngine.Object.FindObjectsOfType<SamSite>())
            {
                if (t != null)
                {
                    SamTargeting at = t.gameObject.GetComponent<SamTargeting>();
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
                        return configData.NPCTargetPlayers;
                    }
                    return configData.TargetPlayers;
                case "npc":
                    if (npc)
                    {
                        return configData.NPCTargetNPCs;
                    }
                    return configData.TargetNPCs;
                case "animal":
                    if (npc)
                    {
                        return configData.NPCTargetAnimals;
                    }
                    return configData.TargetAnimals;
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
                        return configData.NPCTargetPlayers;
                    }
                    configData.TargetPlayers = enabled;
                    return configData.TargetPlayers;
                case "npc":
                    if (npc)
                    {
                        configData.NPCTargetNPCs = enabled;
                        return configData.NPCTargetNPCs;
                    }
                    configData.TargetNPCs = enabled;
                    return configData.TargetNPCs;
                case "animal":
                    if (npc)
                    {
                        configData.NPCTargetAnimals = enabled;
                        return configData.NPCTargetAnimals;
                    }
                    configData.TargetAnimals = enabled;
                    return configData.TargetAnimals;
            }
            SaveConfig();
            return null;
        }

        private void OnEntitySpawned(SamSite samsite)
        {
            if (!enabled) return;
            if (samsite.OwnerID == 0) return;
            BasePlayer player = BasePlayer.Find(samsite.OwnerID.ToString());

            samsite.gameObject.AddComponent<SamTargeting>();
            Message(player.IPlayer, "enabled");
        }

        private class SamTargeting : MonoBehaviour
        {
            private SamSite samsite;

            private void Awake()
            {
                samsite = GetComponent<SamSite>();
                if (samsite != null)
                {
                    InvokeRepeating("FindTargets", 5f, 3f);
                }
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
                                samsite.currentTarget = bce as SamSite.ISamSiteTarget;
                                found = true;
                                break;
                            }
                        }
                    }

                    if (found) return;

                    if (Instance.configData.TargetNPCs || (samsite.OwnerID == 0 && Instance.configData.NPCTargetNPCs))
                    {
                        List<NPCPlayer> localnpc = new List<NPCPlayer>();
                        List<global::HumanNPC> localhnpc = new List<global::HumanNPC>();

                        Vis.Entities(samsite.transform.position, Instance.configData.range, localnpc);
                        foreach (BaseCombatEntity bce in localnpc)
                        {
                            if (string.IsNullOrEmpty(bce.ShortPrefabName)) continue;
                            if (samsite.IsVisibleAndCanSee(bce.transform.position))
                            {
                                samsite.currentTarget = bce as SamSite.ISamSiteTarget;
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            Vis.Entities(samsite.transform.position, Instance.configData.range, localhnpc);
                            foreach (BaseCombatEntity bce in localhnpc)
                            {
                                if (string.IsNullOrEmpty(bce.ShortPrefabName)) continue;
                                if (samsite.IsVisibleAndCanSee(bce.transform.position))
                                {
                                    samsite.currentTarget = bce as SamSite.ISamSiteTarget;
                                    found = true;
                                    break;
                                }
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
                                samsite.currentTarget = bce as SamSite.ISamSiteTarget;
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
            ConfigData config = new ConfigData
            {
                TargetPlayers = true,
                TargetNPCs = true,
                TargetAnimals = true,
                NPCTargetPlayers = false,
                NPCTargetNPCs = false,
                NPCTargetAnimals = false,
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
            public bool TargetPlayers;

            [JsonProperty(PropertyName = "NPC targeting by SamSite")]
            public bool TargetNPCs;

            [JsonProperty(PropertyName = "Animal targeting by SamSite")]
            public bool TargetAnimals;

            [JsonProperty(PropertyName = "Player targeting by NPC SamSite")]
            public bool NPCTargetPlayers;

            [JsonProperty(PropertyName = "NPC targeting by NPC SamSite")]
            public bool NPCTargetNPCs;

            [JsonProperty(PropertyName = "Animal targeting by NPC SamSite")]
            public bool NPCTargetAnimals;

            [JsonProperty(PropertyName = "Animals to exclude")]
            public List<string> exclusions;

            [JsonProperty(PropertyName = "SamSite Range")]
            public float range;

            public VersionNumber Version;
        }
        #endregion
    }
}
