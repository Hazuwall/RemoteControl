using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteControl
{
    /// <summary>
    /// Форма настройки подключения по локальной сети
    /// </summary>
    public partial class LocalNetworkForm : Form
    {
        //Элементы управления
        Button RequestButton = new Button();
        Button AuthorizeButton = new Button();
        TextBox SSIDBox = new TextBox();
        TextBox PasswordBox = new TextBox();
        Label RecommendationLabel = new Label();

        public LocalNetworkForm()
        {
            InitializeComponent();
            this.Text = "Настройка подключения";
            this.MinimumSize = new Size(350, 350);
            this.MaximumSize = new Size(350, 350);

            Label requestLabel = new Label();
            requestLabel.Location = new Point(10, 10);
            requestLabel.Size = new Size(150, 70);
            requestLabel.Text = "Запросить через точку доступа данные о текущем подключении получателя в локальной сети";
            this.Controls.Add(requestLabel);

            this.RequestButton.Text = "Запросить";
            this.RequestButton.Location = new Point(10, 150);
            this.RequestButton.Size = new Size(100, 23);
            this.RequestButton.Click += Request_Click;
            this.Controls.Add(this.RequestButton);

            Label authorizeLabel = new Label();
            authorizeLabel.Location = new Point(170, 10);
            authorizeLabel.Size = new Size(150, 70);
            authorizeLabel.Text = "Авторизовать получателя, передав через точку доступа введённые ниже данные авторизации, и запросить его IP адрес";
            this.Controls.Add(authorizeLabel);

            Label SSIDLabel = new Label();
            SSIDLabel.Text = "SSID";
            SSIDLabel.Font = new Font(SystemFonts.DefaultFont.FontFamily, 7);
            SSIDLabel.Location = new Point(170,90);
            SSIDLabel.Size = new Size(50, 20);

            this.Controls.Add(SSIDLabel);
            this.SSIDBox.Location = new Point(220, 90);
            this.SSIDBox.Size = new Size(100, 20);
            this.SSIDBox.MaxLength = 50;
            this.Controls.Add(this.SSIDBox);

            Label PasswordLabel = new Label();
            PasswordLabel.Text = "Пароль";
            PasswordLabel.Font = new Font(SystemFonts.DefaultFont.FontFamily, 7);
            PasswordLabel.Location = new Point(170, 115);
            PasswordLabel.Size = new Size(50, 20);
            this.Controls.Add(PasswordLabel);

            this.PasswordBox.Location = new Point(220, 115);
            this.PasswordBox.Size = new Size(100, 20);
            this.PasswordBox.PasswordChar = '*';
            this.PasswordBox.MaxLength = 50;
            this.Controls.Add(this.PasswordBox);

            this.AuthorizeButton.Text = "Авторизовать";
            this.AuthorizeButton.Location = new Point(170, 150);
            this.AuthorizeButton.Size = new Size(100,23);
            this.AuthorizeButton.Click += Authorize_Click;
            this.Controls.Add(this.AuthorizeButton);

            this.RecommendationLabel.Location = new Point(10, 200);
            this.RecommendationLabel.Size = new Size(310, 100);
            this.Controls.Add(this.RecommendationLabel);

            this.SetPanelState(Transfer.Current.Mode);
            Transfer.Current.ReceiverChanged += Transfer_ReceiverChanged;
        }

        /// <summary>
        /// Установить получателя, запросив данные о его подключении в локальной сети, при нажатии на соответствующую кнопку
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Request_Click(object sender, EventArgs e)
        {
            try
            {
                //Получение SSID локальной сети получателя
                Transfer.Current.SendCommand(Command.GetSSID);
                byte[] response = new byte[1];
                Transfer.Current.Receive(response);
                if (response[0] != (byte)Response.Success)
                    throw new Exception("Получатель не подключен к локальной сети");
                byte[] ssidData = new byte[50];
                Transfer.Current.Receive(ssidData);
                string SSID = Encoding.UTF8.GetString(ssidData).Trim('\0');

                //Получение IP получателя в сети
                byte[] ipData = new byte[4];
                Transfer.Current.SendCommand(Command.GetIP);
                Transfer.Current.Receive(response);
                if (response[0] != (byte)Response.Success)
                    throw new Exception("Получатель не подключен к локальной сети");
                Transfer.Current.Receive(ipData);
                Transfer.Current.SetReceiver(SSID, new IPAddress(ipData));
                Properties.Settings.Default.LocalNetworkSSID = Transfer.Current.SSID;
                Properties.Settings.Default.LocalNetworkIP = Transfer.Current.ReceiverIPAddress;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                this.RecommendationLabel.ForeColor = Color.Black;
                this.RecommendationLabel.Text = ex.Message;
            }
        }

        /// <summary>
        /// Установить получателя, авторизовав его в локальной сети, при нажатии на соответствующую кнопку
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Authorize_Click(object sender, EventArgs e)
        {
            this.AuthorizeButton.Enabled = false;
            this.RequestButton.Enabled = false;
            this.RecommendationLabel.ForeColor = Color.Black;
            this.RecommendationLabel.Text = "Производится попытка авторизации...";
            try
            {
                //Отправка данных получателю
                string SSID = this.SSIDBox.Text;
                if(String.IsNullOrWhiteSpace(SSID))
                    throw new Exception("SSID локальной сети не введён");
                Transfer.Current.SendCommand(Command.Authorize);
                Transfer.Current.SendString(SSID);
                string password = this.PasswordBox.Text;
                if (password == String.Empty)
                    password = '\0'.ToString();
                Transfer.Current.SendString(password);

                //Ожидание и обработка статуса авторизации
                switch(await Transfer.Current.ReceiveResponseAsync())
                {
                    case Response.Success:
                        break;
                    case Response.SSIDNotFound:
                        throw new Exception("Локальная сеть '" + SSID + "' не найдена");
                    case Response.IncorrectPassword:
                        throw new Exception("Не удалось авторизовать получателя в локальной сети '" + SSID + "'. Убедитесь, что пароль введён верно, и повторите попытку");
                    case Response.TimeOut:
                        throw new TimeoutException("Превышено время ожидания. Убедитесь в активности соединения с точкой доступа в разделе меню Соединение->Проверить состояние");
                    default:
                        throw new Exception("Ошибка авторизации в локальной сети");
                }
                
                //Получение IP адреса в новой локальной сети
                Transfer.Current.SendCommand(Command.GetIP);
                byte[] response = new byte[1];
                Transfer.Current.Receive(response);
                if (response[0] != (byte)Response.Success)
                    throw new Exception("Получатель отключился от локальной сети");
                byte[] ipData = new byte[4];
                Transfer.Current.Receive(ipData);
                Transfer.Current.SetReceiver(SSID, new IPAddress(ipData));
                Properties.Settings.Default.LocalNetworkSSID = Transfer.Current.SSID;
                Properties.Settings.Default.LocalNetworkIP = Transfer.Current.ReceiverIPAddress;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                this.RecommendationLabel.Text = ex.Message;
                this.AuthorizeButton.Enabled = true;
                this.RequestButton.Enabled = true;
            }
        }

        /// <summary>
        /// Обновить панель при изменении получателя
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Transfer_ReceiverChanged(object sender, EventArgs e)
        {
            this.SetPanelState(Transfer.Current.Mode);
        }

        /// <summary>
        /// Установить доступность панели в зависимости от режима подключения. Подключение получателя к новой локальной сети невозможно при активном соединении устройств через локальную сеть
        /// </summary>
        /// <param name="Mode">Режим подключения</param>
        private void SetPanelState(Transfer.ConnectionMode Mode)
        {
            if (Mode == Transfer.ConnectionMode.AccessPoint)
            {
                this.RequestButton.Enabled = true;
                this.AuthorizeButton.Enabled = true;
                this.RecommendationLabel.ForeColor = Color.Black;
                this.RecommendationLabel.Text = String.Empty;
            }
            else
            {
                this.RequestButton.Enabled = false;
                this.AuthorizeButton.Enabled = false;
                this.RecommendationLabel.ForeColor = Color.DarkGreen;
                this.RecommendationLabel.Text = "Соединение по локальной сети '" + Transfer.Current.SSID + "' установлено. Адрес получателя: " + Transfer.Current.ReceiverIPAddress;
                this.SSIDBox.Text = String.Empty;
                this.PasswordBox.Text = String.Empty;
            }
        }
    }
}