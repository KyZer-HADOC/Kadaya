using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KadayaGame : MonoBehaviour
{
    class Enemy { public GameObject root; public float hp; public bool alive=true; }
    GameObject player, sword, camRig; Camera cam;
    readonly List<Enemy> enemies=new();
    Material ninja, shadow, skin, steel, ground, roof;
    Text title, prompt, hud;
    int hp=100, kills; float attackCD, attackTime, intro=3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot(){ if(FindObjectOfType<KadayaGame>()==null)new GameObject("KADAYA_GAME").AddComponent<KadayaGame>(); }

    void Awake(){
        Application.targetFrameRate=60; Screen.orientation=ScreenOrientation.LandscapeLeft;
        MakeMaterials(); World(); UI(); Invoke(nameof(Begin),3f);
    }
    Material M(Color c,float s=.6f){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=c;m.SetFloat("_Smoothness",s);return m;}
    GameObject Cube(string n,Vector3 p,Vector3 s,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.position=p;g.transform.localScale=s;g.GetComponent<Renderer>().material=m;return g;}
    GameObject Sphere(string n,Vector3 p,float s,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=n;g.transform.position=p;g.transform.localScale=Vector3.one*s;g.GetComponent<Renderer>().material=m;return g;}

    void MakeMaterials(){
        ninja=M(new Color(.012f,.014f,.022f),.8f); shadow=M(new Color(.008f,.008f,.012f),.5f);
        skin=M(new Color(.25f,.12f,.075f)); steel=M(new Color(.55f,.6f,.65f),.9f);
        ground=M(new Color(.025f,.035f,.045f)); roof=M(new Color(.16f,.025f,.035f));
    }
    void World(){
        Cube("VillageGround",new Vector3(0,-.4f,0),new Vector3(70,.8f,18),ground);
        for(int i=-7;i<8;i++){float x=i*4.5f;var b=Cube("RuinedHouse",new Vector3(x,1.5f,Random.Range(-6f,6f)),new Vector3(3.5f,3,2.5f),shadow);Cube("Roof",b.transform.position+Vector3.up*1.7f,new Vector3(4,.3f,3),roof);}
        for(int i=0;i<14;i++){var t=Cube("DeadTree",new Vector3(Random.Range(-32f,32f),2,Random.Range(-7f,7f)),new Vector3(.3f,4,.3f),shadow);t.transform.Rotate(0,0,Random.Range(-20,20));}
        Sphere("Moon",new Vector3(9,12,20),5, M(new Color(.55f,.65f,.7f),1));
        RenderSettings.ambientLight=new Color(.025f,.035f,.055f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.015f,.025f,.04f);RenderSettings.fogDensity=.018f;
        var l=new GameObject("MoonLight").AddComponent<Light>();l.type=LightType.Directional;l.intensity=.8f;l.color=new Color(.5f,.65f,1);l.transform.rotation=Quaternion.Euler(35,-30,0);
        player=Ninja(new Vector3(-10,1.2f,0),"KADAYA",false);
        camRig=new GameObject("CameraRig");cam=camRig.AddComponent<Camera>();cam.fieldOfView=55;cam.transform.position=new Vector3(-2,5,-15);cam.transform.LookAt(player.transform.position+Vector3.up);
    }
    GameObject Ninja(Vector3 p,string name,bool enemy){
        var r=new GameObject(name);r.transform.position=p;var body=Cube("Body",p+Vector3.up*1.1f,new Vector3(.8f,1.7f,.55f),enemy?shadow:ninja);body.transform.SetParent(r.transform);
        var head=Sphere("Head",p+Vector3.up*2.25f,.8f,skin);head.transform.SetParent(r.transform);
        var mask=Cube("Mask",p+new Vector3(0,2.35f,.35f),new Vector3(.85f,.4f,.15f),shadow);mask.transform.SetParent(r.transform);
        foreach(float x in new[]{-.25f,.25f}){var leg=Cube("Leg",p+new Vector3(x,.2f,0),new Vector3(.23f,1.2f,.3f),ninja);leg.transform.SetParent(r.transform);}
        var kat=Cube("Katana",p+new Vector3(.9f,1.2f,.05f),new Vector3(1.8f,.08f,.12f),steel);kat.transform.SetParent(r.transform);if(!enemy)sword=kat;
        if(enemy){var e=new Enemy{root=r,hp=100};enemies.Add(e);}return r;
    }
    void UI(){
        var g=new GameObject("HUD");var c=g.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;g.AddComponent<CanvasScaler>().referenceResolution=new Vector2(1280,720);g.AddComponent<GraphicRaycaster>();
        title=T("KADAYA",58,new Vector2(90,570));prompt=T("THE LAST SHADOW",22,new Vector2(95,525));hud=T("",22,new Vector2(40,650));prompt.color=new Color(.72f,.35f,.9f);
    }
    Text T(string s,int size,Vector2 pos){var g=new GameObject("Text");g.transform.SetParent(GameObject.Find("HUD").transform,false);var t=g.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.text=s;t.rectTransform.anchoredPosition=pos;t.rectTransform.sizeDelta=new Vector2(900,70);return t;}
    void Begin(){intro=0;prompt.text="THE LAST SHADOW  •  CHAPTER 1";hud.text="HP 100/100    |    MOVE  WASD / JOYSTICK    |    ATTACK  J";for(int i=0;i<5;i++)Ninja(new Vector3(-1+i*5,1.2f,Random.Range(-4f,4f)),"SHADOW",true);}
    void Update(){
        if(player==null)return;if(intro>0){intro-=Time.deltaTime;cam.transform.LookAt(player.transform.position+Vector3.up);return;}
        attackCD=Mathf.Max(0,attackCD-Time.deltaTime);attackTime=Mathf.Max(0,attackTime-Time.deltaTime);
        float x=Input.GetAxisRaw("Horizontal"),z=Input.GetAxisRaw("Vertical");var d=new Vector3(x,0,z);
        player.transform.position+=d.normalized*5.5f*Time.deltaTime;player.transform.position=new Vector3(Mathf.Clamp(player.transform.position.x,-32,32),1.2f,Mathf.Clamp(player.transform.position.z,-7,7));
        if(d.sqrMagnitude>.01f)player.transform.forward=Vector3.Slerp(player.transform.forward,d.normalized,12*Time.deltaTime);
        if((Input.GetKeyDown(KeyCode.J)||Input.GetKeyDown(KeyCode.Space))&&attackCD<=0)Attack();
        foreach(var e in enemies)if(e.alive){var to=player.transform.position-e.root.transform.position;if(to.magnitude<7){e.root.transform.forward=Vector3.Slerp(e.root.transform.forward,to.normalized,5*Time.deltaTime);e.root.transform.position+=e.root.transform.forward*1.0f*Time.deltaTime;}}
        camRig.transform.position=Vector3.Lerp(camRig.transform.position,player.transform.position+new Vector3(0,5,-13),4*Time.deltaTime);cam.transform.LookAt(player.transform.position+Vector3.up*1.1f);
        hud.text=$"HP {hp}/100    |    SHADOWS BANISHED {kills}    |    KATANA  J";
    }
    void Attack(){
        attackCD=.45f;attackTime=.2f;
        foreach(var e in enemies)if(e.alive&&Vector3.Distance(player.transform.position,e.root.transform.position)<3.6f){e.hp-=55;if(e.hp<=0){e.alive=false;kills++;e.root.SetActive(false);}}
        if(sword)sword.transform.localRotation=Quaternion.Euler(0,0,-55);
        Invoke(nameof(ResetSword),.18f);
    }
    void ResetSword(){if(sword)sword.transform.localRotation=Quaternion.identity;}
}
