#include <Arduino.h>
#include <SPI.h>
#include <Wire.h>
#include <Ethernet.h>
#include <EthernetUdp.h>
#include <esp_task_wdt.h>
#include <esp_system.h>
#include "Config.h"
#include "RigProtocol.h"
#if !defined(CONFIG_IDF_TARGET_ESP32S3)
#error Select ESP32S3 Dev Module
#endif
// CH343 bridge diagnostics always use UART0 GPIO43/44, independent of USB CDC.
// Dashboard UART uses GPIO39/40; GPIO43/44 remain available to USB-UART.

HardwareSerial dash(1);
EthernetUDP udp;
IPAddress localIP(192,168,1,150), pcIP(192,168,1,100), subnet(255,255,255,0);
byte mac[]={0x02,0x53,0x49,0x4d,0x00,0x01};
Rig::Input input;
Rig::Feedback feedback;
Rig::Parser parser;
Rig::Debouncer switches[16], joy;
volatile int32_t ticks=0;
volatile uint8_t encoderPrevious=0;
portMUX_TYPE encoderMux=portMUX_INITIALIZER_UNLOCKED;
bool mcpOK=false, ethernetOK=false, pwmOK=false, armed=false, centered=false;
bool ethernetReady=false, udpReady=false;
// Survives software/watchdog reset; useful even without a touchscreen.
RTC_DATA_ATTR uint32_t lastStage=0;
enum : uint32_t { ST_BOOT=1, ST_ETH_INIT, ST_UDP_INIT, ST_LINK, ST_UART_RX, ST_UDP_RX, ST_INPUT, ST_MOTOR, ST_UDP_TX, ST_UART_TX, ST_IDLE };
uint32_t txOK=0,txFailed=0;
uint8_t w5500Version=0, i2cAckMask=0;
bool hazard=false, haveFeedback=false, haveHeartbeat=false, haveCommand=false;
uint32_t lastFeedback=0,lastHeartbeat=0,lastCommandTime=0,starterUntil=0;
uint16_t lastCommand=0, dashSequence=0, stableSwitches=0;
uint8_t commandResult=0;
int applied=0;
uint16_t rawThrottle=0,rawBrake=0,rawClutch=0;

