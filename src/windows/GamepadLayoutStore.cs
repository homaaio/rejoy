// src/windows/GamepadLayoutStore.cs
// Persists the last-imported gamepad layout (the .json/.png pair from Edit Gamepad,
// plus the per-control action/key mapping the user set up) so it survives both
// reopening the dialog and restarting the whole application.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DualKey
{
    public class SavedGamepadRow
    {
        [JsonPropertyName("overlayId")]
        public string OverlayId { get; set; } = "";

        [JsonPropertyName("action")]
        public string Action { get; set; } = "";

        [JsonPropertyName("keyCode")]
        public int KeyCode { get; set; }
    }

    public class SavedGamepadLayout
    {
        [JsonPropertyName("jsonPath")]
        public string JsonPath { get; set; } = "";

        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; } = "";

        [JsonPropertyName("rows")]
        public List<SavedGamepadRow> Rows { get; set; } = new List<SavedGamepadRow>();
    }

    public static class GamepadLayoutStore
    {
        private static string StorePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DualKey");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "gamepad_layout.json");
            }
        }

        public static void Save(SavedGamepadLayout state)
        {
            try
            {
                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StorePath, json);
            }
            catch { /* non-fatal - the user just has to re-import next time */ }
        }

        /// <summary>Returns null if nothing was saved, or the saved layout's files no longer exist.</summary>
        public static SavedGamepadLayout TryLoad()
        {
            try
            {
                if (!File.Exists(StorePath)) return null;

                string json = File.ReadAllText(StorePath);
                var state = JsonSerializer.Deserialize<SavedGamepadLayout>(json);
                if (state == null) return null;
                if (!File.Exists(state.JsonPath) || !File.Exists(state.ImagePath)) return null;

                return state;
            }
            catch
            {
                return null;
            }
        }
    }
}
