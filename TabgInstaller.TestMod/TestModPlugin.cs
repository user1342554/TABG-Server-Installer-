using BepInEx;
using BepInEx.Configuration;
using System.Collections;
using UnityEngine;
using HarmonyLib;
using System;
using System.IO;
using System.Text;

namespace TabgInstaller.GravityMod
{
    [BepInPlugin("tabginstaller.gravitymod", "Gravity Mod", "1.1.0")]
    public class GravityModPlugin : BaseUnityPlugin
    {
        private ConfigEntry<float> _gravityMult;
        private Harmony _harmony;
        private static float _currentGravityMult = 1f;

        private void Awake()
        {
            _gravityMult = Config.Bind("Gameplay", "DefaultGravityMultiplier", 1f, "Default gravity multiplier when server starts");

            Logger.LogInfo("GravityMod initialized");

            _currentGravityMult = _gravityMult.Value;

            _harmony = new Harmony("tabginstaller.gravitymod");
            var chatMsgCmdType = AccessTools.TypeByName("ChatMessageCommand") ?? Type.GetType("Landfall.Network.ChatMessageCommand, Assembly-CSharp");
            var chatRun = chatMsgCmdType != null ? AccessTools.Method(chatMsgCmdType, "Run") : null;
            if (chatRun != null)
            {
                _harmony.Patch(chatRun, postfix: new HarmonyMethod(typeof(GravityModPlugin), nameof(ChatCommandPostfix)));
                Logger.LogInfo("Gravity command patch applied (/gravity <factor>)");
            }
            else
            {
                Logger.LogWarning("Failed to find ChatMessageCommand.Run - gravity command disabled");
            }

            StartCoroutine(GravityLoop());
        }

        private static void ChatCommandPostfix(byte[] __0, object __1, byte __2)
        {
            try
            {
                string msg;
                using (MemoryStream ms = new MemoryStream(__0))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    br.ReadByte();
                    byte count = br.ReadByte();
                    msg = Encoding.Unicode.GetString(br.ReadBytes(count));
                }

                if (!msg.StartsWith("/gravity", StringComparison.OrdinalIgnoreCase)) return;

                var parts = msg.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    Console.WriteLine("[GravityMod] Usage: /gravity <factor>. Example: /gravity 0.5");
                    return;
                }

                if (!float.TryParse(parts[1], out float factor))
                {
                    Console.WriteLine($"[GravityMod] Invalid factor '{parts[1]}'. Must be a number");
                    return;
                }

                if (factor <= 0f) factor = 1f;

                _currentGravityMult = factor;
                Physics.gravity = new Vector3(0f, -9.81f * _currentGravityMult, 0f);
                Console.WriteLine($"[GravityMod] Gravity multiplier set to {_currentGravityMult}x (Y={Physics.gravity.y})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GravityMod] Error processing /gravity command: {ex}");
            }
        }

        private IEnumerator GravityLoop()
        {
            while (true)
            {
                Physics.gravity = new Vector3(0f, -9.81f * _currentGravityMult, 0f);
                yield return new WaitForSeconds(1f);
            }
        }
    }
} 