extends Node2D

# KADAYA — original vertical slice.
# All visuals are procedurally drawn so the prototype has no external asset dependencies.

const W := 1280.0
const H := 720.0
const GROUND_Y := 590.0
const GRAVITY := 1800.0

var state := "title"
var cinematic_time := 0.0
var title_alpha := 0.0
var player := Vector2(170, GROUND_Y - 76)
var velocity := Vector2.ZERO
var facing := 1.0
var attacking := false
var attack_time := 0.0
var skill_charge := 0.0
var skill_unlocked := false
var dash_unlocked := false
var dash_time := 0.0
var invuln := 0.0
var health := 100
var enemies: Array = []
var particles: Array = []
var message := ""
var message_time := 0.0
var flash := 0.0
var touch_left := false
var touch_right := false

var font: Font
var bold_font: Font
var logo: Texture2D
var music: AudioStreamPlayer
var music_player: AudioStreamGeneratorPlayback
var music_phase := 0.0
var voice_started := false
var menu_hover := -1
var menu_message := ""
var menu_message_time := 0.0
var ambient_time := 0.0

func _ready() -> void:
    font = ThemeDB.fallback_font
    bold_font = ThemeDB.fallback_font
    queue_redraw()
    # Audio starts only after the first stable frame; gameplay must never depend on audio initialization.

func _boot_audio() -> void:
    setup_music()
    queue_redraw()

func _process(delta: float) -> void:
    ambient_time += delta
    if menu_message_time > 0.0:
        menu_message_time -= delta
    if state == "cinematic":
        cinematic_time += delta
        if music_player != null:
            update_music(delta)
        if not voice_started and cinematic_time > 0.45:
            voice_started = true
            speak_intro()
        if cinematic_time > 8.5:
            state = "title"
        queue_redraw()
        return
    if state == "title":
        title_alpha = min(title_alpha + delta * 1.7, 1.0)
        queue_redraw()
        return
    update_game(delta)
    queue_redraw()

func _input(event: InputEvent) -> void:
    if event is InputEventKey and event.pressed and not event.echo:
        if state == "cinematic":
            cinematic_time = 99.0
            state = "title"
            return
        if state == "title":
            if event.keycode == KEY_ENTER or event.keycode == KEY_SPACE:
                start_game()
            return
        if event.keycode == KEY_SPACE:
            do_jump()
        elif event.keycode == KEY_J or event.keycode == KEY_ENTER:
            do_attack()
        elif event.keycode == KEY_Q:
            use_ability()
    if event is InputEventScreenTouch:
        if event.pressed:
            if state == "cinematic":
                state = "title"
                return
            if state == "title":
                handle_menu_touch(event.position)
                return
            handle_touch(event.position, true)
        else:
            handle_touch(event.position, false)
    if event is InputEventMouseMotion and state == "title":
        menu_hover = get_menu_button_at(event.position)
        queue_redraw()
    if event is InputEventMouseButton and event.pressed and state == "title":
        handle_menu_touch(event.position)

func menu_button_rects() -> Array:
    return [
        Rect2(760, 370, 390, 68),
        Rect2(760, 450, 185, 58),
        Rect2(965, 450, 185, 58),
        Rect2(760, 520, 390, 58)
    ]

func get_menu_button_at(p: Vector2) -> int:
    var rects := menu_button_rects()
    for i in range(rects.size()):
        if rects[i].has_point(p):
            return i
    return -1

func handle_menu_touch(p: Vector2) -> void:
    var idx := get_menu_button_at(p)
    if idx == 0:
        start_game()
    elif idx == 1:
        menu_message = "MISSIONS  •  CHAPTER 1 COMING"
        menu_message_time = 2.0
    elif idx == 2:
        menu_message = "ARMORY  •  KADAYA'S GEAR"
        menu_message_time = 2.0
    elif idx == 3:
        menu_message = "SETTINGS  •  AUDIO / VIBRATION"
        menu_message_time = 2.0
    queue_redraw()

func handle_touch(p: Vector2, pressed: bool) -> void:
    if p.y < 500:
        return
    if p.x < 220:
        touch_left = pressed
    elif p.x < 390:
        touch_right = pressed
    elif p.x > 1080 and p.y > 575:
        if pressed: do_attack()
    elif p.x > 1180 and p.y > 575:
        if pressed: do_jump()
    elif p.x > 1080 and p.y > 480 and p.y <= 575:
        if pressed: use_ability()

