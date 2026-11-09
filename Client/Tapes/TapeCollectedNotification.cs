using EFT.Communications;
using EFT.UI;
using UnityEngine;

namespace PlayableMusicTapes.Client;

internal sealed class TapeCollectedNotification : Notification
{
    private const float AnimationSpeed = 0.2f;

    private static readonly int SpeedParameter = Animator.StringToHash("Speed");

    private readonly string _tapeName;
    private readonly bool _hasRecorder;

    public TapeCollectedNotification(string tapeName, bool hasRecorder)
    {
        _tapeName = tapeName;
        _hasRecorder = hasRecorder;
        Duration = ENotificationDurationType.Default;
    }

    public override string Description => "\"" + _tapeName + "\"";

    public override BaseNotificationView CreateView(INotificationViewFactory viewFactory)
    {
        BaseNotificationView view = TapeNotificationViews.Create(viewFactory);
        if (view == null)
        {
            return base.CreateView(viewFactory);
        }

        TapeNotificationViews.AddLines(view, TapeNotificationViews.TitleColor, view._text, Title());
        TapeNotificationViews.SetBackground(view, storyline: true);
        TapeNotificationViews.Finish(view, this);
        TapeNotificationViews.ArrangeColumns(view, isProgress: false);
        view._background.color = TapeNotificationViews.BackgroundColor;
        view._animator.SetFloat(SpeedParameter, AnimationSpeed);
        return view;
    }

    private string Title()
    {
        if (!_hasRecorder)
        {
            return TapeNotificationViews.Localize("tapes/tapeFoundNotify", "Tape found");
        }

        string format = TapeNotificationViews.Localize("tapes/tapeFoundNotify{0}", "Tape found / Recorder ({0})");
        return string.Format(format, TapeRecorder.KeyLabel);
    }
}
