#include <cassert>
#include <cstring>
#include <iostream>
#include "../ControlBoxS3/RigProtocol.h"
int main() {
 using namespace Rig;
 assert(crc16(reinterpret_cast<const uint8_t*>("123456789"),9)==0x29b1);
 Input input; input.encoder=-123456; input.throttle=4095; input.brake=2048; input.clutch=123;
 input.gear=6; input.buttons=0x3ff; input.joyButton=1;
 const auto* bytes=reinterpret_cast<const uint8_t*>(&input);
 assert(bytes[0]==0x49 && bytes[1]==0x53 && bytes[18]==6 && bytes[19]==255 && bytes[20]==3 && bytes[25]==1);
 int32_t decodedEncoder=0; memcpy(&decodedEncoder,bytes+8,4);
 assert(decodedEncoder==-123456);
 for(int i=0;i<6;i++) assert(decodeGear(1<<i)==i+1);
 assert(decodeGear(0)==0 && decodeGear(3)==0 && decodeGear(0xffff)==0);
 int pos=0; uint8_t states[]={0,2,3,1,0};
 for(int i=1;i<5;i++) pos+=quadrature(states[i-1],states[i]);
 assert(pos==4);
 for(int i=4;i>0;i--) pos+=quadrature(states[i],states[i-1]);
 assert(pos==0 && quadrature(0,3)==0 && quadrature(2,1)==0);
 Debouncer d; assert(!d.update(true,100)); assert(!d.update(false,105));
 assert(!d.update(true,110)); assert(!d.update(true,129)); assert(d.update(true,130) && d.stable);
 assert(!d.update(false,135)); assert(d.update(false,155) && !d.stable);
 d.changed=0xfffffff0; d.candidate=true; assert(d.update(true,8));
 assert(newer(1,65535) && !newer(3,3) && !newer(4,5));
 Dashboard dashboard; dashboard.game.speed10=1205; dashboard.game.sequence=65535;
 uint8_t framed[57]; size_t n=frame(framed,DASHBOARD,65000,&dashboard,sizeof(dashboard));
 Parser p; int accepted=0;
 for(size_t i=0;i<n;i++) accepted+=p.push(framed[i],i);
 assert(accepted==1 && p.type==DASHBOARD && p.sequence==65000);
 Dashboard decoded; memcpy(&decoded,p.data(),sizeof(decoded)); assert(decoded.game.speed10==1205);
 // Every single bit flip in the version/header/payload/CRC must fail integrity checks.
 for(size_t index=2;index<n;index++) for(int bit=0;bit<8;bit++) {
   uint8_t bad[57]; memcpy(bad,framed,n); bad[index]^=1<<bit; Parser q; int count=0;
   for(size_t i=0;i<n;i++) count+=q.push(bad[i],i);
   assert(count==0);
   // Next complete frame after an inter-byte timeout always resynchronizes.
   for(size_t i=0;i<n;i++) count+=q.push(framed[i],200+i);
   assert(count==1);
 }
 // All truncated frames, garbage prefix and concatenated frames.
 for(size_t cut=1;cut<n;cut++) {
   Parser q; for(size_t i=0;i<cut;i++) assert(!q.push(framed[i],i));
   int count=0; q.push(0x11,100); q.push(0xa5,101); q.push(0x19,102);
   for(int repeat=0;repeat<3;repeat++) for(size_t i=0;i<n;i++) count+=q.push(framed[i],200+repeat*n+i);
   assert(count==3);
 }
 uint8_t cmd=START; n=frame(framed,COMMAND,42,&cmd,1); assert(n==10);
 assert(frame(framed,COMMAND,42,&cmd,49)==0);
 std::cout << "PASS: UDP layouts, quadrature x4, gear conflicts, debounce/wrap, UART CRC/corruption/truncation/resync, sequence wrap\n";
}