func speak_intro() -> void:
    if DisplayServer.has_feature(DisplayServer.FEATURE_TEXT_TO_SPEECH):
        DisplayServer.tts_speak("K... A... D... A... Y... A...", "", 0.95, 0.9, 0.72)
        await get_tree().create_timer(2.2).timeout
        if state == "cinematic": DisplayServer.tts_speak("The last shadow.", "", 0.85, 0.95, 0.82)

func setup_music() -> void:
    if music != null:
        return
    music = AudioStreamPlayer.new()
    var stream := AudioStreamGenerator.new()
    stream.mix_rate = 44100.0
    stream.buffer_length = 2.5
    music.stream = stream
    music.volume_db = -10.0
    add_child(music)
    music.play()
    music_player = music.get_stream_playback()
    update_music(0.01)

func update_music(delta: float) -> void:
    if music_player == null: return
    var samples := int(44100.0 * delta)
    var notes := [55.0, 65.41, 73.42, 82.41, 98.0, 82.41, 73.42, 65.41]
    for i in range(samples):
        music_phase += 1.0 / 44100.0
        var n := notes[int(floor(music_phase * 2.0)) % notes.size()]
        var bass := sin(TAU * n * 0.5 * music_phase) * 0.055
        var pad := sin(TAU * n * music_phase) * 0.025
        var pulse := sin(TAU * 2.0 * music_phase) * 0.008
        music_player.push_frame(Vector2(bass + pad + pulse, bass + pad + pulse))

func start_game() -> void:
    state = "game"
    player = Vector2(170, GROUND_Y - 76)
    velocity = Vector2.ZERO
    health = 100
    enemies.clear()
    particles.clear()
    dash_unlocked = false
    skill_unlocked = false
    skill_charge = 0.0
    spawn_enemy(610, GROUND_Y - 62, 40)
    spawn_enemy(850, GROUND_Y - 62, 50)
    spawn_enemy(1090, GROUND_Y - 62, 65)
    message = "THE FORGOTTEN VILLAGE"
    message_time = 3.0

func update_game(delta: float) -> void:
    if message_time > 0: message_time -= delta
    if invuln > 0: invuln -= delta
    if attack_time > 0: attack_time -= delta
    if dash_time > 0: dash_time -= delta
    if attack_time <= 0: attacking = false

    var dir := 0.0
    if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT) or touch_left: dir -= 1.0
    if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT) or touch_right: dir += 1.0

    if dir != 0:
        facing = sign(dir)
        velocity.x = move_toward(velocity.x, dir * 330.0, 1700.0 * delta)
    else:
        velocity.x = move_toward(velocity.x, 0.0, 1250.0 * delta)

    if not is_on_ground():
        velocity.y += GRAVITY * delta
    elif velocity.y > 0:
        velocity.y = 0

    player += velocity * delta
    player.x = clamp(player.x, 55.0, 1225.0)
    if player.y > GROUND_Y - 76:
        player.y = GROUND_Y - 76
        velocity.y = 0

    if dash_time > 0:
        player.x = clamp(player.x + facing * 650.0 * delta, 55.0, 1225.0)

    if skill_unlocked and skill_charge > 0:
        skill_charge = min(skill_charge + delta * 0.0, 1.0)

    for e in enemies:
        if not e.alive: continue
        e.pos.x += e.dir * e.speed * delta
        if e.pos.x < 420 or e.pos.x > 1180: e.dir *= -1
        e.hit_flash = max(e.hit_flash - delta, 0.0)
        if abs(e.pos.x - player.x) < 50 and abs(e.pos.y - player.y) < 70 and invuln <= 0 and not attacking:
            health -= 12
            invuln = 0.8
            flash = 0.18
            velocity.x = -facing * 240
            velocity.y = -330
            spawn_burst(player, 8)

    if attacking:
        for e in enemies:
            if e.alive and abs(e.pos.x - (player.x + facing * 62)) < 75 and abs(e.pos.y - player.y) < 80:
                e.hp -= 55
                e.hit_flash = 0.12
                spawn_burst(e.pos, 12)
                if e.hp <= 0:
                    e.alive = false
                    message = "SHADOW BANISHED"
                    message_time = 1.1

    var dead_count := 0
    for e in enemies:
        if not e.alive: dead_count += 1

    if dead_count >= 1 and not dash_unlocked:
        dash_unlocked = true
        message = "NEW ABILITY: SHADOW DASH"
        message_time = 2.8

    if dead_count >= 2 and not skill_unlocked:
        skill_unlocked = true
        message = "NEW ABILITY: VORTEX SPHERE"
        message_time = 3.0

    if dead_count == enemies.size() and enemies.size() > 0:
        message = "THE FIRST GATE OPENS..."
        message_time = 3.0

    if health <= 0:
        state = "title"
        title_alpha = 0
    update_particles(delta)
    flash = max(flash - delta, 0.0)

