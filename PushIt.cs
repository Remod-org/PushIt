using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PushIt", "RFC1920", "1.0.6")]
    [Description("Rearrange furniture and other items by pushing and pulling them into place.")]
    internal class PushIt : RustPlugin
    {
        private ConfigData configData;
        public static PushIt Instance;
        public bool debug = true;
        private const string permPush = "pushit.use";
        public Dictionary<ulong, ulong> pushentity = new Dictionary<ulong, ulong>();

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));
        private void LMessage(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["noentity"] = "Unable to find pushable entity",
                ["nointeract"] = "Interaction with {0} is disallowed.",
                ["noblocks"] = "Interaction with building blocks is disallowed.",
                ["startpush"] = "Pushing {0}",
                ["endpush"] = "No longer pushing {0}",
                ["rotating"] = "Rotating {0}clockwise",
                ["counter"] = "counter-",
                ["pulling"] = "Pulling from the {0}",
                ["pushing"] = "Pushing from the {0}",
                ["toofar"] = "Target is too far away",
                ["obstruction"] = "We hit {0}.  Backing up...",
                ["notauthorized"] = "You don't have permission to do that !!"
            }, this);
        }
        #endregion

        private void OnServerInitialized()
        {
            LoadConfigVariables();
            AddCovalenceCommand("push", "PushPullCmd");
            permission.RegisterPermission(permPush, this);
            Instance = this;
        }

        private void DoLog(string message)
        {
            if (debug) Interface.Oxide.LogInfo(message);
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
            if (!iplayer.HasPermission(permPush)) { Message(iplayer, "notauthorized"); return; }
            BasePlayer player = iplayer.Object as BasePlayer;
            BaseEntity ent = FindEntity(player);
            if (ent == null)
            {
                Message(iplayer, "noentity");
                return;
            }

            PushMe haspush = ent?.gameObject.GetComponent<PushMe>();
            string entname = ent?.ShortPrefabName;
            if (haspush != null)
            {
                UnityEngine.Object.Destroy(haspush);
                pushentity.Remove(player.userID);
                Message(iplayer, "endpush", entname);
                return;
            }

            PushMe push = ent.gameObject.AddComponent<PushMe>();
            push.player = player;
            pushentity.Add(player.userID, (uint)ent.net.ID.Value);
            Message(iplayer, "startpush", entname);
        }

        private BaseEntity FindEntity(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit)) return null;

            string entity_name = hit.GetEntity().ShortPrefabName;
            if (hit.GetEntity() is BuildingBlock && configData.disallowBlocks)
            {
                Message(player.IPlayer, "noblocks", entity_name);
                return null;
            }
            else if ((entity_name.Contains("shutter") || entity_name.Contains("door") || entity_name.Contains("reinforced") ||
                     entity_name.Contains("shopfront") || entity_name.Contains("bars") || entity_name.Contains("hatch") ||
                     entity_name.Contains("garagedoor") || entity_name.Contains("cell") || entity_name.Contains("fence")) && configData.disallowBlocks)
            {
                Message(player.IPlayer, "nointeract", entity_name);
                return null;
            }
            else if (hit.GetEntity() is BasePlayer && configData.disallowPlayer)
            {
                Message(player.IPlayer, "nointeract", entity_name);
                return null;
            }
            else if ((hit.GetEntity() is NPCPlayer || hit.GetEntity() is global::HumanNPC || hit.GetEntity() is ScientistNPC) && configData.disallowNPC)
            {
                Message(player.IPlayer, "nointeract", entity_name);
                return null;
            }
            else if (entity_name.Contains("vehicle") && configData.disallowOthers)
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

            private bool atleft;
            private bool atright;

            private float height;
            private float depth;
            private float width;

            private Vector3 left;
            private Vector3 right;
            private Vector3 front;
            private Vector3 back;

            private Vector3 direction;
            private Vector3 checkpos;
            private float checkDistance;
            private string obstring;
            private string pushtring;

            private void Awake()
            {
                target = gameObject.GetComponent<BaseEntity>();
                height = target.bounds.size.y;
                depth = target.bounds.size.z;
                width = target.bounds.size.x;
            }

            private void OnDestroy()
            {
            }

            private void OnTriggerEnter(Collider col)
            {
                Instance.DoLog($"Trigger Enter: {col.gameObject.name}");
            }

            private void FixedUpdate()
            {
                if (player == null) return;

                if (player.serverInput.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    PushPull(-1);
                }
                else if (player.serverInput.WasJustPressed(BUTTON.FIRE_SECONDARY))
                {
                    PushPull();
                    return;
                }
                else if (player.serverInput.IsDown(BUTTON.RELOAD))
                {
                    if (player.serverInput.IsDown(BUTTON.SPRINT))
                    {
                        Rotate(true);
                        return;
                    }
                    Rotate();
                }
            }

            public void PushPull(float directional = 1)
            {
                if (Vector3.Distance(player.transform.position, target.transform.position) > Instance.configData.minDistance)
                {
                    Instance.Message(player.IPlayer, "toofar");
                    return;
                }

                Instance.DoLog($"Player position: {player.transform.position}");
                Instance.DoLog($"Old position: {target.transform.position}");
                Instance.DoLog($"Checking for obstruction at {checkpos.ToString()} ({obstring}), distance: {checkDistance.ToString()}, directional {directional.ToString()}");

                DetectSide();
                if (directional < 1)
                {
                    // Only while pushing (and cannot see the other side)
                    if (checkHit()) return;
                }

                if (directional > 0)
                {
                    Instance.Message(player.IPlayer, "pulling", pushtring);
                }
                else
                {
                    Instance.Message(player.IPlayer, "pushing", pushtring);
                }

                float savey = target.transform.position.y;
                Vector3 newpos = new Vector3(player.transform.position.x, savey, player.transform.position.z);
                target.transform.position = Vector3.MoveTowards(target.transform.position, newpos, directional * 1f * Time.deltaTime);

                if (stab?.grounded == false) stab.grounded = true;
                Instance.DoLog($"New position: {target.transform.position}");

                ServerMgr.Instance.StartCoroutine(RefreshChildren());
            }

            private bool checkHit()
            {
                RaycastHit[] hits = Physics.RaycastAll(checkpos, direction, checkDistance);//, layerMask, QueryTriggerInteraction.Collide)
                for (int i = 0; i < hits.Length; i++)
                {
                    string ihit = hits[i].GetEntity()?.ShortPrefabName;
                    if (ihit.Length > 0 && hits[i].GetEntity() != target && ihit != "floor" && (Instance.configData?.noHitItems?.Contains(ihit) == false))
                    {
                        Instance.DoLog($"Hit a {ihit}");
                        player.SendConsoleCommand("ddraw.text", 5, Color.yellow, hits[i].transform.position, "<size=40>HIT</size>");
                        Instance.Message(player.IPlayer, "obstruction", ihit);
                        return true;
                    }
                }
                return false;
            }

            private void Rotate(bool ccw = false)
            {
                if (Vector3.Distance(player.transform.position, target.transform.position) > Instance.configData.minDistance)
                {
                    Instance.Message(player.IPlayer, "toofar");
                    return;
                }

                //DetectSide(true, ccw);
                if (ccw)
                {
                    //if (checkHit()) return;
                    gameObject.transform.Rotate(0, -0.5f, 0);
                }
                else
                {
                    //if (checkHit()) return;
                    gameObject.transform.Rotate(0, +0.5f, 0);
                }

                ServerMgr.Instance.StartCoroutine(RefreshChildren());
            }

            private IEnumerator RefreshChildren()
            {
                target.transform.hasChanged = true;
                for (int i = 0; i < target.children.Count; i++)
                {
                    target.children[i].transform.hasChanged = true;
                    target.children[i].SendNetworkUpdateImmediate();
                    target.children[i].UpdateNetworkGroup();
                }
                target.SendNetworkUpdateImmediate();
                target.UpdateNetworkGroup();
                yield return new WaitForEndOfFrame();
            }

            private void DetectSide(bool rotatation=false, bool ccw=false)
            {
                atleft = false; atright = false;
                front = target.transform.TransformPoint(Vector3.forward * (depth / 2));
                left = target.transform.TransformPoint(Vector3.right * (width / 2));
                right = target.transform.TransformPoint(Vector3.right * -(width / 2));
                back = target.transform.TransformPoint(Vector3.forward * -(depth / 2));

                float fdist = Vector3.Distance(player.transform.position, front);
                float bdist = Vector3.Distance(player.transform.position, back);
                float ldist = Vector3.Distance(player.transform.position, left);
                float rdist = Vector3.Distance(player.transform.position, right);

                atright = true; atleft = false;
                if (ldist < rdist)
                {
                    atleft = true; atright = false;
                }

                Vector3 ybump = new Vector3(0, 0.1f, 0);
                Vector3 cbump = new Vector3(0, height/2, 0);

                if (rotatation)
                {
                    if (ccw)
                    {
                        // checkpos should be right rear
                        return;
                    }
                    // checkpos should be left rear
                    return;
                }

                if (fdist < bdist)
                {
                    if (fdist > ldist && atleft)
                    {
                        Instance.DoLog("You are to the left of the target.");
                        checkDistance = width / 10;
                        checkpos = right + ybump;
                        direction = right - target.transform.position;
                        player.SendConsoleCommand("ddraw.arrow", 5, Color.white, target.transform.position + cbump, right + cbump, 0.25f);
                        obstring = "right";
                        pushtring = "left";
                    }
                    else if (fdist > rdist && atright)
                    {
                        Instance.DoLog("You are to the right of the target.");
                        checkDistance = width / 10;
                        checkpos = left + ybump;
                        direction = left - target.transform.position;
                        player.SendConsoleCommand("ddraw.arrow", 5, Color.white, target.transform.position + cbump, left + cbump, 0.25f);
                        obstring = "left";
                        pushtring = "right";
                    }
                    else
                    {
                        Instance.DoLog("You are in front of the target.");
                        checkDistance = depth / 10;
                        checkpos = back + ybump;
                        direction = back - target.transform.position;
                        player.SendConsoleCommand("ddraw.arrow", 5, Color.white, target.transform.position + cbump, back + cbump, 0.25f);
                        obstring = "rear";
                        pushtring = "front";
                    }
                }
                else if (bdist > ldist && atleft)
                {
                    Instance.DoLog("You are to the left of the target.");
                    checkDistance = width / 10;
                    checkpos = right + ybump;
                    direction = right - target.transform.position;
                    obstring = "right";
                    pushtring = "left";
                }
                else if (bdist > rdist && atright)
                {
                    Instance.DoLog("You are to the right of the target.");
                    checkDistance = width / 10;
                    checkpos = left + ybump;
                    direction = left - target.transform.position;
                    player.SendConsoleCommand("ddraw.arrow", 5, Color.white, target.transform.position + cbump, left + cbump, 0.25f);
                    obstring = "left";
                    pushtring = "right";
                }
                else
                {
                    Instance.DoLog("You are in back of the target.");
                    checkDistance = depth / 10;
                    checkpos = front + ybump;
                    direction = front - target.transform.position;
                    player.SendConsoleCommand("ddraw.arrow", 5, Color.white, target.transform.position + cbump, front + cbump, 0.25f);
                    obstring = "front";
                    pushtring = "rear";
                }
            }
        }

        #region Configuration
        public class ConfigData
        {
            [JsonProperty(PropertyName = "Disallow building blocks, doors, and windows")]
            public bool disallowBlocks;

            [JsonProperty(PropertyName = "Do not get stuck on these items.  Floors always included.")]
            public List<string> noHitItems;

            [JsonProperty(PropertyName = "Disallow moving players")]
            public bool disallowPlayer;

            [JsonProperty(PropertyName = "Disallow moving NPCs")]
            public bool disallowNPC;

            [JsonProperty(PropertyName = "Disallow other things that can cause trouble")]
            public bool disallowOthers;

            [JsonProperty(PropertyName = "Minimum distance to maintain to the target")]
            public float minDistance;

            public bool debug;
            public VersionNumber Version;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new ConfigData
            {
                disallowBlocks = true,
                noHitItems = new List<string>() { "rug.deployed" },
                disallowOthers = true,
                disallowPlayer = true,
                disallowNPC = true,
                minDistance = 5f,
                debug = false,
                Version = Version
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            if (configData.minDistance == 0)
            {
                configData.minDistance = 5;
            }

            if (configData.Version < new VersionNumber(1, 0, 4))
            {
                configData.noHitItems = new List<string>() { "rug.deployed" };
            }

            configData.Version = Version;
            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion Configuration
    }
}
