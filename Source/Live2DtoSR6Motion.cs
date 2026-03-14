using System;
using System.IO.Ports;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;
using Live2D.Cubism.Core;
using System.Linq;

namespace SR6PluginProject
{
    [BepInPlugin("com.aoligeas.sr6.live2d", "Live2D to SR6 Motion", "1.0.0")]
    public class SR6Plugin : BaseUnityPlugin
    {
        [Serializable]
        public class AxisConfig
        {
            public string DisplayLabel; public string TCodeKey;
            public string Name = "未绑定";
            public float Min = 0f, Max = 1f;
            public float Multiplier = 1.0f, Offset = 0.5f;
            public bool Invert = false;
            [NonSerialized] public float CurrentVal = 0.5f, RawValue = 0f;
            [NonSerialized] public string MinStr = "0", MaxStr = "1";
            [NonSerialized] public bool IsActive = false, ShowPicker = false;
            public AxisConfig(string label, string key) { DisplayLabel = label; TCodeKey = key; }
        }

        private SerialPort _serialPort;
        private bool _showMenu = true, _isInit = false;
        private string _comPort = "COM4";
        private Rect _windowRect = new Rect(20, 20, 500, 850);
        private int _windowID = 1001;
        private Vector2 _scrollPos, _pickerScrollPos;

        private float _uiOpacity = 0.95f;
        private Color _pinkTheme = new Color(0.87f, 0.41f, 0.45f); 
        private Color _pinkDark = new Color(0.4f, 0.15f, 0.2f);    
        private Color _greenNeon = new Color(0f, 1f, 0f);          
        private Texture2D _pinkTex, _darkTex, _neonTex;

        private List<string> _templateNames = new List<string>() { "默认模板" };
        private int _selectedTemplateIndex = 0;
        private string _newTemplateName = "新场景名称";
        private Dictionary<string, float> _paramMaxMove = new Dictionary<string, float>();
        private KeyCode _menuKey = KeyCode.F9;
        private List<CubismParameter> _allSceneParams = new List<CubismParameter>();
        private string _searchText = "";

        private List<AxisConfig> _axisList = new List<AxisConfig>() {
            new AxisConfig("上下 L0", "L0"), new AxisConfig("倾斜 R1", "R1"),
            new AxisConfig("前后 L1", "L1"), new AxisConfig("左右 L2", "L2"),
            new AxisConfig("旋转 R0", "R0"), new AxisConfig("俯仰 R2", "R2")
        };

        private int _targetHz = 50, _displayHz = 0, _actualSendCount = 0;
        private float _globalSmooth = 0.25f, _counterTimer = 0f, _lastSendTime = 0f;
        private bool _isRangeLocked = true;

        void Awake()
        {
            Logger.LogInfo($"v{Info.Metadata.Version} 初始化成功！爱来自 aoligeas");
            _menuKey = (KeyCode)PlayerPrefs.GetInt("SR6_MenuKey", (int)KeyCode.F9);
            _uiOpacity = PlayerPrefs.GetFloat("SR6_Opacity", 0.95f);
            _targetHz = PlayerPrefs.GetInt("SR6_TargetHz", 50); 

            _pinkTex = MakeTex(1, 1, _pinkTheme);
            _darkTex = MakeTex(1, 1, _pinkDark);
            _neonTex = MakeTex(1, 1, _greenNeon);

            LoadTemplateList();
            LoadCurrentTemplate();
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix); result.Apply();
            return result;
        }

        private void ScanModel()
        {
            _allSceneParams = FindObjectsOfType<CubismParameter>().ToList();
            _isInit = true;
        }

        void Update()
        {
            if (Input.GetKeyDown(_menuKey)) _showMenu = !_showMenu;

            
            if (!_isInit || (_allSceneParams.Count == 0 && Time.frameCount % 300 == 0))
            {
                ScanModel();
                
                if (!_isInit) LoadCurrentTemplate();
                _isInit = true;
            }

            foreach (var axis in _axisList) axis.IsActive = false;
            foreach (var param in _allSceneParams)
            {
                if (param == null) continue;
                foreach (var axis in _axisList)
                {
                    if (axis.Name != "未绑定" && param.name == axis.Name)
                    {
                        axis.RawValue = param.Value; axis.IsActive = true;
                        if (!_isRangeLocked) UpdateAxisRange(axis);
                    }
                }
            }

            foreach (var axis in _axisList)
            {
                float r = Mathf.Max(0.01f, axis.Max - axis.Min);
                float ratio = (Mathf.Clamp(axis.RawValue, axis.Min, axis.Max) - axis.Min) / r;
                float target = axis.IsActive ? (((ratio - 0.5f) * axis.Multiplier) + axis.Offset) : axis.Offset;
                target = Mathf.Clamp01(axis.Invert ? 1f - target : target);
                axis.CurrentVal = Mathf.Lerp(axis.CurrentVal, target, _globalSmooth);
            }

            if (Time.time - _lastSendTime >= 1f / Mathf.Max(1, _targetHz)) { SendData(); _lastSendTime = Time.time; _actualSendCount++; }
            if (Time.time - _counterTimer >= 1f) { _displayHz = _actualSendCount; _actualSendCount = 0; _counterTimer = Time.time; }
        }