func is_on_ground() -> bool:
    return player.y >= GROUND_Y - 77.0

func do_jump() -> void:
    if state == "game" and is_on_ground():
        velocity.y = -680
        spawn_burst(player + Vector2(0, 70), 5)

func do_attack() -> void:
    if state != "game" or attack_time > 0: return
    attacking = true
    attack_time = 0.22

func use_ability() -> void:
    if state != "game": return
    if dash_unlocked and not skill_unlocked:
        dash_time = 0.20
        spawn_burst(player, 14)
        message = "SHADOW DASH"
        message_time = 0.6
    elif skill_unlocked:
        fire_vortex()

func fire_vortex() -> void:
    var origin := player + Vector2(facing * 70, -20)
    for i in range(30):
        var a := TAU * float(i) / 30.0
        particles.append({"pos":origin, "vel":Vector2(cos(a),sin(a)) * (180 + i*4), "life":0.65, "kind":"orb"})
    for e in enemies:
        if e.alive and abs(e.pos.x-origin.x) < 300 and abs(e.pos.y-origin.y) < 140:
            e.hp -= 130
            e.hit_flash = 0.4
            if e.hp <= 0: e.alive = false
    message = "VORTEX SPHERE!"
    message_time = 1.3
    flash = 0.08

func spawn_enemy(x: float, y: float, hp: int) -> void:
    enemies.append({"pos":Vector2(x,y),"hp":hp,"max_hp":hp,"dir":-1.0,"speed":35.0+hp*0.25,"alive":true,"hit_flash":0.0})

func spawn_burst(pos: Vector2, count: int) -> void:
    for i in range(count):
        var a: float = TAU * float(i) / float(max(count,1))
        particles.append({"pos":pos,"vel":Vector2(cos(a),sin(a))*randf_range(80,280),"life":randf_range(0.25,0.55),"kind":"spark"})

func update_particles(delta: float) -> void:
    for p in particles:
        p.pos += p.vel * delta
        p.vel *= 0.94
        p.life -= delta
    particles = particles.filter(func(p): return p.life > 0)

func _draw() -> void:
    draw_rect(Rect2(0, 0, W, H), Color("#030308"))
    draw_rect(Rect2(0, 0, W, 5), Color("#8c4db5"))
    if state == "cinematic":
        draw_cinematic()
    elif state == "title":
        draw_title()
    else:
        draw_game()
    if flash > 0:
        draw_rect(Rect2(0,0,W,H),Color(1,1,1,flash*2.5))

func draw_cinematic() -> void:
    draw_rect(Rect2(0,0,W,H),Color("#07060d"))
    draw_circle(Vector2(1040,130),76,Color("#d8d2b4"))
    draw_circle(Vector2(1070,110),76,Color("#07060d"))
    for i in range(8):
        var bx := float(i)*180.0
        draw_colored_polygon(PackedVector2Array([Vector2(bx,510),Vector2(bx+70,250),Vector2(bx+145,510)]),Color("#11101a"))
    for i in range(7):
        draw_rect(Rect2(i*210-30,480,160,110),Color("#0d0c14"))
        draw_colored_polygon(PackedVector2Array([Vector2(i*210-50,480),Vector2(i*210+50,410),Vector2(i*210+160,480)]),Color("#16131d"))
    var a: float = clampf((cinematic_time-0.2)*1.8,0.0,1.0)
    if cinematic_time > 1.0: draw_ninja(Vector2(350,505),1.0,1.0)
    if cinematic_time > 2.0: draw_ninja(Vector2(500,505),-1.0,0.7)
    if cinematic_time > 3.0:
        draw_rect(Rect2(0,0,W,H),Color(0.35,0.02,0.02,clamp((cinematic_time-3.0)*0.35,0,0.55)))
        draw_string(font,Vector2(90,150),"THE NIGHT THE VILLAGE FELL",HORIZONTAL_ALIGNMENT_LEFT,-1,42,Color(1,1,1,a))
    if cinematic_time > 5.0:
        draw_string(font,Vector2(90,215),"Sixteen years later...",HORIZONTAL_ALIGNMENT_LEFT,-1,28,Color(0.72,0.68,0.75,a))
        draw_ninja(Vector2(640,500),1.0,1.15)
    if cinematic_time > 6.0:
        draw_string(font,Vector2(90,610),"A boy. A sealed power. A forgotten promise.",HORIZONTAL_ALIGNMENT_LEFT,-1,30,Color(0.92,0.86,0.76,a))

