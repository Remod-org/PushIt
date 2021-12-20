using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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
                ["endpush"] = "No longer pushing {0}",
                ["nopushpull"] = "You cannot interact with a {0}",
                ["obstruction"] = "We hit {0}.  Backing up...",
                ["notauthorized"] = "You don't have permission to do that !!"
            }, this);
        }
        #endregion

        private void OnServerInitialized()
        {
            LoadConfigVariables();
            AddCovalenceCommand("push", "PushPullCmd");
            Instance = this;
        }

        //private void OnHammerHit(BasePlayer player, HitInfo hit)
        //{
        //    BaseEntity target = hit.HitEntity;

        //    float depth = target.bounds.size.z;
        //    float width = target.bounds.size.x;

        //    Vector3 rayPos = target.transform.TransformPoint(Vector3.forward * (depth / 2)); // forward
        //    Vector3 rayPos3 = target.transform.TransformPoint(Vector3.right * (width / 2)); // left
        //    Vector3 rayPos4 = target.transform.TransformPoint(Vector3.right * -(width / 2)); //right
        //    Vector3 rayPosBack = target.transform.TransformPoint(Vector3.forward * -(depth / 2)); //back

        //    player.SendConsoleCommand("ddraw.line", 30, Color.blue, target.transform.position, rayPos);
        //    player.SendConsoleCommand("ddraw.line", 30, Color.green, target.transform.position, rayPos3);
        //    player.SendConsoleCommand("ddraw.line", 30, Color.red, target.transform.position, rayPos4);
        //    player.SendConsoleCommand("ddraw.line", 30, Color.blue, target.transform.position, rayPosBack);
        //    //player.SendConsoleCommand("ddraw.text", 60, Color.green, target.transform.position, "<size=20>CTR</size>");
        //    //player.SendConsoleCommand("ddraw.text", 60, Color.blue, left, "<size=20>left</size>");
        //    //player.SendConsoleCommand("ddraw.text", 60, Color.blue, right, "<size=20>right</size>");
        //}

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
            BasePlayer player = iplayer.Object as BasePlayer;
            BaseEntity ent = FindEntity(player);
            if (ent == null)
            {
                Message(iplayer, "noentity");
                return;
            }

            PushMe haspush = ent.gameObject.GetComponent<PushMe>();
            string entname = ent.ShortPrefabName;
            if (haspush != null)
            {
                UnityEngine.Object.Destroy(haspush);
                pushentity.Remove(player.userID);
                Message(iplayer, "endpush", entname);
                return;
            }

            PushMe push = ent.gameObject.AddComponent<PushMe>();
            push.player = player;
            pushentity.Add(player.userID, ent.net.ID);
            Message(iplayer, "startpush", entname);
        }

        //private void OnPlayerInput(BasePlayer player, InputState input)
        //{
        //    if (player == null) return;
        //    if (input == null) return;

        //    //if (input.current.buttons > 0)
        //    //    Puts($"OnPlayerInput: {input.current.buttons}");

        //    if (pushentity.ContainsKey(player.userID))
        //    {
        //        BaseEntity ent = FindEntity(player);
        //        PushMe haspush = ent.gameObject.GetComponent<PushMe>();
        //        if (haspush == null) return;

        //        //if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
        //        //{
        //        //    DoLog("Pressed use on a push entity");
        //        //}
        //        //else if (input.WasJustPressed(BUTTON.FIRE_SECONDARY))
        //        //{
        //        //    haspush.directional = -1;
        //        //}
        //    }
        //}

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

            public bool atleft;
            public bool atright;
            public bool infront;
            public bool inback;

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

            private void Awake()
            {
                target = gameObject.GetComponent<BaseEntity>();
                depth = target.bounds.size.z;
                width = target.bounds.size.x;
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
                    else
                    {
                        Rotate();
                    }
                }
            }

            private void DetectSide()
            {
                atleft = false; atright = false; infront = false; inback = false;

                front = target.transform.TransformPoint(Vector3.forward * (depth / 2)); // forward
                left = target.transform.TransformPoint(Vector3.right * (width / 2)); // left
                right = target.transform.TransformPoint(Vector3.right * -(width / 2)); //right
                back = target.transform.TransformPoint(Vector3.forward * -(depth / 2)); //back

                float f = Vector3.Distance(player.transform.position, front);
                float b = Vector3.Distance(player.transform.position, back);
                float l = Vector3.Distance(player.transform.position, left);
                float r = Vector3.Distance(player.transform.position, right);

                if (l < r)
                {
                    atleft = true; atright = false;
                }
                else
                {
                    atright = true; atleft = false;
                }

                if (f < b)
                {
                    if (f > l && atleft)
                    {
                        Instance.DoLog("You are to the left of the target.");
                        checkDistance = width / 6;
                        checkpos = right;
                        obstring = "right";
                    }
                    else if (f > r && atright)
                    {
                        Instance.DoLog("You are to the right of the target.");
                        checkDistance = width / 6;
                        checkpos = left;
                        obstring = "left";
                    }
                    else
                    {
                        Instance.DoLog("You are in front of the target.");
                        infront = true;
                        inback = false;
                        checkDistance = depth / 6;
                        checkpos = back;
                        obstring = "rear";
                    }
                }
                else if (b > l && atleft)
                {
                    Instance.DoLog("You are to the left of the target.");
                    checkDistance = width / 6;
                    checkpos = right;
                    obstring = "right";
                }
                else if (b > r && atright)
                {
                    Instance.DoLog("You are to the right of the target.");
                    checkDistance = width / 6;
                    checkpos = left;
                    obstring = "left";
                }
                else
                {
                    Instance.DoLog("You are in back of the target.");
                    inback = true;
                    infront = false;
                    checkDistance = depth / 6;
                    checkpos = front;
                    obstring = "front";
                }
            }

            public void PushPull(float directional = 1)
            {
                float savey = target.transform.position.y;
                Instance.DoLog($"Player position: {player.transform.position}");
                Instance.DoLog($"Old position: {target.transform.position}");

                DetectSide();

                if (directional > 0)
                {
                    // pull
                    //direction = player.transform.position - target.transform.position;
                    direction = player.transform.position - checkpos;
                }
                else
                {
                    //direction = target.transform.position - player.transform.position;
                    direction = checkpos - player.transform.position;
                }

                Instance.DoLog($"Checking for obstruction at {checkpos.ToString()} ({obstring}), distance: {checkDistance.ToString()}");
                RaycastHit hit;
                if (Physics.Raycast(checkpos, direction, out hit, checkDistance, layerMask, QueryTriggerInteraction.Collide) && !hit.GetEntity().ToString().Contains("floor"))
                {
                    Instance.DoLog($"I hit {hit.GetEntity()}, backing up!");
                    Instance.Message(player.IPlayer, "obstruction", hit.GetEntity());
                    return;
                }

                Vector3 newpos = new Vector3(player.transform.position.x, savey, player.transform.position.z);
                //target.transform.position = Vector3.MoveTowards(target.transform.position, player.transform.position, directional * 1f * Time.deltaTime);
                target.transform.position = Vector3.MoveTowards(target.transform.position, newpos, directional * 1f * Time.deltaTime);
                //target.transform.position.y.SnapTo(savey);

                if (stab?.grounded == false) stab.grounded = true;
                Instance.DoLog($"New position: {target.transform.position}");

                ServerMgr.Instance.StartCoroutine(RefreshTrain());
                //target.transform.hasChanged = true;
                //target.UpdateNetworkGroup();
                //target.SendNetworkUpdateImmediate();
            }

            private void Rotate(bool ccw = false)
            {
                if (ccw)
                {
                    gameObject.transform.Rotate(0, -0.5f, 0);
                }
                else
                {
                    gameObject.transform.Rotate(0, +0.5f, 0);
                }
                ServerMgr.Instance.StartCoroutine(RefreshTrain());
            }

            //private IEnumerator RefreshTrain()
            //{
            //    target.transform.hasChanged = true;
            //    target.SendNetworkUpdateImmediate();
            //    target.UpdateNetworkGroup();
            //    target.gameObject.GetComponent<Model>().transform.hasChanged = true;
            //    yield return new WaitForEndOfFrame();
            //}

            private IEnumerator RefreshTrain()
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
