#pragma once
#include <stdint.h>
#include <stddef.h>
#include <string.h>
#ifdef ARDUINO_ARCH_ESP32
#include <esp_attr.h>
#define RIG_ISR IRAM_ATTR
#define RIG_DATA DRAM_ATTR
#else
#define RIG_ISR
#define RIG_DATA
#endif
namespace Rig {
#pragma pack(push, 1)
struct Input {
  uint16_t magic=0x5349, version=2;
  uint32_t timestamp=0;
  int32_t encoder=0;
  uint16_t throttle=0, brake=0, clutch=0;
  uint8_t gear=0;
  uint16_t buttons=0;
  uint16_t joyX=2048, joyY=2048;
  uint8_t joyButton=0, reserved[2]={0,0};
};
struct Feedback {
  uint16_t magic=0x4642;
  int16_t torque=0;
  uint16_t speed10=0, rpm=0;
  uint8_t gear=0, temperature=0, fuel=0, flags=0, rumbleHz=0, rumble=0;
  uint16_t sequence=0;
};
struct Dashboard {
  Feedback game;
  int32_t encoder=0;
  uint16_t throttle=0, brake=0, clutch=0, buttons=0;
  uint8_t gear=0, status=0;
  uint16_t rawThrottle=0, rawBrake=0, rawClutch=0;
  uint16_t ack=0;
  uint8_t result=0;
};
#pragma pack(pop)
static_assert(sizeof(Input)==28, "UDP input layout");
static_assert(offsetof(Input, buttons)==19 && offsetof(Input, joyX)==21, "UDP offsets");
static_assert(sizeof(Feedback)==16, "UDP feedback layout");
static_assert(sizeof(Dashboard)==39, "UART dashboard layout");
enum : uint8_t { HEARTBEAT=1, COMMAND=2, DASHBOARD=3 };
enum : uint8_t { START=1, HAZARD=2, CENTER=3, ARM=4, STOP=5 };
enum : uint8_t { LINK=1, GAME=2, MCP=4, ARMED=8, CENTERED=16, FFB_ALLOWED=32, GEAR_FAULT=64 };
inline bool newer(uint16_t a,uint16_t b) { uint16_t d=uint16_t(a-b); return d!=0 && d<0x8000; }
inline uint16_t crc16(const uint8_t* p,size_t n) {
  uint16_t crc=0xffff;
  while(n--) { crc^=uint16_t(*p++)<<8; for(int i=0;i<8;i++) crc=(crc&0x8000)?uint16_t((crc<<1)^0x1021):uint16_t(crc<<1); }
  return crc;
}
// A5 5A | version=1 type length seqLE | payload | CRC16-CCITT LE.
// CRC covers version through payload. Max payload 48; interbyte timeout 40ms.
inline size_t frame(uint8_t* out,uint8_t type,uint16_t seq,const void* data,uint8_t len) {
  if(len>48) return 0;
  out[0]=0xa5; out[1]=0x5a; out[2]=1; out[3]=type; out[4]=len;
  out[5]=uint8_t(seq); out[6]=uint8_t(seq>>8);
  if(len) memcpy(out+7,data,len);
  uint16_t crc=crc16(out+2,5+len); out[7+len]=uint8_t(crc); out[8+len]=uint8_t(crc>>8);
  return 9+len;
}
struct Parser {
  uint8_t buf[57]={}, used=0, type=0, length=0;
  uint16_t sequence=0;
  uint32_t last=0;
  const uint8_t* data() const { return buf+7; }
  bool push(uint8_t b,uint32_t now) {
    if(used && uint32_t(now-last)>40) used=0;
    last=now;
    if(used==0) { if(b==0xa5) buf[used++]=b; return false; }
    if(used==1) { if(b==0x5a) buf[used++]=b; else used=(b==0xa5)?1:0; return false; }
    buf[used++]=b;
    if((used==3 && buf[2]!=1) || (used==5 && buf[4]>48)) { used=0; return false; }
    if(used>=9 && used==uint8_t(9+buf[4])) {
      used=0; uint8_t len=buf[4];
      if(crc16(buf+2,5+len)!=(uint16_t(buf[7+len])|(uint16_t(buf[8+len])<<8))) return false;
      type=buf[3]; length=len; sequence=uint16_t(buf[5])|(uint16_t(buf[6])<<8); return true;
    }
    return false;
  }
};
struct Debouncer {
  bool stable=false, candidate=false;
  uint32_t changed=0;
  bool update(bool value,uint32_t now) {
    if(value!=candidate) { candidate=value; changed=now; }
    if(candidate!=stable && uint32_t(now-changed)>=20) { stable=candidate; return true; }
    return false;
  }
};
inline uint8_t decodeGear(uint16_t bits) {
  bits &= 63;
  if(!bits || (bits & (bits-1))) return 0;
  for(uint8_t i=0;i<6;i++) if(bits & (1<<i)) return i+1;
  return 0;
}
static RIG_DATA const int8_t quadratureTable[16]={0,-1,1,0, 1,0,0,-1, -1,0,0,1, 0,1,-1,0};
inline RIG_ISR int8_t quadrature(uint8_t previous,uint8_t current) {
  // ISR code/table remain available while the flash cache is disabled.
  return quadratureTable[(previous<<2)|current];
}
}
