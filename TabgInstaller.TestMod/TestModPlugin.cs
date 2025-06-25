using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using HarmonyLib;
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace TabgInstaller.JumpMod
{
    [BepInPlugin("tabginstaller.jumpmod", "Jump Height Mod", "3.0.0")]
    public class JumpModPlugin : BaseUnityPlugin
    {
        private ConfigEntry<float> _jumpMultiplier;
        private ConfigEntry<bool> _debugMode;
        private Harmony _harmony;
        private static JumpModPlugin _instance;
        private static float _currentJumpMult = 1f;
        private static bool _isDebugMode = false;
        
        // Cache for original jump values
        private static Dictionary<object, float> _originalJumpValues = new Dictionary<object, float>();
        private static Dictionary<string, float> _originalStaticValues = new Dictionary<string, float>();

        private void Awake()
        {
            _instance = this;
            
            _jumpMultiplier = Config.Bind("Gameplay", "DefaultJumpMultiplier", 1f, "Default jump height multiplier");
            _debugMode = Config.Bind("Debug", "EnableDebugLogging", true, "Enable detailed debug logging");

            _currentJumpMult = _jumpMultiplier.Value;
            _isDebugMode = _debugMode.Value;

            Logger.LogInfo("JumpMod v3.0.0 initializing...");

            _harmony = new Harmony("tabginstaller.jumpmod");

            // Apply patches
            ApplyChatCommandPatch();
            FindAndPatchJumpMethods();
            FindAndPatchCurseSystem();

            Logger.LogInfo($"JumpMod initialized! Default jump multiplier: {_currentJumpMult}x");
        }

        private void ApplyChatCommandPatch()
        {
            try
            {
                var chatTypes = new[]
                {
                    "ChatMessageCommand",
                    "Landfall.Network.ChatMessageCommand",
                    "Network.ChatMessageCommand"
                };

                Type chatMsgCmdType = null;
                foreach (var typeName in chatTypes)
                {
                    chatMsgCmdType = AccessTools.TypeByName(typeName) ?? Type.GetType($"{typeName}, Assembly-CSharp");
                    if (chatMsgCmdType != null) break;
                }

                var chatRun = chatMsgCmdType?.GetMethod("Run");
                if (chatRun != null)
                {
                    _harmony.Patch(chatRun, postfix: new HarmonyMethod(typeof(JumpModPlugin), nameof(ChatCommandPostfix)));
                    Logger.LogInfo("Chat command patch applied (/jump <multiplier>)");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error patching chat command: {ex}");
            }
        }

        private void FindAndPatchJumpMethods()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var processed = new HashSet<string>();

            foreach (var assembly in assemblies)
            {
                try
                {
                    if (assembly.FullName.Contains("System") || assembly.FullName.Contains("Unity")) continue;

                    foreach (var type in assembly.GetTypes())
                    {
                        if (processed.Contains(type.FullName)) continue;
                        processed.Add(type.FullName);

                        // Look for player/character related types
                        if (IsPlayerRelatedType(type))
                        {
                            if (_isDebugMode)
                                Logger.LogInfo($"Scanning type: {type.Name}");

                            PatchJumpFieldsAndProperties(type);
                            PatchJumpMethods(type);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_isDebugMode)
                        Logger.LogWarning($"Error scanning assembly {assembly.FullName}: {ex.Message}");
                }
            }
        }

        private bool IsPlayerRelatedType(Type type)
        {
            var name = type.Name.ToLower();
            return name.Contains("player") || 
                   name.Contains("character") || 
                   name.Contains("movement") || 
                   name.Contains("controller") ||
                   name.Contains("motor") ||
                   name.Contains("physics") ||
                   name.Contains("jump");
        }

        private void PatchJumpFieldsAndProperties(Type type)
        {
            // Look for jump-related fields
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var fieldName = field.Name.ToLower();
                if (fieldName.Contains("jump") && 
                    (fieldName.Contains("force") || fieldName.Contains("power") || 
                     fieldName.Contains("height") || fieldName.Contains("velocity") ||
                     fieldName.Contains("speed") || fieldName.Contains("strength")))
                {
                    Logger.LogInfo($"Found jump field: {type.Name}.{field.Name} ({field.FieldType})");
                    
                    // If it's a float field, we might be able to modify it directly
                    if (field.FieldType == typeof(float))
                    {
                        try
                        {
                            if (field.IsStatic)
                            {
                                var originalValue = (float)field.GetValue(null);
                                _originalStaticValues[$"{type.FullName}.{field.Name}"] = originalValue;
                                field.SetValue(null, originalValue * _currentJumpMult);
                                Logger.LogInfo($"Modified static jump field {field.Name}: {originalValue} -> {originalValue * _currentJumpMult}");
                            }
                        }
                        catch (Exception ex)
                        {
                            if (_isDebugMode)
                                Logger.LogWarning($"Could not modify field {field.Name}: {ex.Message}");
                        }
                    }
                }
            }

            // Look for jump-related properties
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var prop in properties)
            {
                var propName = prop.Name.ToLower();
                if (propName.Contains("jump") && 
                    (propName.Contains("force") || propName.Contains("power") || 
                     propName.Contains("height") || propName.Contains("velocity")))
                {
                    Logger.LogInfo($"Found jump property: {type.Name}.{prop.Name}");
                    
                    // Try to patch getter
                    var getter = prop.GetGetMethod(true);
                    if (getter != null && !getter.IsAbstract)
                    {
                        try
                        {
                            _harmony.Patch(getter, postfix: new HarmonyMethod(typeof(JumpModPlugin), nameof(JumpValueGetterPostfix)));
                            Logger.LogInfo($"Patched jump property getter: {prop.Name}");
                        }
                        catch { }
                    }
                }
            }
        }

        private void PatchJumpMethods(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            
            foreach (var method in methods)
            {
                var methodName = method.Name.ToLower();
                
                // Skip property accessors and constructors
                if (method.IsSpecialName || method.IsConstructor) continue;
                
                // Look for jump-related methods
                if (methodName.Contains("jump") || 
                    methodName.Contains("dojump") || 
                    methodName.Contains("performjump") ||
                    methodName.Contains("applyjump") ||
                    methodName.Contains("startjump") ||
                    methodName.Contains("onjump"))
                {
                    try
                    {
                        _harmony.Patch(method, 
                            prefix: new HarmonyMethod(typeof(JumpModPlugin), nameof(JumpMethodPrefix)),
                            postfix: new HarmonyMethod(typeof(JumpModPlugin), nameof(JumpMethodPostfix)));
                        Logger.LogInfo($"Patched jump method: {type.Name}.{method.Name}");
                    }
                    catch (Exception ex)
                    {
                        if (_isDebugMode)
                            Logger.LogWarning($"Failed to patch {method.Name}: {ex.Message}");
                    }
                }
                
                // Also look for velocity/force application methods that might be used for jumping
                if ((methodName.Contains("addforce") || methodName.Contains("setvelocity") || 
                     methodName.Contains("applyforce") || methodName.Contains("addvelocity")) &&
                    method.GetParameters().Length > 0)
                {
                    try
                    {
                        _harmony.Patch(method, prefix: new HarmonyMethod(typeof(JumpModPlugin), nameof(VelocityMethodPrefix)));
                        if (_isDebugMode)
                            Logger.LogInfo($"Patched velocity method: {type.Name}.{method.Name}");
                    }
                    catch { }
                }
            }
        }

        private void FindAndPatchCurseSystem()
        {
            // Look for curse-related types that might affect jumping
            var curseTypes = new[]
            {
                "Curse",
                "CurseEffect",
                "JumpCurse",
                "LowJumpCurse",
                "HighJumpCurse",
                "CurseManager",
                "CurseSystem"
            };

            foreach (var curseName in curseTypes)
            {
                var curseType = AccessTools.TypeByName(curseName) ?? 
                               Type.GetType($"{curseName}, Assembly-CSharp") ??
                               Type.GetType($"Landfall.{curseName}, Assembly-CSharp");
                
                if (curseType != null)
                {
                    Logger.LogInfo($"Found curse type: {curseType.FullName}");
                    
                    // Look for methods that apply jump modifications
                    var methods = curseType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (var method in methods)
                    {
                        if (method.Name.ToLower().Contains("apply") || 
                            method.Name.ToLower().Contains("effect") ||
                            method.Name.ToLower().Contains("jump"))
                        {
                            try
                            {
                                _harmony.Patch(method, postfix: new HarmonyMethod(typeof(JumpModPlugin), nameof(CurseMethodPostfix)));
                                Logger.LogInfo($"Patched curse method: {method.Name}");
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        #region Harmony Patches

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

                if (!msg.StartsWith("/jump", StringComparison.OrdinalIgnoreCase)) return;

                var parts = msg.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    _instance.Logger.LogInfo("Usage: /jump <multiplier>. Example: /jump 2 (for double jump height)");
                    return;
                }

                if (!float.TryParse(parts[1], out float factor))
                {
                    _instance.Logger.LogInfo($"Invalid multiplier '{parts[1]}'. Must be a number");
                    return;
                }

                if (factor <= 0f) factor = 0.1f; // Allow very low jumps

                _currentJumpMult = factor;
                _instance.Logger.LogInfo($"Jump multiplier set to {_currentJumpMult}x");
                
                // Re-apply to static fields
                foreach (var kvp in _originalStaticValues)
                {
                    var parts2 = kvp.Key.Split('.');
                    var typeName = string.Join(".", parts2.Take(parts2.Length - 1));
                    var fieldName = parts2.Last();
                    
                    var type = Type.GetType(typeName);
                    if (type != null)
                    {
                        var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field != null)
                        {
                            field.SetValue(null, kvp.Value * _currentJumpMult);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _instance.Logger.LogError($"Error processing /jump command: {ex}");
            }
        }

        private static void JumpValueGetterPostfix(ref float __result)
        {
            if (__result > 0)
            {
                __result *= _currentJumpMult;
            }
        }

        private static bool JumpMethodPrefix(object __instance)
        {
            if (_isDebugMode)
            {
                var method = new System.Diagnostics.StackFrame(1).GetMethod();
                _instance.Logger.LogInfo($"Jump method called: {method?.DeclaringType?.Name}.{method?.Name}");
            }
            
            // Store original values if this is a new instance
            if (!_originalJumpValues.ContainsKey(__instance))
            {
                CacheOriginalJumpValues(__instance);
            }
            
            // Apply multiplier to instance fields
            ApplyJumpMultiplierToInstance(__instance);
            
            return true; // Continue with original method
        }

        private static void JumpMethodPostfix(object __instance)
        {
            // Restore original values after jump
            RestoreOriginalJumpValues(__instance);
        }

        private static void VelocityMethodPrefix(object[] __args)
        {
            // Look for Vector3 parameters that might be jump velocities
            for (int i = 0; i < __args.Length; i++)
            {
                if (__args[i] is Vector3 vec)
                {
                    // If it's an upward velocity, scale it
                    if (vec.y > 0.1f && Mathf.Abs(vec.x) < vec.y && Mathf.Abs(vec.z) < vec.y)
                    {
                        vec.y *= _currentJumpMult;
                        __args[i] = vec;
                        
                        if (_isDebugMode)
                            _instance.Logger.LogInfo($"Scaled jump velocity Y component to: {vec.y}");
                    }
                }
                else if (__args[i] is Vector2 vec2)
                {
                    // For 2D games
                    if (vec2.y > 0.1f)
                    {
                        vec2.y *= _currentJumpMult;
                        __args[i] = vec2;
                    }
                }
            }
        }

        private static void CurseMethodPostfix()
        {
            if (_isDebugMode)
            {
                var method = new System.Diagnostics.StackFrame(1).GetMethod();
                _instance.Logger.LogInfo($"Curse method called: {method?.DeclaringType?.Name}.{method?.Name}");
            }
        }

        private static void CacheOriginalJumpValues(object instance)
        {
            var type = instance.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            foreach (var field in fields)
            {
                var fieldName = field.Name.ToLower();
                if (fieldName.Contains("jump") && field.FieldType == typeof(float))
                {
                    try
                    {
                        var value = (float)field.GetValue(instance);
                        _originalJumpValues[instance] = value;
                        break; // Usually there's one main jump value
                    }
                    catch { }
                }
            }
        }

        private static void ApplyJumpMultiplierToInstance(object instance)
        {
            var type = instance.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            foreach (var field in fields)
            {
                var fieldName = field.Name.ToLower();
                if (fieldName.Contains("jump") && field.FieldType == typeof(float))
                {
                    try
                    {
                        var currentValue = (float)field.GetValue(instance);
                        field.SetValue(instance, currentValue * _currentJumpMult);
                        
                        if (_isDebugMode)
                            _instance.Logger.LogInfo($"Modified instance jump field {field.Name}: {currentValue} -> {currentValue * _currentJumpMult}");
                    }
                    catch { }
                }
            }
        }

        private static void RestoreOriginalJumpValues(object instance)
        {
            if (_originalJumpValues.TryGetValue(instance, out float originalValue))
            {
                var type = instance.GetType();
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                foreach (var field in fields)
                {
                    var fieldName = field.Name.ToLower();
                    if (fieldName.Contains("jump") && field.FieldType == typeof(float))
                    {
                        try
                        {
                            field.SetValue(instance, originalValue);
                        }
                        catch { }
                    }
                }
            }
        }

        #endregion

        private void OnDestroy()
        {
            // Restore original static values
            foreach (var kvp in _originalStaticValues)
            {
                var parts = kvp.Key.Split('.');
                var typeName = string.Join(".", parts.Take(parts.Length - 1));
                var fieldName = parts.Last();
                
                var type = Type.GetType(typeName);
                if (type != null)
                {
                    var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        field.SetValue(null, kvp.Value);
                    }
                }
            }
            
            _harmony?.UnpatchSelf();
        }
    }
} 