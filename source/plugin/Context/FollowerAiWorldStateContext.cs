using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace COTL_AL_NPCs
{
    internal static class FollowerAiWorldStateContext
    {
        internal static string BuildPromptContext()
        {
            var lines = new List<string>
            {
                $"current_day={SafeCurrentDay()}",
                BuildFaithLine(),
                BuildHungerLine(),
                BuildSanitationGaugeLine(),
                BuildWeatherLine(),
            };

            return string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        private static string BuildFaithLine()
        {
            if (!TryReadCultFaith(out var current))
                return "cult_faith=unavailable; threshold=unknown";

            var percent = NormalizePercent(current, 100f);
            return $"cult_faith={percent:0}%; state={ClassifyLowHigh(percent, 35f, 70f, "low", "stable", "high")}; thresholds=low_below_35%, stable_35_to_69%, high_70%_or_more";
        }

        private static string BuildHungerLine()
        {
            var satiationValues = FollowerAiFollowerFacts.GetCurrentFollowers()
                .Where(fact => fact != null && fact.AvailabilityStatus == Lamb.UI.FollowerSelect.FollowerSelectEntry.Status.Available)
                .Where(fact => fact.Satiation >= 0f)
                .Select(fact => NormalizePercent(fact.Satiation, 100f))
                .ToList();

            if (satiationValues.Count <= 0)
                return "cult_hunger=unavailable; threshold=unknown";

            var lowCount = satiationValues.Count(value => value < 25f);
            var criticalCount = satiationValues.Count(value => value < 10f);
            var average = satiationValues.Average();
            return $"cult_hunger=average_satiation_{average:0}%; hungry_followers={lowCount}; starving_followers={criticalCount}; state={ClassifyLowHigh(average, 25f, 65f, "hungry", "fed", "well_fed")}; vanilla_trigger_thresholds=hunger_concern_below_25%, starvation_risk_below_10%";
        }

        private static string BuildSanitationGaugeLine()
        {
            if (!FollowerAiWorldStateReflection.TryReadStaticFloat("IllnessBar", "IllnessNormalized", out var normalized))
                return "Sanitation=unavailable; threshold=unknown";

            var percent = NormalizePercent(normalized, 1f);
            var state = ClassifyLowHigh(percent, 35f, 70f, "clean", "strained", "hazardous");
            return $"Sanitation={state}; gauge={percent:0}% dirty; thresholds=clean_below_35%, strained_35_to_69%, hazardous_70%_or_more";
        }

        private static string BuildWeatherLine()
        {
            var weather = ReadWeatherText();
            return string.IsNullOrWhiteSpace(weather)
                ? "Weather=unavailable"
                : $"Weather={weather}";
        }

        private static bool TryReadCultFaith(out float current)
        {
            current = 0f;

            var type = FollowerAiWorldStateReflection.FindType("CultFaithManager");
            var instance = FollowerAiWorldStateReflection.GetStaticMemberValue(type, "Instance", "instance");
            if (FollowerAiWorldStateReflection.TryReadFloatMember(instance, out current, "CurrentFaith", "Faith", "_faith", "faith"))
                return true;

            if (FollowerAiWorldStateReflection.TryReadStaticFloat("CultFaithManager", "CultFaithNormalised", out current))
                return true;

            return FollowerAiWorldStateReflection.TryReadStaticFloat("CultFaithManager", "CultFaithNormalized", out current);
        }

        private static string ReadWeatherText()
        {
            var controllerType = FollowerAiWorldStateReflection.FindType("WeatherSystemController");
            var controller = FollowerAiWorldStateReflection.GetStaticMemberValue(controllerType, "Instance", "instance");
            var weatherType = FollowerAiWorldStateReflection.ReadTextMember(controller, "CurrentWeatherType", "currentWeatherType", "CurrentWeather", "weather");
            var strength = FollowerAiWorldStateReflection.ReadTextMember(controller, "CurrentWeatherStrength", "currentWeatherStrength");
            if (!string.IsNullOrWhiteSpace(weatherType))
                return DescribeWeather(weatherType, strength);

            if (FollowerAiWorldStateReflection.TryReadBoolMember(controller, out var isRaining, "IsRaining") && isRaining)
                return "rain";
            if (FollowerAiWorldStateReflection.TryReadBoolMember(controller, out var isSnowing, "IsSnowing") && isSnowing)
                return "snow";
            if (FollowerAiWorldStateReflection.TryReadBoolMember(controller, out var isWindy, "IsWindy") && isWindy)
                return "wind";

            var seasonsType = FollowerAiWorldStateReflection.FindType("SeasonsManager");
            var seasons = FollowerAiWorldStateReflection.GetStaticMemberValue(seasonsType, "Instance", "instance");
            var seasonWeather = FollowerAiWorldStateReflection.ReadTextMember(seasons, "CurrentWeatherEvent", "currentWeatherEvent", "WeatherEventID");
            if (string.IsNullOrWhiteSpace(seasonWeather))
                seasonWeather = FollowerAiWorldStateReflection.ReadStaticTextMember(seasonsType, "CurrentWeatherEvent", "currentWeatherEvent", "WeatherEventID");
            if (!string.IsNullOrWhiteSpace(seasonWeather))
                return DescribeWeather(seasonWeather, string.Empty);

            foreach (var fallbackTypeName in new[] { "WeatherManager", "WeatherSystem" })
            {
                var fallbackType = FollowerAiWorldStateReflection.FindType(fallbackTypeName);
                var fallback = FollowerAiWorldStateReflection.GetStaticMemberValue(fallbackType, "Instance", "instance");
                var value = FollowerAiWorldStateReflection.ReadTextMember(fallback, "CurrentWeather", "Weather", "CurrentWeatherType", "weather");
                if (string.IsNullOrWhiteSpace(value))
                    value = FollowerAiWorldStateReflection.ReadStaticTextMember(fallbackType, "CurrentWeather", "Weather", "CurrentWeatherType", "weather");
                if (!string.IsNullOrWhiteSpace(value))
                    return DescribeWeather(value, string.Empty);
            }

            return string.Empty;
        }

        private static string DescribeWeather(string weatherType, string strength)
        {
            var type = NormalizeWeatherToken(weatherType);
            var intensity = NormalizeWeatherToken(strength);
            if (string.IsNullOrWhiteSpace(type) ||
                type == "none" ||
                type == "default" ||
                type == "clear")
                return "clear skies; active weather effect=none";

            var intensityText = WeatherStrengthText(intensity);
            switch (type)
            {
                case "raining":
                case "rain":
                    return JoinWeatherWords(intensityText, "rain");
                case "windy":
                case "wind":
                    return JoinWeatherWords(intensityText, "wind");
                case "snowing":
                case "snow":
                    return intensity == "dusting"
                        ? "a light dusting of snow"
                        : JoinWeatherWords(intensityText, "snow");
                case "heat":
                case "heatwave":
                    return JoinWeatherWords(intensityText, "heatwave conditions");
                case "blizzard":
                    return JoinWeatherWords(intensityText, "blizzard");
                case "typhoon":
                    return JoinWeatherWords(intensityText, "typhoon");
                default:
                    return SplitCamelCase(weatherType).ToLowerInvariant();
            }
        }

        private static string WeatherStrengthText(string strength)
        {
            switch (strength)
            {
                case "light":
                    return "light";
                case "medium":
                    return "moderate";
                case "heavy":
                    return "heavy";
                case "extreme":
                    return "extreme";
                case "dusting":
                    return "light";
                default:
                    return string.Empty;
            }
        }

        private static string JoinWeatherWords(string intensity, string weather)
        {
            return string.IsNullOrWhiteSpace(intensity) ? weather : $"{intensity} {weather}";
        }

        private static string NormalizeWeatherToken(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
        }

        private static string SplitCamelCase(string value)
        {
            var text = (value ?? string.Empty).Trim().Replace("_", " ").Replace("-", " ");
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return System.Text.RegularExpressions.Regex.Replace(text, "([a-z])([A-Z])", "$1 $2");
        }

        private static int SafeCurrentDay()
        {
            try
            {
                return TimeManager.CurrentDay;
            }
            catch
            {
                return 0;
            }
        }

        private static float NormalizePercent(float value, float assumedMax)
        {
            if (value <= 1.01f)
                return Mathf.Clamp(value * 100f, 0f, 100f);

            return Mathf.Clamp(value / Math.Max(1f, assumedMax) * 100f, 0f, 100f);
        }

        private static string ClassifyLowHigh(float value, float lowThreshold, float highThreshold, string low, string middle, string high)
        {
            if (value < lowThreshold)
                return low;
            return value >= highThreshold ? high : middle;
        }
    }
}