func draw_title() -> void:
    draw_rect(Rect2(0,0,W,H),Color("#030308"))
    draw_ultra_background()
    var moon_pos := Vector2(1040,125)
    draw_circle(moon_pos,82,Color(0.92,0.88,0.73,0.10))
    draw_circle(moon_pos,60,Color("#ddd4b2"))
    draw_circle(moon_pos + Vector2(17,-10),60,Color("#030308"))
    draw_mountain_layer(0.82,Color("#080811"))
    draw_mountain_layer(0.66,Color("#0d0c17"))
    for i in range(28):
        var x := fmod(float(i)*97.0 + ambient_time*(18.0 + float(i%4)*7.0),W+80.0)-40.0
        var y := 100.0 + fmod(float(i)*53.0 + sin(ambient_time*0.8+i)*35.0,510.0)
        draw_circle(Vector2(x,y),1.5+float(i%3)*0.8,Color(0.78,0.48,0.95,0.18))
    draw_ninja(Vector2(355,545),1.0,1.85)
    draw_circle(Vector2(360,485),170,Color(0.38,0.16,0.52,0.035))
    draw_string(bold_font,Vector2(92,120),"KADAYA",HORIZONTAL_ALIGNMENT_LEFT,-1,92,Color("#eee7d7"))
    draw_string(font,Vector2(98,154),"THE LAST SHADOW",HORIZONTAL_ALIGNMENT_LEFT,-1,20,Color("#a98bc4"))
    draw_line(Vector2(98,174),Vector2(535,174),Color(0.65,0.40,0.85,0.45),2)
    draw_string(font,Vector2(98,207),"AN ORIGINAL NINJA ACTION ADVENTURE",HORIZONTAL_ALIGNMENT_LEFT,-1,16,Color(0.68,0.64,0.72,0.8))
    var rects := menu_button_rects()
    for i in range(rects.size()):
        draw_menu_button(rects[i],i,menu_hover == i)
    draw_string(font,Vector2(760,620),"LANDSCAPE  •  60 FPS TARGET  •  ANDROID",HORIZONTAL_ALIGNMENT_LEFT,-1,13,Color(0.48,0.45,0.53,0.7))
    if menu_message_time > 0.0:
        draw_rect(Rect2(735,285,440,54),Color(0.02,0.01,0.04,0.94))
        draw_rect(Rect2(735,285,440,54),Color(0.48,0.25,0.65,0.55),false,1.5)
        draw_string(font,Vector2(755,320),menu_message,HORIZONTAL_ALIGNMENT_LEFT,-1,16,Color("#e5d9ef"))

func draw_menu_button(rect: Rect2,idx: int,hovered: bool) -> void:
    var pulse := 0.5 + sin(ambient_time*2.2)*0.5
    var primary := idx == 0
    var fill := Color(0.16,0.07,0.22,0.94) if primary else Color(0.035,0.03,0.06,0.94)
    if hovered:
        fill = Color(0.28,0.10,0.36,0.98)
    draw_rect(rect,Color(0.0,0.0,0.0,0.45),false,8)
    draw_rect(rect,fill)
    draw_rect(rect,Color(0.70,0.42,0.88,0.72 if hovered or primary else 0.34),false,2)
    if primary:
        draw_line(rect.position+Vector2(24,rect.size.y-8),rect.position+Vector2(rect.size.x-24,rect.size.y-8),Color(0.92,0.66,1.0,0.55+0.25*pulse),3)
    var labels := ["BEGIN CHAPTER 1","MISSIONS","ARMORY","SETTINGS"]
    var sizes := [25,17,17,17]
    draw_string(bold_font if primary else font,rect.position+Vector2(0,rect.size.y*0.63),labels[idx],HORIZONTAL_ALIGNMENT_CENTER,rect.size.x,sizes[idx],Color("#f2eadf"))

func draw_gradient_background() -> void:
    draw_ultra_background()

