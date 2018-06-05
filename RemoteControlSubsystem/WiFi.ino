#include <ESP8266WiFi.h>
#include <WiFiUdp.h>

//Команда, полученная от компьютера
enum Command {
  NONE_CMD = 0,
  STOP_CMD = 1,
  BACKWARD_CMD = 2,
  FORWARD_CMD = 3,
  LEFT_TURN_CMD = 4,
  RIGHT_TURN_CMD = 5,
  AUTOPILOT_CMD = 6,
  CHECK_CONNECTION_CMD = 7,
  AUTHORIZE_CMD = 8,
  GET_IP_CMD = 9,
  GET_SSID_CMD = 10
};
//Ответ на запрос
enum Response {
  FAIL_RSP = 1,
  SSID_NOT_FOUND_RSP = 2,
  INCORRECT_PASSWORD_RSP = 3,
  TIMEOUT_RSP = 4, //используется на стороне отправителя
  SUCCESS_RSP = 5,
  STATE_RSP = 6
};

WiFiUDP Udp;
Command lastMotionCommand = NONE_CMD;
IPAddress ip = Udp.remoteIP(); //IP-адрес последнего управляющего устройства

void setup() {
  Serial.begin(115200);
  WiFi.softAP("RobotAccessPoint");
  WiFi.begin();
  Udp.begin(4005);
  delay(1000);
}

//Доставить команду центральной плате
void deliverMotionCommand(Command command) {
  if(lastMotionCommand != command) {
    Serial.print((uint8_t)command);
    lastMotionCommand = command;
  }
}

//Произвести попытку авторизации в сети, используя полученные данные
Response tryAuthorize() {
  //Получение SSID
  delay(50);
  if(Udp.parsePacket()==0) {
    return FAIL_RSP;
  }
  char ssidPacket[51];
  int ssidLength = Udp.read(ssidPacket,50);
  ssidPacket[ssidLength]=0;

  //Получение пароля
  delay(50);
  if(Udp.parsePacket()==0) {
    return FAIL_RSP;
  }
  char passwordPacket[51];
  int passwordLength = Udp.read(passwordPacket,50);
  passwordPacket[passwordLength]=0;

  //Авторизация и отправка ответа о статусе подключения
  WiFi.begin(ssidPacket,passwordPacket[0]==0 ? "" : passwordPacket);
  uint8_t status = WiFi.waitForConnectResult();
  if(status == WL_CONNECTED) {
    WiFi.mode(WIFI_AP_STA);
    return SUCCESS_RSP;
  }
  if (status == WL_NO_SSID_AVAIL) {
    return SSID_NOT_FOUND_RSP;
  }
  if (status == WL_CONNECT_FAILED) {
    return INCORRECT_PASSWORD_RSP;
  }
  return FAIL_RSP;
}

//Отправить ответ устройству, от которого был получен текущий пакет
void sendResponse(Response response) {
  Udp.beginPacket(ip, 4005);
  Udp.write((uint8_t)response);
  Udp.endPacket();
}

void loop() {
  //Получение команды
  Command command = NONE_CMD;
  if(Udp.parsePacket()) {
    uint8_t data = Udp.read();
    if(data < 11) {
      command = (Command)data;
      ip = Udp.remoteIP();
    }
  }

  //Если от устройства отправителя закончилась передача команды, не являющаяся "автопилотом", то передать команду остановки
  if(lastMotionCommand!=NONE_CMD && lastMotionCommand!=AUTOPILOT_CMD && command==NONE_CMD){
    command = STOP_CMD;
  }

  //Обработка команды
  switch(command)
  {
    case CHECK_CONNECTION_CMD:
    deliverMotionCommand(STOP_CMD);
    sendResponse(SUCCESS_RSP);
    break;
    
    case AUTHORIZE_CMD:
    deliverMotionCommand(STOP_CMD);
    sendResponse(tryAuthorize());
    break;
    
    case GET_IP_CMD:
    deliverMotionCommand(STOP_CMD);
    if(WiFi.isConnected()) {
      sendResponse(SUCCESS_RSP);
      char data[4];
      for(uint8_t i = 0; i < 4;i++){
        data[i]=WiFi.localIP()[i];
      }
      Udp.beginPacket(ip, 4005);
      Udp.write(data,4);
      Udp.endPacket();
    }
    else {
      sendResponse(FAIL_RSP);
    }
    break;
    
    case GET_SSID_CMD:
    deliverMotionCommand(STOP_CMD);
    if(WiFi.isConnected()) {
      sendResponse(SUCCESS_RSP);
      String ssid = WiFi.SSID();
      Udp.beginPacket(ip, 4005);
      Udp.write(ssid.c_str(),ssid.length());
      Udp.endPacket();
    }
    else {
      sendResponse(FAIL_RSP);
    }
    break;

    case NONE_CMD:
    lastMotionCommand = NONE_CMD;
    break;
    
    default:
    deliverMotionCommand(command);
    break;
  }
  //Очистка памяти от пришедших пакетов и ожидание следующих
  while(Udp.parsePacket()) { }

  //Передача состояния движения последнему отправителю
  if(Serial.available()>0) {
    char state[12];
    int len = Serial.available();
    for(i=0;i<len;i++){
      state[i] = Serial.read();
    }
    state[len]=0;
    Udp.beginPacket(ip, 4004);
    Udp.write(STATE_RSP);
    Udp.write(state,len);
    Udp.endPacket();
  }
  delay(150);
}
