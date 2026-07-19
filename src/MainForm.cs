using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ReJoy
{
    public partial class MainForm : Form
    {
        // WinMM структура для джойстика
        [StructLayout(LayoutKind.Sequential)]
        struct JOYINFOEX
        {
            public int dwSize;
            public int dwFlags;
            public int dwXpos;
            public int dwYpos;
            public int dwZpos;
            public int dwRpos;
            public int dwUpos;
            public int dwVpos;
            public int dwButtons;
            public int dwButtonNumber;
            public int dwPOV;
            public int dwReserved1;
            public int dwReserved2;
        }

        [DllImport("winmm.dll")]
        static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);

        // Компоненты
        private JoystickEmulator emulator;
        private JoystickHider hider;
        private WebServer webServer;
        private Timer updateTimer;

        // UI элементы
        private Label lblStatus, lblStickInfo, lblDeadzone, lblButtons;
        private Panel stickPanel;
        private CheckBox chkEmulation;
        private Button btnHide, btnWeb;
        private TrackBar deadzoneSlider;

        // Состояние джойстика
        private float leftX, leftY, rightX, rightY;
        private int buttons;
        private bool connected;

        public MainForm()
        {
            emulator = new JoystickEmulator();
            hider = new JoystickHider();

            // Запуск веб-сервера
            Task.Run(async () =>
            {
                webServer = new WebServer(GetJsonData);
                await webServer.StartAsync();
            });

            InitializeUI();

            // Таймер обновления (~60 FPS)
            updateTimer = new Timer();
            updateTimer.Interval = 16;
            updateTimer.Tick += UpdateJoystickState;
            updateTimer.Start();
        }

        private void InitializeUI()
        {
            // Настройка формы
            this.Text = "🎮 ReJoy - DualShock 3 Emulator v1.0";
            this.Size = new Size(520, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(26, 26, 46);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Заголовок
            Label lblTitle = new Label
            {
                Text = "ReJoy Controller",
                ForeColor = Color.FromArgb(233, 69, 96),
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                Location = new Point(20, 15),
                Size = new Size(300, 40)
            };
            this.Controls.Add(lblTitle);

            // Статус
            lblStatus = new Label
            {
                Text = "🔍 Поиск DualShock 3...",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 60),
                Size = new Size(470, 25)
            };
            this.Controls.Add(lblStatus);

            // Панель визуализации стиков
            stickPanel = new Panel
            {
                Location = new Point(20, 95),
                Size = new Size(220, 220),
                BackColor = Color.FromArgb(40, 40, 60),
                BorderStyle = BorderStyle.FixedSingle
            };
            stickPanel.Paint += DrawSticks;
            this.Controls.Add(stickPanel);

            // Информация о стиках
            lblStickInfo = new Label
            {
                Text = "Левый стик: 0.00, 0.00\nПравый стик: 0.00, 0.00",
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Consolas", 9),
                Location = new Point(260, 95),
                Size = new Size(230, 50)
            };
            this.Controls.Add(lblStickInfo);

            // Информация о кнопках
            lblButtons = new Label
            {
                Text = "Кнопки: нет нажатий",
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Consolas", 9),
                Location = new Point(260, 150),
                Size = new Size(230, 25)
            };
            this.Controls.Add(lblButtons);

            // Настройка Deadzone
            Label lblDzTitle = new Label
            {
                Text = "Deadzone:",
                ForeColor = Color.White,
                Location = new Point(260, 185),
                Size = new Size(80, 20)
            };
            this.Controls.Add(lblDzTitle);

            deadzoneSlider = new TrackBar
            {
                Minimum = 0,
                Maximum = 50,
                Value = 15,
                Location = new Point(260, 210),
                Size = new Size(200, 30)
            };
            deadzoneSlider.ValueChanged += (s, e) =>
            {
                emulator.Deadzone = deadzoneSlider.Value / 50f;
                lblDeadzone.Text = $"Deadzone: {emulator.Deadzone:F1}";
            };
            this.Controls.Add(deadzoneSlider);

            lblDeadzone = new Label
            {
                Text = "Deadzone: 0.3",
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(340, 185),
                Size = new Size(120, 20)
            };
            this.Controls.Add(lblDeadzone);

            // Чекбокс эмуляции клавиатуры
            chkEmulation = new CheckBox
            {
                Text = "⌨️ Эмуляция клавиатуры",
                ForeColor = Color.White,
                Location = new Point(260, 250),
                Size = new Size(220, 25),
                Font = new Font("Segoe UI", 10)
            };
            chkEmulation.CheckedChanged += (s, e) =>
            {
                emulator.Enabled = chkEmulation.Checked;
                if (!chkEmulation.Checked)
                {
                    emulator.ReleaseAll();
                    chkEmulation.ForeColor = Color.White;
                }
                else
                {
                    chkEmulation.ForeColor = Color.FromArgb(0, 255, 136);
                }
            };
            this.Controls.Add(chkEmulation);

            // Кнопка скрытия джойстика
            btnHide = new Button
            {
                Text = "👻 Скрыть джойстик",
                Location = new Point(260, 290),
                Size = new Size(220, 35),
                BackColor = Color.FromArgb(255, 170, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btnHide.Click += BtnHide_Click;
            this.Controls.Add(btnHide);

            // Кнопка веб-интерфейса
            btnWeb = new Button
            {
                Text = "🌐 Веб-интерфейс",
                Location = new Point(260, 335),
                Size = new Size(220, 35),
                BackColor = Color.FromArgb(233, 69, 96),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btnWeb.Click += (s, e) =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "http://localhost:8080",
                    UseShellExecute = true
                });
            this.Controls.Add(btnWeb);

            // Подсказка
            Label lblHint = new Label
            {
                Text = "💡 Для скрытия джойстика нужны права администратора\n   Веб-интерфейс: http://localhost:8080",
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Segoe UI", 8),
                Location = new Point(20, 385),
                Size = new Size(470, 40)
            };
            this.Controls.Add(lblHint);
        }

        private void DrawSticks(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Сетка для левого стика
            Pen gridPen = new Pen(Color.FromArgb(60, 60, 80), 1);
            g.DrawLine(gridPen, 60, 20, 60, 100);
            g.DrawLine(gridPen, 20, 60, 100, 60);

            // Левый стик (зеленый)
            Pen leftPen = new Pen(Color.FromArgb(0, 255, 136), 2);
            g.DrawEllipse(leftPen, 20, 20, 80, 80);
            float lx = 60 + leftX * 30;
            float ly = 60 + leftY * 30;
            g.FillEllipse(Brushes.LimeGreen, lx - 8, ly - 8, 16, 16);

            // Сетка для правого стика
            g.DrawLine(gridPen, 150, 20, 150, 100);
            g.DrawLine(gridPen, 110, 60, 190, 60);

            // Правый стик (красный)
            Pen rightPen = new Pen(Color.FromArgb(233, 69, 96), 2);
            g.DrawEllipse(rightPen, 110, 20, 80, 80);
            float rx = 150 + rightX * 30;
            float ry = 60 + rightY * 30;
            g.FillEllipse(Brushes.Red, rx - 8, ry - 8, 16, 16);

            // Подписи
            g.DrawString("L", new Font("Arial", 8), Brushes.Gray, 55, 105);
            g.DrawString("R", new Font("Arial", 8), Brushes.Gray, 145, 105);
        }

        private void UpdateJoystickState(object sender, EventArgs e)
        {
            try
            {
                JOYINFOEX joyInfo = new JOYINFOEX();
                joyInfo.dwSize = Marshal.SizeOf(typeof(JOYINFOEX));
                joyInfo.dwFlags = 0xFF; // Получить все данные

                int result = joyGetPosEx(0, ref joyInfo);

                if (result == 0) // JOYERR_NOERROR
                {
                    connected = true;
                    lblStatus.Text = "✅ DualShock 3 подключен";
                    lblStatus.ForeColor = Color.FromArgb(0, 255, 136);

                    // Конвертация координат (0-65535 -> -1.0 до 1.0)
                    leftX = (joyInfo.dwXpos - 32767) / 32767f;
                    leftY = (joyInfo.dwYpos - 32767) / 32767f;
                    rightX = (joyInfo.dwZpos - 32767) / 32767f;
                    rightY = (joyInfo.dwRpos - 32767) / 32767f;
                    buttons = joyInfo.dwButtons;

                    // Обновление информации
                    lblStickInfo.Text = $"Левый: {leftX,6:F2}, {leftY,6:F2}\n" +
                                       $"Правый: {rightX,6:F2}, {rightY,6:F2}";

                    string btnStr = "Кнопки: ";
                    if (buttons == 0) btnStr += "нет нажатий";
                    else
                    {
                        for (int i = 0; i < 13; i++)
                        {
                            if ((buttons & (1 << i)) != 0)
                                btnStr += $"[{i}] ";
                        }
                    }
                    lblButtons.Text = btnStr;

                    // Эмуляция клавиатуры
                    if (emulator.Enabled)
                    {
                        float dz = emulator.Deadzone;
                        ProcessStickEmulation("left_stick_left", "left_stick_right", leftX, dz);
                        ProcessStickEmulation("left_stick_up", "left_stick_down", leftY, dz);
                        ProcessStickEmulation("right_stick_left", "right_stick_right", rightX, dz);
                        ProcessStickEmulation("right_stick_up", "right_stick_down", rightY, dz);
                    }

                    // Перерисовка панели стиков
                    stickPanel.Invalidate();
                }
                else
                {
                    connected = false;
                    lblStatus.Text = "❌ Джойстик не подключен";
                    lblStatus.ForeColor = Color.FromArgb(233, 69, 96);
                    leftX = leftY = rightX = rightY = 0;
                    buttons = 0;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"⚠️ Ошибка: {ex.Message}";
                lblStatus.ForeColor = Color.Yellow;
                connected = false;
            }
        }

        private void ProcessStickEmulation(string negAction, string posAction, float value, float deadzone)
        {
            if (value < -deadzone)
            {
                emulator.PressKey(negAction);
                emulator.ReleaseKey(posAction);
            }
            else if (value > deadzone)
            {
                emulator.PressKey(posAction);
                emulator.ReleaseKey(negAction);
            }
            else
            {
                emulator.ReleaseKey(negAction);
                emulator.ReleaseKey(posAction);
            }
        }

        private void BtnHide_Click(object sender, EventArgs e)
        {
            if (!hider.IsHidden)
            {
                if (hider.HideJoystick())
                {
                    btnHide.Text = "👁️ Показать джойстик";
                    btnHide.BackColor = Color.FromArgb(0, 180, 100);
                    MessageBox.Show("Джойстик скрыт от системы!\nТеперь игры будут думать, что это клавиатура.", 
                                   "ReJoy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Не удалось скрыть джойстик.\nЗапустите программу от имени администратора.", 
                                   "ReJoy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                if (hider.ShowJoystick())
                {
                    btnHide.Text = "👻 Скрыть джойстик";
                    btnHide.BackColor = Color.FromArgb(255, 170, 0);
                    MessageBox.Show("Джойстик снова видим для системы.", 
                                   "ReJoy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Не удалось восстановить джойстик.", 
                                   "ReJoy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string GetJsonData()
        {
            return "{" +
                   $"\"connected\":{connected.ToString().ToLower()}," +
                   $"\"leftStick\":{{\"x\":{leftX:F2},\"y\":{leftY:F2}}}," +
                   $"\"rightStick\":{{\"x\":{rightX:F2},\"y\":{rightY:F2}}}," +
                   $"\"buttons\":{buttons}" +
                   "}";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            updateTimer?.Stop();
            emulator?.ReleaseAll();
            webServer?.Stop();
            base.OnFormClosing(e);
        }
    }
}
