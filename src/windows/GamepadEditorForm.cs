// src/windows/GamepadEditorForm.cs
// Settings -> Edit Gamepad: import an input-overlay preset (.png + .json) and use
// it as a visual reference for binding each controller button to a keyboard key.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DualKey
{
    public class GamepadEditorForm : Form
    {
        private static readonly string[] ActionNames = new string[]
        {
            "(Unassigned)",
            "cross", "circle", "triangle", "square",
            "l1", "r1", "l2", "r2", "l3", "r3",
            "select", "start", "ps_button",
            "dpad_up", "dpad_down", "dpad_left", "dpad_right",
            "left_stick_up", "left_stick_down", "left_stick_left", "left_stick_right",
            "right_stick_up", "right_stick_down", "right_stick_left", "right_stick_right",
        };
        private const string Unassigned = "(Unassigned)";

        private class BindingRow
        {
            public OverlayElement Element;
            public string Action;
            public int KeyCode;
        }

        private readonly JoystickEmulator emulator;
        private GamepadLayout layout;
        private readonly List<BindingRow> rows = new List<BindingRow>();
        private int waitingRow = -1;

        private GamepadCanvas canvas;
        private DataGridView grid;
        private Label lblLayoutName;
        private Button btnImport;
        private Button btnOk;
        private Button btnCancel;
        private DataGridViewComboBoxColumn colAction;
        private DataGridViewButtonColumn colKey;

        public GamepadEditorForm(JoystickEmulator emulator)
        {
            this.emulator = emulator;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Edit Gamepad";
            Size = new Size(940, 620);
            MinimumSize = new Size(760, 480);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            BackColor = SystemColors.Control;
            ShowIcon = true;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { /* not critical */ }

            // ---- bottom button bar (Windows convention: OK then Cancel, bottom-right) ----
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 52 };
            var buttonFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 12, 12, 12)
            };

            btnCancel = new Button { Text = "Cancel", Size = new Size(90, 28), DialogResult = DialogResult.Cancel };
            btnOk = new Button { Text = "OK", Size = new Size(90, 28), DialogResult = DialogResult.OK, Margin = new Padding(0, 0, 8, 0) };
            btnOk.Click += OnOkClick;

            buttonFlow.Controls.Add(btnCancel);
            buttonFlow.Controls.Add(btnOk);
            bottomPanel.Controls.Add(buttonFlow);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            // ---- top header bar ----
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(12, 10, 12, 10) };

            btnImport = new Button
            {
                Text = "Import Layout (.png + .json)...",
                Location = new Point(12, 10),
                Size = new Size(210, 30)
            };
            btnImport.Click += OnImportClick;

            lblLayoutName = new Label
            {
                Text = "No layout loaded - click Import to load an input-overlay preset.",
                AutoSize = false,
                Location = new Point(232, 0),
                Size = new Size(660, 52),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DimGray
            };

            topPanel.Controls.Add(btnImport);
            topPanel.Controls.Add(lblLayoutName);

            // ---- main split area: image on the left, binding grid on the right ----
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 6
            };

            canvas = new GamepadCanvas { Dock = DockStyle.Fill };
            canvas.ElementSelected += OnCanvasElementSelected;

            var canvasHost = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(1) };
            canvasHost.Controls.Add(canvas);

            var rightPanel = new Panel { Dock = DockStyle.Fill };
            var lblHint = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "Click a highlighted area on the image, or a row below, to bind it.",
                ForeColor = Color.DimGray,
                Padding = new Padding(4, 6, 4, 0)
            };

            grid = BuildGrid();

            rightPanel.Controls.Add(grid);
            rightPanel.Controls.Add(lblHint);

            split.Panel1.Controls.Add(canvasHost);
            split.Panel2.Controls.Add(rightPanel);

            // Docking order matters: Fill-docked control must be added last so it
            // claims whatever space the Top/Bottom bars don't use.
            Controls.Add(bottomPanel);
            Controls.Add(topPanel);
            Controls.Add(split);

            Shown += (s, e) =>
            {
                try { split.SplitterDistance = (int)(ClientSize.Width * 0.58); }
                catch { /* ignore if the form is smaller than the minimum splitter distance */ }
            };
        }

        private DataGridView BuildGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                EditMode = DataGridViewEditMode.EditOnEnter,
            };
            g.RowTemplate.Height = 28;

            var colControl = new DataGridViewTextBoxColumn
            {
                Name = "colControl",
                HeaderText = "Control",
                ReadOnly = true,
                FillWeight = 85
            };

            colAction = new DataGridViewComboBoxColumn
            {
                Name = "colAction",
                HeaderText = "DualKey Action",
                FillWeight = 115,
                FlatStyle = FlatStyle.Flat
            };
            colAction.Items.AddRange(ActionNames);

            colKey = new DataGridViewButtonColumn
            {
                Name = "colKey",
                HeaderText = "Key",
                FillWeight = 90,
                UseColumnTextForButtonValue = false
            };

            g.Columns.Add(colControl);
            g.Columns.Add(colAction);
            g.Columns.Add(colKey);

            g.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (g.IsCurrentCellDirty && g.CurrentCell != null && g.CurrentCell.ColumnIndex == colAction.Index)
                    g.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            g.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < rows.Count && e.ColumnIndex == colAction.Index)
                {
                    string value = g.Rows[e.RowIndex].Cells[colAction.Index].Value as string;
                    rows[e.RowIndex].Action = value ?? Unassigned;
                }
            };
            g.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == colKey.Index)
                    BeginKeyCapture(e.RowIndex);
            };
            g.SelectionChanged += (s, e) =>
            {
                if (g.SelectedRows.Count > 0)
                {
                    int idx = g.SelectedRows[0].Index;
                    if (idx >= 0 && idx < rows.Count)
                        canvas.Highlight(rows[idx].Element);
                }
            };

            return g;
        }

        private void BeginKeyCapture(int rowIndex)
        {
            waitingRow = rowIndex;
            grid.Rows[rowIndex].Cells[colKey.Index].Value = "Press a key...";
            grid.InvalidateRow(rowIndex);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (waitingRow >= 0 && waitingRow < rows.Count)
            {
                int row = waitingRow;
                waitingRow = -1;

                if (keyData == Keys.Escape)
                {
                    // Escape cancels the capture instead of binding Escape itself.
                    grid.Rows[row].Cells[colKey.Index].Value = GetKeyName(rows[row].KeyCode);
                    grid.InvalidateRow(row);
                    return true;
                }

                Keys baseKey = keyData & Keys.KeyCode;
                int code = (int)baseKey;
                rows[row].KeyCode = code;
                grid.Rows[row].Cells[colKey.Index].Value = GetKeyName(code);
                grid.InvalidateRow(row);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnImportClick(object sender, EventArgs e)
        {
            using (var jsonDialog = new OpenFileDialog
            {
                Title = "Select the overlay layout file (.json)",
                Filter = "Input Overlay layout (*.json)|*.json|All files (*.*)|*.*"
            })
            {
                if (jsonDialog.ShowDialog(this) != DialogResult.OK) return;

                string startDir = Path.GetDirectoryName(jsonDialog.FileName);

                using (var pngDialog = new OpenFileDialog
                {
                    Title = "Select the matching image (.png)",
                    Filter = "PNG image (*.png)|*.png|All files (*.*)|*.*",
                    InitialDirectory = startDir
                })
                {
                    // Convenience: pre-select a same-named .png if one sits next to the .json.
                    string guess = Path.Combine(startDir, Path.GetFileNameWithoutExtension(jsonDialog.FileName) + ".png");
                    if (File.Exists(guess))
                        pngDialog.FileName = guess;

                    if (pngDialog.ShowDialog(this) != DialogResult.OK) return;

                    try
                    {
                        GamepadLayout loaded = GamepadLayout.Load(jsonDialog.FileName, pngDialog.FileName);
                        ApplyLayout(loaded);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "Couldn't load that layout:\n" + ex.Message, "Import Layout",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ApplyLayout(GamepadLayout loaded)
        {
            layout = loaded;
            List<OverlayElement> bindable = layout.Document.Elements.Where(el => el.IsBindable).ToList();

            rows.Clear();
            grid.Rows.Clear();

            foreach (var el in bindable)
            {
                string guess = GamepadLayout.GuessAction(el.Id);
                int keyCode = 0;
                if (guess != null && emulator.Bindings.ContainsKey(guess))
                    keyCode = emulator.Bindings[guess];

                var row = new BindingRow
                {
                    Element = el,
                    Action = guess ?? Unassigned,
                    KeyCode = keyCode
                };
                rows.Add(row);
                grid.Rows.Add(el.Id, row.Action, GetKeyName(row.KeyCode));
            }

            canvas.SetSkin(layout.Image, bindable);
            lblLayoutName.Text = Path.GetFileName(layout.ImagePath) + "  (" + Path.GetFileName(layout.JsonPath) + ")  \u2014 "
                + bindable.Count + " controls found";
            lblLayoutName.ForeColor = Color.Black;
        }

        private void OnCanvasElementSelected(object sender, OverlayElement element)
        {
            int idx = rows.FindIndex(r => r.Element == element);
            if (idx < 0) return;

            grid.ClearSelection();
            grid.Rows[idx].Selected = true;
            grid.CurrentCell = grid.Rows[idx].Cells[0];
            try { grid.FirstDisplayedScrollingRowIndex = Math.Max(0, idx - 2); }
            catch { /* row may already be visible */ }
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            foreach (var row in rows)
            {
                if (row.Action != Unassigned && !string.IsNullOrEmpty(row.Action))
                    emulator.UpdateBinding(row.Action, row.KeyCode);
            }
            // Form.DialogResult/Close() happen automatically because btnOk.DialogResult == OK.
        }

        private static string GetKeyName(int keyCode)
        {
            if (keyCode == 0) return "None";
            try { return ((Keys)keyCode).ToString(); }
            catch { return "None"; }
        }
    }
}
