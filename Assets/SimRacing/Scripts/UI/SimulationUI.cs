using UnityEngine;
namespace SimRacing
{
    public sealed class SimulationUI : MonoBehaviour
    {
        SimulationApp app;GUIStyle title,heading,body,small,button,selected,big,toggleStyle;Texture2D panel,line;
        Vector2 settingsScroll;float fps;
        static readonly Color Ink=new Color(.90f,.94f,.96f),Muted=new Color(.73f,.8f,.85f),Accent=new Color(.29f,.88f,.72f);
        void Awake(){app=GetComponent<SimulationApp>();}
        void Update(){fps=Mathf.Lerp(fps,1/Mathf.Max(.001f,Time.unscaledDeltaTime),.04f);}
        Texture2D Solid(Color color){var t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();return t;}
        GUIStyle Text(int size,Color color,FontStyle weight=FontStyle.Normal)=>new GUIStyle(GUI.skin.label){fontSize=size,fontStyle=weight,normal={textColor=color},wordWrap=true};
        void Styles()
        {
            if(title!=null)return;panel=Solid(new Color(.025f,.045f,.065f,.93f));line=Solid(new Color(.15f,.22f,.27f));
            title=Text(55,Ink,FontStyle.Bold);heading=Text(25,Ink,FontStyle.Bold);body=Text(18,Ink);small=Text(14,Muted);big=Text(52,Ink,FontStyle.Bold);
            button=new GUIStyle(GUI.skin.button){fontSize=18,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(18,10,4,4),normal={background=Solid(new Color(.075f,.11f,.15f)),textColor=Ink},hover={background=Solid(new Color(.12f,.2f,.25f)),textColor=Color.white},active={background=Solid(new Color(.16f,.29f,.30f)),textColor=Color.white}};
            selected=new GUIStyle(button){normal={background=Solid(new Color(.12f,.29f,.28f)),textColor=Accent}};
            toggleStyle=new GUIStyle(GUI.skin.toggle){fontSize=18,normal={textColor=Ink},onNormal={textColor=Accent}};
        }
        void Label(float x,float y,float w,float h,string text,GUIStyle style=null)=>GUI.Label(new Rect(x,y,w,h),text,style??body);
        bool Button(float x,float y,float w,float h,string text,bool active=false)=>GUI.Button(new Rect(x,y,w,h),text,active?selected:button);
        void Panel(float x,float y,float w,float h)=>GUI.DrawTexture(new Rect(x,y,w,h),panel);
        void OnGUI()
        {
            Styles();GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
            string state=app.ScreenState;
            if(state=="drive"){Hud();return;}
            if(state=="loading"){Label(72,280,1100,80,"ĐANG CHUẨN BỊ CHUYẾN ĐI",heading);Label(72,330,1100,60,"Đường đua, xe và điều kiện thời tiết…",body);return;}
            if(state=="pause"){Hud();Panel(390,180,500,340);Label(430,214,400,50,"TẠM DỪNG",heading);if(Button(430,285,420,52,"Tiếp tục"))app.Resume();if(Button(430,351,420,52,"Trở về gara"))app.ToMenu();Label(430,430,420,55,"Sau khi tiếp tục: về N, nhả ga, giữ phanh rồi nhấn F9.",small);return;}
            if(state=="results"){Results();return;}
            Panel(0,0,1280,720);Label(48,32,450,40,"SIM / RACING",heading);Label(850,40,385,35,$"CẤP {1+app.Profile.xp/500}     {app.Profile.Stars} SAO     {app.Profile.credits:N0} CR",small);
            GUI.DrawTexture(new Rect(48,88,1184,1),line);
            if(state=="settings"){Settings();return;}
            Label(48,125,1184,75,"CẢM NHẬN TỪNG KHÚC CUA",title);Label(50,208,900,35,"Vô lăng thật. Số tay. Một chuyến đi của bạn.",body);
            if(Button(48,275,225,55,"Lái tự do",app.Mode=="free"))app.Mode="free";
            if(Button(48,341,225,55,"Đua nhanh",app.Mode=="race"))app.Mode="race";
            if(Button(48,407,225,55,"Career",app.Mode=="career"))app.Mode="career";
            if(Button(48,533,225,48,"Thiết lập rig & hình ảnh"))app.ScreenState="settings";
            if(Button(48,590,225,48,"Thoát"))Application.Quit();
            if(app.Mode=="career") {
                for(int i=0;i<DriverProgress.Challenges.Length;i++) {var c=DriverProgress.Challenges[i];bool locked=app.Profile.Stars<c.requires_stars;
                    if(Button(313,275+i*57,610,49,c.name+(locked?$"  ·  Cần {c.requires_stars} sao":""),i==app.ChallengeIndex))app.ChallengeIndex=i;}
                Label(954,278,270,170,DriverProgress.Challenges[app.ChallengeIndex].objective,heading);
                if(Button(954,490,278,66,"BẮT ĐẦU"))app.StartDrive();
            } else {
                Label(313,270,400,30,"CHỌN XE",small);
                string[] ids={"mercedes","ferrari","bugatti"},names={"Mercedes W210","Ferrari F40","Bugatti Centodieci"};
                for(int i=0;i<3;i++)if(Button(313+i*309,308,293,63,names[i]+(i==2&&app.Profile.Stars<7?" · 7 sao":""),app.CarId==ids[i]))app.CarId=ids[i];
                Label(313,389,430,28,"CUNG ĐƯỜNG",small);
                if(Button(313,426,450,53,"Đại Lộ Ven Biển",app.TrackId=="coastal"))app.TrackId="coastal";
                if(Button(779,426,453,53,"Đèo Sơn Cước",app.TrackId=="mountain"))app.TrackId="mountain";
                string[] weather={"clear","rain","fog","night"},labels={"Nắng","Mưa","Sương mù","Đêm"};
                for(int i=0;i<4;i++)if(Button(313+i*156,504,144,49,labels[i],app.Weather==weather[i]))app.Weather=weather[i];
                if(Button(954,504,278,66,"BẮT ĐẦU CHUYẾN ĐI"))app.StartDrive();
                Label(313,579,919,50,app.Mode=="race"?"3 vòng trên tuyến đóng · Đồng hồ vòng và ghost thành tích tốt nhất":"Giao thông hai chiều · Tiếp nhiên liệu · Cứu hộ khi dừng xe",small);
            }
            Label(313,637,919,46,string.IsNullOrEmpty(app.Message)?"Rig UDP 9999  ·  Ga, phanh, côn và cần số từ bộ điều khiển vật lý":app.Message,small);
        }
        void Hud()
        {
            if(!app.Car || !app.Session)return;var car=app.Car;var session=app.Session;
            Panel(22,18,660,68);Label(38,28,620,27,session.Objective,body);Label(38,57,620,22,$"{app.Weather.ToUpperInvariant()}   ·   {session.Elapsed:0.0} s   ·   {session.Distance/1000:0.00} km",small);
            Label(1095,28,165,30,$"{fps:0} FPS  ·  {app.Rig.PacketsPerSecond} Hz",small);
            if(!string.IsNullOrEmpty(app.Rig.Status)){Panel(225,96,830,62);Label(245,111,790,46,app.Rig.Status,body);}
            if(!session.Active && !session.Finished && !app.Rig.Held){Label(600,210,180,90,Mathf.CeilToInt(app.Countdown).ToString(),big);}
            if(app.CameraRig.Mode!=0){Panel(1000,526,258,148);Label(1020,536,175,65,car.Speed.ToString("0"),big);Label(1155,553,90,35,"km/h",small);Label(1020,604,220,40,$"{(car.Gear==0?"N":car.Gear==6?"R":car.Gear.ToString())}    {car.RPM:0} rpm",heading);}
            Panel(22,584,320,91);Label(38,594,290,25,$"XĂNG {car.Fuel:0} L   ·   XE {car.Health:0}%",body);Label(38,627,288,38,car.Refueling?"ĐANG TIẾP NHIÊN LIỆU":car.EngineRunning?"ĐỘNG CƠ ĐANG CHẠY":"ĐỀ MÁY TỪ RIG",small);
            Label(375,678,870,30,"F1–F4 / V: góc nhìn     F9: mở khóa rig     Backspace: cứu hộ khi dừng     Esc: tạm dừng",small);
            if(session.Notice!=""){Panel(300,172,680,48);Label(319,184,644,38,session.Notice,body);}
            if(car.Refueling || (Vector3.Distance(car.transform.position,app.Track.fuelStation)<15 && !car.Refueling))Label(385,620,550,45,car.Refueling?"Đang đổ xăng…":"Dừng tại trạm, tắt máy và giữ phanh để đổ xăng",body);
            MiniMap();
        }
        void MiniMap()
        {
            if(!app.Track || !app.Car)return;Panel(1085,92,173,173);var center=new Vector2(1171,178);float scale=.065f;
            Color old=GUI.color;GUI.color=Muted;
            foreach(var point in app.Track.route){Vector3 d=point-app.Car.transform.position;float x=Mathf.Clamp(d.x*scale,-76,76),y=Mathf.Clamp(-d.z*scale,-76,76);GUI.DrawTexture(new Rect(center.x+x,center.y+y,2,2),Texture2D.whiteTexture);}
            GUI.color=Accent;GUI.DrawTexture(new Rect(center.x-3,center.y-3,6,6),Texture2D.whiteTexture);GUI.color=old;
        }
        void Settings()
        {
            Label(48,117,1100,50,"THIẾT LẬP",heading);var c=app.Rig.calibration;
            Label(48,183,490,32,"Chất lượng hình ảnh",body);
            string[] names={"Nhẹ","Cân bằng","Cao"};for(int i=0;i<3;i++)if(Button(48+i*165,226,153,45,names[i],app.Quality==i)){app.Quality=i;app.ApplyQuality();}
            Label(48,299,470,30,$"Chuyển động đầu: {app.CameraMotion:P0}",body);app.CameraMotion=GUI.HorizontalSlider(new Rect(48,343,470,25),app.CameraMotion,0,1);
            Label(48,386,470,65,"Lái xe bằng rig. Bàn phím dùng cho camera, tạm dừng và cứu hộ.",body);
            Label(48,476,470,90,"Khởi hành: giữ phanh, về N, nhả ga → F9 → bật khóa và đề máy → đạp côn, vào số 1 và nhả côn từ từ.",small);
            Label(605,182,620,30,$"Rig: {app.Rig.PacketsPerSecond} gói/giây · Encoder {app.Rig.Raw.encoder}",body);
            Label(605,227,600,28,$"Nửa hành trình vô lăng: {c.steeringHalfRangeTicks:0} ticks",small);c.steeringHalfRangeTicks=GUI.HorizontalSlider(new Rect(605,266,610,22),c.steeringHalfRangeTicks,1000,20000);
            if(Button(605,307,285,45,"Đặt vị trí giữa hiện tại"))c.steeringCenterTicks=app.Rig.Raw.encoder;
            c.invertSteering=GUI.Toggle(new Rect(929,318,280,35),c.invertSteering," Đảo chiều vô lăng",toggleStyle);
            Label(605,375,610,28,$"Phản hồi lực: {c.ffbStrength:P0} · Giới hạn: {c.ffbLimit:P0}",small);
            c.ffbStrength=GUI.HorizontalSlider(new Rect(605,420,285,22),c.ffbStrength,0,1);c.ffbLimit=GUI.HorizontalSlider(new Rect(929,420,285,22),c.ffbLimit,.1f,1);
            c.invertThrottle=GUI.Toggle(new Rect(605,470,190,35),c.invertThrottle," Đảo chân ga",toggleStyle);c.invertBrake=GUI.Toggle(new Rect(810,470,190,35),c.invertBrake," Đảo phanh",toggleStyle);c.invertClutch=GUI.Toggle(new Rect(1015,470,190,35),c.invertClutch," Đảo côn",toggleStyle);
            Label(605,535,610,42,$"GA {app.Rig.Raw.throttle:P0}     PHANH {app.Rig.Raw.brake:P0}     CÔN {app.Rig.Raw.clutch:P0}",body);
            if(Button(48,615,270,55,"LƯU VÀ TRỞ VỀ")){app.SavePreferences();app.ScreenState="menu";}
        }
        void Results()
        {
            var r=app.LastResult;if(r==null)return;Panel(0,0,1280,720);
            Label(90,70,1100,70,r.passed?"HOÀN THÀNH CHUYẾN ĐI":"KẾT QUẢ CHUYẾN ĐI",title);
            Label(95,180,1000,70,$"{r.stars} SAO    +{r.xp} XP    +{r.credits} CR",heading);
            Label(95,275,1000,60,$"THỜI GIAN  {r.time:0.0} s       QUÃNG ĐƯỜNG  {r.distance:0.00} km       TÌNH TRẠNG XE  {r.health:0}%",body);
            Label(95,369,1060,100,r.feedback,heading);
            if(Button(95,557,360,60,"TRỞ VỀ GARA"))app.ToMenu();
        }
    }
}
