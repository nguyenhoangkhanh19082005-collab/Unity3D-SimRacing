#include <Arduino.h>
#include <SPI.h>
#include <TFT_eSPI.h>
#include <XPT2046_Touchscreen.h>
#include "Config.h"
#include "RigProtocol.h"
#if !defined(CONFIG_IDF_TARGET_ESP32)
#error Select ESP32 Dev Module (WROOM32)
#endif
// Use the SPI peripheral opposite to TFT_eSPI, with independent physical pins.
#ifdef USE_HSPI_PORT
SPIClass touchSPI(VSPI);
#else
SPIClass touchSPI(HSPI);
#endif
TFT_eSPI tft;
TFT_eSprite panel(&tft);
XPT2046_Touchscreen touch(TOUCH_CS,TOUCH_IRQ);
HardwareSerial box(2);
Rig::Parser parser;
Rig::Dashboard state;
uint32_t lastState=0, pendingSince=0, lastRetry=0;
uint16_t commandSequence=0, pendingSequence=0;
uint8_t pendingCommand=0;
bool received=false, synced=false, diagnostics=false, spriteOK=false;
char notice[32]="Waiting for control box";

bool boxAlive(uint32_t now) { return received && uint32_t(now-lastState)<500; }
void sendFrame(uint8_t type,uint16_t seq,const void* data,uint8_t len) {
  uint8_t bytes[57]; size_t n=Rig::frame(bytes,type,seq,data,len); box.write(bytes,n);
}
void sendCommand(uint8_t cmd,uint32_t now) {
  if(!synced || !boxAlive(now)) { snprintf(notice,sizeof(notice),"Control box offline"); return; }
  if(pendingCommand && cmd!=Rig::STOP) { snprintf(notice,sizeof(notice),"Wait for command ACK"); return; }
  do { ++commandSequence; } while(commandSequence==0);
  pendingSequence=commandSequence; pendingCommand=cmd; pendingSince=lastRetry=now;
  sendFrame(Rig::COMMAND,pendingSequence,&pendingCommand,1);
  snprintf(notice,sizeof(notice),"Command sent...");
}
void readBox(uint32_t now) {
  for(int i=0;i<256 && box.available();i++) {
    if(!parser.push(box.read(),now) || parser.type!=Rig::DASHBOARD || parser.length!=sizeof(state)) continue;
    memcpy(&state,parser.data(),sizeof(state)); lastState=now; received=true;
    if(!synced) {
      commandSequence=state.ack; synced=true; pendingCommand=0;
      // Restart/reconnect always explicitly disarms the motor.
      sendCommand(Rig::STOP,now);
    } else if(pendingCommand && state.ack==pendingSequence) {
      snprintf(notice,sizeof(notice),state.result==0?"Command accepted":"Rejected: check interlocks");
      pendingCommand=0;
    }
  }
}
void button(int x,int y,int w,const char* text,uint16_t color) {
  tft.fillRoundRect(x,y,w,30,4,color); tft.setTextDatum(MC_DATUM); tft.setTextColor(TFT_WHITE,color);
  tft.drawString(text,x+w/2,y+15,2);
}
void drawButtons() {
  button(1,164,104,"START",0x0369); button(108,164,104,"HAZARD",0xa300); button(215,164,104,"DATA",0x3186);
  button(1,198,104,"CENTER hold",0x3186); button(108,198,104,"ARM hold",0x0369); button(215,198,104,"STOP FFB",TFT_RED);
}
void drawPanel(uint32_t now) {
  bool online=boxAlive(now), game=online && (state.status & Rig::GAME);
  panel.fillSprite(0x1082); panel.setTextDatum(TL_DATUM);
  panel.setTextColor(game?TFT_GREEN:TFT_ORANGE,0x1082);
  panel.drawString(!online?"UART OFFLINE":game?"GAME ONLINE":"GAME OFFLINE",5,3,2);
  panel.setTextColor(TFT_WHITE,0x1082);
  panel.drawString(online && (state.status & Rig::ARMED)?"FFB ARMED":"FFB LOCKED",209,3,2);
  if(diagnostics) {
    char line[64];
    snprintf(line,sizeof(line),"Encoder: %ld",long(state.encoder)); panel.drawString(line,5,29,2);
    snprintf(line,sizeof(line),"T:%u  B:%u  C:%u",state.throttle,state.brake,state.clutch); panel.drawString(line,5,50,2);
    snprintf(line,sizeof(line),"Gear:%u  Buttons:0x%03X",state.gear,state.buttons); panel.drawString(line,5,71,2);
    snprintf(line,sizeof(line),"ETH:%u MCP:%u Center:%u",!!(state.status&Rig::LINK),!!(state.status&Rig::MCP),!!(state.status&Rig::CENTERED)); panel.drawString(line,5,92,2);
    snprintf(line,sizeof(line),"ADC %u / %u / %u",state.rawThrottle,state.rawBrake,state.rawClutch); panel.drawString(line,5,112,2);
    panel.drawString((state.status&Rig::GEAR_FAULT)?"FAULT: multiple gear switches":(state.status&Rig::FFB_ALLOWED)?"FFB build enabled":"FFB disabled in Config.h",5,133,2);
  } else {
    char value[48];
    if(game) snprintf(value,sizeof(value),"%.1f km/h",state.game.speed10/10.0); else snprintf(value,sizeof(value),"--.- km/h");
    panel.setTextSize(3); panel.drawString(value,9,32,1); panel.setTextSize(1);
    snprintf(value,sizeof(value),"RPM %u",game?state.game.rpm:0); panel.drawString(value,9,65,2);
    const char* gears[]={"N","1","2","3","4","5","R"};
    snprintf(value,sizeof(value),"GEAR %s",game && state.game.gear<=6?gears[state.game.gear]:"-"); panel.drawString(value,211,65,2);
    panel.drawRect(9,87,300,11,TFT_DARKGREY);
    if(game) panel.fillRect(10,88,constrain(int(state.game.rpm)*298/8000,0,298),9,state.game.rpm>6500?TFT_RED:TFT_CYAN);
    if(game) snprintf(value,sizeof(value),"Fuel %u%%   Water %u C",state.game.fuel,state.game.temperature);
    else snprintf(value,sizeof(value),"Fuel --   Water --");
    panel.drawString(value,9,104,2);
    if(game) snprintf(value,sizeof(value),"%s %s %s %s",state.game.flags&1?"RUN":"OFF",state.game.flags&4?"LEFT":"",state.game.flags&8?"RIGHT":"",state.game.flags&32?"HIGH":state.game.flags&16?"LOW":"");
    else snprintf(value,sizeof(value),"No live game telemetry");
    panel.drawString(value,9,126,2);
  }
  panel.pushSprite(0,0);
  tft.fillRect(0,230,320,10,TFT_BLACK); tft.setTextDatum(TL_DATUM); tft.setTextColor(TFT_WHITE,TFT_BLACK); tft.drawString(notice,2,231,1);
}
int hitButton(int x,int y) {
  if(y>=164 && y<194) return x<107?0:x<214?1:2;
  if(y>=198 && y<228) return x<107?3:x<214?4:5;
  return -1;
}
void readTouch(uint32_t now) {
  static int held=-1;
  static uint32_t downAt=0;
  static bool pressed=false,fired=false;
  if(!touch.touched()) { held=-1; pressed=false; fired=false; return; }
  TS_Point point=touch.getPoint();
  int px=point.x,py=point.y;
  if(TOUCH_SWAP_XY) { int tmp=px; px=py; py=tmp; }
  int x=constrain(map(px,TOUCH_X_MIN,TOUCH_X_MAX,0,319),0,319);
  int y=constrain(map(py,TOUCH_Y_MIN,TOUCH_Y_MAX,0,239),0,239);
  if(TOUCH_INVERT_X) x=319-x; if(TOUCH_INVERT_Y) y=239-y;
  int hit=hitButton(x,y);
  if(!pressed) { pressed=true; held=hit; downAt=now; fired=false; }
  if(hit!=held) { held=-1; return; } // Sliding into another button never activates it.
  if(fired || held<0) return;
  uint32_t hold=held==4?1500:held==3?700:60;
  if(uint32_t(now-downAt)<hold) return;
  fired=true;
  switch(held) {
    case 0: sendCommand(Rig::START,now); break;
    case 1: sendCommand(Rig::HAZARD,now); break;
    case 2: diagnostics=!diagnostics; break;
    case 3: sendCommand(Rig::CENTER,now); break;
    case 4: sendCommand(Rig::ARM,now); break;
    case 5: sendCommand(Rig::STOP,now); break;
  }
}
void setup() {
  Serial.begin(115200); box.setRxBufferSize(512); box.begin(115200,SERIAL_8N1,UART_RX,UART_TX);
  pinMode(BACKLIGHT,OUTPUT); digitalWrite(BACKLIGHT,HIGH);
  tft.init(); tft.setRotation(1); tft.fillScreen(TFT_BLACK);
  touchSPI.begin(TOUCH_SCK,TOUCH_MISO,TOUCH_MOSI,TOUCH_CS); touch.begin(touchSPI); touch.setRotation(1);
  panel.setColorDepth(16); spriteOK=panel.createSprite(320,160)!=nullptr;
  drawButtons();
  if(!spriteOK) { tft.setTextColor(TFT_RED); tft.drawString("Display allocation failed",5,40,2); }
}
void loop() {
  uint32_t now=millis(); static uint32_t heartbeatAt=0,drawAt=0,touchAt=0;
  // Lease requires fresh inbound dashboard too: a broken S3->screen wire
  // must eventually disarm even when the screen->S3 wire still works.
  if(spriteOK && boxAlive(now) && uint32_t(now-heartbeatAt)>=100) { heartbeatAt=now; sendFrame(Rig::HEARTBEAT,0,nullptr,0); }
  readBox(now);
  if(!boxAlive(now)) { synced=false; pendingCommand=0; snprintf(notice,sizeof(notice),"Waiting for control box"); }
  if(pendingCommand) {
    if(uint32_t(now-pendingSince)>1000) { pendingCommand=0; snprintf(notice,sizeof(notice),"No ACK: check UART"); }
    else if(uint32_t(now-lastRetry)>=120) { lastRetry=now; sendFrame(Rig::COMMAND,pendingSequence,&pendingCommand,1); }
  }
  if(spriteOK && uint32_t(now-touchAt)>=10) { touchAt=now; readTouch(now); }
  if(spriteOK && uint32_t(now-drawAt)>=100) { drawAt=now; drawPanel(now); }
  delay(1);
}
