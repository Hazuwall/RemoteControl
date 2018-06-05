using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteControl
{
    /// <summary>
    /// Главная форма, отвечающая за дистанционное управление
    /// </summary>
    public partial class MainForm : Form
    {
        //Переменные, отвечающие за передачу данных при нажатии
        private bool DoSendCommand = false;
        private Command SendingMotionCommand;

        //Файл для записи сосотояния движения
        StreamWriter Log;

        //Раздел меню, отображающий текущего получателя
        private ToolStripItemCollection ReceiverItems;

        //Направляющие кнопки
        Dictionary<Command, Button> MotionButtons = new Dictionary<Command, Button>();

        //Связанные формы
        private StateForm StateForm;
        private LocalNetworkForm LocalNetworkForm;

        public MainForm()
        {
            InitializeComponent();
            this.Text = "Дистанционное управление";
            this.KeyPreview = true;
            this.MinimumSize = new Size(270, 320);
            this.MaximumSize = new Size(270, 320);

            //Главный пункт меню
            MenuStrip menuStrip = new MenuStrip();
            ToolStripMenuItem connectionMenu = new ToolStripMenuItem();
            connectionMenu.Text = "Соединение";
            menuStrip.Items.Add(connectionMenu);

            //Раздел меню установки получателя
            ToolStripMenuItem setReceiverMenu = new ToolStripMenuItem();
            setReceiverMenu.Text = "Рабочая сеть";
            connectionMenu.DropDownItems.Add(setReceiverMenu);

            ToolStripMenuItem setReceiverMenuItem1 = new ToolStripMenuItem();
            setReceiverMenuItem1.Text = "Точка доступа";
            setReceiverMenuItem1.Checked = true;
            setReceiverMenuItem1.Click += SetAccessPointAsReceiver_Click;
            setReceiverMenu.DropDownItems.Add(setReceiverMenuItem1);

            if (!String.IsNullOrEmpty(Properties.Settings.Default.LocalNetworkSSID) && !String.IsNullOrEmpty(Properties.Settings.Default.LocalNetworkIP))
            {
                //Последняя подключенная сеть, если таковая была
                ToolStripMenuItem setReceiverMenuItem2 = new ToolStripMenuItem();
                setReceiverMenuItem2.Text = Properties.Settings.Default.LocalNetworkSSID;
                setReceiverMenuItem2.Tag = Properties.Settings.Default.LocalNetworkIP;
                setReceiverMenuItem2.Checked = false;
                setReceiverMenuItem2.Click += SetReceiver_Click;
                setReceiverMenu.DropDownItems.Add(setReceiverMenuItem2);
            }

            ToolStripMenuItem setReceiverMenuItem3 = new ToolStripMenuItem();
            setReceiverMenuItem3.Text = "Локальная сеть...";
            setReceiverMenuItem3.Click += SetReceiverAtLocalNetwork_Click;
            setReceiverMenu.DropDownItems.Add(setReceiverMenuItem3);

            this.ReceiverItems = setReceiverMenu.DropDownItems;
            Transfer.Current.ReceiverChanged += Transfer_ReceiverChanged;

            //Раздел меню проверки состояния
            ToolStripMenuItem stateItem = new ToolStripMenuItem();
            stateItem.Text = "Проверить состояние";
            stateItem.Click += CheckState_Click;
            connectionMenu.DropDownItems.Add(stateItem);

            this.Controls.Add(menuStrip);
            this.MainMenuStrip = menuStrip;

            //Кнопки управления роботом
            Font motionButtonFont = new Font("Microsoft Sans Serif", 25);
            Size motionButtonSize = new Size(60, 60);

            Button motionButton = new Button();
            motionButton.Text = "\x2191";
            motionButton.Font = motionButtonFont;
            motionButton.Location = new Point(95, 50);
            motionButton.Size = motionButtonSize;
            motionButton.Tag = Command.Forward;
            motionButton.MouseDown += Button_MouseDown;
            motionButton.MouseUp += Button_MouseUp;
            this.Controls.Add(motionButton);
            this.MotionButtons.Add(Command.Forward, motionButton);

            motionButton = new Button();
            motionButton.Text = "\x2193";
            motionButton.Font = motionButtonFont;
            motionButton.Location = new Point(95, 115);
            motionButton.Size = motionButtonSize;
            motionButton.Tag = Command.Backward;
            motionButton.MouseDown += Button_MouseDown;
            motionButton.MouseUp += Button_MouseUp;
            this.Controls.Add(motionButton);
            this.MotionButtons.Add(Command.Backward, motionButton);

            motionButton = new Button();
            motionButton.Text = "\x2190";
            motionButton.Font = motionButtonFont;
            motionButton.Location = new Point(30, 115);
            motionButton.Size = motionButtonSize;
            motionButton.Tag = Command.LeftTurn;
            motionButton.MouseDown += Button_MouseDown;
            motionButton.MouseUp += Button_MouseUp;
            this.Controls.Add(motionButton);
            this.MotionButtons.Add(Command.LeftTurn, motionButton);

            motionButton = new Button();
            motionButton.Text = "\x2192";
            motionButton.Font = motionButtonFont;
            motionButton.Location = new Point(160, 115);
            motionButton.Size = motionButtonSize;
            motionButton.Tag = Command.RightTurn;
            motionButton.MouseDown += Button_MouseDown;
            motionButton.MouseUp += Button_MouseUp;
            this.Controls.Add(motionButton);
            this.MotionButtons.Add(Command.RightTurn, motionButton);

            Button autopilotButton = new Button();
            autopilotButton.Text = "Автопилот";
            autopilotButton.Font = new Font("Microsoft Sans Serif", 10); ;
            autopilotButton.Location = new Point(30, 200);
            autopilotButton.Size = new Size(125,27);
            autopilotButton.Tag = Command.Autopilot;
            autopilotButton.Click += Autopilot_Click;
            this.Controls.Add(autopilotButton);

            //Подписка на освобождение клавиш клавиатуры
            this.KeyUp += MainForm_KeyUp;

            //Подключение к файлу лога и его очистка
            this.Log = new StreamWriter("log.txt", false, Encoding.Default);

            //Отображение состояния движения
            Label MotionStateLabel = new Label();
            MotionStateLabel.Location = new Point(30, 250);
            MotionStateLabel.Size = new Size(240, 23);
            this.Controls.Add(MotionStateLabel);

            //Функция для изменения текста из другого потока
            Progress<string> progress = new Progress<string>(s => MotionStateLabel.Text = s);
            //Поток отправки и принятия данных
            Task.Factory.StartNew(() => Cyclic(progress), TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Циклическая функция, отправляющая текущую команду при зажатой клавише и принимающая информацию о состоянии движения
        /// </summary>
        /// <param name="progress">Прогресс для вывода состояния движения</param>
        public async void Cyclic(IProgress<string> progress)
        {
            byte zeroizeSpeedIndex = 0;
            string avgSpeed = "0";
            string path = "0";
            while (true)
            {
                try
                {
                    if (Transfer.Current.IsStateAvailable())
                    {
                        string[] stateStrings = Transfer.Current.ReceiveState().Split(' ');
                        zeroizeSpeedIndex = 0;
                        if (stateStrings[0] != avgSpeed || stateStrings[1] != path)
                        {
                            //Если получено новое состояние движения, то вывести его
                            avgSpeed = stateStrings[0];
                            path = stateStrings[1];
                            progress.Report(String.Format("Скорость: {0,-18} Путь: {1}", avgSpeed, path));
                            this.Log.WriteLine(String.Format("{0} Speed {1,-4} Path {2}", DateTime.Now.ToString("HH:mm:ss"), avgSpeed, path));
                        }
                    }
                    else
                    {
                        //Если за 4,5 секунды не обновилось состояние, то обнулить скорость
                        if (zeroizeSpeedIndex > 90)
                        {
                            zeroizeSpeedIndex = 0;
                            progress.Report(String.Format("Скорость: {0,-18} Путь: {1}", 0, path));
                        }
                        else
                            zeroizeSpeedIndex++;
                    }

                    //Каждые 200 мс отправлять текущую команду движения, если зажата кнопка
                    if (this.DoSendCommand)
                        Transfer.Current.SendCommand(this.SendingMotionCommand);
                }
                catch { }
                await Task.Delay(50);
            }
        }

        #region Обработчики нажатий по командным кнопкам

        /// <summary>
        /// Начать отправление команды при нажатии мышью по командной кнопке
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_MouseDown(object sender, MouseEventArgs e)
        {
            this.SendingMotionCommand = (Command)(sender as Button).Tag;
            this.DoSendCommand = true;
        }

        /// <summary>
        /// Начать отправление команды при нажатии по соответствующей клавиши на клавиатуре
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="keyData"></param>
        /// <returns></returns>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Command command;
            if (keyData == Keys.Up || keyData == Keys.W)
                command = Command.Forward;
            else if (keyData == Keys.Down || keyData == Keys.S)
                command = Command.Backward;
            else if (keyData == Keys.Left || keyData == Keys.A)
                command = Command.LeftTurn;
            else if (keyData == Keys.Right || keyData == Keys.D)
                command = Command.RightTurn;
            else
                return base.ProcessCmdKey(ref msg, keyData); //передать обработку клавиши следующему элементу управления

            this.MotionButtons[command].Focus();
            this.SendingMotionCommand = command;
            this.DoSendCommand = true;
            return true; //закончить обработку клавиши
        }

        /// <summary>
        /// Приостановить отправление команды, если клавиша мыши отпущена с командной кнопки
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_MouseUp(object sender, MouseEventArgs e)
        {
            this.DoSendCommand = false;
        }

        /// <summary>
        /// Приостановить отправление команды, если отпущена клавиша клавиатуры
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            this.DoSendCommand = false;
        }

        /// <summary>
        /// Включить режим автопилота при нажатии на соответствующую кнопку
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Autopilot_Click(object sender, EventArgs e)
        {
            try
            {
                Transfer.Current.SendCommand(Command.Autopilot);
            }
            catch { }
        }
        #endregion

        #region Обработчики нажатий по пунктам меню

        /// <summary>
        /// Установить точку доступа как получателя при клике на соответствующий пункт меню
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetAccessPointAsReceiver_Click(object sender, EventArgs e)
        {
            Transfer.Current.SetAccessPointAsReceiver();
        }

        /// <summary>
        /// Установить выбранную сеть в качестве получателя
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetReceiver_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            Transfer.Current.SetReceiver(item.Text, IPAddress.Parse(item.Tag as string));
        }

        /// <summary>
        /// Открыть форму установки получателя в локальной сети при клике на соответствующий пункт меню
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetReceiverAtLocalNetwork_Click(object sender, EventArgs e)
        {
            if (this.LocalNetworkForm == null)
            {
                this.LocalNetworkForm = new LocalNetworkForm();
                this.LocalNetworkForm.FormClosing += LocalNetworkForm_FormClosing;
                this.LocalNetworkForm.Show();
            }
            else
                this.LocalNetworkForm.Focus();
        }

        /// <summary>
        /// Открыть форму проверки состояния подключения при клике на соответствующий пункт меню
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckState_Click(object sender, EventArgs e)
        {
            if (this.StateForm == null)
            {
                this.StateForm = new StateForm();
                this.StateForm.FormClosing += StateForm_FormClosing;
                this.StateForm.Show();
            }
            else
                this.StateForm.Focus();
        }
        #endregion

        /// <summary>
        /// Обновить меню при изменении получателя
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Transfer_ReceiverChanged(object sender, EventArgs e)
        {
            if (Transfer.Current.Mode == Transfer.ConnectionMode.AccessPoint) {
                (this.ReceiverItems[0] as ToolStripMenuItem).Checked = true;
                (this.ReceiverItems[1] as ToolStripMenuItem).Checked = false;
            }
            else
            {
                (this.ReceiverItems[0] as ToolStripMenuItem).Checked = false;
                ToolStripMenuItem localNetworkItem = (this.ReceiverItems[1] as ToolStripMenuItem);
                if(localNetworkItem.Tag == null) {
                    localNetworkItem = new ToolStripMenuItem();
                    localNetworkItem.Click += SetReceiver_Click;
                    this.ReceiverItems.Insert(1,localNetworkItem);
                }
                localNetworkItem.Text = Transfer.Current.SSID;
                localNetworkItem.Tag = Transfer.Current.ReceiverIPAddress;
                localNetworkItem.Checked = true;
            }
        }

        /// <summary>
        /// Удалить связь с закрытой формой состояния
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StateForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.StateForm.FormClosing -= StateForm_FormClosing;
            this.StateForm = null;
        }

        /// <summary>
        /// Удалить связь с закрытой формой подключения по локальной сети
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LocalNetworkForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.LocalNetworkForm.FormClosing -= LocalNetworkForm_FormClosing;
            this.LocalNetworkForm = null;
        }
    }
}