using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace COTL_AL_NPCs
{
    internal static partial class FollowerAiTournamentLedger
    {
        private const string SaveFileName = "TournamentLedger.json";
        private static FollowerAiTournamentState state;
        private static string loadedScopeKey = string.Empty;
        private static float nextReconcileAtRealtime;

        private static string SavePath => Path.Combine(FollowerAiSaveScope.CurrentScopedDirectory, SaveFileName);

        internal static FollowerAiTournamentState State
        {
            get
            {
                EnsureLoaded();
                return state;
            }
        }

        internal static void ResetForSaveScopeChange()
        {
            state = null;
            loadedScopeKey = string.Empty;
            nextReconcileAtRealtime = 0f;
        }

        internal static void Update()
        {
            if (UnityEngine.Time.realtimeSinceStartup < nextReconcileAtRealtime)
                return;

            nextReconcileAtRealtime = UnityEngine.Time.realtimeSinceStartup + 5f;
            ReconcileLiveFollowerStatuses(saveIfChanged: true);
        }

        internal static void EnsureLoaded()
        {
            var currentScope = FollowerAiSaveScope.CurrentSaveKey;
            if (state != null && string.Equals(loadedScopeKey, currentScope, StringComparison.OrdinalIgnoreCase))
                return;

            loadedScopeKey = currentScope;
            state = LoadState();
            EnsureDraftShape(state.Draft);
            ReconcileLiveFollowerStatuses(saveIfChanged: false);
        }

        internal static void Save()
        {
            EnsureLoaded();
            try
            {
                var json = JsonConvert.SerializeObject(state, Formatting.Indented);
                FollowerAiFileStore.WriteAllTextAtomic(SavePath, json);
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Tournament ledger save failed: {ex.Message}");
            }
        }

        internal static void AddFollowerToFirstOpenSlot(FollowerAiFollowerFact fact)
        {
            if (fact == null)
                return;

            EnsureLoaded();
            EnsureDraftShape(state.Draft);

            var existing = state.Draft.Entrants.FirstOrDefault(entrant => entrant.FollowerID == fact.ID);
            if (existing != null)
            {
                existing.Name = fact.Name;
                existing.Status = BuildStatus(fact, existing.Status);
                Save();
                return;
            }

            var open = state.Draft.Entrants.FirstOrDefault(entrant => string.IsNullOrWhiteSpace(entrant.Name) && entrant.FollowerID <= 0);
            if (open == null)
            {
                open = new FollowerAiTournamentEntrant { Slot = state.Draft.Entrants.Count + 1 };
                state.Draft.Entrants.Add(open);
            }

            open.FollowerID = fact.ID;
            open.Name = fact.Name;
            open.Seed = string.IsNullOrWhiteSpace(open.Seed) ? open.Slot.ToString() : open.Seed;
            open.Status = BuildStatus(fact, open.Status);
            Save();
        }

        internal static void ClearEntrant(FollowerAiTournamentEntrant entrant)
        {
            if (entrant == null)
                return;

            entrant.FollowerID = 0;
            entrant.Name = string.Empty;
            entrant.Seed = string.Empty;
            entrant.Status = "Alive";
            entrant.Notes = string.Empty;
            Save();
        }

        internal static void AddBlankMatch()
        {
            EnsureLoaded();
            state.Draft.Matches.Add(new FollowerAiTournamentMatch());
            Save();
        }

        internal static void RemoveMatchAt(int index)
        {
            EnsureLoaded();
            if (index < 0 || index >= state.Draft.Matches.Count)
                return;

            state.Draft.Matches.RemoveAt(index);
            Save();
        }

        private static FollowerAiTournamentState LoadState()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    var loaded = JsonConvert.DeserializeObject<FollowerAiTournamentState>(File.ReadAllText(SavePath));
                    if (loaded != null)
                        return loaded;
                }
            }
            catch (Exception ex)
            {
                AICharacterPlugin.Log?.LogWarning($"Tournament ledger load failed: {ex.Message}");
            }

            return new FollowerAiTournamentState();
        }

        private static void EnsureLoadedNoReconcile()
        {
            var currentScope = FollowerAiSaveScope.CurrentSaveKey;
            if (state != null && string.Equals(loadedScopeKey, currentScope, StringComparison.OrdinalIgnoreCase))
                return;

            loadedScopeKey = currentScope;
            state = LoadState();
            EnsureDraftShape(state.Draft);
        }

        private static void EnsureDraftShape(FollowerAiTournamentDraft draft)
        {
            if (draft == null)
            {
                state.Draft = new FollowerAiTournamentDraft();
                draft = state.Draft;
            }

            if (draft.Entrants == null)
                draft.Entrants = new List<FollowerAiTournamentEntrant>();
            if (draft.Matches == null)
                draft.Matches = new List<FollowerAiTournamentMatch>();
            if (draft.Champion == null)
                draft.Champion = new FollowerAiTournamentChampion();
            if (state.Archive == null)
                state.Archive = new List<FollowerAiTournamentArchiveEntry>();

            for (var i = draft.Entrants.Count; i < 10; i++)
            {
                draft.Entrants.Add(new FollowerAiTournamentEntrant
                {
                    Slot = i + 1,
                    Seed = (i + 1).ToString(),
                    Status = "Alive"
                });
            }

            for (var i = 0; i < draft.Entrants.Count; i++)
            {
                if (draft.Entrants[i] == null)
                    draft.Entrants[i] = new FollowerAiTournamentEntrant();
                draft.Entrants[i].Slot = i + 1;
                if (string.IsNullOrWhiteSpace(draft.Entrants[i].Status))
                    draft.Entrants[i].Status = "Alive";
            }
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
    }
}
