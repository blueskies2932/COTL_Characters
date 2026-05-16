using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiTournamentOverlay
    {
        private const int WindowID = 192091;
        private const string InputBlockerOwner = "tournament";
        private static bool visible;
        private static bool currentMatchVisible;
        private static Rect windowRect;
        private static bool windowRectInitialized;
        private static int styleFontSize = -1;
        private static Vector2 mainScroll;
        private static Vector2 followerScroll;
        private static Vector2 matchScroll;
        private static Vector2 archiveScroll;
        private static bool archiveViewVisible;
        private static GUIStyle labelStyle;
        private static GUIStyle smallLabelStyle;
        private static GUIStyle textFieldStyle;
        private static GUIStyle textAreaStyle;
        private static GUIStyle buttonStyle;
        private static GUIStyle smallButtonStyle;
        private static GUIStyle headerStyle;
        private static GUIStyle windowStyle;
        private static GUIStyle sectionStyle;
        private static GUIStyle currentMatchBoxStyle;
        private static GUIStyle currentMatchTitleStyle;
        private static GUIStyle currentMatchNameStyle;
        private static Texture2D windowBackground;
        private static Texture2D sectionBackground;
        private static Texture2D fieldBackground;
        private static Texture2D currentMatchBackground;
        private static string statusMessage = "Tournament ledger ready.";
        private static float statusMessageUntilRealtime;
        private static readonly HashSet<int> openOutcomeDropdowns = new HashSet<int>();
        private static readonly HashSet<string> expandedArchiveEntries = new HashSet<string>();

        internal static void Update()
        {
            FollowerAiTournamentLedger.Update();
            if (visible && !IsPauseScreenOpen())
                Hide();

            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (IsPauseScreenOpen())
                {
                    currentMatchVisible = false;
                    if (visible)
                        Hide();
                    else
                        Show();
                    return;
                }

                if (visible)
                    Hide();
                currentMatchVisible = !currentMatchVisible;
                if (currentMatchVisible)
                    FollowerAiTournamentLedger.EnsureLoaded();
            }
        }

        internal static void Show()
        {
            visible = true;
            FollowerAiTournamentLedger.EnsureLoaded();
            FollowerAiOverlayInputBlocker.Show(InputBlockerOwner);
            SetStatus("Tournament ledger opened.");
        }

        internal static void Hide()
        {
            visible = false;
            FollowerAiOverlayInputBlocker.Hide(InputBlockerOwner);
            FollowerAiTournamentLedger.Save();
        }

        internal static void OnGUI()
        {
            EnsureStyles();

            if (currentMatchVisible && !visible && !IsPauseScreenOpen())
                DrawCurrentMatchOverlay();

            if (!visible)
                return;

            FollowerAiOverlayInputBlocker.Show(InputBlockerOwner);
            HandleKeyboardCloseShortcut();
            EnsureWindowRectInitialized();
            windowRect = GUI.ModalWindow(WindowID, windowRect, DrawWindow, "Fight Pit Tournament Ledger", windowStyle);
            FollowerAiOverlayInputBlocker.ConsumeImGuiPointerEvents();
        }

        private static bool IsPauseScreenOpen()
        {
            return FollowerAiGameState.IsSimulationPaused();
        }

        private static void DrawCurrentMatchOverlay()
        {
            var current = FollowerAiTournamentLedger.GetCurrentMatch();
            var fontSize = GetFontSize();
            var height = Mathf.Clamp(fontSize * 3.1f, 92f, 190f);
            var rect = new Rect(0f, 0f, Screen.width, height);

            GUILayout.BeginArea(rect, currentMatchBoxStyle);
            if (current == null)
            {
                GUILayout.Label("Tournament: no undecided matches", currentMatchTitleStyle);
                GUILayout.EndArea();
                return;
            }

            var round = string.IsNullOrWhiteSpace(current.Round)
                ? $"Match {current.Index}"
                : $"{current.Round}  -  Match {current.Index}";
            GUILayout.Label(round, currentMatchTitleStyle);

            var left = string.IsNullOrWhiteSpace(current.A) ? "TBD" : current.A;
            var right = string.IsNullOrWhiteSpace(current.B) ? "TBD" : current.B;
            var leftRoll = string.IsNullOrWhiteSpace(current.ARoll) ? string.Empty : $"  [{current.ARoll}]";
            var rightRoll = string.IsNullOrWhiteSpace(current.BRoll) ? string.Empty : $"  [{current.BRoll}]";
            GUILayout.Label($"{left}{leftRoll}  vs  {right}{rightRoll}", currentMatchNameStyle);
            GUILayout.EndArea();
        }

        private static void DrawWindow(int windowID)
        {
            var state = FollowerAiTournamentLedger.State;
            if (archiveViewVisible)
            {
                DrawArchiveView(state);
                return;
            }

            var draft = state.Draft;

            GUILayout.BeginVertical();
            mainScroll = GUILayout.BeginScrollView(mainScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            DrawDraftHeader(draft);
            GUILayout.Space(12f);
            DrawEntrants(draft);
            GUILayout.Space(12f);
            DrawMatches(draft);
            GUILayout.Space(12f);
            DrawChampion(draft);
            GUILayout.Space(12f);
            DrawArchiveSummary(state);

            GUILayout.EndScrollView();
            DrawFooter();
            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width - 40f, 28f));
        }

        private static void DrawDraftHeader(FollowerAiTournamentDraft draft)
        {
            GUILayout.Label("Tournament", headerStyle);
            GUILayout.BeginVertical(sectionStyle);
            draft.TournamentName = TextField("Name", draft.TournamentName);
            draft.TournamentDate = TextField("Date", draft.TournamentDate);
            draft.TournamentTheme = TextField("Theme", draft.TournamentTheme);
            draft.SinEarned = IntField("Sin earned", draft.SinEarned);
            var previousSpecialRewards = draft.SpecialRewards ?? string.Empty;
            draft.SpecialRewards = TextArea("Special rewards", draft.SpecialRewards, GetFontSize() * 2.2f);
            if (!string.Equals(previousSpecialRewards, draft.SpecialRewards, StringComparison.Ordinal) &&
                draft.Champion != null &&
                (string.IsNullOrWhiteSpace(draft.Champion.ChampionRewards) ||
                 string.Equals(draft.Champion.ChampionRewards, previousSpecialRewards, StringComparison.Ordinal)))
            {
                draft.Champion.ChampionRewards = draft.SpecialRewards;
            }
            draft.TournamentNotes = TextArea("Notes", draft.TournamentNotes, GetFontSize() * 2.6f);
            GUILayout.EndVertical();
        }

        private static void DrawEntrants(FollowerAiTournamentDraft draft)
        {
            GUILayout.Label("Entrants", headerStyle);
            GUILayout.BeginHorizontal();
            var fontSize = GetFontSize();
            GUILayout.BeginVertical(sectionStyle, GUILayout.Width(fontSize * 10f));
            GUILayout.Label("Available Followers", labelStyle);
            var choices = FollowerAiTournamentLedger.GetAvailableFollowerChoices();
            followerScroll = GUILayout.BeginScrollView(followerScroll, GUILayout.Height(fontSize * 8f));
            if (choices.Count == 0)
            {
                GUILayout.Label("No available followers found right now.", smallLabelStyle);
            }
            else
            {
                foreach (var fact in choices)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{fact.Name}  L{fact.Level}", smallLabelStyle, GUILayout.Width(fontSize * 6.2f));
                    if (GUILayout.Button("Add", smallButtonStyle, GUILayout.Width(fontSize * 2.5f), GUILayout.Height(fontSize * 0.95f)))
                    {
                        FollowerAiTournamentLedger.AddFollowerToFirstOpenSlot(fact);
                        SetStatus($"Added {fact.Name}.");
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Refresh Statuses", buttonStyle, GUILayout.Height(fontSize * 1.15f)))
            {
                var changed = FollowerAiTournamentLedger.ReconcileLiveFollowerStatuses(saveIfChanged: true);
                SetStatus(changed == 1 ? "Updated 1 entrant status." : $"Updated {changed} entrant statuses.");
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(sectionStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("#", smallLabelStyle, GUILayout.Width(fontSize * 2f));
            GUILayout.Label("Follower", smallLabelStyle, GUILayout.Width(fontSize * 9f));
            GUILayout.Label("Seed", smallLabelStyle, GUILayout.Width(fontSize * 4f));
            GUILayout.Label("Status", smallLabelStyle, GUILayout.Width(fontSize * 7f));
            GUILayout.Label("Notes", smallLabelStyle, GUILayout.Width(fontSize * 13f));
            GUILayout.EndHorizontal();

            foreach (var entrant in draft.Entrants)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(entrant.Slot.ToString(), smallLabelStyle, GUILayout.Width(fontSize * 2f));
                entrant.Name = GUILayout.TextField(entrant.Name ?? string.Empty, textFieldStyle, GUILayout.Width(fontSize * 9f), GUILayout.Height(fontSize * 1.05f));
                entrant.Seed = GUILayout.TextField(entrant.Seed ?? string.Empty, textFieldStyle, GUILayout.Width(fontSize * 4f), GUILayout.Height(fontSize * 1.05f));
                GUILayout.Label(string.IsNullOrWhiteSpace(entrant.Status) ? "Alive" : entrant.Status, smallLabelStyle, GUILayout.Width(fontSize * 7f));
                entrant.Notes = GUILayout.TextField(entrant.Notes ?? string.Empty, textFieldStyle, GUILayout.Width(fontSize * 13f), GUILayout.Height(fontSize * 1.05f));
                if (GUILayout.Button("Clear", smallButtonStyle, GUILayout.Width(fontSize * 4.5f), GUILayout.Height(fontSize * 1.05f)))
                {
                    FollowerAiTournamentLedger.ClearEntrant(entrant);
                    SetStatus("Cleared entrant slot.");
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Apply Selected Followers To Bracket", buttonStyle, GUILayout.Height(fontSize * 1.15f)))
            {
                var changed = FollowerAiTournamentLedger.ApplyEntrantsToBracketTemplate();
                SetStatus(changed == 1
                    ? "Updated 1 bracket field."
                    : $"Updated {changed} bracket fields.");
            }
        }

        private static void DrawMatches(FollowerAiTournamentDraft draft)
        {
            GUILayout.Label("Matches", headerStyle);
            if (FollowerAiTournamentLedger.ApplyWinnerPropagation(saveIfChanged: true))
                SetStatus("Advanced decided winners into later matches.");
            GUILayout.BeginVertical(sectionStyle);
            var fontSize = GetFontSize();
            matchScroll = GUILayout.BeginScrollView(matchScroll, GUILayout.Height(fontSize * 13.5f));
            for (var i = 0; i < draft.Matches.Count; i++)
            {
                var match = draft.Matches[i];
                GUILayout.BeginVertical(sectionStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Match {i + 1}", labelStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", smallButtonStyle, GUILayout.Width(fontSize * 4.2f), GUILayout.Height(fontSize * 0.95f)))
                {
                    FollowerAiTournamentLedger.RemoveMatchAt(i);
                    SetStatus("Removed match.");
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    break;
                }
                GUILayout.EndHorizontal();

                match.Round = LabeledFieldBlock("Round", match.Round);
                GUILayout.BeginHorizontal();
                match.A = LabeledFieldBlock("Fighter A", match.A, fontSize * 8f);
                match.ARoll = LabeledFieldBlock("Roll", match.ARoll, fontSize * 3.5f);
                match.B = LabeledFieldBlock("Fighter B", match.B, fontSize * 8f);
                match.BRoll = LabeledFieldBlock("Roll", match.BRoll, fontSize * 3.5f);
                GUILayout.EndHorizontal();
                if (FollowerAiTournamentLedger.ApplyRollOutcome(match))
                    SetStatus($"Match {i + 1}: roll result applied.");
                GUILayout.BeginHorizontal();
                if (FollowerAiTournamentLedger.MatchHasDecisiveRolls(match))
                {
                    GUILayout.BeginVertical(GUILayout.Width(fontSize * 8f));
                    GUILayout.Label("Winner", smallLabelStyle, GUILayout.ExpandWidth(true));
                    GUILayout.Label(string.IsNullOrWhiteSpace(match.Winner) ? "-" : match.Winner, labelStyle, GUILayout.Height(fontSize * 1.05f));
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical(GUILayout.Width(fontSize * 8f));
                    GUILayout.Label("Bad Target", smallLabelStyle, GUILayout.ExpandWidth(true));
                    GUILayout.Label(string.IsNullOrWhiteSpace(match.BadTarget) ? "-" : match.BadTarget, labelStyle, GUILayout.Height(fontSize * 1.05f));
                    GUILayout.EndVertical();
                }
                else
                {
                    DrawManualOutcomeSelector(match, i + 1, fontSize);
                }
                match.BadThing = LabeledFieldBlock("Bad Thing", match.BadThing, fontSize * 9f);
                GUILayout.EndHorizontal();
                match.Notes = TextArea("Notes", match.Notes, fontSize * 2.2f);
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Add Match", buttonStyle, GUILayout.Height(fontSize * 1.15f)))
            {
                FollowerAiTournamentLedger.AddBlankMatch();
                SetStatus("Added match.");
            }
            if (GUILayout.Button("Add 10 Follower Bracket Template", buttonStyle, GUILayout.Height(fontSize * 1.15f)))
            {
                FollowerAiTournamentLedger.AddTenFollowerBracketTemplate();
                SetStatus("Added 10 follower bracket template.");
            }
            GUILayout.EndVertical();
        }

        private static void DrawManualOutcomeSelector(FollowerAiTournamentMatch match, int matchNumber, float fontSize)
        {
            GUILayout.BeginVertical(GUILayout.Width(fontSize * 16f));
            GUILayout.Label("Winner", smallLabelStyle, GUILayout.ExpandWidth(true));

            var options = BuildMatchOutcomeOptions(match);
            var selected = GetOutcomeSelectionIndex(match, options);
            var selectedLabel = selected > 0 && selected < options.Length ? options[selected] : "Select";
            if (GUILayout.Button(selectedLabel, buttonStyle, GUILayout.Height(fontSize * 1.1f), GUILayout.ExpandWidth(true)))
            {
                if (openOutcomeDropdowns.Contains(matchNumber))
                    openOutcomeDropdowns.Remove(matchNumber);
                else
                    openOutcomeDropdowns.Add(matchNumber);
            }

            if (openOutcomeDropdowns.Contains(matchNumber))
            {
                GUILayout.BeginVertical(sectionStyle);
                for (var i = 1; i < options.Length; i++)
                {
                    if (!GUILayout.Button(options[i], smallButtonStyle, GUILayout.Height(fontSize * 0.95f), GUILayout.ExpandWidth(true)))
                        continue;

                    openOutcomeDropdowns.Remove(matchNumber);
                    if (FollowerAiTournamentLedger.ApplyManualOutcome(match, options[i]))
                        SetStatus($"Match {matchNumber}: manual result applied.");
                    break;
                }
                GUILayout.EndVertical();
            }

            GUILayout.Label($"Bad Target: {(string.IsNullOrWhiteSpace(match.BadTarget) ? "-" : match.BadTarget)}", smallLabelStyle, GUILayout.Height(fontSize * 0.95f));
            GUILayout.EndVertical();
        }

        private static string[] BuildMatchOutcomeOptions(FollowerAiTournamentMatch match)
        {
            var options = new List<string> { "Select" };
            if (!string.IsNullOrWhiteSpace(match?.A))
                options.Add(match.A);
            if (!string.IsNullOrWhiteSpace(match?.B) &&
                !options.Any(option => string.Equals(option, match.B, StringComparison.Ordinal)))
            {
                options.Add(match.B);
            }

            return options.ToArray();
        }

        private static int GetOutcomeSelectionIndex(FollowerAiTournamentMatch match, string[] options)
        {
            if (match == null || options == null || options.Length == 0 || string.IsNullOrWhiteSpace(match.Winner))
                return 0;

            for (var i = 1; i < options.Length; i++)
            {
                if (string.Equals(options[i], match.Winner, StringComparison.Ordinal))
                    return i;
            }

            return 0;
        }

        private static void DrawChampion(FollowerAiTournamentDraft draft)
        {
            GUILayout.Label("Champion", headerStyle);
            GUILayout.BeginVertical(sectionStyle);
            var champion = draft.Champion;
            champion.WinnerOriginal = TextField("Winner original name", champion.WinnerOriginal);
            champion.WinnerName = TextField("Winner new name", champion.WinnerName);
            champion.WinnerTitle = TextField("Title", champion.WinnerTitle);
            champion.WinnerRole = TextField("Role", champion.WinnerRole);
            champion.WinnerJob = TextField("Job", champion.WinnerJob);
            champion.AvatarNotes = TextArea("Avatar notes", champion.AvatarNotes, GetFontSize() * 2f);
            champion.ChampionRewards = TextArea("Champion rewards", champion.ChampionRewards, GetFontSize() * 2.2f);
            GUILayout.EndVertical();
        }

        private static void DrawArchiveSummary(FollowerAiTournamentState state)
        {
            GUILayout.Label("Archive", headerStyle);
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label($"Archived tournaments: {state.Archive.Count}", labelStyle);
            if (GUILayout.Button("View Champions", buttonStyle, GUILayout.Height(GetFontSize() * 1.15f)))
            {
                archiveViewVisible = true;
                SetStatus("Viewing tournament champions.");
            }
            if (GUILayout.Button("Archive Current Draft", buttonStyle, GUILayout.Height(GetFontSize() * 1.15f)))
            {
                if (FollowerAiTournamentLedger.ArchiveCurrentDraft(out var archiveMessage))
                {
                    mainScroll = Vector2.zero;
                    matchScroll = Vector2.zero;
                    openOutcomeDropdowns.Clear();
                }
                SetStatus(archiveMessage);
            }
            GUILayout.EndVertical();
        }

        private static void DrawArchiveView(FollowerAiTournamentState state)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Champions", headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back To Ledger", buttonStyle, GUILayout.Width(GetFontSize() * 6f), GUILayout.Height(GetFontSize() * 1.15f)))
            {
                archiveViewVisible = false;
                SetStatus("Returned to tournament ledger.");
            }
            GUILayout.EndHorizontal();

            archiveScroll = GUILayout.BeginScrollView(archiveScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var archive = state?.Archive ?? new List<FollowerAiTournamentArchiveEntry>();
            if (archive.Count == 0)
            {
                GUILayout.BeginVertical(sectionStyle);
                GUILayout.Label("No champions archived yet.", labelStyle);
                GUILayout.EndVertical();
            }
            else
            {
                foreach (var entry in archive.OrderByDescending(item => item?.CreatedAt ?? System.DateTime.MinValue))
                    DrawArchivedChampion(entry);
            }
            GUILayout.EndScrollView();
            DrawFooter();
            GUILayout.EndVertical();
        }

        private static void DrawArchivedChampion(FollowerAiTournamentArchiveEntry entry)
        {
            if (entry == null)
                return;

            var fontSize = GetFontSize();
            var id = string.IsNullOrWhiteSpace(entry.ID) ? entry.CreatedAt.Ticks.ToString() : entry.ID;
            var champion = entry.Champion ?? new FollowerAiTournamentChampion();
            var winner = FirstNonEmpty(champion.WinnerName, champion.WinnerOriginal, FindFinalWinner(entry), "Unknown Champion");
            var title = string.IsNullOrWhiteSpace(champion.WinnerTitle) ? winner : $"{winner}, {champion.WinnerTitle}";
            var expanded = expandedArchiveEntries.Contains(id);

            GUILayout.BeginVertical(sectionStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(expanded ? "Hide Overview" : "Show Overview", smallButtonStyle, GUILayout.Width(fontSize * 6f), GUILayout.Height(fontSize * 1.0f)))
            {
                if (expanded)
                    expandedArchiveEntries.Remove(id);
                else
                    expandedArchiveEntries.Add(id);
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"{SafeText(entry.TournamentName, "Unnamed Tournament")}  |  {SafeText(entry.TournamentDate, "No date")}", smallLabelStyle);
            if (!string.IsNullOrWhiteSpace(champion.ChampionRewards))
                GUILayout.Label($"Rewards: {champion.ChampionRewards}", smallLabelStyle);

            if (expandedArchiveEntries.Contains(id))
            {
                GUILayout.Space(fontSize * 0.35f);
                DrawArchiveTextBlock("Theme", entry.TournamentTheme);
                DrawArchiveTextBlock("Special rewards", entry.SpecialRewards);
                DrawArchiveTextBlock("Champion role", champion.WinnerRole);
                DrawArchiveTextBlock("Champion job", champion.WinnerJob);
                DrawArchiveTextBlock("Avatar notes", champion.AvatarNotes);
                DrawArchiveTextBlock("Tournament notes", entry.TournamentNotes);

                GUILayout.Label("Entrants", labelStyle);
                foreach (var entrant in entry.Entrants ?? new List<FollowerAiTournamentEntrant>())
                {
                    if (entrant == null || string.IsNullOrWhiteSpace(entrant.Name))
                        continue;
                    GUILayout.Label($"#{entrant.Seed}: {entrant.Name} - {SafeText(entrant.Status, "Alive")}", smallLabelStyle);
                }

                GUILayout.Label("Tournament Overview", labelStyle);
                foreach (var match in entry.Matches ?? new List<FollowerAiTournamentMatch>())
                {
                    if (match == null)
                        continue;
                    var winnerText = string.IsNullOrWhiteSpace(match.Winner) ? "undecided" : match.Winner;
                    var badText = string.IsNullOrWhiteSpace(match.BadTarget) ? "none" : match.BadTarget;
                    var punishment = string.IsNullOrWhiteSpace(match.BadThing) ? string.Empty : $" ({match.BadThing})";
                    GUILayout.Label($"{SafeText(match.Round, "Match")}: {SafeText(match.A, "TBD")} vs {SafeText(match.B, "TBD")} -> winner {winnerText}; bad target {badText}{punishment}", smallLabelStyle);
                }
            }
            GUILayout.EndVertical();
        }

        private static void DrawArchiveTextBlock(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            GUILayout.Label($"{label}: {value}", smallLabelStyle);
        }

        private static string FindFinalWinner(FollowerAiTournamentDraft draft)
        {
            return (draft?.Matches ?? new List<FollowerAiTournamentMatch>())
                .FirstOrDefault(match => match != null && string.Equals(match.Round, "Final", System.StringComparison.OrdinalIgnoreCase))
                ?.Winner ?? string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static void DrawFooter()
        {
            GUILayout.BeginHorizontal();
            var fontSize = GetFontSize();
            if (GUILayout.Button("Save", buttonStyle, GUILayout.Width(fontSize * 3.5f), GUILayout.Height(fontSize * 1.15f)))
            {
                FollowerAiTournamentLedger.Save();
                SetStatus("Saved tournament ledger.");
            }
            if (GUILayout.Button("Close", buttonStyle, GUILayout.Width(fontSize * 3.5f), GUILayout.Height(fontSize * 1.15f)))
                Hide();
            GUILayout.FlexibleSpace();
            var text = Time.realtimeSinceStartup < statusMessageUntilRealtime ? statusMessage : "F8 toggles this ledger.";
            GUILayout.Label(text, smallLabelStyle, GUILayout.Width(fontSize * 9f));
            GUILayout.EndHorizontal();
        }

        private static string TextField(string label, string value)
        {
            GUILayout.Label(label, labelStyle, GUILayout.ExpandWidth(true));
            value = GUILayout.TextField(value ?? string.Empty, textFieldStyle, GUILayout.Height(GetFontSize() * 1.15f), GUILayout.ExpandWidth(true));
            return value;
        }

        private static string TextFieldInline(string label, string value, float width)
        {
            var labelWidth = Mathf.Clamp(label.Length * GetFontSize() * 0.45f, GetFontSize() * 1.2f, GetFontSize() * 5f);
            GUILayout.Label(label, smallLabelStyle, GUILayout.Width(labelWidth));
            return GUILayout.TextField(value ?? string.Empty, textFieldStyle, GUILayout.Width(width), GUILayout.Height(GetFontSize() * 1.05f));
        }

        private static string LabeledFieldBlock(string label, string value, float width = 0f)
        {
            GUILayout.BeginVertical(width > 0f ? GUILayout.Width(width) : GUILayout.ExpandWidth(true));
            GUILayout.Label(label, smallLabelStyle, GUILayout.ExpandWidth(true));
            value = GUILayout.TextField(value ?? string.Empty, textFieldStyle, GUILayout.Height(GetFontSize() * 1.05f), GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();
            return value;
        }

        private static string TextArea(string label, string value, float height)
        {
            GUILayout.Label(label, labelStyle);
            return GUILayout.TextArea(value ?? string.Empty, textAreaStyle, GUILayout.Height(Mathf.Max(height, GetFontSize() * 1.6f)));
        }

        private static int IntField(string label, int value)
        {
            var text = TextField(label, value.ToString());
            return int.TryParse(text, out var parsed) ? parsed : value;
        }

        private static void SetStatus(string message)
        {
            statusMessage = message;
            statusMessageUntilRealtime = Time.realtimeSinceStartup + 3f;
        }

        private static void EnsureStyles()
        {
            var fontSize = GetFontSize();
            if (labelStyle != null && styleFontSize == fontSize)
                return;

            styleFontSize = fontSize;
            windowBackground = FollowerAiOverlayGui.SolidTexture(new Color(0.02f, 0.025f, 0.025f, 0.96f));
            sectionBackground = FollowerAiOverlayGui.SolidTexture(new Color(0.01f, 0.012f, 0.012f, 0.88f));
            fieldBackground = FollowerAiOverlayGui.SolidTexture(new Color(0.14f, 0.15f, 0.15f, 0.98f));
            currentMatchBackground = FollowerAiOverlayGui.SolidTexture(new Color(0f, 0f, 0f, 0.94f));
            windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = Mathf.Max(12, fontSize / 3),
                padding = new RectOffset(12, 12, 28, 12)
            };
            FollowerAiOverlayGui.ApplyBackground(windowStyle, windowBackground);
            sectionStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 6, 6)
            };
            FollowerAiOverlayGui.ApplyBackground(sectionStyle, sectionBackground);
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = false
            };
            smallLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(20, Mathf.RoundToInt(fontSize * 0.76f)),
                wordWrap = false
            };
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize + 4,
                fontStyle = FontStyle.Bold
            };
            currentMatchBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(18, 18, 12, 12),
                margin = new RectOffset(0, 0, 0, 0)
            };
            FollowerAiOverlayGui.ApplyBackground(currentMatchBoxStyle, currentMatchBackground);
            currentMatchTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(24, Mathf.RoundToInt(fontSize * 0.72f)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
            currentMatchNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = fontSize,
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(0, 8, 4, 8),
                normal = { textColor = Color.white },
                focused = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };
            FollowerAiOverlayGui.ApplyBackground(textFieldStyle, fieldBackground);
            textAreaStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = fontSize,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 8, 4, 8),
                normal = { textColor = Color.white },
                focused = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white }
            };
            FollowerAiOverlayGui.ApplyBackground(textAreaStyle, fieldBackground);
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                wordWrap = false
            };
            smallButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(20, Mathf.RoundToInt(fontSize * 0.76f)),
                wordWrap = false
            };
        }

        private static void EnsureWindowRectInitialized()
        {
            var targetWidth = Mathf.Clamp(Screen.width * 0.96f, 920f, Screen.width - 16f);
            var targetHeight = Mathf.Clamp(Screen.height * 0.94f, 620f, Screen.height - 16f);

            if (!windowRectInitialized)
            {
                windowRect = FollowerAiOverlayGui.ClampToScreen(new Rect(
                    Mathf.Max(20f, (Screen.width - targetWidth) * 0.5f),
                    Mathf.Max(20f, (Screen.height - targetHeight) * 0.5f),
                    targetWidth,
                    targetHeight),
                    760f,
                    520f,
                    4f);
                windowRectInitialized = true;
                return;
            }

            windowRect = FollowerAiOverlayGui.ClampToScreen(windowRect, 760f, 520f, 4f);
        }

        private static int GetFontSize()
        {
            return FollowerAiOverlayGui.GetScaledFontSize(18, 72);
        }

        private static void HandleKeyboardCloseShortcut()
        {
            var ev = Event.current;
            if (ev == null || ev.type != EventType.KeyDown || ev.keyCode != KeyCode.Escape)
                return;

            Hide();
            ev.Use();
        }

    }
}
