using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiInvocations
    {
        private static string FilePath => Path.Combine(BepInEx.Paths.ConfigPath, "COTL_AL_NPCs", "Invocations.json");

        private static void EnsureLoaded()
        {
            lock (Sync)
            {
                if (state != null)
                    return;

                state = Load();
                EnsureDefaultEntries(state);
                Save();
            }
        }

        private static FollowerAiInvocationState Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new FollowerAiInvocationState();

                var loaded = JsonConvert.DeserializeObject<FollowerAiInvocationState>(File.ReadAllText(FilePath));
                return loaded ?? new FollowerAiInvocationState();
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Invocation file load failed: {ex.Message}");
                return new FollowerAiInvocationState();
            }
        }

        private static void Save()
        {
            lock (Sync)
            {
                if (state == null)
                    state = new FollowerAiInvocationState();

                EnsureDefaultEntries(state);
                var json = JsonConvert.SerializeObject(state, Formatting.Indented);
                FollowerAiFileStore.WriteAllTextAtomic(FilePath, json);
            }
        }

        private static void EnsureDefaultEntries(FollowerAiInvocationState target)
        {
            if (target.Invocations == null)
                target.Invocations = new List<FollowerAiInvocationEntry>();

            EnsureEntry(target, MaxCultFaithID, "Fill cult faith to maximum");
            EnsureEntry(target, ClearVanillaFollowerRolesID, "Clear vanilla follower roles");
        }

        private static void EnsureEntry(FollowerAiInvocationState target, string id, string name)
        {
            var entry = target.Invocations.FirstOrDefault(item => string.Equals(item?.Id, id, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                target.Invocations.Add(new FollowerAiInvocationEntry { Id = id, Name = name, Code = string.Empty });
                return;
            }

            entry.Id = id;
            entry.Name = name;
            if (entry.Code == null)
                entry.Code = string.Empty;
        }
    }
}
