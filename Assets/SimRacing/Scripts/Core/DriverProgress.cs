using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace SimRacing
{
    [Serializable] public sealed class Challenge
    {
        public string id,name,description,objective,track,car,rules;
        public int laps=1,requires_stars,traffic_count=12,max_harsh_brakes=1;
        public float distance_m,speed_limit=80,min_health=75,target_time=300;
        public bool manual_required;
    }
    [Serializable] public sealed class ChallengeRecord {public string id;public int stars;public float bestTime;}
    [Serializable] public sealed class LapRecord {public string key;public float time;}
    [Serializable] public sealed class DriverProfile
    {
        public int version=1,xp,credits,cleanRaces;
        public float distance;
        public List<ChallengeRecord> challenges=new List<ChallengeRecord>();
        public List<LapRecord> bestLaps=new List<LapRecord>();
        public int Stars => challenges.Sum(x=>x.stars);
        public bool Valid => version==1 && xp>=0 && credits>=0 && cleanRaces>=0 && !float.IsNaN(distance) && !float.IsInfinity(distance) && distance>=0
            && challenges!=null && challenges.All(x=>x!=null && !string.IsNullOrEmpty(x.id) && x.stars>=0 && x.stars<=3 && x.bestTime>0 && !float.IsInfinity(x.bestTime))
            && bestLaps!=null && bestLaps.All(x=>x!=null && !string.IsNullOrEmpty(x.key) && x.time>0 && !float.IsInfinity(x.time));
    }
    public sealed class Result
    {
        public bool passed;public int stars,xp,credits;public string feedback;
        public float time,bestLap,distance,health;public int violations;
        public int position,racers;public string standings="";
    }
    public static class DriverProgress
    {
        public static readonly Challenge[] Challenges={
            new Challenge{id="manual_start",name="Khởi hành và dừng xe",description="Thực hành ga và côn",objective="Số tay · đi 180 m · dừng trong vòng xanh",track="coastal",car="mercedes",distance_m=180,speed_limit=40,manual_required=true,min_health=95,rules="training",traffic_count=0},
            new Challenge{id="clean_coast",name="Chuyến đi ven biển",description="Lái êm cùng xe dân sinh",objective="Đi 1,2 km và dừng tại điểm đến",track="coastal",car="mercedes",requires_stars=1,distance_m=1200,speed_limit=60,min_health=85,rules="road",traffic_count=8},
            new Challenge{id="mountain_line",name="Hành trình lên đèo",description="Đọc cua, chọn số, giữ tốc độ",objective="Đi 1,6 km và dừng tại điểm đến",track="mountain",car="mercedes",requires_stars=3,distance_m=1600,speed_limit=60,min_health=85,rules="road",traffic_count=10},
            new Challenge{id="speed_master",name="Coastal Time Trial",description="Tuyến đóng dành cho thể thao",objective="Một vòng · mục tiêu 180 giây",track="coastal",car="ferrari",requires_stars=5,target_time=180,rules="closed",traffic_count=0},
            new Challenge{id="road_master",name="Mountain Time Trial",description="Hai vòng đèo trên tuyến đóng",objective="Hai vòng · mục tiêu 420 giây",track="mountain",car="ferrari",requires_stars=8,laps=2,target_time=420,rules="closed",traffic_count=0}
        };
        public static Result Evaluate(DriveSession session)
        {
            if(session.Race!=null)return EvaluateRace(session);
            var c=session.challenge;var car=session.car;
            bool passed=session.Completed && car.Recoveries==0 && car.Health>=(c?.min_health??75) && (!(c?.manual_required??false)||session.ManualLaunch);
            bool clean=session.Violations==0 && car.Health>=95;
            bool bonus=session.RoadRules?session.HarshBrakes<=(c?.max_harsh_brakes??1)&&session.Overspeed<2:session.Elapsed>0&&session.Elapsed<=(c?.target_time??300);
            int stars=passed?1+(clean?1:0)+(bonus?1:0):0;
            return new Result{passed=passed,stars=stars,xp=passed?80+stars*55:20,credits=passed?300+stars*250:0,time=session.Elapsed,bestLap=session.BestLap,distance=session.Distance/1000,health=car.Health,violations=session.Violations,
                feedback=!passed?"Hoàn thành đúng tuyến, điểm dừng và thao tác số tay; giữ xe nguyên vẹn, không cứu hộ.":!clean?"Giữ khoảng cách và tốc độ để có chuyến đi sạch hơn.":!bonus?"Quan sát xa, giảm tốc sớm và giữ nhịp lái ổn định.":"Chuyến đi ổn định. Bạn đã hoàn thành tốt mục tiêu."};
        }
        /// <summary>Grid race: stars from the finishing position (win 3, podium 2, finish 1).</summary>
        static Result EvaluateRace(DriveSession session)
        {
            var race=session.Race;var car=session.car;
            race.MarkPlayerFinished(session.Completed,session.Elapsed);
            int position=race.PlayerPosition,racers=race.Entries.Count;
            bool passed=session.Completed;
            int stars=!passed?0:position==1?3:position<=3?2:1;
            return new Result{passed=passed,stars=stars,position=position,racers=racers,standings=race.StandingsReport(),
                xp=passed?60+(racers-position+1)*45:20,credits=passed?200+(racers-position+1)*180:0,
                time=session.Elapsed,bestLap=session.BestLap,distance=session.Distance/1000,health=car.Health,violations=session.Violations,
                feedback=!passed?"Bỏ cuộc · Xe hỏng hoặc chưa hoàn thành đủ số vòng.":position==1?$"CHIẾN THẮNG! Bạn về nhất trong {racers} tay đua.":position<=3?$"Lên bục podium · Hạng {position}/{racers}.":$"Về đích hạng {position}/{racers}. Vào cua sớm hơn và giữ đà trên đoạn thẳng."};
        }

        public static void Record(DriverProfile profile,DriveSession session,Result result,string key)
        {
            profile.xp+=result.xp;profile.credits+=result.credits;profile.distance+=result.distance;
            if(result.passed && result.violations==0 && result.health>=95)profile.cleanRaces++;
            if(result.passed && session.challenge!=null) {
                var r=profile.challenges.Find(x=>x.id==session.challenge.id);
                if(r==null){r=new ChallengeRecord{id=session.challenge.id,bestTime=result.time};profile.challenges.Add(r);}
                r.stars=Mathf.Max(r.stars,result.stars);r.bestTime=Mathf.Min(r.bestTime,result.time);
            }
            if(result.passed && result.bestLap>0 && result.bestLap<9999) {
                var r=profile.bestLaps.Find(x=>x.key==key);if(r==null){r=new LapRecord{key=key,time=result.bestLap};profile.bestLaps.Add(r);}r.time=Mathf.Min(r.time,result.bestLap);
            }
            LocalStore.Save("profile.json",profile);
        }
    }
}
