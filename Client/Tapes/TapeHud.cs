using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace PlayableMusicTapes.Client;

internal sealed class TapeHud : MonoBehaviour
{
    private const int MaxListedTapes = 6;
    private const float TapeListRefreshSeconds = 0.5f;
    private const float RefreshDelayAfterMove = 1.5f;

    private const float LineHeight = 20f;
    private const float PanelWidth = 360f;
    private const float Padding = 12f;
    private const float ScreenMargin = 16f;
    private const float GapAboveRecorder = 18f;

    private static readonly Vector2 RecorderOnScreen = new Vector2(0.645f, 0.585f);
    private static readonly Color PanelColor = new Color(0.04f, 0.04f, 0.045f, 0.82f);
    private static readonly Color TextColor = new Color(0.88f, 0.88f, 0.86f);
    private static readonly Color DimColor = new Color(0.6f, 0.6f, 0.58f);
    private static readonly Color SelectedColor = new Color(0.95f, 0.8f, 0.42f);
    private static readonly Color TitleColor = new Color(0.84f, 0.8f, 0.68f);

    private readonly struct HudLine
    {
        public readonly string Text;
        public readonly GUIStyle Style;

        public HudLine(string text, GUIStyle style)
        {
            Text = text;
            Style = style;
        }
    }

    private readonly List<Item> _tapes = new List<Item>();
    private int _selected;
    private float _nextRefresh;
    private bool _visible;

    private float _scale;
    private GUIStyle _panelStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _textStyle;
    private GUIStyle _dimStyle;
    private GUIStyle _selectedStyle;

    private void Update()
    {
        Player player = TapeRecorder.RaidPlayer;
        if (player == null || !player.HealthController.IsAlive || ThirdPartyCompat.TheirTapeActive() || Cursor.visible)
        {
            return;
        }

        if (Plugin.Pressed(Plugin.TapeKey))
        {
            TapeListen.ToggleHold(player);
            _nextRefresh = 0f;
            return;
        }

        if (!TapeListen.Holding)
        {
            return;
        }

        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + TapeListRefreshSeconds;
            RefreshTapeList();
        }

        HandleFilterAndVolumeKeys();
        HandleMouseWheel();