func draw_ultra_background() -> void:
    for i in range(32):
        var t := float(i)/31.0
        draw_rect(Rect2(0,t*H,W,H/31.0+2),Color(0.008+0.028*t,0.006+0.014*t,0.018+0.045*t,1))
    for i in range(9):
        var x := fmod(float(i)*175.0+ambient_time*(10.0+i),W+260.0)-130.0
        var y := 235.0+float(i%3)*88.0
        draw_circle(Vector2(x,y),120.0+float(i%4)*28.0,Color(0.30,0.20,0.40,0.025))
    for i in range(55):
        var x := fmod(float(i)*83.0+ambient_time*(90.0+float(i%5)*14.0),W+100.0)-50.0
        var y := fmod(float(i)*47.0+ambient_time*150.0,H+80.0)-40.0
        draw_line(Vector2(x,y),Vector2(x-10,y+26),Color(0.55,0.50,0.68,0.055),1)

func draw_mountain_layer(parallax: float,color: Color) -> void:
    var shift := fmod(ambient_time*7.0*parallax,260.0)
    var pts := PackedVector2Array([
        Vector2(-260+shift,510),Vector2(-100+shift,350),Vector2(30+shift,465),
        Vector2(190+shift,290),Vector2(350+shift,470),Vector2(540+shift,330),
        Vector2(710+shift,475),Vector2(900+shift,300),Vector2(1080+shift,465),
        Vector2(1260+shift,330),Vector2(1510+shift,510),Vector2(1510+shift,650),
        Vector2(-260+shift,650)
    ])
    draw_colored_polygon(pts,color)

func draw_game() -> void:
    draw_gradient_background()
    for i in range(7):
        var yy := 80.0+i*70.0
        draw_line(Vector2(0,yy),Vector2(W,yy+30),Color(0.25,0.18,0.28,0.10),2)
    draw_circle(Vector2(1040,105),55,Color("#d4cba9"))
    draw_colored_polygon(PackedVector2Array([Vector2(0,590),Vector2(0,430),Vector2(180,340),Vector2(340,450),Vector2(520,300),Vector2(760,450),Vector2(930,330),Vector2(1280,450),Vector2(1280,590)]),Color("#12111b"))
    draw_rect(Rect2(0,GROUND_Y,W,H-GROUND_Y),Color("#111016"))
    draw_line(Vector2(0,GROUND_Y),Vector2(W,GROUND_Y),Color("#5d5262"),3)
    for x in [80,320,740,980,1160]:
        draw_rect(Rect2(x,GROUND_Y-170,110,170),Color("#17141d"))
        draw_colored_polygon(PackedVector2Array([Vector2(x-18,GROUND_Y-170),Vector2(x+55,GROUND_Y-225),Vector2(x+128,GROUND_Y-170)]),Color("#211b28"))
    for e in enemies:
        if e.alive: draw_enemy(e)
    draw_ninja(player,facing,1.0)
    for p in particles:
        var r := 4.0 if p.kind=="spark" else 9.0
        draw_circle(p.pos,r,Color(0.70,0.45,1.0,clamp(p.life*2.0,0,1)))

    draw_rect(Rect2(28,24,270,78),Color(0.02,0.02,0.04,0.88))
    draw_string(font,Vector2(45,52),"KADAYA  •  AGE 16",HORIZONTAL_ALIGNMENT_LEFT,-1,18,Color("#d8cfbd"))
    draw_rect(Rect2(45,65,210,14),Color("#27232e"))
    draw_rect(Rect2(45,65,210*health/100.0,14),Color("#b33c4c"))
    draw_string(font,Vector2(45,96),"HP %d / 100"%health,HORIZONTAL_ALIGNMENT_LEFT,-1,14,Color("#a9a2ad"))

    draw_rect(Rect2(1010,24,242,92),Color(0.02,0.02,0.04,0.88))
    draw_string(font,Vector2(1030,52),"ABILITIES",HORIZONTAL_ALIGNMENT_LEFT,-1,17,Color("#d8cfbd"))
    draw_string(font,Vector2(1030,78),"DASH  "+("READY" if dash_unlocked else "LOCKED"),HORIZONTAL_ALIGNMENT_LEFT,-1,15,Color("#b7a6d8"))
    draw_string(font,Vector2(1030,99),"VORTEX  "+("READY" if skill_unlocked else "LOCKED"),HORIZONTAL_ALIGNMENT_LEFT,-1,15,Color("#b7a6d8"))

    draw_control_button(Rect2(32,575,96,96),"◀",touch_left)
    draw_control_button(Rect2(140,575,96,96),"▶",touch_right)
    draw_control_button(Rect2(1080,575,96,96),"⚔",attacking)
    draw_control_button(Rect2(1180,575,68,68),"↑",false)
    draw_control_button(Rect2(1075,485,100,72),"✦",skill_unlocked)
    draw_string(font,Vector2(1090,555),"SKILL",HORIZONTAL_ALIGNMENT_LEFT,-1,12,Color("#d8cce2"))

    if message_time > 0:
        var alpha: float = clampf(message_time,0.0,1.0)
        draw_rect(Rect2(280,125,720,58),Color(0.03,0.02,0.06,0.86*alpha))
        draw_string(bold_font,Vector2(320,162),message,HORIZONTAL_ALIGNMENT_CENTER,640,25,Color(0.93,0.88,0.78,alpha))

