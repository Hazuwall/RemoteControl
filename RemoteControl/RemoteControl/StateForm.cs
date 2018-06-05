using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteControl
{
    /// <summary>
    /// Форма проверки состояния подключения между компьютером и роботом
    /// </summary>
    public partial class StateForm : Form
    {
        //Текстовые поля информации о подключении
        private Label connectionModeLabel = new Label();
        private Label compStateLabel = new Label();
        private Label robotStateLabel = new Label();
        private Label recommendationLabel = new Label();

        public StateForm()
        {
            InitializeComponent();
            this.Text = "Проверка состояния";
            this.MinimumSize = new Size(300, 300);
            this.MaximumSize = new Size(300, 300);

            //Заполнение пользовательского интерфейса
            this.connectionModeLabel.Location = new Point(10,10);
            this.connectionModeLabel.Size = new Size(280, 30);
            this.connectionModeLabel.ForeColor = Color.DarkGreen;
            this.Controls.Add(this.connectionModeLabel);

            this.compStateLabel.Location = new Point(10, 50);
            this.compStateLabel.Size = new Size(280, 20);
            this.Controls.Add(this.compStateLabel);

            this.robotStateLabel.Location = new Point(10, 80);
            this.robotStateLabel.Size = new Size(280, 30);
            this.Controls.Add(this.robotStateLabel);

            Button button = new Button();
            button.Text = "Проверить соединение";
            button.Click += Check_Click;
            button.Location = new Point(10, 120);
            this.Controls.Add(button);

            this.recommendationLabel.Location = new Point(10, 170);
            this.recommendationLabel.Size = new Size(280, 120);
            this.Controls.Add(this.recommendationLabel);

            this.CheckConnectionState();
        }

        /// <summary>
        /// Проверить состояние подключения при нажатии на соответствующую кнопку
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Check_Click(object sender, EventArgs e)
        {
            this.CheckConnectionState();
        }
        /// <summary>
        /// Проверить состояние подключения
        /// </summary>
        private void CheckConnectionState()
        {
            //Режим соединения
            if (Transfer.Current.Mode == Transfer.ConnectionMode.AccessPoint)
                this.connectionModeLabel.Text = "Режим соединения: напрямую через точку доступа получателя";
            else
                this.connectionModeLabel.Text = "Режим соединения: через общую локальную сеть '" + Transfer.Current.SSID + "'";

            //Подключение компьютера
            if (GetSSID() == Transfer.Current.SSID)
            {
                this.compStateLabel.Text = "Компьютер: подключен";
                this.compStateLabel.ForeColor = Color.DarkGreen;
            }
            else
            {
                this.compStateLabel.Text = "Компьютер: не подключен";
                this.compStateLabel.ForeColor = Color.DarkRed;
                this.robotStateLabel.Text = "Получатель: -";
                this.robotStateLabel.ForeColor = Color.Black;
                this.recommendationLabel.Text = "Подключите компьютер к Wi-Fi сети '" + Transfer.Current.SSID + "'";
                return;
            }

            //Подключение получателя
            try {
                Transfer.Current.SendCommand(Command.CheckConnection);
                byte[] response = new byte[1];
                Transfer.Current.Receive(response);
                if(response[0]==(byte)Response.Success)
                {
                    this.robotStateLabel.Text = "Получатель: подключен по адресу " + Transfer.Current.ReceiverIPAddress;
                    this.robotStateLabel.ForeColor = Color.DarkGreen;
                    this.recommendationLabel.Text = String.Empty;
                }
                else
                    throw new Exception("Получен неверный ответ");
            }
            catch (Exception ex)
            {
                this.robotStateLabel.Text = "Получатель: не подключен";
                this.robotStateLabel.ForeColor = Color.DarkRed;
                this.recommendationLabel.Text = ex.Message;
            }
        }
        /// <summary>
        /// Получить название Wi-Fi сети к которой подключен компьютер в данный момент
        /// </summary>
        /// <returns></returns>
        private static string GetSSID()
        {
            Process process = new Process
            {
                StartInfo =
                {
                    FileName = "netsh.exe",
                    Arguments = "wlan show interfaces",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string line = output.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                             .FirstOrDefault(l => l.Contains("SSID") && !l.Contains("BSSID"));
            if (line == null)
                return String.Empty;
            var ssid = line.Substring(line.IndexOf(':') + 1).Trim();
            return ssid;
        }
    }
}