        private void UpdateAxisRange(AxisConfig a)
        {
            if (a.RawValue < a.Min) { a.Min = a.RawValue; a.MinStr = a.Min.ToString("F1"); }
            if (a.RawValue > a.Max) { a.Max = a.RawValue; a.MaxStr = a.Max.ToString("F1"); }
        }

        private void SendData()
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;
            string cmd = "";
            foreach (var a in _axisList) cmd += $"{a.TCodeKey}{Mathf.Clamp(Mathf.RoundToInt(a.CurrentVal * 99f), 0, 99):D2} ";
            cmd += "I50\r\n";
            try { _serialPort.Write(cmd); } catch { }
        }

        void OnGUI()
        {
            if (!_showMenu) return;

            GUI.skin.box.normal.background = _darkTex; 
            GUI.skin.textField.normal.background = _darkTex;
            GUI.skin.textField.focused.background = _darkTex;
            GUI.skin.textField.normal.textColor = Color.white;
            GUI.skin.button.normal.background = _darkTex;

            Color windowBg = _pinkTheme; windowBg.a = _uiOpacity;
            GUI.backgroundColor = windowBg;
            GUI.contentColor = Color.white;

            _windowRect = GUI.Window(_windowID, _windowRect, DrawWindow, "<b><color=#FFD1D1>Live2D-to-SR6-Motion</color></b>");
        }

        void DrawWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, 500, 25));
            GUILayout.BeginArea(new Rect(15, 35, 470, 800));

            GUI.skin.box.normal.background = _darkTex;

            GUILayout.BeginVertical("box");
            GUILayout.Label("<color=#FFD1D1><b>场景模板 (启动自动激活)</b></color>");
            for (int i = 0; i < _templateNames.Count; i++)
            {
                GUILayout.BeginHorizontal();
                bool isSel = _selectedTemplateIndex == i;
                GUI.backgroundColor = isSel ? Color.white : _pinkDark;
                if (GUILayout.Button(isSel ? $"<b><color=#DE6A73>[ {_templateNames[i]} ]</color></b>" : _templateNames[i], GUILayout.Height(25), GUILayout.ExpandWidth(true)))
                { _selectedTemplateIndex = i; LoadCurrentTemplate(); ScanModel(); }
                GUI.backgroundColor = _pinkTheme;
                if (GUILayout.Button("▲", GUILayout.Width(25))) MoveTemplate(i, -1);
                if (GUILayout.Button("▼", GUILayout.Width(25))) MoveTemplate(i, 1);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            _newTemplateName = GUILayout.TextField(_newTemplateName, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("新建", GUILayout.Width(45))) CreateNewTemplate();
            if (GUILayout.Button("重置", GUILayout.Width(45))) ResetCurrentTemplate();
            if (GUILayout.Button("<color=red>删</color>", GUILayout.Width(35))) DeleteTemplate();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.skin.box.normal.background = Texture2D.whiteTexture;
            GUI.backgroundColor = _pinkTheme;

            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            _comPort = GUILayout.TextField(_comPort, GUILayout.Width(60));
            if (GUILayout.Button(_serialPort != null && _serialPort.IsOpen ? "断开" : "连接")) ConnectSerial();
            GUILayout.FlexibleSpace();
            GUILayout.Label("菜单键:");
            string keyStr = GUILayout.TextField(_menuKey.ToString(), GUILayout.Width(50));
            if (Enum.TryParse(keyStr, true, out KeyCode newK) && _menuKey != newK) { _menuKey = newK; PlayerPrefs.SetInt("SR6_MenuKey", (int)_menuKey); }
            GUILayout.EndHorizontal();

            _uiOpacity = LabelSlider("透明度", _uiOpacity, 0.1f, 1.0f);
            _targetHz = (int)LabelSlider($"输出频率: <color=cyan>{_displayHz}Hz</color>", _targetHz, 10, 200);

            if (GUILayout.Button("<color=yellow>保存当前全部配置</color>"))
            {
                SaveTemplateList(); SaveCurrentTemplate();
                PlayerPrefs.SetFloat("SR6_Opacity", _uiOpacity);
                PlayerPrefs.SetInt("SR6_TargetHz", _targetHz);
                PlayerPrefs.Save();
            }
            GUILayout.EndVertical();

            _isRangeLocked = GUILayout.Toggle(_isRangeLocked, _isRangeLocked ? "【映射范围已锁定】" : "【正在自动记录动作范围】", "button", GUILayout.Height(30));

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            _globalSmooth = LabelSlider("全局平滑", _globalSmooth, 0.05f, 0.8f);

            foreach (var axis in _axisList) DrawAxisBox(axis);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawAxisBox(AxisConfig cfg)
        {
            GUI.backgroundColor = _pinkTheme;
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{cfg.DisplayLabel}</b>", GUILayout.Width(80));
            GUI.backgroundColor = _pinkDark;
            if (GUILayout.Button(cfg.Name, GUILayout.Width(140)))
            {
                cfg.ShowPicker = !cfg.ShowPicker;
                if (cfg.ShowPicker) { _paramMaxMove.Clear(); ScanModel(); }
            }
            bool newInvert = GUILayout.Toggle(cfg.Invert, "反转", GUILayout.Width(50));
            if (newInvert != cfg.Invert)
            {
                cfg.Invert = newInvert;
                SaveCurrentTemplate(); 
            }

            if (GUILayout.Button("C", GUILayout.Width(25))) cfg.Offset = 0.5f;
            GUILayout.EndHorizontal();

            if (cfg.ShowPicker) DrawParameterPicker(cfg);

            if (cfg.IsActive)
            {
                GUILayout.BeginHorizontal();
                cfg.MinStr = GUILayout.TextField(cfg.MinStr, GUILayout.Width(50)); GUILayout.Label(":", GUILayout.Width(10));
                cfg.MaxStr = GUILayout.TextField(cfg.MaxStr, GUILayout.Width(50)); GUILayout.Label($"(Raw:{cfg.RawValue:F1})", GUILayout.Width(80));
                if (_isRangeLocked) { float.TryParse(cfg.MinStr, out cfg.Min); float.TryParse(cfg.MaxStr, out cfg.Max); }
                GUILayout.EndHorizontal();
                cfg.Multiplier = LabelSlider("倍率", cfg.Multiplier, 0.1f, 3.0f);
            }
            cfg.Offset = LabelSlider(cfg.IsActive ? "偏移" : "调试", cfg.Offset, 0f, 1f);

            DrawNeonHandleBar(cfg.CurrentVal, cfg.IsActive);

            GUILayout.EndVertical();
        }

        private void DrawNeonHandleBar(float v, bool active)
        {
            Rect r = GUILayoutUtility.GetRect(400, 12);
            GUI.DrawTexture(r, _darkTex); 

            float handleWidth = 10f;
            Rect handleRect = new Rect(r.x + (v * (r.width - handleWidth)), r.y - 1, handleWidth, 14);
            GUI.DrawTexture(handleRect, active ? _neonTex : Texture2D.whiteTexture); 
        }

        private void DrawParameterPicker(AxisConfig cfg)
        {
            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            GUILayout.Label("搜索:", GUILayout.Width(40));
            _searchText = GUILayout.TextField(_searchText);
            GUILayout.EndHorizontal();
            _pickerScrollPos = GUILayout.BeginScrollView(_pickerScrollPos, GUILayout.Height(250));// 爱来自aoligeas
            foreach (var p in _allSceneParams) { if (p == null) continue; float v = Mathf.Abs(p.Value); if (!_paramMaxMove.ContainsKey(p.name)) _paramMaxMove[p.name] = 0f; if (v > _paramMaxMove[p.name]) _paramMaxMove[p.name] = v; }
            var sortedParams = _allSceneParams.Where(p => p != null && (string.IsNullOrEmpty(_searchText) || p.name.ToLower().Contains(_searchText.ToLower()))).OrderByDescending(p => _paramMaxMove.ContainsKey(p.name) ? _paramMaxMove[p.name] : 0).ToList();
            foreach (var p in sortedParams)
            {
                float mv = _paramMaxMove.ContainsKey(p.name) ? _paramMaxMove[p.name] : 0f;
                string col = mv > 0.05f ? "#00FF00" : "white";
                GUILayout.BeginHorizontal();
                if (GUILayout.Button($"<color={col}>{p.name}</color>", GUILayout.ExpandWidth(true))) { cfg.Name = p.name; cfg.ShowPicker = false; _paramMaxMove.Clear(); _searchText = ""; }
                GUILayout.Label($"<b><color={col}>{p.Value:F1}</color></b>", GUILayout.Width(50));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private float LabelSlider(string l, float v, float min, float max)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(l, GUILayout.Width(110));
            GUI.color = _pinkTheme; float r = GUILayout.HorizontalSlider(v, min, max); GUI.color = Color.white;
            GUILayout.Label(r.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal(); return r;
        }

        private void ConnectSerial() { try { if (_serialPort != null && _serialPort.IsOpen) _serialPort.Close(); else { _serialPort = new SerialPort(_comPort, 115200); _serialPort.Open(); } } catch { } }
        private void ResetRanges() { foreach (var a in _axisList) { a.Min = 0f; a.Max = 0.1f; a.MinStr = "0"; a.MaxStr = "0.1"; } }
        private void MoveTemplate(int idx, int dir) { int target = idx + dir; if (target < 0 || target >= _templateNames.Count) return; string t = _templateNames[idx]; _templateNames[idx] = _templateNames[target]; _templateNames[target] = t; if (_selectedTemplateIndex == idx) _selectedTemplateIndex = target; else if (_selectedTemplateIndex == target) _selectedTemplateIndex = idx; SaveTemplateList(); }
        private void CreateNewTemplate() { if (!_templateNames.Contains(_newTemplateName)) { _templateNames.Add(_newTemplateName); _selectedTemplateIndex = _templateNames.Count - 1; SaveTemplateList(); SaveCurrentTemplate(); ScanModel(); } }
        private void ResetCurrentTemplate() { foreach (var a in _axisList) { a.Name = "未绑定"; a.Multiplier = 1.0f; a.Offset = 0.5f; a.Invert = false; a.Min = 0f; a.Max = 1f; a.MinStr = "0"; a.MaxStr = "1"; } _globalSmooth = 0.25f; _isRangeLocked = true; }
        private void DeleteTemplate() { if (_templateNames.Count <= 1) return; _templateNames.RemoveAt(_selectedTemplateIndex); _selectedTemplateIndex = 0; SaveTemplateList(); LoadCurrentTemplate(); }
        private void SaveTemplateList() => PlayerPrefs.SetString("SR6_TempList", string.Join("|", _templateNames.ToArray()));
        private void LoadTemplateList() { string l = PlayerPrefs.GetString("SR6_TempList", "默认模板"); _templateNames = new List<string>(l.Split('|')); }
        private void SaveCurrentTemplate() { string pre = "SR6_T_" + _templateNames[_selectedTemplateIndex]; PlayerPrefs.SetString("SR6_LastCOM", _comPort); foreach (var a in _axisList) { PlayerPrefs.SetString(pre + a.TCodeKey + "_N", a.Name); PlayerPrefs.SetFloat(pre + a.TCodeKey + "_M", a.Multiplier); PlayerPrefs.SetFloat(pre + a.TCodeKey + "_O", a.Offset); PlayerPrefs.SetInt(pre + a.TCodeKey + "_I", a.Invert ? 1 : 0); PlayerPrefs.SetFloat(pre + a.TCodeKey + "_Min", a.Min); PlayerPrefs.SetFloat(pre + a.TCodeKey + "_Max", a.Max); } PlayerPrefs.Save(); }
        private void LoadCurrentTemplate() { string pre = "SR6_T_" + _templateNames[_selectedTemplateIndex]; _comPort = PlayerPrefs.GetString("SR6_LastCOM", "COM4"); foreach (var a in _axisList) { a.Name = PlayerPrefs.GetString(pre + a.TCodeKey + "_N", "未绑定"); a.Multiplier = PlayerPrefs.GetFloat(pre + a.TCodeKey + "_M", 1.0f); a.Offset = PlayerPrefs.GetFloat(pre + a.TCodeKey + "_O", 0.5f); a.Invert = PlayerPrefs.GetInt(pre + a.TCodeKey + "_I", 0) == 1; a.Min = PlayerPrefs.GetFloat(pre + a.TCodeKey + "_Min", 0f); a.Max = PlayerPrefs.GetFloat(pre + a.TCodeKey + "_Max", 1f); a.MinStr = a.Min.ToString("F1"); a.MaxStr = a.Max.ToString("F1"); } }
    }
}
