using System;
using EFT.Communications;
using EFT.UI;
using TMPro;
using UnityEngine;

namespace PlayableMusicTapes.Client;

internal sealed class TapeProgressNotification : Notification
{
    private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.7843137f);

    private readonly string _description;
    private readonly float _trackLength;
    private BaseNotificationView _view;
    private TextMeshProUGUI _timerLine;
    private string _shownTime;

    public TapeProgressNotification(string trackLabel, float trackLength, int trackNumber, int trackCount)
    {
        _description = trackCount > 1 ? trackLabel + "  (" + trackNumber + "/" + trackCount + ")" : trackLabel;
        _trackLength = trackLength;
        Duration = ENotificationDurationType.Infinite;
    }

    public override string Description => _description;

    public override BaseNotificationView CreateView(INotificationViewFactory viewFactory)
    {
        _view = TapeNotificationViews.Create(viewFactory);
        if (_view == null)
        {
            return base.CreateView(viewFactory);
        }

        string keyFormat = TapeNotificationViews.Localize("Tapes/RecorderKey{0}", "Recorder: ({0})");
        var lines = TapeNotificationViews.AddLines(_view, Color.white, null, TimerText(0f), string.Format(keyFormat, TapeRecorder.KeyLabel));
        _timerLine = lines.Count > 0 ? lines[0] : null;

        TapeNotificationViews.SetBackground(_view, storyline: false);
        TapeNotificationViews.Finish(_view, this);
        TapeNotificationViews.ArrangeColumns(_view, isProgress: true);
        _view._background.color = PanelColor;
        return _view;
    }

    public void SetTime(float seconds)
    {
        if (_timerLine == null)
        {
            return;
        }

        string text = TimerText(seconds);
        if (text != _shownTime)
        {
            _shownTime = text;
            _timerLine.text = text;
        }
    }

    public void Close()
    {
        if (_view != null && _view.gameObject.activeInHierarchy && !_view.IsHiding)
        {
            _view.HideNotification(false);
        }

        _view = null;
        _timerLine = null;
    }

    private string TimerText(float seconds)
    {
        string format = TapeNotificationViews.Localize("Tapes/Playback{0}{1}", "Playing: {0}/{1}");
        return string.Format(format, Clock(seconds), Clock(_trackLength));
    }

    private static string Clock(float seconds)
    {
        return TimeSpan.FromSeconds(Math.Max(0, Math.Floor(seconds))).ToString("mm\\:ss");
    }
}