func draw_control_button(rect: Rect2,label: String,active: bool) -> void:
    draw_rect(rect,Color(0.01,0.008,0.02,0.72))
    draw_rect(rect,Color(0.55,0.34,0.70,0.72 if active else 0.32),false,2)
    if active:
        draw_circle(rect.get_center(),min(rect.size.x,rect.size.y)*0.30,Color(0.52,0.22,0.72,0.16))
    draw_string(bold_font,rect.position+Vector2(0,rect.size.y*0.64),label,HORIZONTAL_ALIGNMENT_CENTER,rect.size.x,28,Color("#f0e8db"))

func draw_ninja(pos: Vector2, dir: float, scale_v: float) -> void:
    var s := scale_v
    var c := Color("#15121c")
    draw_circle(pos+Vector2(0,-58*s),22*s,c)
    draw_rect(Rect2(pos.x-18*s,pos.y-42*s,36*s,52*s),c)
    draw_colored_polygon(PackedVector2Array([pos+Vector2(-18,-35)*s,pos+Vector2(18,-35)*s,pos+Vector2(34,20)*s,pos+Vector2(-30,20)*s]),c)
    draw_colored_polygon(PackedVector2Array([pos+Vector2(-5*dir,-42)*s,pos+Vector2(18*dir,-39)*s,pos+Vector2(58*dir,-25)*s,pos+Vector2(14*dir,-31)*s]),Color("#5d365e"))
    draw_circle(pos+Vector2(0,-58*s),16*s,Color("#c79d7d"))
    draw_rect(Rect2(pos.x-18*s,pos.y-67*s,36*s,12*s),Color("#111018"))
    draw_circle(pos+Vector2(8*dir*s,-61*s),2.8*s,Color("#b36cff"))
    draw_line(pos+Vector2(-10,-2)*s,pos+Vector2(-20,25)*s,c,8*s)
    draw_line(pos+Vector2(10,-2)*s,pos+Vector2(20,25)*s,c,8*s)
    draw_line(pos+Vector2(20*dir,-10)*s,pos+Vector2(66*dir,-45)*s,Color("#d7d0c8"),5*s)
    draw_line(pos+Vector2(19*dir,-8)*s,pos+Vector2(28*dir,2)*s,Color("#6f4c75"),5*s)
    if attacking:
        draw_arc(pos+Vector2(38*dir,-25)*s,55*s,(-1.0 if dir>0 else 2.1),1.0 if dir>0 else 4.2,18,Color(0.9,0.85,0.7,0.8),7*s)

func draw_enemy(e: Dictionary) -> void:
    var p: Vector2 = e.pos
    var hit: float = e.hit_flash
    draw_circle(p+Vector2(0,-38),18,Color("#6e2539") if hit<=0 else Color("#ffffff"))
    draw_rect(Rect2(p.x-16,p.y-22,32,55),Color("#281722"))
    draw_colored_polygon(PackedVector2Array([p+Vector2(-20,-20),p+Vector2(20,-20),p+Vector2(30,15),p+Vector2(-28,15)]),Color("#21141f"))
    draw_circle(p+Vector2(-6,-41),3,Color("#f15a6b"))
    draw_circle(p+Vector2(6,-41),3,Color("#f15a6b"))
    draw_rect(Rect2(p.x-25,p.y-62,50,5),Color("#211c26"))
    draw_rect(Rect2(p.x-25,p.y-62,50*max(float(e.hp)/float(e.max_hp),0.0),5),Color("#b53d54"))