void IRAM_ATTR encoderISR() {
  uint8_t state=(digitalRead(ENCODER_A)<<1)|digitalRead(ENCODER_B);
  portENTER_CRITICAL_ISR(&encoderMux);
  ticks+=Rig::quadrature(encoderPrevious,state)*ENCODER_SIGN;
  encoderPrevious=state;
  portEXIT_CRITICAL_ISR(&encoderMux);
}
int32_t readEncoder() {
  portENTER_CRITICAL(&encoderMux); int32_t value=ticks; portEXIT_CRITICAL(&encoderMux); return value;
}
void motorOff() {
  digitalWrite(REN,LOW); digitalWrite(LEN,LOW);
  if(pwmOK) { ledcWrite(RPWM,0); ledcWrite(LPWM,0); }
  applied=0;
}
void disarm() { armed=false; motorOff(); }
bool writeMCP(uint8_t reg,uint8_t value) {
  Wire.beginTransmission(0x20); Wire.write(reg); Wire.write(value); return Wire.endTransmission()==0;
}
bool initMCP() {
  // BANK=0 after power/reset. IOCON sequential mode + all inputs/pullups.
  return writeMCP(0x0a,0) && writeMCP(0,255) && writeMCP(1,255) && writeMCP(0x0c,255) && writeMCP(0x0d,255);
}
bool readMCP(uint16_t &value) {
  Wire.beginTransmission(0x20); Wire.write(0x12);
  if(Wire.endTransmission(false)!=0 || Wire.requestFrom(uint8_t(0x20),uint8_t(2))!=2) return false;
  uint16_t raw=Wire.read(); raw|=uint16_t(Wire.read())<<8; value=uint16_t(~raw); return true;
}
uint16_t pedal(int raw,int released,int pressed,uint16_t old) {
  int normalized=constrain((raw-released)*4095/(pressed-released),0,4095);
  return (old*3+normalized)/4;
}
bool gameAlive(uint32_t now) { return ethernetOK && haveFeedback && uint32_t(now-lastFeedback)<FEEDBACK_TIMEOUT; }
bool screenAlive(uint32_t now) { return haveHeartbeat && uint32_t(now-lastHeartbeat)<SCREEN_TIMEOUT; }
void command(uint8_t cmd,uint32_t now) {
  commandResult=0;
  switch(cmd) {
    case Rig::STOP: disarm(); break;
    case Rig::START:
      if(mcpOK && (stableSwitches&(1<<6))) starterUntil=now+250;
      else commandResult=1;
      break;
    case Rig::HAZARD: hazard=!hazard; break;
    case Rig::CENTER:
      if(armed || (gameAlive(now) && feedback.speed10>10)) { commandResult=1; break; }
      portENTER_CRITICAL(&encoderMux); ticks=0; portEXIT_CRITICAL(&encoderMux);
      centered=true; break;
    case Rig::ARM:
      if(ENABLE_FFB && pwmOK && centered && gameAlive(now) && screenAlive(now) && mcpOK && (stableSwitches&(1<<6)) && !(input.reserved[0]&1) && abs(readEncoder())<STEERING_HARD_LIMIT) armed=true;
      else commandResult=1;
      break;
    default: commandResult=2; break;
  }
}
void readDashboard(uint32_t now) {
  // Bound work so a noisy UART cannot starve watchdog/motor control.
  for(int i=0;i<192 && dash.available();i++) {
    if(!parser.push(dash.read(),now)) continue;
    if(parser.type==Rig::HEARTBEAT && parser.length==0) { haveHeartbeat=true; lastHeartbeat=now; }
    if(parser.type==Rig::COMMAND && parser.length==1) {
      if(!screenAlive(now)) continue;
      // Same sequence is ACKed again without repeating side effects.
      if(haveCommand && parser.sequence==lastCommand) continue;
      if(haveCommand && !Rig::newer(parser.sequence,lastCommand)) continue;
      lastCommand=parser.sequence; haveCommand=true; lastCommandTime=now;
      command(parser.data()[0],now);
    }
  }
}
void readFeedback(uint32_t now) {
  // Ethernet 2.0.2 does not guard socketRecvAvailable() against an invalid
  // socket index. Never parse on a failed begin() or an offline controller.
  if(!ethernetReady || !udpReady || !ethernetOK) return;
  for(int i=0;i<4;i++) {
    int size=udp.parsePacket(); if(size<=0) break;
    if(size!=sizeof(Rig::Feedback) || udp.remoteIP()!=pcIP || udp.remotePort()!=9999) { udp.flush(); continue; }
    Rig::Feedback candidate;
    if(udp.read(reinterpret_cast<uint8_t*>(&candidate),sizeof(candidate))!=sizeof(candidate)) continue;
    if(candidate.magic!=0x4642 || candidate.torque < -1000 || candidate.torque>1000 || candidate.gear>6 || candidate.fuel>100 || candidate.rpm>20000) continue;
    if(gameAlive(now) && !Rig::newer(candidate.sequence,feedback.sequence)) continue;
    feedback=candidate; haveFeedback=true; lastFeedback=now;
  }
}
void scanInputs(uint32_t now) {
  uint16_t raw=0;
  bool valid=readMCP(raw);
  if(!valid) {
    mcpOK=false; stableSwitches=0; hazard=false; starterUntil=0; disarm();
    for(auto &button:switches) button=Rig::Debouncer();
  } else if(!mcpOK) {
    // Re-enable pullups after a peripheral reset/brownout; next scan reads it.
    mcpOK=initMCP();
  } else {
    for(int i=0;i<16;i++) {
      bool changed=switches[i].update(raw&(1<<i),now);
      if(switches[i].stable) stableSwitches|=1<<i; else stableSwitches&=~(1<<i);
      if(i==8 && changed && switches[i].stable) hazard=!hazard; // Momentary hazard -> latched state.
    }
  }
  input.timestamp=now; input.encoder=readEncoder();
  rawThrottle=analogRead(PEDAL_THROTTLE); rawBrake=analogRead(PEDAL_BRAKE); rawClutch=analogRead(PEDAL_CLUTCH);
  input.throttle=pedal(rawThrottle,THROTTLE_RELEASED,THROTTLE_PRESSED,input.throttle);
  input.brake=pedal(rawBrake,BRAKE_RELEASED,BRAKE_PRESSED,input.brake);
  input.clutch=pedal(rawClutch,CLUTCH_RELEASED,CLUTCH_PRESSED,input.clutch);
  input.gear=Rig::decodeGear(stableSwitches);
  uint16_t gearBits=stableSwitches&63;
  input.reserved[0]=(gearBits && (gearBits&(gearBits-1)))?1:0;
  input.buttons=(stableSwitches>>6)&0x3ff;
  input.buttons &= ~(1<<2);
  if(hazard) input.buttons|=1<<2;
  if(starterUntil && int32_t(starterUntil-now)>0) input.buttons|=1<<1;
  else starterUntil=0;
  // Ignition is a physical maintained switch and always overrides touch starter.
  if(!(input.buttons&1)) input.buttons&=~(1<<1);
  input.joyX=analogRead(JOY_X); input.joyY=analogRead(JOY_Y);
  joy.update(digitalRead(JOY_BUTTON)==LOW,now); input.joyButton=joy.stable;
  if(!mcpOK) { input.throttle=0; input.brake=4095; input.clutch=4095; input.gear=0; input.buttons=0; }
}
void updateMotor(uint32_t now) {
  if(!ENABLE_FFB || !pwmOK || !armed || !centered || !mcpOK || !gameAlive(now) || !screenAlive(now) || !(input.buttons&1) || input.reserved[0] || abs(readEncoder())>=STEERING_HARD_LIMIT) {
    disarm(); return;
  }
  int target=feedback.torque*MOTOR_SIGN*PWM_LIMIT/1000;
  if(abs(feedback.torque)<20) target=0;
  // Slew 2 duty steps/ms; turn both legs OFF for >=1 control tick on reversal.
  if((applied>0 && target<0)||(applied<0 && target>0)) { motorOff(); return; }
  if(target>applied) applied=min(target,applied+2);
  else applied=max(target,applied-2);
  ledcWrite(RPWM,0); ledcWrite(LPWM,0);
  if(applied==0) { digitalWrite(REN,LOW); digitalWrite(LEN,LOW); return; }
  ledcWrite(applied>0?RPWM:LPWM,abs(applied));
  digitalWrite(REN,HIGH); digitalWrite(LEN,HIGH);
}
void sendDashboard(uint32_t now) {
  Rig::Dashboard state;
  if(gameAlive(now)) state.game=feedback;
  state.encoder=input.encoder; state.throttle=input.throttle; state.brake=input.brake; state.clutch=input.clutch;
  state.rawThrottle=rawThrottle; state.rawBrake=rawBrake; state.rawClutch=rawClutch;
  state.buttons=input.buttons; state.gear=input.gear;
  state.status=(ethernetOK?Rig::LINK:0)|(gameAlive(now)?Rig::GAME:0)|(mcpOK?Rig::MCP:0)|(armed?Rig::ARMED:0)|(centered?Rig::CENTERED:0)|(ENABLE_FFB?Rig::FFB_ALLOWED:0)|(input.reserved[0]?Rig::GEAR_FAULT:0);
  state.ack=haveCommand?lastCommand:0; state.result=commandResult;
  uint8_t bytes[57]; size_t n=Rig::frame(bytes,Rig::DASHBOARD,++dashSequence,&state,sizeof(state));
  if(dash.availableForWrite()>=int(n)) dash.write(bytes,n);
}
void setup() {
  pinMode(REN,OUTPUT); pinMode(LEN,OUTPUT); digitalWrite(REN,LOW); digitalWrite(LEN,LOW);
  pinMode(RPWM,OUTPUT); pinMode(LPWM,OUTPUT); digitalWrite(RPWM,LOW); digitalWrite(LPWM,LOW);
  bool r=ledcAttach(RPWM,20000,8), l=ledcAttach(LPWM,20000,8); pwmOK=r&&l; motorOff();
  uint32_t previousStage=lastStage; lastStage=ST_BOOT;
  Serial0.setTxBufferSize(512);
  Serial0.begin(115200,SERIAL_8N1,44,43);
  Serial0.printf("\n[RIG fix1] reset=%d previousStage=%lu FFB=%d\n",int(esp_reset_reason()),(unsigned long)previousStage,int(ENABLE_FFB));
  dash.setRxBufferSize(512); dash.begin(115200,SERIAL_8N1,DASH_RX,DASH_TX);
  analogReadResolution(12); analogSetAttenuation(ADC_11db);
  pinMode(JOY_BUTTON,INPUT_PULLUP);
  pinMode(ENCODER_A,INPUT_PULLUP); pinMode(ENCODER_B,INPUT_PULLUP);
  encoderPrevious=(digitalRead(ENCODER_A)<<1)|digitalRead(ENCODER_B);
  attachInterrupt(digitalPinToInterrupt(ENCODER_A),encoderISR,CHANGE);
  attachInterrupt(digitalPinToInterrupt(ENCODER_B),encoderISR,CHANGE);
  Wire.begin(I2C_SDA,I2C_SCL,100000); Wire.setTimeOut(5); mcpOK=initMCP();
  for(uint8_t address=0x20;address<=0x27;address++) {
    Wire.beginTransmission(address);
    if(Wire.endTransmission()==0) i2cAckMask|=1<<(address-0x20);
  }
  pinMode(ETH_CS,OUTPUT); digitalWrite(ETH_CS,HIGH);
  pinMode(ETH_RST,OUTPUT); digitalWrite(ETH_RST,LOW); delay(25); digitalWrite(ETH_RST,HIGH); delay(150);
  SPI.begin(ETH_SCK,ETH_MISO,ETH_MOSI,ETH_CS);
  // Read W5500 VERSIONR directly at 1 MHz before the library's 14 MHz probe.
  // 0x04 is W5500; 0x00/0xff suggests missing SPI response, not an IP error.
  SPI.beginTransaction(SPISettings(1000000,MSBFIRST,SPI_MODE0));
  digitalWrite(ETH_CS,LOW);
  SPI.transfer(0x00); SPI.transfer(0x39); SPI.transfer(0x00);
  w5500Version=SPI.transfer(0x00);
  digitalWrite(ETH_CS,HIGH); SPI.endTransaction();
  Serial0.printf("[RIG] W5500 VERSION@1MHz=%02x (expected 04), I2C ACK20..27=%02x\n",w5500Version,i2cAckMask);
  Ethernet.init(ETH_CS);
  lastStage=ST_ETH_INIT;
  Ethernet.begin(mac,localIP,IPAddress(0,0,0,0),IPAddress(0,0,0,0),subnet);
  ethernetReady=Ethernet.hardwareStatus()==EthernetW5500;
  lastStage=ST_UDP_INIT;
  if(ethernetReady) {
    Ethernet.setRetransmissionTimeout(10); Ethernet.setRetransmissionCount(1);
    udpReady=udp.begin(9999)==1;
  }
  Serial0.printf("[RIG] W5500=%d UDP=%d MCP=%d SPI SCK=%d MISO=%d MOSI=%d CS=%d RST=%d\n",int(ethernetReady),int(udpReady),int(mcpOK),ETH_SCK,ETH_MISO,ETH_MOSI,ETH_CS,ETH_RST);
  esp_task_wdt_config_t wdt={}; wdt.timeout_ms=1000; wdt.idle_core_mask=0; wdt.trigger_panic=true;
  if(esp_task_wdt_status(nullptr)==ESP_ERR_INVALID_STATE) esp_task_wdt_init(&wdt);
  else esp_task_wdt_reconfigure(&wdt);
  esp_task_wdt_add(nullptr);
  Serial0.println("[RIG] Ready. Motor locked. Missing W5500: check SPI/power, then reset S3.");
}
void loop() {
  uint32_t now=millis();
  static uint32_t scanAt=0,sendAt=0,dashAt=0,motorAt=0,linkAt=0;
  lastStage=ST_LINK;
  if(uint32_t(now-linkAt)>=100) { linkAt=now; ethernetOK=ethernetReady && udpReady && Ethernet.linkStatus()==LinkON; }
  // Expire the old lease BEFORE accepting traffic after a scheduling/network gap.
  if(armed && (!gameAlive(now) || !screenAlive(now))) disarm();
  lastStage=ST_UART_RX; readDashboard(now);
  lastStage=ST_UDP_RX; readFeedback(now);
  if(uint32_t(now-scanAt)>=5) { lastStage=ST_INPUT; scanAt=now; scanInputs(now); }
  if(uint32_t(now-motorAt)>=1) { lastStage=ST_MOTOR; motorAt=now; updateMotor(now); }
  if(!screenAlive(now)) { disarm(); haveCommand=false; starterUntil=0; }
  if(uint32_t(now-sendAt)>=10 && ethernetReady && udpReady && ethernetOK) {
    lastStage=ST_UDP_TX; sendAt=now;
    if(udp.beginPacket(pcIP,9999) && udp.write(reinterpret_cast<uint8_t*>(&input),sizeof(input))==sizeof(input) && udp.endPacket()) ++txOK;
    else ++txFailed;
  }
  if(uint32_t(now-dashAt)>=50) { lastStage=ST_UART_TX; dashAt=now; sendDashboard(now); }
  static uint32_t debugAt=0;
  if(uint32_t(now-debugAt)>=1000 && Serial0.availableForWrite()>=180) {
    debugAt=now;
    Serial0.printf("[RIG] ms=%lu chip=%d ver=%02x i2c=%02x link=%d udp=%d mcp=%d tx=%lu fail=%lu adc=%u/%u/%u gear=%u buttons=%03x\n",(unsigned long)now,int(ethernetReady),w5500Version,i2cAckMask,int(ethernetOK),int(udpReady),int(mcpOK),(unsigned long)txOK,(unsigned long)txFailed,rawThrottle,rawBrake,rawClutch,input.gear,input.buttons);
  }
  lastStage=ST_IDLE;
  esp_task_wdt_reset(); delay(1);
}
