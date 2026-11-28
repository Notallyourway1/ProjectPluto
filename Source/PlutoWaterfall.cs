using System;
using System.Reflection;
using UnityEngine;

namespace ProjectPluto
{
    // Finds a Waterfall plume on a part and links one of its controllers to the plugin. The controller
    // must be Waterfall's "custom" type, which holds whatever value a plugin sets. Waterfall is optional,
    // so it is reached by reflection, with no reference to its DLL.
    public static class PlutoWaterfall
    {
        private const string ModuleName = "ModuleWaterfallFX";
        private const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

        // Null when the part has no Waterfall module with that moduleID (Waterfall not installed).
        public static PlutoPlumeLink Find(Part part, string moduleID, string controller)
        {
            if (part == null || string.IsNullOrEmpty(moduleID) || string.IsNullOrEmpty(controller))
                return null;
            for (int i = 0; i < part.Modules.Count; i++)
            {
                PartModule module = part.Modules[i];
                if (module == null || module.moduleName != ModuleName)
                    continue;
                try
                {
                    Type type = module.GetType();
                    FieldInfo id = type.GetField("moduleID", Public);
                    if (id == null || !string.Equals(id.GetValue(module) as string, moduleID))
                        continue;
                    MethodInfo get = type.GetMethod("GetControllerValues", Public, null, new[] { typeof(string) }, null);
                    MethodInfo set = type.GetMethod("SetControllerValue", Public, null,
                        new[] { typeof(string), typeof(float) }, null);
                    if (get == null || set == null || get.ReturnType != typeof(float[]))
                    {
                        Debug.LogWarning("[ProjectPluto] This Waterfall version has no controller methods the " +
                            "plugin knows; the ramjet plume will not follow thrust.");
                        return null;
                    }
                    var getValues = (Func<string, float[]>)Delegate.CreateDelegate(typeof(Func<string, float[]>), module, get);
                    var setValue = (Action<string, float>)Delegate.CreateDelegate(typeof(Action<string, float>), module, set);
                    return new PlutoPlumeLink(controller, getValues, setValue);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ProjectPluto] Could not link the Waterfall plume: {e.Message}");
                    return null;
                }
            }
            return null;
        }
    }
}
