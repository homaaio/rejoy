// src/windows/OverlayLayout.cs
// Parses "input-overlay" (https://github.com/univrsal/input-overlay) preset packs
// so their .png/.json pairs can be reused as a visual gamepad-binding editor.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DualKey
{
    /// <summary>
    /// A single element ("button", "d-pad", background skin, ...) from an
    /// input-overlay preset .json file.
    /// </summary>
    public class OverlayElement
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("pos")]
        public int[] Pos { get; set; } = new int[] { 0, 0 };

        [JsonPropertyName("mapping")]
        public int[] Mapping { get; set; } = new int[] { 0, 0, 0, 0 };

        // NOTE: input-overlay presets store "z_level" inconsistently (sometimes a
        // string, sometimes a number). We intentionally don't map that field here -
        // System.Text.Json simply ignores properties with no matching member, which
        // sidesteps that inconsistency entirely.

        [JsonIgnore]
        public int X => (Pos != null && Pos.Length > 0) ? Pos[0] : 0;

        [JsonIgnore]
        public int Y => (Pos != null && Pos.Length > 1) ? Pos[1] : 0;

        [JsonIgnore]
        public int Width => (Mapping != null && Mapping.Length > 2) ? Mapping[2] : 0;

        [JsonIgnore]
        public int Height => (Mapping != null && Mapping.Length > 3) ? Mapping[3] : 0;

        /// <summary>Type 0 is always the background skin image - never a button.</summary>
        [JsonIgnore]
        public bool IsBackground => Type == 0;

        /// <summary>Type 5 is the little dot/handle that shows analog stick tilt - not a discrete button.</summary>
        [JsonIgnore]
        public bool IsAnalogIndicator => Type == 5;

        [JsonIgnore]
        public bool IsBindable => !IsBackground && !IsAnalogIndicator;
    }

    public class OverlayDocument
    {
        [JsonPropertyName("default_width")]
        public int DefaultWidth { get; set; }

        [JsonPropertyName("default_height")]
        public int DefaultHeight { get; set; }

        [JsonPropertyName("elements")]
        public List<OverlayElement> Elements { get; set; } = new List<OverlayElement>();
    }

    /// <summary>A fully loaded gamepad skin: the parsed layout plus the bitmap it refers to.</summary>
    public class GamepadLayout
    {
        public string JsonPath { get; }
        public string ImagePath { get; }
        public OverlayDocument Document { get; }
        public System.Drawing.Image Image { get; }

        private GamepadLayout(string jsonPath, string imagePath, OverlayDocument document, System.Drawing.Image image)
        {
            JsonPath = jsonPath;
            ImagePath = imagePath;
            Document = document;
            Image = image;
        }

        public static GamepadLayout Load(string jsonPath, string imagePath)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("Layout file not found.", jsonPath);
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Image file not found.", imagePath);

            string json = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            OverlayDocument doc;
            try
            {
                doc = JsonSerializer.Deserialize<OverlayDocument>(json, options) ?? new OverlayDocument();
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("That .json file doesn't look like an input-overlay layout: " + ex.Message, ex);
            }

            if (doc.Elements == null || doc.Elements.Count == 0)
                throw new InvalidDataException("That .json file doesn't contain any overlay elements.");

            // Load fully into memory and clone into a fresh Bitmap so the source file
            // can be closed/overwritten immediately (Image.FromStream otherwise keeps
            // the backing stream alive for the image's lifetime).
            System.Drawing.Image image;
            byte[] bytes = File.ReadAllBytes(imagePath);
            using (var ms = new MemoryStream(bytes))
            using (var temp = System.Drawing.Image.FromStream(ms))
            {
                image = new System.Drawing.Bitmap(temp);
            }

            return new GamepadLayout(jsonPath, imagePath, doc, image);
        }

        /// <summary>
        /// Best-guess mapping from an overlay element's id/name (e.g. "a", "lb", "dpad-up")
        /// to one of DualKey's internal binding action names. Returns null when unsure -
        /// the user can still assign it manually in the editor.
        /// </summary>
        public static string GuessAction(string overlayId)
        {
            if (string.IsNullOrWhiteSpace(overlayId)) return null;

            string key = overlayId.ToLowerInvariant();
            key = key.Replace(" ", "").Replace("-", "").Replace("_", "");

            switch (key)
            {
                case "a": case "cross": return "cross";
                case "b": case "circle": return "circle";
                case "x": case "square": return "square";
                case "y": case "triangle": return "triangle";
                case "select": case "back": return "select";
                case "start": return "start";
                case "guide": case "home": case "ps": case "psbutton": return "ps_button";
                case "l1": case "lb": case "leftbumper": case "leftshoulder": return "l1";
                case "r1": case "rb": case "rightbumper": case "rightshoulder": return "r1";
                case "l2": case "lt": case "lefttrigger":
                case "triggerl": case "triggerzl": case "zl": return "l2";
                case "r2": case "rt": case "righttrigger":
                case "triggerr": case "triggerzr": case "zr": return "r2";
                case "l3": case "ls": case "leftstickbutton": return "l3";
                case "r3": case "rs": case "rightstickbutton": return "r3";
                case "dpadup": case "up": return "dpad_up";
                case "dpaddown": case "down": return "dpad_down";
                case "dpadleft": case "left": return "dpad_left";
                case "dpadright": case "right": return "dpad_right";
                default: return null;
            }
        }
    }
}
