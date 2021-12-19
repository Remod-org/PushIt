using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PushIt", "RFC1920", "1.0.1")]
    [Description("Rearrange furniture by pushing it into place.")]
    internal class PushIt : RustPlugin
    {
        public Dictionary<ulong, ulong> pushentity = new Dictionary<ulong, ulong>();

        private ConfigData configData;
        public static PushIt Instance;
        public bool debug = true;

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        private void LMessage(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["noentity"] = "Unable to find pushable entity",
                ["startpush"] = "Pushing {0}",
                ["startpull"] = "Pulling {0}",
                ["endpush"] = "No longer pushing {0}",
                ["endpull"] = "No longer pulling {0}",
                ["nopushpull"] = "You cannot interact with a {0}",
                ["obstruction"] = "We hit something.  Backing up...",
                ["notauthorized"] = "You don't have permission to do that !!"
            }, this);
        }
        #endregion

        private void OnServerInitialized()
        {
            LoadConfigVariables();
            AddCovalenceCommand("push", "PushPullCmd");
            AddCovalenceCommand("pull", "PushPullCmd");
            Instance = this;
        }

        private void DoLog(string message)
        {
            if (debug)
            {
                Interface.Oxide.LogInfo(message);
            }
        }

        private void Unload()
        {
            DestroyAll<PushMe>();
        }

        private void DestroyAll<T>() where T : MonoBehaviour
        {
            foreach (T type in UnityEngine.Object.FindObjectsOfType<T>())
            {
                UnityEngine.Object.Destroy(type);
            }
        }

        private void PushPullCmd(IPlayer iplayer, string command, string[] args)
        {
            bool pull = false;
            if (command == "pull") pull = true;

            BasePlayer player = iplayer.Object as BasePlayer;
            BaseEntity ent = FindEntity(player);
            if (ent == null)
            {
                Message(iplayer, "noentity");
                return;
            }

            PushMe haspush = ent.gameObject.GetComponent<PushMe>();
            //string entname = ent.GetType().Name;
            string entname = ent.ShortPrefabName;
            if (haspush != null)
            {
                UnityEngine.Object.Destroy(haspush);
                pushentity.Remove(player.userID);
                if (pull)
                {
                    Message(iplayer, "endpull", entname);
                }
                else
                {
                    Message(iplayer, "endpush", entname);
                }
                return;
            }

            PushMe push = ent.gameObject.AddComponent<PushMe>();
            push.player = player;
            push.pull = pull;
            pushentity.Add(player.userID, ent.net.ID);
            if (pull)
            {
                Message(iplayer, "startpull", entname);
            }
            else
            {
                Message(iplayer, "startpush", entname);
            }
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            //if (input.WasJustPressed(BUTTON.USE))
            if (input.WasJustPressed(BUTTON.SPRINT))
            {
                BaseEntity ent = FindEntity(player);
                PushMe haspush = ent.gameObject.GetComponent<PushMe>();
                if (haspush == null) return;
                DoLog("Pressed use on a push entity");
            }
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null) return null;
            BaseEntity ent = FindEntity(player);
            PushMe haspush = ent.gameObject.GetComponent<PushMe>();
            if (haspush == null) return null;

            return true;
        }

        private BaseEntity FindEntity(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit)) return null;

            string entity_name = hit.GetEntity().ShortPrefabName;
            if (hit.GetEntity() is BuildingBlock && configData.disallowBlocks)
            {
                return null;
            }
            else if ((entity_name.Contains("shutter") || entity_name.Contains("door") || entity_name.Contains("reinforced") ||
                     entity_name.Contains("shopfront") || entity_name.Contains("bars") || entity_name.Contains("hatch") ||
                     entity_name.Contains("garagedoor") || entity_name.Contains("cell") || entity_name.Contains("fence")) && configData.disallowBlocks)
            {
                Message(player.IPlayer, "nointeract", entity_name);
                return null;
            }

            return hit.GetEntity();
        }

        public class PushMe : MonoBehaviour
        {
            private BaseEntity target;
            private StabilityEntity stab;
            public BasePlayer player;
            private readonly static int layerMask = LayerMask.GetMask(new[] { "Construction", "Deployed", "World", "Terrain" });

            public bool pull;
            public float directional;

            private void Awake()
            {
                target = gameObject.GetComponent<BaseEntity>();
                stab = target?.GetComponent<StabilityEntity>();
                Instance.DoLog($"Found entity: {target.ShortPrefabName}");
            }

            private void FixedUpdate()
            {
                if (player == null) return;

                if (player.serverInput.WasJustPressed(BUTTON.USE))
                {
                    directional = pull ? 1 : -1;
                    PushPull();
                }
            }

            public void PushPull()
            {
                Vector3 oldpos = target.transform.position;
                float savey = oldpos.y;
                Instance.DoLog($"Player position: {player.transform.position}");
                Instance.DoLog($"Old position: {target.transform.position}");

                target.transform.position = Vector3.MoveTowards(target.transform.position, player.transform.position, directional * 2f * Time.deltaTime);
                target.transform.position.y.SnapTo(savey);

                Vector3 direction = target.transform.position - player.transform.position;
                if (Physics.Raycast(target.transform.position, direction, 0.45f, layerMask, QueryTriggerInteraction.Collide))
                {
                    Instance.DoLog("I hit something, backing up!");
                    Instance.Message(player.IPlayer, "obstruction");
                    target.transform.position = oldpos;
                }

                if (stab?.grounded == false) stab.grounded = true;
                Instance.DoLog($"New position: {target.transform.position}");

                target.transform.hasChanged = true;
                target.UpdateNetworkGroup();
                target.SendNetworkUpdateImmediate();
            }
        }

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Disallow building blocks, doors, and windows")]
            public bool disallowBlocks;

            public bool debug;
            public VersionNumber Version;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new ConfigData
            {
                disallowBlocks = true,
                debug = false,
                Version = Version
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            configData.Version = Version;
            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
    }
}
