using UnityEngine;

namespace PlayableMusicTapes.Client;

internal sealed class Runner : MonoBehaviour
{
    private static Runner _host;

    public static Runner Host
    {
        get
        {
            if (_host == null)
            {
                var hostObject = new GameObject("PlayableMusicTapes.Runner");
                DontDestroyOnLoad(hostObject);
                _host = hostObject.AddComponent<Runner>();
            }

            return _host;
        }
    }
}
