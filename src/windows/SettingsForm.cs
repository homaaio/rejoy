// src/windows/SettingsForm.cs - полный файл с новой вкладкой
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DualKey
{
    public class SettingsForm : Form
    {
        private JoystickEmulator emulator;
        private TabControl tabControl;
        private TabPage bindingsPage;
        private TabPage sensitivityPage;
        private TabPage indicatorsPage;

        private Dictionary<string, Label> actionLabels;
        private Dictionary<string, Button> bindButtons;
        private string currentAction = null;

        private TrackBar deadzoneSlider;
        private Label deadzoneValue;

        // Индикаторы
        private CheckBox chkIndicatorsEnabled;
        private ComboBox cmbIndicatorMode;
        private TrackBar trackIndicatorSpeed;
        private Label lblIndicatorSpeed;
        private Panel[] indicatorPreviewPanels;
        private Button[] indicatorColorButtons;
        private Color[] indicatorColors;
        private NumericUpDown[] indicatorDelayBoxes;
        private Label[] indicatorDelayLabels;

        public SettingsForm(JoystickEmulator emulator)
        {
            this.emulator = emulator;
            this.Text = "DualKey Settings";
            this.Size = new Size(500, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            indicatorColors = new Color[] { Color.Red, Color.Red, Color.Red, Color.Red };

            tabControl = new TabControl { Dock = DockStyle.Fill };
            
            bindingsPage = new TabPage("Key Bindings");
            sensitivityPage = new TabPage("Sensitivity");
            indicatorsPage = new TabPage("Player Indicators");
            
            tabControl.TabPages.Add(bindingsPage);
            tabControl.TabPages.Add(sensitivityPage);
            tabControl.TabPages.Add(indicatorsPage);
            
            this.Controls.Add(tabControl);

            BuildBindingsPage();
            BuildSensitivityPage();
            BuildIndicatorsPage();
            PopulateBindings();
        }

        private void BuildBindingsPage()
        {
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            bindingsPage.Controls.Add(panel);

            actionLabels = new Dictionary<string, Label>();
            bindButtons = new Dictionary<string, Button>();

            string[] actions = {
                "left_stick_up", "left_stick_down", "left_stick_left", "left_stick_right",
                "right_stick_up", "right_stick_down", "right_stick_left", "right_stick_right",
                "cross", "circle", "triangle", "square",
                "l1", "r1", "l2", "r2",
                "l3", "r3", "select", "start", "ps_button",
                "dpad_up", "dpad_down", "dpad_left", "dpad_right"
            };

            string[] friendlyNames = {
                "Left Stick Up", "Left Stick Down", "Left Stick Left", "Left Stick Right",
                "Right Stick Up", "Right Stick Down", "Right Stick Left", "Right Stick Right",
                "Cross (A)", "Circle (B)", "Triangle (X)", "Square (Y)",
                "L1", "R1", "L2", "R2",
                "L3", "R3", "Select", "Start", "PS Button",
                "D-Pad Up", "D-Pad Down", "D-Pad Left", "D-Pad Right"
            };

            int y = 10;
            for (int i = 0; i < actions.Length; i++)
            {
                var label = new Label
                {
                    Text = friendlyNames[i],
                    Location = new Point(10, y),
                    Size = new Size(150, 25),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                panel.Controls.Add(label);

                var btn = new Button
                {
                    Text = GetKeyName(emulator.Bindings.ContainsKey(actions[i]) ? emulator.Bindings[actions[i]] : 0),
                    Location = new Point(170, y),
                    Size = new Size(130, 25),
                    Tag = actions[i],
                };
                btn.Click += OnBindButtonClick;
                btn.KeyDown += OnBindKeyDown;
                panel.Controls.Add(btn);

                actionLabels[actions[i]] = label;
                bindButtons[actions[i]] = btn;

                y += 30;
            }
        }

        private string GetKeyName(int keyCode)
        {
            try { return ((Keys)keyCode).ToString(); }
            catch { return "None"; }
        }

        private void PopulateBindings()
        {
            foreach (var kvp in emulator.Bindings)
            {
                if (bindButtons.ContainsKey(kvp.Key))
                {
                    bindButtons[kvp.Key].Text = GetKeyName(kvp.Value);
                }
            }
        }

        private void OnBindButtonClick(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            currentAction = (string)btn.Tag;
            btn.Text = "Press a key...";
            btn.BackColor = Color.LightYellow;
            btn.Focus();
        }

        private void OnBindKeyDown(object sender, KeyEventArgs e)
        {
            if (currentAction == null) return;

            Button btn = bindButtons[currentAction];
            int keyCode = (int)e.KeyCode;

            emulator.UpdateBinding(currentAction, keyCode);
            btn.Text = GetKeyName(keyCode);
            btn.BackColor = SystemColors.Control;
            currentAction = null;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void BuildSensitivityPage()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            sensitivityPage.Controls.Add(panel);

            var label = new Label
            {
                Text = "Stick Deadzone",
                Location = new Point(20, 30),
                Size = new Size(200, 25)
            };
            panel.Controls.Add(label);

            deadzoneSlider = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = (int)(emulator.Deadzone * 100),
                Location = new Point(20, 70),
                Size = new Size(250, 30),
                TickFrequency = 10
            };
            deadzoneSlider.ValueChanged += (s, e) =>
            {
                emulator.Deadzone = deadzoneSlider.Value / 100f;
                deadzoneValue.Text = emulator.Deadzone.ToString("F2");
            };
            panel.Controls.Add(deadzoneSlider);

            deadzoneValue = new Label
            {
                Text = emulator.Deadzone.ToString("F2"),
                Location = new Point(280, 70),
                Size = new Size(60, 25)
            };
            panel.Controls.Add(deadzoneValue);
        }

        private void BuildIndicatorsPage()
        {
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            indicatorsPage.Controls.Add(panel);

            int y = 10;

            // Включение/отключение индикаторов
            chkIndicatorsEnabled = new CheckBox
            {
                Text = "Enable player indicators",
                Location = new Point(10, y),
                Size = new Size(200, 25),
                Checked = true
            };
            panel.Controls.Add(chkIndicatorsEnabled);
            y += 35;

            // Режим мигания
            var lblMode = new Label
            {
                Text = "Mode:",
                Location = new Point(10, y),
                Size = new Size(50, 25)
            };
            panel.Controls.Add(lblMode);

            cmbIndicatorMode = new ComboBox
            {
                Location = new Point(70, y),
                Size = new Size(150, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbIndicatorMode.Items.AddRange(new string[] { "Static", "Blink All", "Running Light", "Alternating" });
            cmbIndicatorMode.SelectedIndex = 0;
            panel.Controls.Add(cmbIndicatorMode);
            y += 35;

            // Скорость мигания
            var lblSpeed = new Label
            {
                Text = "Speed (ms):",
                Location = new Point(10, y),
                Size = new Size(80, 25)
            };
            panel.Controls.Add(lblSpeed);

            trackIndicatorSpeed = new TrackBar
            {
                Minimum = 100,
                Maximum = 2000,
                Value = 500,
                Location = new Point(90, y),
                Size = new Size(200, 30),
                TickFrequency = 200
            };
            trackIndicatorSpeed.ValueChanged += (s, e) =>
            {
                lblIndicatorSpeed.Text = $"{trackIndicatorSpeed.Value} ms";
            };
            panel.Controls.Add(trackIndicatorSpeed);

            lblIndicatorSpeed = new Label
            {
                Text = "500 ms",
                Location = new Point(300, y),
                Size = new Size(80, 25)
            };
            panel.Controls.Add(lblIndicatorSpeed);
            y += 45;

            // Настройка каждого индикатора
            var lblIndicators = new Label
            {
                Text = "Indicator Settings:",
                Location = new Point(10, y),
                Size = new Size(150, 25),
                Font = new Font(Font, FontStyle.Bold)
            };
            panel.Controls.Add(lblIndicators);
            y += 30;

            indicatorPreviewPanels = new Panel[4];
            indicatorColorButtons = new Button[4];
            indicatorDelayBoxes = new NumericUpDown[4];
            indicatorDelayLabels = new Label[4];

            for (int i = 0; i < 4; i++)
            {
                int index = i;

                // Номер игрока
                var lblPlayer = new Label
                {
                    Text = $"Player {i + 1}:",
                    Location = new Point(10, y),
                    Size = new Size(60, 25)
                };
                panel.Controls.Add(lblPlayer);

                // Превью индикатора
                indicatorPreviewPanels[i] = new Panel
                {
                    Location = new Point(80, y),
                    Size = new Size(30, 25),
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = indicatorColors[i]
                };
                panel.Controls.Add(indicatorPreviewPanels[i]);

                // Кнопка выбора цвета
                indicatorColorButtons[i] = new Button
                {
                    Text = "Color...",
                    Location = new Point(120, y),
                    Size = new Size(70, 25)
                };
                int capturedIndex = i;
                indicatorColorButtons[i].Click += (s, e) =>
                {
                    ColorDialog cd = new ColorDialog();
                    cd.Color = indicatorColors[capturedIndex];
                    if (cd.ShowDialog() == DialogResult.OK)
                    {
                        indicatorColors[capturedIndex] = cd.Color;
                        indicatorPreviewPanels[capturedIndex].BackColor = cd.Color;
                    }
                };
                panel.Controls.Add(indicatorColorButtons[i]);

                // Задержка
                indicatorDelayLabels[i] = new Label
                {
                    Text = "Delay:",
                    Location = new Point(200, y),
                    Size = new Size(40, 25)
                };
                panel.Controls.Add(indicatorDelayLabels[i]);

                indicatorDelayBoxes[i] = new NumericUpDown
                {
                    Location = new Point(245, y),
                    Size = new Size(60, 25),
                    Minimum = 0,
                    Maximum = 5000,
                    Increment = 50,
                    Value = 0
                };
                panel.Controls.Add(indicatorDelayBoxes[i]);

                var lblMs = new Label
                {
                    Text = "ms",
                    Location = new Point(310, y),
                    Size = new Size(30, 25)
                };
                panel.Controls.Add(lblMs);

                y += 35;
            }

            // Кнопка сброса
            var btnReset = new Button
            {
                Text = "Reset to Defaults",
                Location = new Point(10, y + 10),
                Size = new Size(130, 30)
            };
            btnReset.Click += (s, e) =>
            {
                chkIndicatorsEnabled.Checked = true;
                cmbIndicatorMode.SelectedIndex = 0;
                trackIndicatorSpeed.Value = 500;
                for (int i = 0; i < 4; i++)
                {
                    indicatorColors[i] = Color.Red;
                    indicatorPreviewPanels[i].BackColor = Color.Red;
                    indicatorDelayBoxes[i].Value = 0;
                }
            };
            panel.Controls.Add(btnReset);
        }

        // Публичные методы для получения настроек индикаторов
        public bool IndicatorsEnabled => chkIndicatorsEnabled.Checked;
        public int IndicatorMode => cmbIndicatorMode.SelectedIndex;
        public int IndicatorSpeed => trackIndicatorSpeed.Value;
        public Color[] IndicatorColors => indicatorColors;
        public int[] IndicatorDelays
        {
            get
            {
                int[] delays = new int[4];
                for (int i = 0; i < 4; i++)
                    delays[i] = (int)indicatorDelayBoxes[i].Value;
                return delays;
            }
        }
    }
}
