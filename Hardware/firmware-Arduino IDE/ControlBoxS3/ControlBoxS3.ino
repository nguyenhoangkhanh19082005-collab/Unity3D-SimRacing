#include <Arduino.h>
#include <SPI.h>
#include <Wire.h>
#include <Ethernet.h>
#include <EthernetUdp.h>
#include <esp_task_wdt.h>
#include <esp_system.h>
#include "Config.h"
#include "RigProtocol.h"
#include "AdcFilter.h"
#if !defined(CONFIG_IDF_TARGET_ESP32S3)
#error Select ESP32S3 Dev Module
#endif
// CH343 bridge diagnostics always use UART0 GPIO43/44, independent of USB CDC.
// Dashboard UART uses GPIO39/40; GPIO43/44 remain available to USB-UART.

HardwareSerial dash(1);
EthernetUDP udp;
IPAddress localIP(192,168,1,150), pcIP(192,168,1,100), subnet(255,255,255,0);
// Until the game answers, input packets go to the limited broadcast address so the PC
// receives them whatever its exact address is. The first feedback packet tells us the
// game's real IP; from then on packets are unicast to it (back to broadcast after 1 s of silence).
IPAddress broadcastIP(255,255,255,255), gameIP(192,168,1,100);
bool gameKnown=false;
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
uint8_t w5500Version=0, i2cAckMask=0, mcpAddr=0x20, mcpFailures=0;
uint32_t mcpRetryAt=0;
int activeSDA=I2C_SDA, activeSCL=I2C_SCL;
bool hazard=false, haveFeedback=false, haveHeartbeat=false, haveCommand=false;
uint32_t lastFeedback=0,lastHeartbeat=0,lastCommandTime=0,starterUntil=0;
uint16_t lastCommand=0, dashSequence=0, stableSwitches=0;
uint8_t commandResult=0;
int applied=0;
uint16_t rawThrottle=0,rawBrake=0,rawClutch=0;
uint8_t fallbackGear=0;
// The shifter microswitches are only pressed while the lever is pushed into a gate: once the lever is
// let go they open again. The last gear selected is therefore latched here and kept when no switch (or
// two switches) is pressed and while the MCP23017 recovers. The game's key N selects neutral.
uint8_t latchedGear=0;
int8_t prevJoyShift=0;

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
  Wire.beginTransmission(mcpAddr); Wire.write(reg); Wire.write(value); return Wire.endTransmission()==0;
}
bool initMCP() {
  // BANK=0 after power/reset. IOCON sequential mode + all inputs/pullups.
  return writeMCP(0x0a,0) && writeMCP(0,255) && writeMCP(1,255) && writeMCP(0x0c,255) && writeMCP(0x0d,255);
}
bool readMCP(uint16_t &value) {
  Wire.beginTransmission(mcpAddr); Wire.write(0x12);
  if(Wire.endTransmission(false)!=0 || Wire.requestFrom(mcpAddr,uint8_t(2))!=2) return false;
  uint16_t raw=Wire.read(); raw|=uint16_t(Wire.read())<<8; value=uint16_t(~raw); return true;
}
// Looks for the MCP23017 (0x20..0x27) on the given pins. Never call pinMode() after
// Wire.begin(): on arduino-esp32 3.x that detaches the pins from the I2C peripheral.
bool probeI2C(int sda, int scl) {
  Wire.end();
  Wire.begin(sda, scl, 100000); Wire.setTimeOut(5);
  uint8_t mask=0, firstFound=0;
  for(uint8_t addr=0x20; addr<=0x27; addr++) {
    Wire.beginTransmission(addr);
    if(Wire.endTransmission()==0) { mask|=1<<(addr-0x20); if(!firstFound) firstFound=addr; }
  }
  i2cAckMask=mask;
  if(!mask) return false;
  activeSDA=sda; activeSCL=scl; mcpAddr=firstFound;
  mcpOK=initMCP(); mcpFailures=0;
  return mcpOK;
}
// Tries the configured pins first, then the swapped pair (a common wiring mistake).
bool findMCP() {
  if(probeI2C(I2C_SDA,I2C_SCL)) return true;
  if(probeI2C(I2C_SCL,I2C_SDA)) { Serial0.printf("[RIG] CANH BAO: MCP23017 chi tra loi khi DAO SDA/SCL (SDA=%d SCL=%d). Sua Config.h hoac day.\n",I2C_SCL,I2C_SDA); return true; }
  probeI2C(I2C_SDA,I2C_SCL);
  return false;
}
// ---- ADC anti-jitter: oversample + trimmed mean + EMA + hysteresis ----
// Values inside the filter are in 1/16 ADC count so small drifts are not lost to truncation.
constexpr int ADC_SAMPLES=16;        // per scan (5 ms), trimmed mean drops min & max
constexpr int ADC_EMA_SHIFT=3;       // smoothing: 1/8 per scan (~40 ms time constant)
constexpr int PEDAL_DEADBAND=2;      // raw counts the filtered value must move before output changes
constexpr int JOY_DEADBAND=6;
AdcFilter fThrottle,fBrake,fClutch,fJoyX,fJoyY;
int32_t readTrimmed16(int pin) {     // returns mean in 1/16 count
  int32_t sum=0, lo=4095, hi=0;
  for(int i=0;i<ADC_SAMPLES;i++) { int v=analogRead(pin); sum+=v; if(v<lo) lo=v; if(v>hi) hi=v; }
  return (sum-lo-hi)*16/(ADC_SAMPLES-2);
}
int32_t filterAdc(AdcFilter& f,int pin,int deadband) {
  int32_t s=readTrimmed16(pin);
  if(f.ema<0) { f.ema=f.held=s; }
  else {
    int32_t d=s-f.ema;
    f.ema+= (d>=0) ? (d>>ADC_EMA_SHIFT) : -((-d)>>ADC_EMA_SHIFT);
    int32_t h=f.ema-f.held; if(h<0) h=-h;
    if(h>=deadband*16) f.held=f.ema;  // hysteresis: ignore noise smaller than deadband
  }
  return f.held;
}
uint16_t pedalNorm(int32_t held16,int released,int pressed) {
  int64_t num=int64_t(held16-released*16)*4095;
  int32_t n=int32_t(num/(int64_t(pressed-released)*16));
  n=constrain(n,0,4095);
  if(n<=16) n=0; else if(n>=4079) n=4095;   // snap to ends so a resting pedal reads exactly 0 / 4095
  return uint16_t(n);
}
bool gameAlive(uint32_t now) { return ethernetOK && haveFeedback && uint32_t(now-lastFeedback)<FEEDBACK_TIMEOUT; }
bool screenAlive(uint32_t now) { return haveHeartbeat && uint32_t(now-lastHeartbeat)<SCREEN_TIMEOUT; }
void command(uint8_t cmd,uint32_t now) {
  commandResult=0;
  switch(cmd) {
    case Rig::STOP: disarm(); break;
    case Rig::START:
      if(!mcpOK || (stableSwitches&(1<<6))) starterUntil=now+250;
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
    IPAddress from=udp.remoteIP();
    // Accept the game from any address on our /24 (the W5500 has no gateway, so it could not answer anyone else).
    bool sameSubnet=from[0]==localIP[0] && from[1]==localIP[1] && from[2]==localIP[2] && from[3]!=localIP[3];
    if(size!=sizeof(Rig::Feedback) || !sameSubnet || udp.remotePort()!=9999) { udp.flush(); continue; }
    Rig::Feedback candidate;
    if(udp.read(reinterpret_cast<uint8_t*>(&candidate),sizeof(candidate))!=sizeof(candidate)) continue;
    if(candidate.magic!=0x4642 || candidate.torque < -1000 || candidate.torque>1000 || candidate.gear>6 || candidate.fuel>100 || candidate.rpm>20000) continue;
    if(gameAlive(now) && !Rig::newer(candidate.sequence,feedback.sequence)) continue;
    feedback=candidate; haveFeedback=true; lastFeedback=now;
    if(!gameKnown || gameIP!=from) Serial0.printf("[RIG] Game found at %u.%u.%u.%u\n",from[0],from[1],from[2],from[3]);
    gameIP=from; gameKnown=true;
  }
}
void scanInputs(uint32_t now) {
  uint16_t raw=0;
  bool valid=false;
  if(mcpOK) {
    // Glitched I2C reads must not drop the shifter: give up only after 5 failures in a row (25 ms).
    if(readMCP(raw)) { valid=true; mcpFailures=0; }
    else if(++mcpFailures>=5) { mcpOK=false; Serial0.println("[RIG] MCP23017 mat ket noi -> che do du phong (joystick doi so)"); }
  }
  if(!mcpOK) {
    stableSwitches=0; disarm();
    for(auto &button:switches) button=Rig::Debouncer();
  } else if(valid) {
    for(int i=0;i<16;i++) {
      bool changed=switches[i].update(raw&(1<<i),now);
      if(switches[i].stable) stableSwitches|=1<<i; else stableSwitches&=~(1<<i);
      if(i==8 && changed && switches[i].stable) hazard=!hazard; // Momentary hazard -> latched state.
    }
  }
  input.timestamp=now; input.encoder=readEncoder();
  int32_t t16=filterAdc(fThrottle,PEDAL_THROTTLE,PEDAL_DEADBAND);
  int32_t b16=filterAdc(fBrake,PEDAL_BRAKE,PEDAL_DEADBAND);
  int32_t c16=filterAdc(fClutch,PEDAL_CLUTCH,PEDAL_DEADBAND);
  rawThrottle=t16>>4; rawBrake=b16>>4; rawClutch=c16>>4;
  input.throttle=pedalNorm(t16,THROTTLE_RELEASED,THROTTLE_PRESSED);
  input.brake=pedalNorm(b16,BRAKE_RELEASED,BRAKE_PRESSED);
  input.clutch=pedalNorm(c16,CLUTCH_RELEASED,CLUTCH_PRESSED);
  input.joyX=filterAdc(fJoyX,JOY_X,JOY_DEADBAND)>>4; input.joyY=filterAdc(fJoyY,JOY_Y,JOY_DEADBAND)>>4;
  bool joyChanged=joy.update(digitalRead(JOY_BUTTON)==LOW,now);
  input.joyButton=joy.stable;

  if(mcpOK) {
    // GEAR_PIN (Config.h) says which MCP23017 input (GPA0..GPA5) each gear 1..5,R presses.
    uint16_t gearBits=0;
    for(int g=0; g<6; g++) if(stableSwitches & (1<<GEAR_PIN[g])) gearBits |= 1<<g;
    uint8_t decoded=Rig::decodeGear(gearBits);
    if(decoded) { latchedGear=decoded; fallbackGear=decoded; } // Exactly one switch pressed: new gear.
    input.gear=latchedGear; // Lever released (no switch) or between gates (two switches): keep the gear.
    input.reserved[0]=(gearBits && (gearBits&(gearBits-1)))?1:0;
    input.buttons=(stableSwitches>>6)&0x3ff;
    input.buttons &= ~(1<<2);
    if(hazard) input.buttons|=1<<2;
    if(starterUntil && int32_t(starterUntil-now)>0) input.buttons|=1<<1;
    else starterUntil=0;
    // Ignition is a physical maintained switch and always overrides touch starter.
    if(!(input.buttons&1)) input.buttons&=~(1<<1);
  } else {
    // Fallback mode when MCP23017 is offline: pass real pedals & encoder through!
    // Ignition is kept ON (bit 0 = 1). Pressing the joystick button (GPIO17) gives a 250 ms starter pulse.
    // Moving joystick Y full up/down (>3300 or <800) also shifts up/down, starting from the latched gear.
    input.reserved[0]=0;
    int8_t shiftDir = (input.joyY > 3300) ? 1 : (input.joyY < 800) ? -1 : 0;
    if(shiftDir != 0 && prevJoyShift == 0) {
      if(shiftDir > 0) {
        if(fallbackGear == 6) fallbackGear = 0;
        else if(fallbackGear < 5) fallbackGear++;
      } else {
        if(fallbackGear == 0) fallbackGear = 6;
        else if(fallbackGear == 6) fallbackGear = 0;
        else fallbackGear--;
      }
    }
    if(shiftDir != 0 && prevJoyShift == 0) latchedGear = fallbackGear;
    prevJoyShift = shiftDir;
    if(joyChanged && joy.stable) {
      starterUntil = now + 250;
    }
    input.gear = latchedGear;
    input.buttons = 1; // Ignition ON
    if(hazard) input.buttons |= (1<<2);
    if(starterUntil && int32_t(starterUntil-now)>0) input.buttons |= (1<<1);
    else starterUntil=0;
  }
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
  Serial0.printf("\n[RIG fix5] reset=%d previousStage=%lu FFB=%d\n",int(esp_reset_reason()),(unsigned long)previousStage,int(ENABLE_FFB));
  dash.setRxBufferSize(512); dash.begin(115200,SERIAL_8N1,DASH_RX,DASH_TX);
  analogReadResolution(12); analogSetAttenuation(ADC_11db);
  pinMode(JOY_BUTTON,INPUT_PULLUP);
  pinMode(ENCODER_A,INPUT_PULLUP); pinMode(ENCODER_B,INPUT_PULLUP);
  encoderPrevious=(digitalRead(ENCODER_A)<<1)|digitalRead(ENCODER_B);
  attachInterrupt(digitalPinToInterrupt(ENCODER_A),encoderISR,CHANGE);
  attachInterrupt(digitalPinToInterrupt(ENCODER_B),encoderISR,CHANGE);
  mcpOK=findMCP();
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
  Serial0.println("[RIG] Ready. Motor locked. Pedals & encoder unlocked even if MCP=0.");
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
  // MCP23017 missing or dropped out: look for it again once a second.
  if(!mcpOK && uint32_t(now-mcpRetryAt)>=1000) {
    lastStage=ST_INPUT; mcpRetryAt=now;
    if(findMCP()) Serial0.printf("[RIG] MCP23017 OK addr=%02x SDA=%d SCL=%d\n",mcpAddr,activeSDA,activeSCL);
  }
  if(uint32_t(now-sendAt)>=10 && ethernetReady && udpReady && ethernetOK) {
    lastStage=ST_UDP_TX; sendAt=now;
    bool unicast=gameKnown && uint32_t(now-lastFeedback)<1000;
    if(udp.beginPacket(unicast?gameIP:broadcastIP,9999) && udp.write(reinterpret_cast<uint8_t*>(&input),sizeof(input))==sizeof(input) && udp.endPacket()) ++txOK;
    else ++txFailed;
  }
  if(uint32_t(now-dashAt)>=50) { lastStage=ST_UART_TX; dashAt=now; sendDashboard(now); }
  static uint32_t debugAt=0;
  if(uint32_t(now-debugAt)>=1000 && Serial0.availableForWrite()>=220) {
    debugAt=now;
    bool unicast=gameKnown && uint32_t(now-lastFeedback)<1000;
    Serial0.printf("[RIG] ms=%lu chip=%d ver=%02x i2c=%02x link=%d udp=%d mcp=%d tx=%lu fail=%lu dst=%s game=%d enc=%ld adc=%u/%u/%u out=%u/%u/%u gear=%u sw=%04x btn=%03x\n",(unsigned long)now,int(ethernetReady),w5500Version,i2cAckMask,int(ethernetOK),int(udpReady),int(mcpOK),(unsigned long)txOK,(unsigned long)txFailed,unicast?"game":"bcast",int(gameAlive(now)),(long)input.encoder,rawThrottle,rawBrake,rawClutch,input.throttle,input.brake,input.clutch,input.gear,stableSwitches,input.buttons);
  }
  lastStage=ST_IDLE;
  esp_task_wdt_reset(); delay(1);
}
