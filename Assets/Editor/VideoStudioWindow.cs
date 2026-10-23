using System;
using System.IO;
using System.Linq;
using TechnicalSwordAction.PlayerState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A dockable panel keeps controls, diagnostics and capture status outside the clip.</summary>
public sealed class VideoStudioWindow : EditorWindow
{
    private CombatVideoStudio studio;
    [SerializeField] private StudioPreset selectedSet;
    [SerializeField] private int tab,enemyIndex,gimmickIndex;
    private Vector2 scroll,spawn=new Vector2(1.25f,1);
    private Editor presetEditor;
    private string error;
    private static readonly string[] EnemyModes={"待機（F3で単発）","1回だけ攻撃","定間隔で反復","追跡AI"};
    private static readonly string[] GimmickKinds={"ブロック","すり抜け床","移動床"};
    [MenuItem("Tools/Technical Sword Action/Video/Studio Panel")]
    public static void ShowWindow()=>GetWindow<VideoStudioWindow>("撮影スタジオ");
    private void OnEnable() { minSize=new Vector2(340,430);EditorApplication.update+=Refresh; }
    private void OnDisable() { EditorApplication.update-=Refresh;if(presetEditor!=null)DestroyImmediate(presetEditor); }
    private void Refresh()
    {
        if(studio==null)studio=FindFirstObjectByType<CombatVideoStudio>();
        if(Application.isPlaying || VideoStudioCapture.IsBusy)Repaint();
    }
    private void OnGUI()
    {
        GUILayout.Label("撮影スタジオ",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("白グリッド / 単色床 / アクション用ラフ",EditorStyles.miniLabel);
        if(studio==null)
        {
            EditorGUILayout.HelpBox("自由に操作して敵・足場・アクションを試し、進捗用の映像を撮れます。",MessageType.Info);
            if(GUILayout.Button("スタジオSceneを開く"))VideoStudioSceneBuilder.Open();
            return;
        }
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button(Application.isPlaying?"Play終了":"Playで試す"))EditorApplication.isPlaying=!EditorApplication.isPlaying;
            using(new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if(GUILayout.Button("リセット  F2"))studio.ResetStudio();
                if(GUILayout.Button("Game画面へ"))FocusGame();
            }
        }
        tab=GUILayout.Toolbar(tab,new[]{"セット","敵","ギミック","撮影"});
        scroll=EditorGUILayout.BeginScrollView(scroll);
        try
        {
            if(tab==0)DrawSets();else if(tab==1)DrawEnemies();else if(tab==2)DrawGimmicks();else DrawCapture();
        }
        catch(Exception exception) { error=exception.Message; }
        if(!string.IsNullOrEmpty(error))EditorGUILayout.HelpBox(error,MessageType.Error);
        EditorGUILayout.EndScrollView();
        if(Application.isPlaying)
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField(studio.Player.ActionState+" / "+studio.LastEvent,EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("F1 情報表示 / F2 リセット / F3 最初の敵の単発攻撃",EditorStyles.miniLabel);
        }
    }
    private void BeginEdit(string label)
    {
        error=null;
        if(!Application.isPlaying)
            foreach(var root in studio.gameObject.scene.GetRootGameObjects())Undo.RegisterFullObjectHierarchyUndo(root,label);
    }
    private void Changed()
    {
        if(!Application.isPlaying)EditorSceneManager.MarkSceneDirty(studio.gameObject.scene);
    }
    private void DrawSets()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("配置中",studio.CurrentPreset!=null?studio.CurrentPreset.displayName:"カスタム");
        if(selectedSet==null)selectedSet=studio.CurrentPreset;
        using(new EditorGUILayout.HorizontalScope())
        {
            for(int i=0;i<studio.Presets.Length;i++)
                if(GUILayout.Button(studio.Presets[i].displayName))selectedSet=studio.Presets[i];
        }
        selectedSet=(StudioPreset)EditorGUILayout.ObjectField("セットAsset",selectedSet,typeof(StudioPreset),false);
        if(selectedSet!=null)
        {
            EditorGUILayout.HelpBox(selectedSet.description,MessageType.None);
            if(GUILayout.Button("このセットを配置してリセット",GUILayout.Height(30)))
            { BeginEdit("Apply studio set");studio.ApplyPreset(selectedSet);Changed();enemyIndex=gimmickIndex=0; }
        }
        if(GUILayout.Button("現在のセットを別名保存…"))SaveSet();
        if(GUILayout.Button("現在位置をリセット位置に登録"))
        { BeginEdit("Capture studio reset points");studio.CaptureResetPoint();Changed(); }
        if(GUILayout.Button("セットの左右を反転"))
        { BeginEdit("Mirror studio set");studio.MirrorLayout();Changed(); }
        EditorGUILayout.HelpBox("セット切替は05/06だけを置き換えます。手置きのPrefabや独自ギミックは07_CustomContentへ。StudioResetAnchorを付けるとF2で位置を復元します。",MessageType.Info);
        if(GUILayout.Button("手置き用ルートを選択"))Selection.activeTransform=studio.CustomContent;
        if(selectedSet!=null)
        {
            bool expanded=EditorGUILayout.Foldout(SessionState.GetBool("TSA.Studio.EditSet",false),"セットAssetの設定",true);
            SessionState.SetBool("TSA.Studio.EditSet",expanded);
            if(expanded)
            { Editor.CreateCachedEditor(selectedSet,null,ref presetEditor);presetEditor.OnInspectorGUI(); }
        }
        DrawActions();
    }
    private void DrawActions()
    {
        EditorGUILayout.Space();GUILayout.Label("実際のアクションで試す",EditorStyles.boldLabel);
        using(new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("1段入力"))studio.RequestAction(PlayerActionRequest.Attack);
                if(GUILayout.Button("4段コンボ"))studio.BeginComboPreview();
                if(GUILayout.Button("弾き"))studio.RequestAction(PlayerActionRequest.Parry);
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("ジャンプ"))studio.RequestAction(PlayerActionRequest.Jump);
                if(GUILayout.Button("ダッシュ"))studio.RequestAction(PlayerActionRequest.Dash);
                if(GUILayout.Button("←向く"))studio.Face(-1);
                if(GUILayout.Button("→向く"))studio.Face(1);
            }
            if(GUILayout.Button("HP・ゲージ・回復回数を補充"))studio.RefillPlayer();
        }
        var actions=Application.isPlaying?studio.Player.GetComponent<PlayerInputRouter>()?.Actions:InputSystem.actions;
        if(actions!=null)
            foreach(string name in new[]{"Move","Jump","Attack","Dash","Parry","Pause"})
            {
                var action=actions.FindAction("Gameplay/"+name);
                if(action!=null)EditorGUILayout.LabelField(name,action.GetBindingDisplayString(group:"Keyboard&Mouse"),EditorStyles.miniLabel);
            }
        EditorGUILayout.HelpBox("ゲームパッドも通常の入力設定で操作できます。Jump/Fallは既存の代用ポーズ、未制作のアクション絵は今後差し替えます。",MessageType.None);
    }
    private void DrawEnemies()
    {
        spawn=EditorGUILayout.Vector2Field("追加位置",spawn);
        if(GUILayout.Button("近接ダミーを追加"))
        { BeginEdit("Add studio enemy");studio.AddEnemy(new StudioEnemySetup{position=spawn});enemyIndex=studio.Enemies.Count-1;Changed(); }
        if(studio.Enemies.Count==0){EditorGUILayout.HelpBox("敵なし。追加するかセットを切り替えてください。",MessageType.Info);return;}
        enemyIndex=Mathf.Clamp(enemyIndex,0,studio.Enemies.Count-1);
        enemyIndex=EditorGUILayout.Popup("対象",enemyIndex,studio.Enemies.Select(e=>e.name).ToArray());
        var enemy=studio.Enemies[enemyIndex];
        var data=enemy.Setup.Copy();
        EditorGUI.BeginChangeCheck();
        data.position=EditorGUILayout.Vector2Field("リセット位置",data.position);
        data.hp=EditorGUILayout.IntSlider("HP",data.hp,1,100);
        data.mode=(StudioEnemyMode)EditorGUILayout.Popup("攻撃方法",(int)data.mode,EnemyModes);
        data.windupSeconds=EditorGUILayout.Slider("予備動作（秒）",data.windupSeconds,.1f,3);
        data.intervalSeconds=EditorGUILayout.Slider("回復後の間隔（秒）",data.intervalSeconds,.1f,5);
        data.damage=EditorGUILayout.IntSlider("ダメージ",data.damage,0,5);
        data.moveSpeed=EditorGUILayout.Slider("追跡速度",data.moveSpeed,0,6);
        if(EditorGUI.EndChangeCheck())
        {
            BeginEdit("Configure studio enemy");Undo.RecordObject(enemy,"Configure studio enemy");
            enemy.Configure(data,studio.Player.transform);if(Application.isPlaying)studio.ResetStudio();Changed();
        }
        using(new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.LabelField($"HP {enemy.Health.CurrentHp}/{enemy.Health.MaxHp} / {enemy.AI.CurrentStateName}");
            if(GUILayout.Button("この敵が1回攻撃"))enemy.AttackOnce();
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("パリィを実判定で試す"))studio.BeginParryPreview(enemy,false);
                if(GUILayout.Button("パリィ → 反撃"))studio.BeginParryPreview(enemy,true);
            }
        }
        if(GUILayout.Button("Sceneで選択"))Selection.activeGameObject=enemy.gameObject;
        if(GUILayout.Button("この敵を除く")){BeginEdit("Remove studio enemy");studio.RemoveEnemy(enemy);Changed();}
        EditorGUILayout.HelpBox("予備動作は色と武器で表示。単発は実際の攻撃と回復が終わって待機に戻ります。パリィのお試しは、射程内の敵に対して通常の入力を自動で送ります。",MessageType.None);
    }
    private void DrawGimmicks()
    {
        spawn=EditorGUILayout.Vector2Field("追加位置",spawn);
        using(new EditorGUILayout.HorizontalScope())
            for(int i=0;i<3;i++)
                if(GUILayout.Button(GimmickKinds[i]+"追加"))
                {
                    BeginEdit("Add studio gimmick");studio.AddGimmick(new StudioGimmickSetup{kind=(StudioGimmickKind)i,position=spawn,size=new Vector2(2,i==0?1:.25f)});
                    gimmickIndex=studio.Gimmicks.Count-1;Changed();
                }
        if(studio.Gimmicks.Count==0)return;
        gimmickIndex=Mathf.Clamp(gimmickIndex,0,studio.Gimmicks.Count-1);
        gimmickIndex=EditorGUILayout.Popup("対象",gimmickIndex,studio.Gimmicks.Select((g,i)=>$"{i+1}: {GimmickKinds[(int)g.Setup.kind]}").ToArray());
        var gimmick=studio.Gimmicks[gimmickIndex];var data=gimmick.Setup.Copy();
        EditorGUI.BeginChangeCheck();
        data.kind=(StudioGimmickKind)EditorGUILayout.Popup("種類",(int)data.kind,GimmickKinds);
        data.position=EditorGUILayout.Vector2Field("リセット位置",data.position);data.size=EditorGUILayout.Vector2Field("幅・高さ",data.size);
        if(data.kind==StudioGimmickKind.MovingPlatform)
        { data.travel=EditorGUILayout.Vector2Field("移動量",data.travel);data.periodSeconds=EditorGUILayout.Slider("往復の秒数",data.periodSeconds,.5f,10); }
        if(EditorGUI.EndChangeCheck())
        { BeginEdit("Configure studio gimmick");Undo.RecordObject(gimmick,"Configure studio gimmick");gimmick.Configure(data,studio.Player);if(Application.isPlaying)studio.ResetStudio();Changed(); }
        if(GUILayout.Button("Sceneで選択"))Selection.activeGameObject=gimmick.gameObject;
        if(GUILayout.Button("このギミックを除く")){BeginEdit("Remove studio gimmick");studio.RemoveGimmick(gimmick);Changed();}
        EditorGUILayout.HelpBox("移動床は戦闘時計で動き、上に立った主人公を運びます。スロー・Pause・リセットに追従します。独自のギミックはCustomContentへ置けます。",MessageType.None);
    }
    private void DrawCapture()
    {
        EditorGUI.BeginChangeCheck();
        bool follow=EditorGUILayout.Toggle("主人公に追従",studio.FollowCamera);
        float[] sizes={1.6875f,VideoCombatPresentation.DefaultCloseSize,VideoCombatPresentation.DefaultNormalSize,2.8125f,4.21875f};
        int selected=Array.FindIndex(sizes,s=>Mathf.Approximately(s,studio.CameraSize));if(selected<0)selected=2;
        selected=EditorGUILayout.Popup("カメラ距離",selected,new[]{"アップ（10倍）","寄り（8倍）","標準（7倍）","広め（6倍）","ステージ確認（4倍）"});
        Vector2 center=studio.FixedCameraCenter;if(!follow)center=EditorGUILayout.Vector2Field("固定する中心",center);
        if(EditorGUI.EndChangeCheck())
        { BeginEdit("Studio camera");studio.FollowCamera=follow;studio.CameraSize=sizes[selected];studio.FixedCameraCenter=center;studio.RefreshCameraFrame();Changed(); }
        using(new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            bool hybrid=EditorGUILayout.Toggle("ドット＋短い発光",studio.Vfx.HybridGlow);studio.Vfx.HybridGlow=hybrid;
            bool show=EditorGUILayout.Toggle("Game画面に情報表示 F1",studio.ShowDiagnostics);studio.ShowDiagnostics=show;
            if(GUILayout.Button(CombatTimeController.IsPaused?"戦闘を再開":"戦闘を一時停止"))CombatTimeController.SetPaused(!CombatTimeController.IsPaused);
            if(GUILayout.Button("1080p静止画を保存  F8"))VideoStudioCapture.SaveScreenshot(studio);
            using(new EditorGUI.DisabledScope(VideoStudioCapture.IsBusy&&!VideoStudioCapture.IsRecording))
                if(GUILayout.Button(VideoStudioCapture.IsRecording?"録画を終了してMP4保存  F9":"無音動画の録画を開始  F9",GUILayout.Height(36)))
                { if(VideoStudioCapture.IsRecording)VideoStudioCapture.StopRecording();else VideoStudioCapture.TryStart(studio); }
        }
        EditorGUILayout.HelpBox(VideoStudioCapture.Status,MessageType.None);
        EditorGUILayout.LabelField("出力", "1920 × 1080 / 60fps / MP4 / 無音");
        float[] limits={0,6,10,15,30};int limit=Array.IndexOf(limits,VideoStudioCapture.TimeLimit);if(limit<0)limit=0;
        VideoStudioCapture.TimeLimit=limits[EditorGUILayout.Popup("自動停止",limit,new[]{"F9で終了","6秒","10秒","15秒","30秒"})];
        if(GUILayout.Button("保存先を開く")){Directory.CreateDirectory(VideoStudioCapture.OutputFolder);EditorUtility.RevealInFinder(VideoStudioCapture.OutputFolder);}
        if(GUILayout.Button("保存先を変更…"))
        { string folder=EditorUtility.OpenFolderPanel("撮影の保存先",VideoStudioCapture.OutputFolder,"");if(!string.IsNullOrEmpty(folder))VideoStudioCapture.OutputFolder=folder; }
        EditorGUILayout.LabelField(VideoStudioCapture.OutputFolder,EditorStyles.wordWrappedMiniLabel);
        if(!string.IsNullOrEmpty(VideoStudioCapture.LastOutput)&&GUILayout.Button("最後の書き出しを表示"))EditorUtility.RevealInFinder(VideoStudioCapture.LastOutput);
        if(GUILayout.Button("FFmpeg本体を指定…"))
        { string path=EditorUtility.OpenFilePanel("FFmpeg本体",Path.GetDirectoryName(VideoStudioCapture.FFmpegPath)??"","exe");if(!string.IsNullOrEmpty(path))VideoStudioCapture.FFmpegPath=path; }
        EditorGUILayout.HelpBox("操作パネルや情報表示はCameraの書き出しに入りません。録画開始後はGame画面へ。F9で終了し、保存完了を待ってからファイルを共有してください。",MessageType.Info);
        EditorGUILayout.Space();GUILayout.Label("固定シナリオも引き続き使用可能",EditorStyles.boldLabel);
        using(new EditorGUI.DisabledScope(Application.isPlaying||VideoStudioCapture.IsBusy))
        {
            if(GUILayout.Button("通常コンボ → 背後パリィのScene"))OpenTake(CombatVideoSceneBuilder.ScenePath);
            if(GUILayout.Button("攻撃キャンセル → 反撃 → 再攻撃のScene"))OpenTake(CombatVideoSceneBuilder.DefenseCancelScenePath);
        }
    }
    private void SaveSet()
    {
        string path=EditorUtility.SaveFilePanelInProject("現在のセットを保存","StudioSet","asset","セット管理下の敵・ギミックと登録済みリセット位置を保存します。",VideoStudioSceneBuilder.SetFolder);
        if(string.IsNullOrEmpty(path))return;
        var snapshot=studio.SnapshotPreset();AssetDatabase.CreateAsset(snapshot,path);selectedSet=snapshot;
        EditorGUIUtility.PingObject(snapshot);
    }
    private static void OpenTake(string path)
    { if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(path); }
    private static void FocusGame()
        =>EditorApplication.ExecuteMenuItem("Window/General/Game");
}