        if (TapeListen.HoldReady && !TapeRecorder.Busy)
        {
            HandleRecorderKeys();
        }
    }

    private void LateUpdate()
    {
        _visible = TapeListen.Holding && (_visible || TapeListen.HoldReady);
    }

    private void HandleFilterAndVolumeKeys()
    {
        string loadedTapeId = TapeCatalog.TapeIdOf(TapeDeck.TapeIn(TapeListen.HeldRecorder));

        if (Plugin.Pressed(Plugin.FilterNextKey) && loadedTapeId != null)
        {
            TapeFilterSettings.CyclePreset(loadedTapeId, 1);
        }
        else if (Plugin.Pressed(Plugin.FilterPrevKey) && loadedTapeId != null)
        {
            TapeFilterSettings.CyclePreset(loadedTapeId, -1);
        }
        else if (Plugin.Pressed(Plugin.VolumeUpKey))
        {
            TapeFilterSettings.ChangeVolume(1);
        }
        else if (Plugin.Pressed(Plugin.VolumeDownKey))
        {
            TapeFilterSettings.ChangeVolume(-1);
        }
    }

    private void HandleMouseWheel()
    {
        float wheel = Input.mouseScrollDelta.y;
        if (wheel == 0f || _tapes.Count == 0)
        {
            return;
        }

        int direction = wheel < 0f ? 1 : -1;
        _selected = (_selected + direction + _tapes.Count) % _tapes.Count;
    }

    private void HandleRecorderKeys()
    {
        if (Plugin.Pressed(Plugin.PlayKey))
        {
            TapeListen.HoldPlayStop();
        }
        else if (Plugin.Pressed(Plugin.NextKey))
        {
            TapeListen.HoldNextSong();
        }
        else if (Plugin.Pressed(Plugin.InsertKey) && _tapes.Count > 0)
        {
            TapeListen.HoldInsert(_tapes[_selected]);
            _nextRefresh = Time.unscaledTime + RefreshDelayAfterMove;
        }
        else if (Plugin.Pressed(Plugin.RestartKey))
        {
            TapeListen.HoldRestart();
        }
        else if (Plugin.Pressed(Plugin.EjectKey))
        {
            TapeListen.HoldEject();
            _nextRefresh = Time.unscaledTime + RefreshDelayAfterMove;
        }
    }

    private void RefreshTapeList()
    {
        Item recorder = TapeListen.HeldRecorder;
        Item previouslySelected = _selected < _tapes.Count ? _tapes[_selected] : null;

        _tapes.Clear();
        _tapes.AddRange(TapeRecorder.CarriedTapes()
            .Where(tape => TapeDeck.RecorderHolding(tape) != recorder)
            .OrderBy(TapeCatalog.TitleOf));

        int keptIndex = previouslySelected != null ? _tapes.IndexOf(previouslySelected) : -1;
        if (keptIndex >= 0)
        {
            _selected = keptIndex;
        }
        else
        {
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _tapes.Count - 1));
        }
    }

    private void OnGUI()
    {
        if (!TapeListen.Holding || !_visible || Cursor.visible || TapeRecorder.RaidPlayer == null)
        {
            return;
        }

        CreateStyles();
        List<HudLine> lines = BuildLines();
        DrawPanel(lines);
    }

    private List<HudLine> BuildLines()
    {
        var lines = new List<HudLine>();
        Item loaded = TapeDeck.TapeIn(TapeListen.HeldRecorder);
        bool playing = loaded != null && TapeRecorder.PlayingItem == loaded;

        AddTapeLines(lines, loaded, playing);
        lines.Add(new HudLine(" ", _dimStyle));
        AddTapeList(lines);
        lines.Add(new HudLine(" ", _dimStyle));
        AddKeyHints(lines, playing);
        return lines;
    }

    private void AddTapeLines(List<HudLine> lines, Item loaded, bool playing)
    {
        lines.Add(new HudLine("MIXTAPE PLAYER", _titleStyle));
        lines.Add(new HudLine(loaded != null ? TapeCatalog.TitleOf(loaded) : "No tape", _textStyle));

        TrackInfo track = playing ? TapeRecorder.PlayingTrack : null;
        if (track != null)
        {
            lines.Add(new HudLine($">  {TapeRecorder.PlayingTrackNumber:00}/{TapeRecorder.PlayingTrackCount:00}  {track.Label}", _textStyle));
            lines.Add(new HudLine($"     {Clock(TapeRecorder.PlayingTime)} / {Clock(track.Length)}", _dimStyle));
        }
        else
        {
            lines.Add(new HudLine(loaded != null ? "Stopped" : " ", _dimStyle));
        }

        string tapeId = TapeCatalog.TapeIdOf(loaded);
        if (tapeId != null)
        {
            lines.Add(new HudLine("Filter:  < " + TapeFilterSettings.PresetFor(tapeId) + " >", _textStyle));
        }
        else
        {
            lines.Add(new HudLine("Filter:  -", _dimStyle));
        }

        int volume = Mathf.RoundToInt(TapeFilterSettings.Volume * 10f);
        string volumeBar = new string('|', volume) + new string('.', 10 - volume);
        lines.Add(new HudLine("Volume:  [" + volumeBar + "]  " + volume * 10 + " %", _textStyle));
    }

    private void AddTapeList(List<HudLine> lines)
    {
        if (_tapes.Count == 0)
        {
            lines.Add(new HudLine("No other tapes on you", _dimStyle));
            return;
        }

        int first = Mathf.Clamp(_selected - MaxListedTapes / 2, 0, Mathf.Max(0, _tapes.Count - MaxListedTapes));
        int last = Mathf.Min(_tapes.Count, first + MaxListedTapes);
        for (int i = first; i < last; i++)
        {
            bool selected = i == _selected;
            string marker = selected ? ">  " : "    ";
            lines.Add(new HudLine(marker + TapeCatalog.TitleOf(_tapes[i]), selected ? _selectedStyle : _dimStyle));
        }
    }

    private void AddKeyHints(List<HudLine> lines, bool playing)
    {
        GUIStyle keyStyle = TapeListen.HoldReady ? _textStyle : _dimStyle;
        string play = Key(Plugin.PlayKey);
        string next = Key(Plugin.NextKey);
        string insert = Key(Plugin.InsertKey);
        string eject = Key(Plugin.EjectKey);
        string restart = Key(Plugin.RestartKey);
        string filters = Key(Plugin.FilterPrevKey) + "/" + Key(Plugin.FilterNextKey);
        string volume = Key(Plugin.VolumeDownKey) + "/" + Key(Plugin.VolumeUpKey);

        lines.Add(new HudLine($"[{play}] {(playing ? "Stop" : "Play")}     [{next}] Next song", keyStyle));
        lines.Add(new HudLine($"[{insert}] Insert selected     [{eject}] Eject", keyStyle));
        lines.Add(new HudLine($"[{restart}] Restart tape     [Wheel] Select tape", keyStyle));
        lines.Add(new HudLine($"[{filters}] Filter     [{volume}] Volume", _textStyle));
        lines.Add(new HudLine($"[{Key(Plugin.TapeKey)}] Put away", keyStyle));
    }

    private void DrawPanel(List<HudLine> lines)
    {
        float lineHeight = LineHeight * _scale;
        float width = PanelWidth * _scale;
        float padding = Padding * _scale;
        float height = lines.Count * lineHeight + padding * 2f;

        Vector2 position = PanelPosition(width, height);
        var panel = new Rect(position.x, position.y, width, height);
        GUI.Box(panel, GUIContent.none, _panelStyle);

        float y = panel.y + padding;
        foreach (HudLine line in lines)
        {
            GUI.Label(new Rect(panel.x + padding, y, width - padding * 2f, lineHeight), line.Text, line.Style);
            y += lineHeight;
        }
    }

    private Vector2 PanelPosition(float width, float height)
    {
        float margin = ScreenMargin * _scale;
        float gap = GapAboveRecorder * _scale;
        float x = RecorderOnScreen.x * Screen.width - width * 0.5f;
        float y = RecorderOnScreen.y * Screen.height - gap - height;

        x = Mathf.Clamp(x, margin, Mathf.Max(margin, Screen.width - width - margin));
        y = Mathf.Clamp(y, margin, Mathf.Max(margin, Screen.height - height - margin));
        return new Vector2(x, y);
    }

    private void CreateStyles()
    {
        float scale = Mathf.Max(0.75f, Screen.height / 1080f);
        if (_panelStyle != null && Mathf.Approximately(scale, _scale))
        {
            return;
        }

        _scale = scale;
        Font font = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(candidate => candidate.name.ToLowerInvariant().Contains("bender"));

        var background = new Texture2D(1, 1);
        background.SetPixel(0, 0, PanelColor);
        background.Apply();
        _panelStyle = new GUIStyle(GUI.skin.box);
        _panelStyle.normal.background = background;

        _textStyle = TextStyle(font, 14, TextColor, FontStyle.Normal);
        _dimStyle = TextStyle(font, 13, DimColor, FontStyle.Normal);
        _selectedStyle = TextStyle(font, 14, SelectedColor, FontStyle.Bold);
        _titleStyle = TextStyle(font, 16, TitleColor, FontStyle.Bold);
    }

    private GUIStyle TextStyle(Font font, int size, Color color, FontStyle fontStyle)
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(size * _scale),
            fontStyle = fontStyle,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        style.normal.textColor = color;
        if (font != null)
        {
            style.font = font;
        }

        return style;
    }

    private static string Key(ConfigEntry<KeyboardShortcut> entry)
    {
        return Plugin.Label(entry);
    }

    private static string Clock(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainder = totalSeconds % 60;
        return minutes.ToString("00") + ":" + remainder.ToString("00");
    }
}
