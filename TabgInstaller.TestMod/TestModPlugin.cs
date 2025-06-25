using BepInEx;
using BepInEx.Configuration;
using System.Collections;
using UnityEngine;
using HarmonyLib;
using System;
using System.IO;
using System.Text;

namespace TabgInstaller.TestMod
{
    [BepInPlugin("tabginstaller.testmod", "Test Mod", "1.0.0")]
    public class TestModPlugin : BaseUnityPlugin
    {
        private ConfigEntry<string> _message;
        private ConfigEntry<float> _interval;
        private ConfigEntry<float> _gravityMult;
        private ConfigEntry<float> _timeScale;
        private Harmony _harmony;

        private void Awake()
        {
            _message = Config.Bind("General", "Message", "Test mod loaded!", "Message written to server log periodically");
            _interval = Config.Bind("General", "IntervalSeconds", 15f, "Seconds between messages");
            _gravityMult = Config.Bind("Gameplay", "GravityMultiplier", 0.5f, "Multiply world gravity by this factor (e.g., 0.5 = half gravity)");
            _timeScale = Config.Bind("Gameplay", "TimeScale", 0.5f, "Multiply game time scale (0.5 = slow motion)");

            Logger.LogInfo($"TestMod initialized. Message='{_message.Value}', Interval={_interval.Value}s");

            Time.timeScale = _timeScale.Value;
            Logger.LogInfo($"Time scale set to {_timeScale.Value}");

            _harmony = new Harmony("tabginstaller.gravitymod");
            var chatMsgCmdType = AccessTools.TypeByName("ChatMessageCommand") ?? Type.GetType("Landfall.Network.ChatMessageCommand, Assembly-CSharp");
            var chatRun = chatMsgCmdType != null ? AccessTools.Method(chatMsgCmdType, "Run") : null;
            if (chatRun != null)
            {
                _harmony.Patch(chatRun, postfix: new HarmonyMethod(typeof(TestModPlugin), nameof(ChatCommandPostfix)));
                Logger.LogInfo("Gravity command patch applied (/gravity <factor>)");
            }
            else
            {
                Logger.LogWarning("Failed to find ChatMessageCommand.Run - gravity command disabled");
            }

            StartCoroutine(Loop());
        }

        private IEnumerator Loop()
        {
            while (true)
            {
                Logger.LogInfo($"[TestMod] {_message.Value}");
                yield return new WaitForSeconds(_interval.Value);
            }
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

                Physics.gravity = new Vector3(0f, -9.81f * factor, 0f);
                Console.WriteLine($"[GravityMod] Gravity multiplier set to {factor}x (Y={Physics.gravity.y})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GravityMod] Error processing /gravity command: {ex}");
            }
        }
    }
} 