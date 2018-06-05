using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteControl
{
    /// <summary>
    /// Класс передачи данных между компьютером и роботом
    /// </summary>
    public class Transfer
    {
        //Текущий экземпляр класса
        private static Transfer _Current = new Transfer();
        public static Transfer Current { get { return _Current; } }

        /// <summary>
        /// Режим подключения
        /// </summary>
        public enum ConnectionMode
        {
            AccessPoint,
            LocalAreaNetwork
        }

        private IPEndPoint ReceiverIP;
        private Socket UdpSocket;
        private Socket StateSocket;
        private bool IsReceiveAsyncCompleted = false;
        public ConnectionMode Mode { get; private set; }
        public string SSID { get; private set; }
        public string ReceiverIPAddress { get { return this.ReceiverIP.Address.ToString(); } }
        public event EventHandler ReceiverChanged = delegate { };

        public Transfer()
        {
            this.UdpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            EndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 4005);
            this.UdpSocket.Bind(localEndPoint); //Привязка сокета к локальному порту 4005
            this.UdpSocket.ReceiveTimeout = 2500;

            this.StateSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            localEndPoint = new IPEndPoint(IPAddress.Any, 4004);
            this.StateSocket.Bind(localEndPoint); //Привязка сокета к локальному порту 4004
            this.SetAccessPointAsReceiver();
        }

        /// <summary>
        /// Установить точку доступа как получателя
        /// </summary>
        public void SetAccessPointAsReceiver()
        {
            this.Mode = ConnectionMode.AccessPoint;
            this.SSID = "RobotAccessPoint";
            IPAddress address = new IPAddress(new byte[] { 192, 168, 4, 1 });
            this.ReceiverIP = new IPEndPoint(address,4005);
            this.ReceiverChanged.Invoke(this,null);
        }
        /// <summary>
        /// Установить получателя
        /// </summary>
        /// <param name="ssid">Название Wi-Fi сети</param>
        /// <param name="ip">IP адрес получателя в сети</param>
        public void SetReceiver(string ssid, IPAddress ip)
        {
            this.Mode = ConnectionMode.LocalAreaNetwork;
            this.SSID = ssid;
            this.ReceiverIP = new IPEndPoint(ip,4005);
            this.ReceiverChanged.Invoke(this, null);
        }
        /// <summary>
        /// Отправить команду получателю
        /// </summary>
        /// <param name="command">Команда</param>
        public void SendCommand(Command command)
        {
            UdpSocket.SendTo(new byte[] { (byte)command }, this.ReceiverIP);
        }
        /// <summary>
        /// Отправить строку получателю
        /// </summary>
        /// <param name="str">Строка</param>
        public void SendString(string str)
        {
            byte[] data = Encoding.UTF8.GetBytes(str);
            this.UdpSocket.SendTo(data, this.ReceiverIP);
        }

        /// <summary>
        /// Проверить, доступны ли непрочитанные данные на порте приёма состояния
        /// </summary>
        /// <returns></returns>
        public bool IsStateAvailable()
        {
            return this.StateSocket.Available > 0;
        }

        /// <summary>
        /// Получить данные
        /// </summary>
        /// <param name="data">Буффер данных</param>
        public void Receive(byte[] data)
        {
            this.UdpSocket.Receive(data);
        }

        /// <summary>
        /// Получить строку состояния
        /// </summary>
        /// <param name="data">Буффер данных</param>
        public string ReceiveState()
        {
            byte[] state = new byte[20];
            this.StateSocket.Receive(state);
            if (state[0] != (byte)Response.State)
                throw new FormatException();
            return Encoding.UTF8.GetString(state).Substring(1).Trim('\0');
        }

        /// <summary>
        /// Получить ответ асинхронно
        /// </summary>
        /// <param name="data">Буффер данных</param>
        public async Task<Response> ReceiveResponseAsync()
        {
            SocketAsyncEventArgs socketArgs = new SocketAsyncEventArgs();
            byte[] data = new byte[1];
            socketArgs.SetBuffer(data, 0, 1);
            socketArgs.Completed += this.ReceiveResponseAsync_Completed;
            this.IsReceiveAsyncCompleted = false;
            UdpSocket.ReceiveAsync(socketArgs);
            await Task.Factory.StartNew(() => {
                DateTime startTime = DateTime.Now;
                TimeSpan timeOut = new TimeSpan(TimeSpan.TicksPerSecond * 15);
                while (!IsReceiveAsyncCompleted)
                {
                    if (DateTime.Now - startTime > timeOut)
                    {
                        socketArgs.Buffer[0] = (byte)Response.TimeOut;
                        break;
                    }
                }
            });
            Response response = (Response)socketArgs.Buffer[0];
            socketArgs.Dispose();
            return response;
        }

        /// <summary>
        /// Подать сигнал о завершении приёма ответа
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReceiveResponseAsync_Completed(object sender, SocketAsyncEventArgs e)
        {
            this.IsReceiveAsyncCompleted = true;
        }
        /// <summary>
        /// Закрыть соединение и освободить используемые ресурсы
        /// </summary>
        public void Close()
        {
            this.UdpSocket.Shutdown(SocketShutdown.Both);
            this.UdpSocket.Close();
            this.UdpSocket = null;
            this.ReceiverIP = null;
            this.SSID = null;
        }
    }
}
